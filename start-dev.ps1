# ==============================================================================
# SCRIPT KHỞI CHẠY TỰ ĐỘNG HỆ THỐNG PHÒNG KHÁM NHA KHOA HOÀNG GIA (PK_NK_HG)
# ==============================================================================

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  KHOI CHAY HE THONG PHONG KHAM NHA KHOA HOANG GIA    " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Kiem tra va khoi dong PostgreSQL container
Write-Host "`n[1/3] Kiem tra PostgreSQL Database..." -ForegroundColor Yellow
if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker compose up -d postgres
    Write-Host "  -> Container PostgreSQL dang khoi chay tai cong 5432." -ForegroundColor Green
} else {
    Write-Host "  -> Docker khong tim thay. Vui long dam bao PostgreSQL local dang chay cong 5432." -ForegroundColor Magenta
}

# 2. Khoi dong Backend API trong cua so rieng
Write-Host "`n[2/3] Khoi dong Backend Web API (.NET 10)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Dang chay Backend API tai http://localhost:5000...' -ForegroundColor Green; dotnet run --project backend/src/Dental.Api"

# 3. Khoi dong Frontend SPA trong cua so rieng
Write-Host "`n[3/3] Khoi dong Frontend SPA (Vue 3 + Vite)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Dang chay Frontend SPA tai http://localhost:5173...' -ForegroundColor Green; cd frontend; npm run dev"

Write-Host "`n>>> HE THONG DANG KHOI CHAY THANH CONG! <<<" -ForegroundColor Green
Write-Host "  - Frontend URL : http://localhost:5173" -ForegroundColor White
Write-Host "  - Backend API  : http://localhost:5000" -ForegroundColor White
Write-Host "  - Swagger UI   : http://localhost:5000/swagger" -ForegroundColor White
Write-Host "  - Tai khoan test: 0900000001 / AdminPassword@123" -ForegroundColor White
Write-Host "======================================================" -ForegroundColor Cyan
