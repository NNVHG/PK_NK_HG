# Khởi chạy hệ thống phòng khám Hoàng Gia trên Windows.
# Cần .NET SDK 10, Node.js 20+ và Docker Desktop đang chạy.
param(
    [switch]$SkipDocker,
    [ValidateSet('Backend', 'Frontend', 'Docker')]
    [string]$LoadSecretsOnly
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

function Stop-WithMessage([string]$Message) {
    Write-Host "`n[LỖI] $Message" -ForegroundColor Red
    exit 1
}

function Get-LocalSecretGroup([string]$Name) {
    switch -Regex ($Name) {
        '^POSTGRES_PASSWORD$' { return 'Docker' }
        '^VIETTEL_TTS_(TOKEN|VOICE|SPEED)$' { return 'Frontend' }
        '^(ConnectionStrings__Default|Jwt__Secret|IdentitySecurity__(HmacKey|CurrentVersion|Keys__[1-9][0-9]*)|Seed__(AdminPhone|AdminPassword|DemoUsers|DemoPassword|PatientTestFixtures)|Payment__Webhook(SecretKey|ActorUserId)|(PasswordRecovery|PatientActivation|GuestBooking)__Sms__(Enabled|AccountSid|AuthToken|From))$' { return 'Backend' }
    }
}

function Read-LocalSecrets {
    $path = Join-Path $PSScriptRoot 'secrets.json'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Stop-WithMessage 'Chưa có secrets.json. Sao chép secrets.example.json rồi điền cấu hình cục bộ theo README.md.'
    }
    try {
        $config = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Stop-WithMessage 'secrets.json không phải JSON hợp lệ. Kiểm tra dấu ngoặc và dấu phẩy trong file.'
    }
    if ($config -isnot [PSCustomObject]) {
        Stop-WithMessage 'secrets.json phải là một đối tượng JSON theo mẫu trong repo.'
    }

    $groups = @{ Backend = @{}; Frontend = @{}; Docker = @{} }
    foreach ($entry in $config.PSObject.Properties) {
        $group = Get-LocalSecretGroup $entry.Name
        if (-not $group) {
            Stop-WithMessage 'secrets.json có trường chưa được hỗ trợ. Dùng tên trường từ secrets.example.json.'
        }
        if ($null -eq $entry.Value) { continue }
        if ($entry.Value -isnot [string] -and $entry.Value -isnot [bool] -and $entry.Value -isnot [ValueType]) {
            Stop-WithMessage 'secrets.json chỉ nhận chuỗi, số hoặc giá trị đúng/sai cho mỗi trường.'
        }
        $value = [string]$entry.Value
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $groups[$group][$entry.Name] = $value
        }
    }
    return $groups
}

function Test-BackendSecrets([hashtable]$Values) {
    if (-not $Values.ContainsKey('ConnectionStrings__Default')) {
        Stop-WithMessage 'Điền ConnectionStrings__Default cho PostgreSQL cục bộ trong secrets.json.'
    }
    if ([Text.Encoding]::UTF8.GetByteCount([string]$Values['Jwt__Secret']) -lt 32) {
        Stop-WithMessage 'Điền Jwt__Secret với khóa ít nhất 32 byte trong secrets.json.'
    }
    $hasHmac = @($Values.Keys | Where-Object {
        $_ -match '^IdentitySecurity__(HmacKey|Keys__[1-9][0-9]*)$' -and
            [Text.Encoding]::UTF8.GetByteCount($Values[$_].Trim()) -ge 32
    }).Count -gt 0
    if (-not $hasHmac) {
        Stop-WithMessage 'Điền IdentitySecurity__HmacKey hoặc khóa HMAC theo phiên bản với ít nhất 32 byte.'
    }
}

function Set-LocalSecretEnvironment([hashtable]$Values) {
    foreach ($key in $Values.Keys) {
        [Environment]::SetEnvironmentVariable($key, $Values[$key], 'Process')
    }
}

function Invoke-WithLocalSecrets([string]$Target, [hashtable]$Values, [scriptblock]$Action) {
    $previous = @{}
    try {
        $environment = [Environment]::GetEnvironmentVariables('Process')
        foreach ($key in $environment.Keys) {
            $group = Get-LocalSecretGroup $key
            if ($group -and $group -ne $Target) {
                $previous[$key] = $environment[$key]
                [Environment]::SetEnvironmentVariable($key, $null, 'Process')
            }
        }
        foreach ($key in $Values.Keys) {
            $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        }
        Set-LocalSecretEnvironment $Values
        & $Action
    }
    finally {
        Set-LocalSecretEnvironment $previous
    }
}

