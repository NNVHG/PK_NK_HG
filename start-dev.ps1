# ==============================================================================
# SCRIPT KHOI CHAY TU DONG HE THONG PHONG KHAM NHA KHOA HOANG GIA (PK_NK_HG)
# Tuong thich: Windows PowerShell 5.1+ va PowerShell 7+
# ==============================================================================
param(
    [switch]$SkipDocker
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Test-PortListening([int]$Port) {
    $conn = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
    return ($null -ne $conn)
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  KHOI CHAY HE THONG PHONG KHAM NHA KHOA HOANG GIA    " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Kiem tra cong cu bat buoc (.NET SDK & Node.js)
Write-Host "`n[1/5] Kiem tra cong cu yeu cau (.NET SDK & Node.js)..." -ForegroundColor Yellow
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "  [LOI] Chua cai dat .NET SDK 10.0. Tai tai: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Red
    exit 1
}
if (-not (Get-Command node -ErrorAction SilentlyContinue) -or -not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    Write-Host "  [LOI] Chua cai dat Node.js. Tai tai: https://nodejs.org/" -ForegroundColor Red
    exit 1
}
Write-Host "  -> .NET SDK: $(dotnet --version) | Node.js: $(node -v)" -ForegroundColor Green

# 2. Tu dong tao cau hinh Development mau neu chua co
Write-Host "`n[2/5] Kiem tra tep cau hinh moi truong Development..." -ForegroundColor Yellow
$devConfigPath = Join-Path $PSScriptRoot "backend/src/Dental.Api/appsettings.Development.json"
$exampleConfigPath = Join-Path $PSScriptRoot "backend/src/Dental.Api/appsettings.Development.example.json"

if (-not (Test-Path $devConfigPath)) {
    if (Test-Path $exampleConfigPath) {
        Copy-Item -Path $exampleConfigPath -Destination $devConfigPath -Force
        Write-Host "  -> Da tu dong tao appsettings.Development.json tu tep mau (bat san du lieu demo 5 vai tro)." -ForegroundColor Green
    } else {
        Write-Host "  [LOI] Khong tim thay tep mau: $exampleConfigPath" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "  -> Da co san appsettings.Development.json." -ForegroundColor Green
}

# 3. Khoi dong co so du lieu PostgreSQL 16
Write-Host "`n[3/5] Kiem tra va khoi dong PostgreSQL (cong 5432)..." -ForegroundColor Yellow
if (-not $SkipDocker -and (Get-Command docker -ErrorAction SilentlyContinue)) {
    $existingContainer = docker ps -a --filter "name=^/dental_db$" --format "{{.Names}}" 2>$null
    if ($existingContainer -eq "dental_db") {
        docker start dental_db *> $null
    } else {
        docker compose up -d postgres
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [CANH BAO] Khong the khoi dong container 'dental_db'. Hay bat Docker Desktop hoac dam bao PostgreSQL 16 local dang chay cong 5432." -ForegroundColor Magenta
    } else {
        Write-Host "  -> Dang doi PostgreSQL trong container 'dental_db' san sang..." -ForegroundColor Gray
        for ($i = 0; $i -lt 30; $i++) {
            docker exec dental_db pg_isready -U postgres -d dental_clinic *> $null
            if ($LASTEXITCODE -eq 0) { break }
            Start-Sleep -Seconds 1
        }
        Write-Host "  -> Co so du lieu PostgreSQL (dental_db) da san sang tai cong 5432." -ForegroundColor Green
    }
} else {
    Write-Host "  -> Bo qua Docker. Dam bao PostgreSQL 16 local dang chay cong 5432 (Database: dental_clinic)." -ForegroundColor Magenta
}

# 4. Cai dat thu vien Frontend lan dau (neu chua co node_modules)
Write-Host "`n[4/5] Kiem tra thu vien giao dien (frontend/node_modules)..." -ForegroundColor Yellow
$nodeModulesPath = Join-Path $PSScriptRoot "frontend/node_modules"
if (-not (Test-Path $nodeModulesPath)) {
    Write-Host "  -> Lan chay dau tien: dang cai dat thu vien npm cho Frontend (vui long doi 15-30 giay)..." -ForegroundColor Cyan
    npm.cmd --prefix (Join-Path $PSScriptRoot "frontend") install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [LOI] Cai dat thu vien npm that bai." -ForegroundColor Red
        exit 1
    }
    Write-Host "  -> Da cai dat xong thu vien Frontend." -ForegroundColor Green
} else {
    Write-Host "  -> Da co san frontend/node_modules." -ForegroundColor Green
}

# 5. Khoi chay Backend API (cong 5000) va Frontend Vite (cong 5173)
Write-Host "`n[5/5] Khoi dong Backend Web API (.NET 10) va Frontend (Vue 3)..." -ForegroundColor Yellow

if (Test-PortListening -Port 5000) {
    Write-Host "  -> Cong 5000 dang hoat dong (Backend API da chay san)." -ForegroundColor Green
} else {
    Start-Process powershell -WorkingDirectory $PSScriptRoot -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", "Write-Host 'Dang chay Backend API tai http://localhost:5000 (tu dong migrate & seed du lieu demo)...' -ForegroundColor Green; dotnet run --project backend/src/Dental.Api"
    Write-Host "  -> Da mo cua so chay Backend API (http://localhost:5000)." -ForegroundColor Green
}

if (Test-PortListening -Port 5173) {
    Write-Host "  -> Cong 5173 dang hoat dong (Frontend da chay san)." -ForegroundColor Green
} else {
    Start-Process powershell -WorkingDirectory (Join-Path $PSScriptRoot "frontend") -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", "Write-Host 'Dang chay Frontend SPA tai http://localhost:5173...' -ForegroundColor Green; npm.cmd run dev"
    Write-Host "  -> Da mo cua so chay Frontend SPA (http://localhost:5173)." -ForegroundColor Green
}

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host "  HE THONG DANG KHOI DONG! DIA CHI TRUY CAP:          " -ForegroundColor Green
Write-Host "  - Giao dien Web : http://localhost:5173" -ForegroundColor White
Write-Host "  - Backend API   : http://localhost:5000" -ForegroundColor White
Write-Host "  - Swagger UI    : http://localhost:5000/swagger" -ForegroundColor White
Write-Host "------------------------------------------------------" -ForegroundColor Cyan
Write-Host "  TAI KHOAN THU NGHIEM (chi dung cho moi truong Demo):" -ForegroundColor Yellow
Write-Host "  - Admin         : 0900000001 / AdminPassword@123" -ForegroundColor White
Write-Host "  - Le tan        : 0900000002 / DemoPassword@123" -ForegroundColor White
Write-Host "  - Nha si        : 0900000003 / DemoPassword@123" -ForegroundColor White
Write-Host "  - Phu ta        : 0900000004 / DemoPassword@123" -ForegroundColor White
Write-Host "  - Benh nhan     : 0900000005 / DemoPassword@123" -ForegroundColor White
Write-Host "======================================================" -ForegroundColor Cyan
