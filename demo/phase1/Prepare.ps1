#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
foreach ($tool in @('dotnet','node','npm','docker')) { Get-Command $tool -ErrorAction Stop | Out-Null }
Push-Location $repo
try {
    dotnet restore backend/DentalClinic.sln --disable-parallel
    if ($LASTEXITCODE -ne 0) { throw 'Khôi phục NuGet thất bại.' }
    dotnet build backend/src/Dental.Api/Dental.Api.csproj --no-restore -m:1 /p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build API thất bại.' }
    Push-Location frontend
    try {
        npm ci --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Cài dependency frontend thất bại.' }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Build frontend thất bại.' }
    } finally { Pop-Location }
    Write-Host 'Đã chuẩn bị mã nguồn. Chạy demo/phase1/Start.ps1 để mở môi trường demo.'
} finally { Pop-Location }
