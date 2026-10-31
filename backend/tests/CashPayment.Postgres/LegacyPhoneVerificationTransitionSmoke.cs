using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Features.Staff.Services;
using Dental.Application.Features.Staff.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class LegacyPhoneVerificationTransitionSmoke
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SimulatedActivationSms : IPatientActivationDelivery
    {
        public bool IsConfigured { get; set; } = true;
        public Dictionary<string, ActivationDispatch> Dispatches { get; } = [];
        public bool Enqueue(ActivationDispatch dispatch)
        {
            Dispatches[dispatch.Phone] = dispatch;
            return true;
        }
    }

    private sealed class SimulatedRecoverySms : IPasswordRecoveryDelivery
    {
        public bool IsConfigured { get; set; } = true;
        public Dictionary<string, string> Codes { get; } = [];
        public bool Enqueue(string phone, RecoveryCode code)
        {
            Codes[phone] = code.Code;
            return true;
        }
    }

    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        // 1. Guard chống chạy trực tiếp trên database production 'dental_db'
        await using (var prodDb = new DentalDbContext(
            new DbContextOptionsBuilder<DentalDbContext>()
                .UseNpgsql("Host=localhost;Database=dental_db;Username=postgres")
                .Options))
        {
            var previewBlocked = false;
            var transitionBlocked = false;
            try { await LegacyPhoneVerificationTransitionRunner.PreviewAsync(prodDb); }
            catch (InvalidOperationException) { previewBlocked = true; }
            try { await LegacyPhoneVerificationTransitionRunner.TransitionLegacyAccountsAsync(prodDb, 1); }
            catch (InvalidOperationException) { transitionBlocked = true; }
            check(previewBlocked && transitionBlocked,
                "Guard từ chối preview và chuyển tiếp OTP legacy trên CSDL production 'dental_db'");
        }

        var clock = new Clock();
        var activationSms = new SimulatedActivationSms();
        var recoverySms = new SimulatedRecoverySms();
        var identityHasher = new KeyedIdentityHasher(new byte[32]);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Jwt:Secret"] = secret;
        builder.Configuration["RateLimiting:Auth:RecoveryRequest:PermitLimit"] = "100";
        builder.Configuration["RateLimiting:Auth:RecoveryReset:PermitLimit"] = "100";

        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddSingleton<VietnamClock>();
        builder.Services.AddSingleton<IPatientActivationDelivery>(activationSms);
        builder.Services.AddSingleton<IPatientActivationProofs, PatientActivationProofs>();
        builder.Services.AddSingleton<IPasswordRecoveryDelivery>(recoverySms);
        builder.Services.AddSingleton<IPasswordRecoveryCodes, PasswordRecoveryCodes>();
        builder.Services.AddSingleton<IKeyedIdentityHasher>(identityHasher);
        builder.Services.AddScoped(_ => new DentalDbContext(options));
        builder.Services.AddScoped<IPatientActivationRepository, PatientActivationRepository>();
        builder.Services.AddScoped<PatientActivationService>();
        builder.Services.AddScoped<IPasswordRecoveryRepository, PasswordRecoveryRepository>();
        builder.Services.AddScoped<PasswordRecoveryService>();
        builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IPatientRepository, PatientRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IAuditLogger, AuditLogger>();
        builder.Services.AddScoped<ITokenService, JwtTokenService>();
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<LoginRequestValidator>();
        builder.Services.AddScoped<StaffService>();
        builder.Services.AddScoped<CreateStaffRequestValidator>();
        builder.Services.AddScoped<StaffQueryRequestValidator>();
        builder.Services.AddScoped<UpdateStaffRequestValidator>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
        {
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "DentalClinic",
                ValidateAudience = true,
                ValidAudience = "DentalClinicClient",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.Zero
            };
            jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync };
        });
        builder.Services.AddAuthorization(o => o.AddApplicationPolicies());
        builder.Services.AddRateLimiter(o =>
        {
            AuthRateLimitPolicies.Configure(o, builder.Configuration);
            PatientActivationRateLimits.Configure(o);
        });

        await using var app = builder.Build();
        var ipSeq = 0;
        app.Use(async (ctx, next) =>
        {
            var n = Interlocked.Increment(ref ipSeq);
            ctx.Connection.RemoteIpAddress = IPAddress.Parse($"10.3.{n / 250}.{n % 250 + 1}");
            try { await next(ctx); } catch { ctx.Response.StatusCode = 500; }
        });
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        await using var db = new DentalDbContext(options);
        var hasher = new BCryptPasswordHasher();
        const string legacyPass = "LegacySecret123!";
        var initialPassHash = hasher.Hash(legacyPass);

        var roles = new Dictionary<string, Role>();
        foreach (var code in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient })
        {
            var r = new Role { RoleCode = code, RoleName = "Role " + code };
            roles[code] = r;
            db.Roles.Add(r);
        }
        await db.SaveChangesAsync();

        // 2. Tạo tài khoản trên cả 5 vai trò (4 vai trò nhân viên + Patient CCCD & Passport) ở trạng thái legacy chưa xác minh,
        // kèm các tài khoản không thuộc diện chuyển đổi (Verified, InitialActivation, PhoneChange, Inactive).
        var legacyAdmin = new User
        {
            Role = roles[RoleCodes.Admin], FullName = "Legacy Admin", Phone = "0971000001",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null, TokenVersion = 1
        };
        var legacyReceptionist = new User
        {
            Role = roles[RoleCodes.Receptionist], FullName = "Legacy Receptionist", Phone = "0971000002",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null, TokenVersion = 2
        };
        var legacyDentist = new User
        {
            Role = roles[RoleCodes.Dentist], FullName = "Legacy Dentist", Phone = "0971000003",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null, TokenVersion = 3
        };
        var legacyAssistant = new User
        {
            Role = roles[RoleCodes.Assistant], FullName = "Legacy Assistant", Phone = "0971000004",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null, TokenVersion = 4
        };
        var legacyPatientCccd = new User
        {
            Role = roles[RoleCodes.Patient], FullName = "Legacy Patient CCCD", Phone = "0971000005",
            LoginByCitizenId = true, PasswordHash = initialPassHash, IsActive = true,
            RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null, TokenVersion = 5
        };
        var legacyPatientPassport = new User
        {
            Role = roles[RoleCodes.Patient], FullName = "Legacy Patient Passport", Phone = "0971000006",
            LoginByCitizenId = true, PasswordHash = initialPassHash, IsActive = true,
            RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null, TokenVersion = 6
        };
        var verifiedDentist = new User
        {
            Role = roles[RoleCodes.Dentist], FullName = "Already Verified Dentist", Phone = "0971000010",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false,
            PhoneVerifiedAtUtc = clock.Now.UtcDateTime, TokenVersion = 7
        };
        var pendingInitialStaff = new User
        {
            Role = roles[RoleCodes.Assistant], FullName = "Pending Initial Staff", Phone = "0971000011",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = true,
            PhoneActivationReason = PhoneActivationReason.InitialActivation, TokenVersion = 8
        };
        var pendingPhoneChangeStaff = new User
        {
            Role = roles[RoleCodes.Receptionist], FullName = "Pending Phone Change Staff", Phone = "0971000012",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = true,
            PhoneActivationReason = PhoneActivationReason.PhoneChange, TokenVersion = 9
        };
        var inactiveStaff = new User
        {
            Role = roles[RoleCodes.Assistant], FullName = "Inactive Staff", Phone = "0971000013",
            PasswordHash = initialPassHash, IsActive = false, RequiresPhoneActivation = false, TokenVersion = 10
        };

        db.Users.AddRange(
            legacyAdmin, legacyReceptionist, legacyDentist, legacyAssistant,
            legacyPatientCccd, legacyPatientPassport,
            verifiedDentist, pendingInitialStaff, pendingPhoneChangeStaff, inactiveStaff);
        await db.SaveChangesAsync();

        const string patientCccdNumber = "079097000005";
        const string patientPassportNumber = "C9700006";
        const string patientPassportCountry = "JP";

        // Khi chưa gắn hồ sơ Patient định danh hợp lệ, bộ chạy chuyển tiếp phải từ chối an toàn trước khi ghi
        {
            var blocked = false;
            try
            {
                await LegacyPhoneVerificationTransitionRunner.TransitionLegacyAccountsAsync(db, legacyAdmin.UserId);
            }
            catch (InvalidOperationException)
            {
                blocked = true;
            }
            check(blocked && !await db.AuditLogs.AnyAsync(),
                "Chặn chuyển tiếp trước khi ghi khi còn tài khoản Patient chưa đối soát định danh hợp lệ");
        }

        var patientProfileCccd = new Patient
        {
            UserId = legacyPatientCccd.UserId, FullName = legacyPatientCccd.FullName, Phone = legacyPatientCccd.Phone,
            DateOfBirth = new DateOnly(1990, 5, 15),
            CitizenIdHash = identityHasher.Hash(patientCccdNumber, IdentityDocumentType.CitizenId),
            CitizenIdMasked = identityHasher.Mask(patientCccdNumber, IdentityDocumentType.CitizenId),
            DocumentType = IdentityDocumentType.CitizenId,
            HashScheme = IdentityHashScheme.HmacCanonical, IdentityKeyVersion = 1, IsActive = true
        };
        var patientProfilePassport = new Patient
        {
            UserId = legacyPatientPassport.UserId, FullName = legacyPatientPassport.FullName, Phone = legacyPatientPassport.Phone,
            DateOfBirth = new DateOnly(1992, 8, 20),
            CitizenIdHash = identityHasher.Hash(patientPassportNumber, IdentityDocumentType.Passport, patientPassportCountry),
            CitizenIdMasked = identityHasher.Mask(patientPassportNumber, IdentityDocumentType.Passport),
            DocumentType = IdentityDocumentType.Passport, IssuingCountryCode = patientPassportCountry,
            HashScheme = IdentityHashScheme.HmacCanonical, IdentityKeyVersion = 1, IsActive = true
        };
        db.Patients.AddRange(patientProfileCccd, patientProfilePassport);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // 3. Trước chuyển tiếp: cả 6 tài khoản legacy (4 vai trò nhân viên + 2 Patient CCCD/Passport) đăng nhập được và JWT hợp lệ
        var legacyTargets = new (User User, string LoginIdentifier, IdentityDocumentType? DocType, string? Country, string Label)[]
        {
            (legacyAdmin, legacyAdmin.Phone, null, null, "ADMIN"),
            (legacyReceptionist, legacyReceptionist.Phone, null, null, "RECEPTIONIST"),
            (legacyDentist, legacyDentist.Phone, null, null, "DENTIST"),
            (legacyAssistant, legacyAssistant.Phone, null, null, "ASSISTANT"),
            (legacyPatientCccd, patientCccdNumber, IdentityDocumentType.CitizenId, null, "PATIENT_CCCD"),
            (legacyPatientPassport, patientPassportNumber, IdentityDocumentType.Passport, patientPassportCountry, "PATIENT_PASSPORT")
        };

        var preTransitionJwts = new Dictionary<int, string>();
        foreach (var item in legacyTargets)
        {
            using var loginRes = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(item.LoginIdentifier, legacyPass, item.DocType, item.Country));
            check(loginRes.StatusCode == HttpStatusCode.OK,
                $"Trước chuyển tiếp: {item.Label} đăng nhập thành công bằng mật khẩu hiện có");
            var loginDto = (await loginRes.Content.ReadFromJsonAsync<LoginResponse>())!;
            preTransitionJwts[item.User.UserId] = loginDto.AccessToken;

            using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginDto.AccessToken);
            using var meRes = await client.SendAsync(meReq);
            var meDto = await meRes.Content.ReadFromJsonAsync<MeResponse>();
            check(meRes.StatusCode == HttpStatusCode.OK && meDto is { PhoneVerified: false },
                $"Trước chuyển tiếp: JWT của {item.Label} truy cập /api/auth/me hợp lệ với PhoneVerified = false");
        }

        // 4. Dry-run preview: báo đúng số lượng từng nhóm, không sửa DB, không sinh audit và không thu hồi JWT
        var dryRunResult = await LegacyPhoneVerificationTransitionRunner.TransitionLegacyAccountsAsync(
            db, legacyAdmin.UserId, isDryRun: true);
        check(dryRunResult is
        {
            IsDryRun: true,
            TotalInspectedActive: 9,
            TransitionedCount: 6,
            TransitionedAdmins: 1,
            TransitionedStaffNonAdmin: 3,
            TransitionedPatients: 2,
            AlreadyPendingLegacyCount: 0,
            AlreadyVerifiedCount: 1,
            OtherPendingCount: 2
        } && !await db.AuditLogs.AnyAsync(a => a.Action == LegacyPhoneVerificationTransitionRunner.TransitionAuditAction),
            "Dry-run preview trả đúng số lượng 6 tài khoản legacy (1 Admin, 3 Staff, 2 Patient) và không ghi dữ liệu");

        using (var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me"))
        {
            meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", preTransitionJwts[legacyAdmin.UserId]);
            using var meRes = await client.SendAsync(meReq);
            check(meRes.StatusCode == HttpStatusCode.OK, "Sau dry-run, JWT hiện tại của Admin vẫn hoạt động bình thường");
        }

        // 5. Kiểm tra rollback nguyên tử khi có lỗi giữa chừng trong transaction
        var rollbackCaught = false;
        try
        {
            await LegacyPhoneVerificationTransitionRunner.TransitionLegacyAccountsAsync(
                db,
                legacyAdmin.UserId,
                isDryRun: false,
                beforeCommitHook: () => throw new InvalidOperationException("Simulated mid-transaction failure"));
        }
        catch (InvalidOperationException)
        {
            rollbackCaught = true;
        }

        var adminAfterRollback = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == legacyAdmin.UserId);
        check(rollbackCaught &&
              !adminAfterRollback.RequiresPhoneActivation &&
              adminAfterRollback.PhoneActivationReason == null &&
              adminAfterRollback.TokenVersion == 1 &&
              !await db.AuditLogs.AnyAsync(a => a.Action == LegacyPhoneVerificationTransitionRunner.TransitionAuditAction),
            "Lỗi giữa transaction hoàn tác toàn bộ trạng thái RequiresPhoneActivation, TokenVersion và AuditLog");

        using (var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me"))
        {
            meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", preTransitionJwts[legacyAdmin.UserId]);
            using var meRes = await client.SendAsync(meReq);
            check(meRes.StatusCode == HttpStatusCode.OK, "Sau rollback, phiên JWT cũ vẫn còn hiệu lực");
        }

        // 6. Thực thi chuyển tiếp có kiểm soát (Commit)
        var commitResult = await LegacyPhoneVerificationTransitionRunner.TransitionLegacyAccountsAsync(
            db, legacyAdmin.UserId, isDryRun: false);
        check(commitResult is
        {
            IsDryRun: false,
            TotalInspectedActive: 9,
            TransitionedCount: 6,
            TransitionedAdmins: 1,
            TransitionedStaffNonAdmin: 3,
            TransitionedPatients: 2,
            AlreadyPendingLegacyCount: 0,
            AlreadyVerifiedCount: 1,
            OtherPendingCount: 2
        }, "Thực thi chuyển tiếp thành công cho đúng 6 tài khoản legacy thuộc cả 5 vai trò");

        // Kiểm tra bảo toàn PasswordHash, Role, Ownership, tăng TokenVersion và không ghi giả PhoneVerifiedAtUtc
        foreach (var item in legacyTargets)
        {
            var reloaded = await db.Users.AsNoTracking().Include(u => u.Role).SingleAsync(u => u.UserId == item.User.UserId);
            check(reloaded.RequiresPhoneActivation &&
                  reloaded.PhoneActivationReason == PhoneActivationReason.LegacyVerification &&
                  reloaded.PhoneVerifiedAtUtc == null &&
                  reloaded.PasswordHash == initialPassHash &&
                  reloaded.RoleId == item.User.RoleId &&
                  reloaded.TokenVersion == item.User.TokenVersion + 1,
                $"Tài khoản {item.Label} sau chuyển tiếp giữ nguyên PasswordHash/Role, tăng TokenVersion và đặt LegacyVerification");
        }

        var reloadedCccdProfile = await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == patientProfileCccd.PatientId);
        var reloadedPassportProfile = await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == patientProfilePassport.PatientId);
        check(reloadedCccdProfile.UserId == legacyPatientCccd.UserId &&
              reloadedPassportProfile.UserId == legacyPatientPassport.UserId,
            "Chuyển tiếp giữ nguyên quyền sở hữu hồ sơ Patient.UserId");

        // Kiểm tra các tài khoản không thuộc nhóm legacy hoàn toàn không bị biến đổi
        var untouchedVerified = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == verifiedDentist.UserId);
        var untouchedInitial = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == pendingInitialStaff.UserId);
        var untouchedPhoneChange = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == pendingPhoneChangeStaff.UserId);
        var untouchedInactive = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == inactiveStaff.UserId);
        check(!untouchedVerified.RequiresPhoneActivation && untouchedVerified.TokenVersion == 7 &&
              untouchedInitial.PhoneActivationReason == PhoneActivationReason.InitialActivation && untouchedInitial.TokenVersion == 8 &&
              untouchedPhoneChange.PhoneActivationReason == PhoneActivationReason.PhoneChange && untouchedPhoneChange.TokenVersion == 9 &&
              !untouchedInactive.IsActive && untouchedInactive.TokenVersion == 10,
            "Các tài khoản đã xác minh, chờ kích hoạt mới, chờ đổi số và bị khóa không bị ảnh hưởng bởi chuyển tiếp");

        // Kiểm tra AuditLogs không chứa PII/hash/SĐT/CCCD
        var transitionAudits = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == LegacyPhoneVerificationTransitionRunner.TransitionAuditAction)
            .ToListAsync();
        check(transitionAudits.Count == 6 &&
              transitionAudits.All(a =>
                  a.UserId == legacyAdmin.UserId &&
                  a.Detail != null &&
                  a.Detail.Contains("DL-366/DL-367") &&
                  a.Detail.Contains("LegacyVerification") &&
                  !a.Detail.Contains("0971000") &&
                  !a.Detail.Contains(patientCccdNumber) &&
                  !a.Detail.Contains(patientPassportNumber) &&
                  !a.Detail.Contains(initialPassHash)),
            "Audit chuyển tiếp ghi đủ 6 bản ghi chuẩn metadata DL-366/DL-367 và tuyệt đối không lộ SĐT/CCCD/PasswordHash");

        // 7. Chạy lặp lần 2 (Idempotency): 0 chuyển đổi mới, không tăng tiếp TokenVersion, không ghi trùng AuditLog
        var secondRun = await LegacyPhoneVerificationTransitionRunner.TransitionLegacyAccountsAsync(
            db, legacyAdmin.UserId, isDryRun: false);
        var auditCountAfterSecondRun = await db.AuditLogs.AsNoTracking()
            .CountAsync(a => a.Action == LegacyPhoneVerificationTransitionRunner.TransitionAuditAction);
        check(secondRun is
        {
            IsDryRun: false,
            TransitionedCount: 0,
            AlreadyPendingLegacyCount: 6,
            AlreadyVerifiedCount: 1,
            OtherPendingCount: 2
        } && auditCountAfterSecondRun == 6,
            "Chạy lặp lần 2 an toàn (idempotent): TransitionedCount = 0, AlreadyPendingLegacyCount = 6, không nhân bản audit");

        // 8. Sau chuyển tiếp: mọi JWT cũ bị thu hồi ngay lập tức; nhập đúng mật khẩu chỉ dẫn tới yêu cầu OTP
        foreach (var item in legacyTargets)
        {
            using (var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me"))
            {
                meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", preTransitionJwts[item.User.UserId]);
                using var meRes = await client.SendAsync(meReq);
                check(meRes.StatusCode == HttpStatusCode.Unauthorized,
                    $"Sau chuyển tiếp: JWT cũ của {item.Label} bị từ chối (401 Unauthorized)");
            }

            using (var loginRes = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(item.LoginIdentifier, legacyPass, item.DocType, item.Country)))
            {
                var body = await loginRes.Content.ReadAsStringAsync();
                check(loginRes.StatusCode == HttpStatusCode.Unauthorized &&
                      body.Contains("AUTH_ACTIVATION_REQUIRED") &&
                      !body.Contains("accessToken", StringComparison.OrdinalIgnoreCase),
                    $"Sau chuyển tiếp: {item.Label} nhập đúng mật khẩu chỉ nhận yêu cầu OTP, không được cấp JWT");
            }
        }

        // 9. Admin vẫn có thể khôi phục mật khẩu bằng OTP sau khi đã chuyển sang LegacyVerification
        clock.Now = clock.Now.AddSeconds(65);
        RecoveryResponse adminRecChallenge;
        using (var recReq = await client.PostAsJsonAsync("/api/auth/password-recovery/request", new RecoveryRequest(legacyAdmin.Phone)))
        {
            check(recReq.StatusCode == HttpStatusCode.Accepted, "Sau chuyển tiếp: Admin yêu cầu OTP khôi phục mật khẩu thành công");
            adminRecChallenge = (await recReq.Content.ReadFromJsonAsync<RecoveryResponse>())!;
        }
        var adminRecCode = recoverySms.Codes[legacyAdmin.Phone];
        using (var recReset = await client.PostAsJsonAsync("/api/auth/password-recovery/reset",
            new ResetPasswordRequest(adminRecChallenge.ChallengeId, legacyAdmin.Phone, adminRecCode, legacyPass)))
        {
            check(recReset.StatusCode == HttpStatusCode.OK, "Sau chuyển tiếp: Admin khôi phục mật khẩu thành công");
        }

        // 10. Hoàn tất xác minh OTP cho cả 6 tài khoản (ADMIN, RECEPTIONIST, DENTIST, ASSISTANT, PATIENT CCCD, PATIENT Passport):
        // tất cả đều có MustChangePassword = false (được giữ mật khẩu hiện tại), sau khi hoàn tất mới nhận được JWT và PhoneVerified = true
        foreach (var item in legacyTargets)
        {
            clock.Now = clock.Now.AddSeconds(65);
            ActivationChallenge challenge;
            using (var reqRes = await client.PostAsJsonAsync("/api/auth/activation/request",
                new ActivationRequest(item.LoginIdentifier, legacyPass, item.DocType, item.Country)))
            {
                check(reqRes.StatusCode == HttpStatusCode.Accepted,
                    $"Yêu cầu OTP xác minh số điện thoại cho {item.Label} được chấp nhận");
                challenge = (await reqRes.Content.ReadFromJsonAsync<ActivationChallenge>())!;
            }

            var otpCode = activationSms.Dispatches[item.User.Phone].Code;
            ActivationGrant grant;
            using (var verRes = await client.PostAsJsonAsync("/api/auth/activation/verify",
                new ActivationVerification(challenge.ChallengeId, otpCode)))
            {
                check(verRes.StatusCode == HttpStatusCode.OK, $"Xác minh mã OTP cho {item.Label} thành công");
                grant = (await verRes.Content.ReadFromJsonAsync<ActivationGrant>())!;
                check(!grant.MustChangePassword,
                    $"{item.Label} ở trạng thái LegacyVerification không bị ép đổi mật khẩu (MustChangePassword = false)");
            }

            using (var compRes = await client.PostAsJsonAsync("/api/auth/activation/complete",
                new ActivationCompletion(grant.CompletionToken, KeepDefaultPassword: true, NewPassword: null, Confirmed: true)))
            {
                check(compRes.StatusCode == HttpStatusCode.OK,
                    $"{item.Label} hoàn tất xác minh OTP và giữ mật khẩu hiện tại thành công");
            }

            using (var loginAfterOtp = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(item.LoginIdentifier, legacyPass, item.DocType, item.Country)))
            {
                check(loginAfterOtp.StatusCode == HttpStatusCode.OK,
                    $"Sau khi hoàn tất OTP: {item.Label} đăng nhập thành công và nhận JWT mới");
                var loginDto = (await loginAfterOtp.Content.ReadFromJsonAsync<LoginResponse>())!;
                check(loginDto.RoleCode == item.User.Role.RoleCode,
                    $"Sau khi hoàn tất OTP: {item.Label} giữ nguyên vai trò {item.User.Role.RoleCode}");

                using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
                meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginDto.AccessToken);
                using var meRes = await client.SendAsync(meReq);
                var meDto = await meRes.Content.ReadFromJsonAsync<MeResponse>();
                check(meRes.StatusCode == HttpStatusCode.OK && meDto is { PhoneVerified: true },
                    $"Sau khi hoàn tất OTP: GET /api/auth/me của {item.Label} trả về PhoneVerified = true");
            }
        }

        // 11. Kiểm kê sau khi cả 6 tài khoản đã hoàn tất OTP: TotalLegacyCandidates = 0, PendingLegacyVerification = 0, VerifiedActive = 7
        var invAfterAllCompleted = await LegacyPhoneVerificationPreflight.InspectSafeAsync(db);
        check(invAfterAllCompleted is
        {
            VerifiedActive: 7,
            PendingLegacyVerification: 0,
            LegacyUnverifiedAdmins: 0,
            LegacyUnverifiedStaffNonAdmin: 0,
            LegacyUnverifiedPatientsEligible: 0,
            LegacyUnverifiedPatientsBlocked: 0
        } && invAfterAllCompleted.TotalLegacyCandidates == 0,
            "Kiểm kê sau khi toàn bộ 6 tài khoản hoàn tất OTP xác nhận 0 ứng viên legacy tồn đọng và 7 tài khoản đã xác minh");

        await app.StopAsync();
    }
}
