param(
    [string]$BaseUrl = 'http://localhost:5000',
    [Parameter(Mandatory)][switch]$IsolatedTestDatabase
)

# Chỉ chạy trên API nối CSDL kiểm chứng riêng; dữ liệu tổng hợp nằm trong test.
# Không in credential, token, response bệnh nhân hoặc nội dung lâm sàng.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$config = Get-Content (Join-Path $root 'backend/src/Dental.Api/appsettings.Development.json') -Raw | ConvertFrom-Json
$results = [Collections.Generic.List[object]]::new()
$auth = @{}
$users = @{}
$stamp = [DateTime]::UtcNow.ToString('HHmmss')
$password = 'VerifyA1_' + [Guid]::NewGuid().ToString('N')
function Assert-Check($label, $condition) {
    $results.Add([pscustomobject]@{ check=$label; passed=[bool]$condition })
    if (!$condition) { throw "Kiểm chứng không đạt: $label" }
}
function Api($label, $method, $path, $role, $body, $expected=200) {
    $parameters = @{ Uri="$BaseUrl$path"; Method=$method; SkipHttpErrorCheck=$true; TimeoutSec=30 }
    if ($role) { $parameters.Headers=$auth[$role] }
    if ($null -ne $body) { $parameters.ContentType='application/json'; $parameters.Body=$body|ConvertTo-Json -Depth 12 }
    $response = Invoke-WebRequest @parameters
    if ($response.StatusCode -eq 429 -and $path.StartsWith('/api/auth/') -and $expected -ne 429) {
        Assert-Check 'Rate limit vẫn được thực thi khi chạy lại test' $true
        Write-Host 'Đạt giới hạn xác thực; chờ hết cửa sổ 60 giây, không đổi cấu hình bảo vệ.'
        Start-Sleep -Seconds 60
        $response = Invoke-WebRequest @parameters
    }
    Assert-Check "$label ($method $path HTTP $expected, thực tế $($response.StatusCode))" ([int]$response.StatusCode -eq $expected)
    if ($response.Content -and $response.Headers.'Content-Type' -match 'json') { return ($response.Content|ConvertFrom-Json) }
}
function B64($bytes) { [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_') }
try {
    Assert-Check 'Đã xác nhận CSDL kiểm thử riêng' $IsolatedTestDatabase.IsPresent
    Assert-Check 'Chỉ kiểm chứng trên localhost' ($BaseUrl -match '^http://(localhost|127\.0\.0\.1):\d+$')
    $accounts=@(
        @{role='ADMIN';phone=$config.Seed.AdminPhone;password=$config.Seed.AdminPassword},
        @{role='RECEPTIONIST';phone='0900000002';password=$config.Seed.DemoPassword},
        @{role='DENTIST';phone='0900000003';password=$config.Seed.DemoPassword},
        @{role='ASSISTANT';phone='0900000004';password=$config.Seed.DemoPassword},
        @{role='PATIENT';phone='0900000005';password=$config.Seed.DemoPassword})
    foreach($account in $accounts) {
        $login=Api "Đăng nhập $($account.role)" POST '/api/auth/login' $null @{phone=$account.phone;password=$account.password}
        Assert-Check "Vai trò $($account.role)" ($login.roleCode -eq $account.role)
        $auth[$account.role]=@{Authorization="Bearer $($login.accessToken)"}; $users[$account.role]=$login.userId
        $null=Api 'Hồ sơ tài khoản' GET '/api/auth/me' $account.role $null
    }
    $null=Api 'Sai mật khẩu' POST '/api/auth/login' $null @{phone=$config.Seed.AdminPhone;password=$password} 401
    $null=Api 'Không token' GET '/api/auth/me' $null $null 401
    $auth.BAD=@{Authorization='Bearer invalid-token'}
    $null=Api 'Token không hợp lệ' GET '/api/auth/me' BAD $null 401
    $encodedHeader=B64 ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $claims=@{sub="$($users.ADMIN)";iss=$config.Jwt.Issuer;aud=$config.Jwt.Audience;nbf=$now-7200;exp=$now-3600}|ConvertTo-Json -Compress
    $unsigned=$encodedHeader+'.'+(B64 ([Text.Encoding]::UTF8.GetBytes($claims)))
    $hmac=[Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($config.Jwt.Secret))
    $auth.EXPIRED=@{Authorization='Bearer '+$unsigned+'.'+(B64 ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($unsigned))))}; $hmac.Dispose()
    $null=Api 'Token đã hết hạn' GET '/api/auth/me' EXPIRED $null 401

    foreach($role in @('ADMIN','RECEPTIONIST','DENTIST','ASSISTANT','PATIENT')) {
        foreach($path in @('/api/staff','/api/audit-logs')) {
            $expected=if($role -eq 'ADMIN'){200}else{403}
            $null=Api "RBAC $role" GET $path $role $null $expected
        }
        $expected=if($role -eq 'PATIENT'){403}else{200}
        $null=Api "Hàng đợi $role" GET '/api/queue' $role $null $expected
        $expected=if($role -in @('ADMIN','DENTIST','ASSISTANT')){200}else{403}
        $null=Api "Danh mục $role" GET '/api/services' $role $null $expected
        $null=Api "Lịch hẹn $role" GET '/api/appointments' $role $null
    }
    $staffPhone='08'+$stamp+'01'
    $staff=Api 'Tạo nhân viên' POST '/api/staff' ADMIN @{fullName='Kiểm chứng nhân viên';phone=$staffPhone;password=$password;roleCode='DENTIST'} 201
    $staffId=$staff.userId
    $null=Api 'Sửa nhân viên' PUT "/api/staff/$staffId" ADMIN @{fullName='Kiểm chứng nhân viên đã sửa';phone=$staffPhone;roleCode='DENTIST'}
    $null=Api 'Khóa nhân viên' POST "/api/staff/$staffId/lock" ADMIN $null
    $null=Api 'Nhân viên khóa không đăng nhập' POST '/api/auth/login' $null @{phone=$staffPhone;password=$password} 403
    $null=Api 'Mở khóa nhân viên' POST "/api/staff/$staffId/unlock" ADMIN $null

    $registration=@{fullName='Kiểm chứng tài khoản bệnh nhân';phone='08'+$stamp+'02';password=$password;dateOfBirth='1990-01-01';gender='Other'}
    $null=Api 'Đăng ký và tự tạo hồ sơ' POST '/api/auth/register' $null $registration
    $login=Api 'Đăng nhập bệnh nhân mới' POST '/api/auth/login' $null @{phone=$registration.phone;password=$password}
    $auth.OWNER=@{Authorization="Bearer $($login.accessToken)"}
    $mine=@(Api 'Hồ sơ thuộc tài khoản' GET '/api/patients/mine' OWNER $null)
    Assert-Check 'Đăng ký sinh hồ sơ sở hữu' ($mine.Count -eq 1)
    $ownerId=$mine[0].patientId
    $profile=@{fullName=$registration.fullName;phone=$registration.phone;dateOfBirth='1990-01-01';gender='Other';email='verify@example.test'}
    $null=Api 'Cập nhật hồ sơ tài khoản' PUT '/api/auth/profile' OWNER $profile
    $null=Api 'Đổi mật khẩu tài khoản test' POST '/api/auth/change-password' OWNER @{oldPassword=$password;newPassword=$password+'B2'}

    $patientData=@{fullName='Kiểm chứng hồ sơ độc lập';phone='08'+$stamp+'03';dateOfBirth='1990-01-01';gender='Other';confirmNotDuplicate=$true}
    $patient=Api 'Tạo bệnh nhân' POST '/api/patients' RECEPTIONIST $patientData 201
    $patientId=$patient.patientId
    $null=Api 'Tìm kiếm bệnh nhân' GET "/api/patients?keyword=$($patient.patientCode)" RECEPTIONIST $null
    $null=Api 'Kiểm tra trùng bệnh nhân' POST '/api/patients/check-duplicate' RECEPTIONIST @{fullName=$patientData.fullName;phone=$patientData.phone;dateOfBirth=$patientData.dateOfBirth}
    $patientData.fullName='Kiểm chứng hồ sơ đã sửa'
    $null=Api 'Cập nhật bệnh nhân' PUT "/api/patients/$patientId" RECEPTIONIST $patientData
    $null=Api 'Chặn đọc bệnh nhân người khác' GET "/api/patients/$patientId" OWNER $null 404
    $null=Api 'Chặn phụ tá tạo bệnh nhân' POST '/api/patients' ASSISTANT $patientData 403

    $queue=Api 'Check-in sinh lần khám' POST '/api/queue/check-in' RECEPTIONIST @{patientId=$patientId;dentistId=$users.DENTIST} 201
    $visitId=$queue.visitId; $queueId=$queue.queueEntryId
    $null=Api 'Chặn check-in trùng' POST '/api/queue/check-in' RECEPTIONIST @{patientId=$patientId} 409
    $null=Api 'Đọc lần khám' GET "/api/visits/$visitId" ADMIN $null
    $null=Api 'Tiền sử bệnh' POST "/api/visits/$visitId/medical-history" ASSISTANT @{items=@(@{type='Allergy';name='Dữ liệu dị ứng kiểm chứng';isCritical=$true;detail='Dữ liệu tổng hợp trong test'})} 201
    $null=Api 'Tiền sử mới nhất' GET "/api/patients/$patientId/medical-history/latest" RECEPTIONIST $null
    $null=Api 'Lịch sử tiền sử' GET "/api/patients/$patientId/medical-history" RECEPTIONIST $null
    $null=Api 'Cảnh báo an toàn' GET "/api/patients/$patientId/safety-alerts" DENTIST $null
    $null=Api 'Ghi sinh hiệu' POST "/api/visits/$visitId/vital-signs" ASSISTANT @{systolicBp=120;diastolicBp=80;pulseBpm=70;temperatureC=36.5} 201
    $null=Api 'Đọc sinh hiệu' GET "/api/patients/$patientId/vital-signs" DENTIST $null
    $null=Api 'Chặn sinh hiệu sai' POST "/api/visits/$visitId/vital-signs" ASSISTANT @{pulseBpm=-1} 400
    $null=Api 'Bắt đầu khám' PUT "/api/queue/$queueId/status" DENTIST @{newStatus=2;dentistId=$users.DENTIST}
    $null=Api 'Lưu chẩn đoán' PUT "/api/visits/$visitId/diagnosis" DENTIST @{diagnosis='Chẩn đoán tổng hợp kiểm chứng';clinicalNotes='Ghi chú tổng hợp kiểm chứng'}
    $null=Api 'FDI người lớn' POST "/api/visits/$visitId/tooth-conditions" DENTIST @{toothNumber=11;surface='B';conditionCode='CARIES'} 201
    $null=Api 'FDI trẻ em' POST "/api/visits/$visitId/tooth-conditions" DENTIST @{toothNumber=51;surface=$null;conditionCode='CARIES'} 201
    $null=Api 'Chặn răng không chuẩn FDI' POST "/api/visits/$visitId/tooth-conditions" DENTIST @{toothNumber=19;surface='B';conditionCode='CARIES'} 400
    $conditions=@(Api 'Đọc lại FDI' GET "/api/visits/$visitId/tooth-conditions" DENTIST $null)
    Assert-Check 'FDI lưu đủ hai bộ răng' ($conditions.Count -eq 2)

    $service=Api 'Tạo dịch vụ có giá' POST '/api/services' ADMIN @{code='VERIFY_'+$stamp;name='Dịch vụ kiểm chứng';durationMinutes=30;initialPrice=100000;effectiveFrom=[DateTimeOffset]::UtcNow.ToString('o')} 201
    $serviceId=$service.dentalServiceId
    $null=Api 'Chỉ định batch nhiều răng' POST "/api/visits/$visitId/services" DENTIST @{serviceId=$serviceId;toothNumbers=@(11,21);quantity=1;surface='B'} 201
    $invoice=Api 'Sinh nháp từ dịch vụ' GET "/api/invoices/by-visit/$visitId" RECEPTIONIST $null
    Assert-Check 'Nháp hai dòng đúng tổng tiền' ($invoice.status -eq 0 -and $invoice.totalAmount -eq 200000 -and $invoice.items.Count -eq 2)
    $null=Api 'Chỉ định toàn hàm' POST "/api/visits/$visitId/services" DENTIST @{serviceId=$serviceId;toothNumbers=$null;quantity=2;surface=$null} 201
    $synced=Api 'Đồng bộ nháp sau thêm dịch vụ' GET "/api/invoices/by-visit/$visitId" RECEPTIONIST $null
    Assert-Check 'Đồng bộ giữ mã và đúng tổng' ($synced.id -eq $invoice.id -and $synced.totalAmount -eq 400000 -and $synced.items.Count -eq 3)
    $null=Api 'Thêm lịch sử giá' POST "/api/services/$serviceId/prices" ADMIN @{amount=150000;effectiveFrom=[DateTimeOffset]::UtcNow.ToString('o')} 201
    $assigned=@(Api 'Snapshot giá dịch vụ cũ' GET "/api/visits/$visitId/services" DENTIST $null)
    Assert-Check 'Chỉ định giữ giá snapshot' (@($assigned|Where-Object unitPrice -ne 100000).Count -eq 0)
    $null=Api 'Kết thúc khám và khóa' PUT "/api/visits/$visitId/complete" DENTIST $null
    $pending=Api 'Hóa đơn chờ thanh toán' GET "/api/invoices/by-visit/$visitId" RECEPTIONIST $null
    Assert-Check 'Kết thúc giữ hóa đơn và chuyển PendingPayment' ($pending.id -eq $invoice.id -and $pending.status -eq 1 -and $pending.totalAmount -eq 400000)
    $null=Api 'Chặn ghi FDI sau khóa' POST "/api/visits/$visitId/tooth-conditions" DENTIST @{toothNumber=12;surface='B';conditionCode='CARIES'} 409
    $null=Api 'Chặn nha sĩ mở khóa' POST "/api/visits/$visitId/unlock" DENTIST @{unlockReason='Lý do tổng hợp kiểm chứng'} 403
    $null=Api 'Admin mở lại khám' POST "/api/visits/$visitId/unlock" ADMIN @{unlockReason='Lý do tổng hợp kiểm chứng'} 204
    $history=@(Api 'Lịch sử mở khóa Admin' GET "/api/visits/$visitId/unlock-history" ADMIN $null)
    Assert-Check 'Giữ lịch sử và hóa đơn đã hủy' ($history.Count -eq 1 -and $history[0].cancelledInvoiceId -eq $invoice.id)
    $null=Api 'Chặn đọc lịch sử mở khóa' GET "/api/visits/$visitId/unlock-history" DENTIST $null 403
    $reopened=Api 'Sinh nháp mới sau mở lại' GET "/api/invoices/by-visit/$visitId" RECEPTIONIST $null
    Assert-Check 'Nháp mới giữ dòng và có mã mới' ($reopened.id -ne $invoice.id -and $reopened.invoiceCode -ne $invoice.invoiceCode -and $reopened.items.Count -eq 3)
    $null=Api 'Khóa lại hồ sơ' POST "/api/visits/$visitId/lock" DENTIST $null
    $null=Api 'Timeline điều trị' GET "/api/patients/$patientId/timeline" DENTIST $null
    $null=Api 'Danh sách lần khám' GET "/api/patients/$patientId/visits" RECEPTIONIST $null
    $logs=Api 'Đọc nhật ký thao tác' GET '/api/audit-logs?pageSize=100' ADMIN $null
    $unlockLogs=@($logs.items|Where-Object action -eq 'MOD_PAT_VISIT_UNLOCKED')
    Assert-Check 'Audit có mở khóa nhưng không chứa lý do nguyên văn' ($unlockLogs.Count -gt 0 -and @($unlockLogs|Where-Object { $_.detail -match 'Lý do tổng hợp' }).Count -eq 0)
    $tomorrow=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'SE Asia Standard Time').AddDays(1).ToString('yyyy-MM-dd')
    $appointment=Api 'Bệnh nhân đặt lịch thuộc hồ sơ' POST '/api/appointments' OWNER @{patientId=$ownerId;appointmentDate=$tomorrow;slotTime='09:00:00';dentistId=$users.DENTIST} 201
    $appointmentId=$appointment.appointmentId
    $null=Api 'Đổi lịch tại quầy' PUT "/api/appointments/$appointmentId/reschedule" RECEPTIONIST @{appointmentDate=$tomorrow;slotTime='09:30:00';dentistId=$users.DENTIST}
    $null=Api 'Hủy lịch tại quầy' PUT "/api/appointments/$appointmentId/cancel" RECEPTIONIST @{reason='Kiểm chứng hủy lịch'}
    $null=Api 'Chặn đặt lịch hồ sơ người khác' POST '/api/appointments' OWNER @{patientId=$patientId;appointmentDate=$tomorrow;slotTime='10:00:00'} 404
    $null=Api 'Ngừng dịch vụ' POST "/api/services/$serviceId/deactivate" ADMIN $null
    $null=Api 'Kích hoạt dịch vụ' POST "/api/services/$serviceId/activate" ADMIN $null
    $null=Api 'Đăng xuất' POST '/api/auth/logout' OWNER $null
} finally {
    $output=Join-Path $root 'docs/testing/MAIN_VERIFICATION_API_RESULTS.json'
    $results|ConvertTo-Json -Depth 5|Set-Content $output -Encoding utf8
    $pass=@($results|Where-Object passed).Count; $fail=$results.Count-$pass
    Write-Output "API checks: total=$($results.Count), pass=$pass, fail=$fail"
    Write-Output "Report: $output"
}