function Test-TcpPort([int]$Port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $task = $client.ConnectAsync('127.0.0.1', $Port)
        return $task.Wait(500) -and $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Wait-HttpEndpoint([string]$Url, [string]$Name, [int]$TimeoutSeconds = 120) {
    Write-Host "  Đang chờ $Name sẵn sàng..." -ForegroundColor Gray
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $null = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
            Write-Host "  Đã sẵn sàng: $Url" -ForegroundColor Green
            return $true
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }
    return $false
}

$localSecrets = Read-LocalSecrets
if ($LoadSecretsOnly) {
    if ($LoadSecretsOnly -eq 'Backend') { Test-BackendSecrets $localSecrets.Backend }
    Set-LocalSecretEnvironment $localSecrets[$LoadSecretsOnly]
    Write-Host "Đã nạp cấu hình $LoadSecretsOnly từ secrets.json vào phiên PowerShell hiện tại." -ForegroundColor Green
    return
}
Test-BackendSecrets $localSecrets.Backend

Write-Host '======================================================' -ForegroundColor Cyan
Write-Host '  KHỞI CHẠY PHÒNG KHÁM NHA KHOA HOÀNG GIA' -ForegroundColor Cyan
Write-Host '======================================================' -ForegroundColor Cyan

# 1. Kiểm tra công cụ và phiên bản tối thiểu.
Write-Host "`n[1/6] Kiểm tra công cụ..." -ForegroundColor Yellow
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCommand) {
    Stop-WithMessage 'Chưa cài .NET SDK 10. Tải tại https://dotnet.microsoft.com/download/dotnet/10.0 rồi mở PowerShell mới.'
}
$dotnetVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $dotnetVersion -notmatch '^10\.') {
    Stop-WithMessage "Dự án cần .NET SDK 10.x, nhưng máy đang chọn phiên bản '$dotnetVersion'. Hãy cài SDK 10 và kiểm tra global.json nếu có."
}

$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
$npmCommand = Get-Command npm.cmd -ErrorAction SilentlyContinue
if (-not $nodeCommand -or -not $npmCommand) {
    Stop-WithMessage 'Chưa cài Node.js 20+ kèm npm. Cài bản LTS từ https://nodejs.org/ rồi mở PowerShell mới.'
}
$nodeVersion = (& node -p 'process.versions.node').Trim()
$nodeMajor = 0
if ($nodeVersion -notmatch '^(\d+)\.' -or -not [int]::TryParse($Matches[1], [ref]$nodeMajor) -or $nodeMajor -lt 20) {
    Stop-WithMessage "Dự án cần Node.js 20 trở lên, nhưng đang có '$nodeVersion'."
}
Write-Host "  .NET SDK $dotnetVersion | Node.js $nodeVersion" -ForegroundColor Green

Write-Host "`n[2/6] Khôi phục package Backend..." -ForegroundColor Yellow
& dotnet restore (Join-Path $PSScriptRoot 'backend/DentalClinic.sln')
if ($LASTEXITCODE -ne 0) {
    Stop-WithMessage 'Không khôi phục được NuGet package. Kiểm tra kết nối Internet và chạy lại start-dev.ps1.'
}
Write-Host '  NuGet package đã sẵn sàng.' -ForegroundColor Green

Write-Host "`n[3/6] Đã kiểm tra cấu hình trong secrets.json." -ForegroundColor Green

# 3. Khởi động PostgreSQL và đợi database sẵn sàng.
Write-Host "`n[4/6] Kiểm tra PostgreSQL..." -ForegroundColor Yellow
if (-not $SkipDocker) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Stop-WithMessage 'Chưa cài Docker Desktop. Cài và khởi động Docker Desktop, hoặc dùng -SkipDocker với PostgreSQL 16 đang chạy cục bộ.'
    }
    & docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        Stop-WithMessage 'Docker Engine chưa sẵn sàng. Mở Docker Desktop, chờ trạng thái Engine running rồi chạy lại.'
    }
    $started = Invoke-WithLocalSecrets 'Docker' $localSecrets.Docker {
        # Chuyển log ra host để giá trị trả về chỉ báo mã thoát.
        & docker compose up -d postgres | Out-Host
        return $LASTEXITCODE -eq 0
    }
    if (-not $started) {
        Stop-WithMessage 'Không khởi động được PostgreSQL bằng Docker Compose. Kiểm tra cửa sổ Docker Desktop và cổng 5432.'
    }

    $databaseReady = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & docker exec dental_db pg_isready -U postgres -d dental_clinic *> $null
        if ($LASTEXITCODE -eq 0) {
            $databaseReady = $true
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $databaseReady) {
        Stop-WithMessage 'PostgreSQL không sẵn sàng sau 2 phút. Xem log container bằng: docker logs dental_db'
    }
}
elseif (-not (Test-TcpPort -Port 5432)) {
    Stop-WithMessage 'Đã chọn -SkipDocker nhưng không kết nối được PostgreSQL tại localhost:5432.'
}
Write-Host '  PostgreSQL đã sẵn sàng tại localhost:5432.' -ForegroundColor Green

