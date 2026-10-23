#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runtime = Join-Path $PSScriptRoot '.runtime'
$apiRoot = Join-Path $repo 'backend/src/Dental.Api'
$dll = Join-Path $apiRoot 'bin/Debug/net10.0/Dental.Api.dll'
$vite = Join-Path $repo 'frontend/node_modules/vite/bin/vite.js'
if (!(Test-Path -LiteralPath $dll) -or !(Test-Path -LiteralPath $vite)) { throw 'Chạy Prepare.ps1 trước.' }
foreach ($port in @(5000,5173)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
        throw "Cổng $port đang được sử dụng. Dừng ứng dụng đó trước; script không tự đóng tiến trình khác."
    }
}
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$secretFile = Join-Path $runtime 'settings.json'
if (!(Test-Path -LiteralPath $secretFile)) {
    @{ DatabasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24));
       JwtSecret = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48)) } |
        ConvertTo-Json | Set-Content -LiteralPath $secretFile -Encoding utf8
}
$settings = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
$envFile = Join-Path $runtime 'database.env'
"PHASE1_DB_PASSWORD=$($settings.DatabasePassword)" | Set-Content -LiteralPath $envFile -Encoding utf8
docker compose --project-name dental-phase1 --env-file $envFile -f (Join-Path $PSScriptRoot 'compose.yml') up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'Không khởi động được PostgreSQL demo. Kiểm tra Docker Desktop và cổng 55432.' }
$variables = @{
    ASPNETCORE_ENVIRONMENT = 'Development'; ASPNETCORE_URLS = 'http://127.0.0.1:5000';
    ConnectionStrings__Default = "Host=127.0.0.1;Port=55432;Database=dental_phase1_demo;Username=phase1;Password=$($settings.DatabasePassword)";
    Jwt__Secret = $settings.JwtSecret; Jwt__Issuer = 'DentalClinic'; Jwt__Audience = 'DentalClinicClient';
    Seed__AdminPhone = '0900000001'; Seed__AdminPassword = 'AdminPassword@123';
    Seed__DemoUsers = 'true'; Seed__DemoPassword = 'DemoPassword@123'; Seed__Phase1 = 'true';
    PatientActivation__Sms__Enabled = 'false'; PasswordRecovery__Sms__Enabled = 'false';
    PatientPhoneChange__Sms__Enabled = 'false'; StaffPhoneChange__Sms__Enabled = 'false'
}
$previous = @{}
$started = @()
try {
    foreach ($key in $variables.Keys) { $previous[$key] = [Environment]::GetEnvironmentVariable($key,'Process'); [Environment]::SetEnvironmentVariable($key,$variables[$key],'Process') }
    $api = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"' + $dll + '"')) -WorkingDirectory $apiRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'api.log') -RedirectStandardError (Join-Path $runtime 'api-error.log')
    $started += $api
    $ready = $false
    for ($i=0; $i -lt 60; $i++) {
        if ($api.HasExited) { throw 'API đã dừng. Xem .runtime/api-error.log và api.log.' }
        try { $response = Invoke-WebRequest 'http://127.0.0.1:5000/api/appointment-requests/catalog' -TimeoutSec 2; if ($response.StatusCode -eq 200) { $ready=$true; break } } catch { }
        Start-Sleep -Milliseconds 1000
    }
    if (!$ready) { throw 'API chưa sẵn sàng sau 60 lần chờ. Xem log migration/seed.' }
    $web = Start-Process -FilePath (Get-Command node).Source -ArgumentList @(('"' + $vite + '"'),'--host','127.0.0.1','--port','5173','--strictPort') -WorkingDirectory (Join-Path $repo 'frontend') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'web.log') -RedirectStandardError (Join-Path $runtime 'web-error.log')
    $started += $web
    $webReady=$false
    for ($i=0; $i -lt 20; $i++) {
        if ($web.HasExited) { throw 'Frontend đã dừng; xem .runtime/web-error.log.' }
        try { if ((Invoke-WebRequest 'http://127.0.0.1:5173' -TimeoutSec 2).StatusCode -eq 200) { $webReady=$true; break } } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (!$webReady) { throw 'Frontend chưa sẵn sàng.' }
    @($started | ForEach-Object { @{ Id=$_.Id; Started=$_.StartTime.ToUniversalTime().Ticks; Executable=$_.Path } }) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runtime 'processes.json') -Encoding utf8
    Write-Host 'Demo sẵn sàng: http://127.0.0.1:5173 | API: http://127.0.0.1:5000/swagger'
    Write-Host 'Database: dental_phase1_demo, cổng 55432. Tài khoản và kịch bản: README.md / index.html.'
} catch {
    foreach ($process in $started) { if (!$process.HasExited) { Stop-Process -InputObject $process } }
    throw
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') }
}
