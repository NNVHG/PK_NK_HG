#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$runtime = Join-Path $PSScriptRoot '.runtime'
$stateFile = Join-Path $runtime 'processes.json'
if (Test-Path -LiteralPath $stateFile) {
    foreach ($saved in @(Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json)) {
        $process = Get-Process -Id $saved.Id -ErrorAction SilentlyContinue
        if ($process -and $process.StartTime.ToUniversalTime().Ticks -eq $saved.Started -and $process.Path -eq $saved.Executable) {
            Stop-Process -InputObject $process
        }
    }
    Remove-Item -LiteralPath $stateFile
}
$envFile = Join-Path $runtime 'database.env'
if (Test-Path -LiteralPath $envFile) {
    docker compose --project-name dental-phase1 --env-file $envFile -f (Join-Path $PSScriptRoot 'compose.yml') stop
    if ($LASTEXITCODE -ne 0) { throw 'Chưa dừng được container demo.' }
}
Write-Host 'Đã dừng demo. Database và tệp tải lên vẫn được giữ để chạy lại.'