# 4. Cài đúng phiên bản package trong package-lock.json ở lần chạy đầu.
Write-Host "`n[5/6] Kiểm tra package Frontend..." -ForegroundColor Yellow
$frontendDirectory = Join-Path $PSScriptRoot 'frontend'
$nodeModulesPath = Join-Path $frontendDirectory 'node_modules'
$packageLockPath = Join-Path $frontendDirectory 'package-lock.json'
if (-not (Test-Path -LiteralPath $packageLockPath)) {
    Stop-WithMessage "Không tìm thấy package-lock.json: $packageLockPath"
}
if (-not (Test-Path -LiteralPath $nodeModulesPath)) {
    Write-Host '  Lần đầu chạy: npm ci đang tải package theo lock file...' -ForegroundColor Cyan
    & npm.cmd --prefix $frontendDirectory ci
    if ($LASTEXITCODE -ne 0) {
        Stop-WithMessage 'npm ci thất bại. Kiểm tra kết nối Internet và thử lại.'
    }
}
else {
    Write-Host '  Đã có frontend/node_modules; bỏ qua cài package.' -ForegroundColor Green
}

# 5. Mở API và web ở cửa sổ riêng, sau đó xác nhận hai dịch vụ trả lời HTTP.
Write-Host "`n[6/6] Khởi động Backend API và Frontend..." -ForegroundColor Yellow
$shellPath = (Get-Process -Id $PID).Path
if (-not $shellPath) {
    Stop-WithMessage 'Không xác định được PowerShell hiện tại để mở cửa sổ chạy dịch vụ.'
}

if (-not (Test-TcpPort -Port 5000)) {
    Invoke-WithLocalSecrets 'Backend' $localSecrets.Backend {
        Start-Process -FilePath $shellPath -WorkingDirectory $PSScriptRoot -ArgumentList @(
            '-NoProfile', '-NoExit', '-ExecutionPolicy', 'Bypass', '-Command', 'dotnet run --project .\backend\src\Dental.Api'
        ) | Out-Null
    }
}
else {
    Write-Host '  Cổng 5000 đã có dịch vụ; dùng dịch vụ đang chạy.' -ForegroundColor Gray
}

if (-not (Test-TcpPort -Port 5173)) {
    Invoke-WithLocalSecrets 'Frontend' $localSecrets.Frontend {
        Start-Process -FilePath $shellPath -WorkingDirectory $frontendDirectory -ArgumentList @(
            '-NoProfile', '-NoExit', '-ExecutionPolicy', 'Bypass', '-Command', 'npm.cmd run dev -- --strictPort'
        ) | Out-Null
    }
}
else {
    Write-Host '  Cổng 5173 đã có dịch vụ; dùng dịch vụ đang chạy.' -ForegroundColor Gray
}

$apiReady = Wait-HttpEndpoint -Url 'http://localhost:5000/swagger/index.html' -Name 'Backend API' -TimeoutSeconds 180
$webReady = Wait-HttpEndpoint -Url 'http://localhost:5173/' -Name 'Frontend' -TimeoutSeconds 90
if (-not $apiReady -or -not $webReady) {
    Write-Host 'Kiểm tra cửa sổ Backend/Frontend để xem lỗi chi tiết. Database hiện có không bị xóa bởi script này.' -ForegroundColor Yellow
    exit 1
}

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host '  HỆ THỐNG ĐÃ SẴN SÀNG' -ForegroundColor Green
Write-Host '  Web       : http://localhost:5173' -ForegroundColor White
Write-Host '  Backend   : http://localhost:5000' -ForegroundColor White
Write-Host '  Swagger   : http://localhost:5000/swagger' -ForegroundColor White
Write-Host '  Tài khoản demo: xem bảng ở mục 5 của README.md.' -ForegroundColor Yellow
Write-Host '  Dừng API/web bằng cách đóng các cửa sổ dịch vụ tương ứng.' -ForegroundColor Gray
Write-Host '======================================================' -ForegroundColor Cyan
