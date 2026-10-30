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

internal static class LegacyAdminOtpPrepSmoke
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Bộ mô phỏng kênh OTP Development phục vụ kiểm thử tự động (không thay thế SMS thật trên Production).
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
            var blocked = false;
            try
            {
                await LegacyPhoneVerificationPreflight.InspectSafeAsync(prodDb);
            }
            catch (InvalidOperationException)
            {
                blocked = true;
            }
            check(blocked, "Guard từ chối kiểm kê chuẩn bị OTP trên CSDL production 'dental_db'");
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
            ctx.Connection.RemoteIpAddress = IPAddress.Parse($"10.2.{n / 250}.{n % 250 + 1}");
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
        var tokenService = new JwtTokenService(builder.Configuration);
        var initialPassHash = hasher.Hash("LegacyPass123!");

        var roles = new Dictionary<string, Role>();
        foreach (var code in new[] { RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient })
        {
            var r = new Role { RoleCode = code, RoleName = "Role " + code };
            roles[code] = r;
            db.Roles.Add(r);
        }
        await db.SaveChangesAsync();

        // 2. Tạo tập dữ liệu hỗn hợp để kiểm chứng phân loại preview:
        // - 1 tài khoản đã xác minh (VerifiedActive)
        // - 1 tài khoản mới chờ kích hoạt (InitialActivation)
        // - 1 tài khoản chờ xác minh đổi số (PhoneChange)
        // - 1 tài khoản đã ở trạng thái LegacyVerification (PendingLegacyVerification)
        // - 1 tài khoản không hoạt động (InactiveUsers)
        // - Nhóm legacy chưa có PhoneVerifiedAtUtc: 1 Admin, 3 Staff (Receptionist, Dentist, Assistant), 1 Patient hợp lệ, 1 Patient thiếu định danh (blocked)
        var verifiedDentist = new User
        {
            Role = roles[RoleCodes.Dentist], FullName = "Verified Dentist", Phone = "0951000001",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false,
            PhoneVerifiedAtUtc = clock.Now.UtcDateTime
        };
        var pendingInitialStaff = new User
        {
            Role = roles[RoleCodes.Assistant], FullName = "New Pending Assistant", Phone = "0951000002",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = true,
            PhoneActivationReason = PhoneActivationReason.InitialActivation
        };
        var pendingPhoneChangePatient = new User
        {
            Role = roles[RoleCodes.Patient], FullName = "Phone Change Patient", Phone = "0951000003",
            LoginByCitizenId = true, PasswordHash = initialPassHash, IsActive = true,
            RequiresPhoneActivation = true, PhoneActivationReason = PhoneActivationReason.PhoneChange
        };
        var pendingLegacyStaff = new User
        {
            Role = roles[RoleCodes.Receptionist], FullName = "Already Marked Legacy", Phone = "0951000004",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = true,
            PhoneActivationReason = PhoneActivationReason.LegacyVerification
        };
        var inactiveStaff = new User
        {
            Role = roles[RoleCodes.Assistant], FullName = "Inactive Staff", Phone = "0951000005",
            PasswordHash = initialPassHash, IsActive = false, RequiresPhoneActivation = false
        };
        var legacyAdmin = new User
        {
            Role = roles[RoleCodes.Admin], FullName = "Legacy Admin", Phone = "0951000010",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null
        };
        var legacyReceptionist = new User
        {
            Role = roles[RoleCodes.Receptionist], FullName = "Legacy Receptionist", Phone = "0951000011",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null
        };
        var legacyDentist = new User
        {
            Role = roles[RoleCodes.Dentist], FullName = "Legacy Dentist", Phone = "0951000012",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null
        };
        var legacyAssistant = new User
        {
            Role = roles[RoleCodes.Assistant], FullName = "Legacy Assistant", Phone = "0951000013",
            PasswordHash = initialPassHash, IsActive = true, RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null
        };
        var legacyPatientEligible = new User
        {
            Role = roles[RoleCodes.Patient], FullName = "Legacy Patient Eligible", Phone = "0951000014",
            LoginByCitizenId = true, PasswordHash = initialPassHash, IsActive = true,
            RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null
        };
        var legacyPatientBlocked = new User
        {
            Role = roles[RoleCodes.Patient], FullName = "Legacy Patient Blocked", Phone = "0951000015",
            LoginByCitizenId = true, PasswordHash = initialPassHash, IsActive = true,
            RequiresPhoneActivation = false, PhoneVerifiedAtUtc = null
        };

        db.Users.AddRange(
            verifiedDentist, pendingInitialStaff, pendingPhoneChangePatient, pendingLegacyStaff,
            inactiveStaff, legacyAdmin, legacyReceptionist, legacyDentist, legacyAssistant,
            legacyPatientEligible, legacyPatientBlocked);
        await db.SaveChangesAsync();

        const string eligibleCccd = "079095000014";
        const string phoneChangeCccd = "079095000003";
        db.Patients.AddRange(
            new Patient
            {
                UserId = pendingPhoneChangePatient.UserId, FullName = pendingPhoneChangePatient.FullName,
                Phone = pendingPhoneChangePatient.Phone, DateOfBirth = new DateOnly(1992, 2, 2),
                CitizenIdHash = identityHasher.Hash(phoneChangeCccd, IdentityDocumentType.CitizenId),
                CitizenIdMasked = identityHasher.Mask(phoneChangeCccd), DocumentType = IdentityDocumentType.CitizenId,
                HashScheme = IdentityHashScheme.HmacCanonical, IdentityKeyVersion = 1, IsActive = true
            },
            new Patient
            {
                UserId = legacyPatientEligible.UserId, FullName = legacyPatientEligible.FullName,
                Phone = legacyPatientEligible.Phone, DateOfBirth = new DateOnly(1990, 1, 1),
                CitizenIdHash = identityHasher.Hash(eligibleCccd, IdentityDocumentType.CitizenId),
                CitizenIdMasked = identityHasher.Mask(eligibleCccd), DocumentType = IdentityDocumentType.CitizenId,
                HashScheme = IdentityHashScheme.HmacCanonical, IdentityKeyVersion = 1, IsActive = true
            }
            // legacyPatientBlocked chưa có hồ sơ Patient hợp lệ -> phải bị phát hiện vào nhóm Blocked
        );
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var invBeforeFix = await LegacyPhoneVerificationPreflight.InspectSafeAsync(db);
        check(invBeforeFix is
        {
            TotalUsers: 11,
            InactiveUsers: 1,
            VerifiedActive: 1,
            PendingInitialActivation: 1,
            PendingPhoneChange: 1,
            PendingLegacyVerification: 1,
            LegacyUnverifiedAdmins: 1,
            LegacyUnverifiedStaffNonAdmin: 3,
            LegacyUnverifiedPatientsEligible: 1,
            LegacyUnverifiedPatientsBlocked: 1,
            InvalidPhoneActiveUsers: 0,
            InconsistentVerifiedPendingUsers: 0,
            ReachableActiveAdmins: 1
        } && invBeforeFix.TotalLegacyCandidates == 5 && !invBeforeFix.CanTransitionSafely,
            "Kiểm kê phân tách chính xác tài khoản mới, đổi số, legacy và chặn khi còn hồ sơ Patient thiếu định danh");

        check(!db.ChangeTracker.Entries().Any() && !await db.AuditLogs.AnyAsync(),
            "Kiểm kê chỉ đọc không theo dõi entity, không tự điền PhoneVerifiedAtUtc giả và không sinh audit");

        // Bổ sung định danh hợp lệ cho hồ sơ bị thiếu để kiểm tra trạng thái sẵn sàng chuyển đổi
        const string fixedCccd = "079095000015";
        db.Patients.Add(new Patient
        {
            UserId = legacyPatientBlocked.UserId, FullName = legacyPatientBlocked.FullName,
            Phone = legacyPatientBlocked.Phone, DateOfBirth = new DateOnly(1991, 3, 3),
            CitizenIdHash = identityHasher.Hash(fixedCccd, IdentityDocumentType.CitizenId),
            CitizenIdMasked = identityHasher.Mask(fixedCccd), DocumentType = IdentityDocumentType.CitizenId,
            HashScheme = IdentityHashScheme.HmacCanonical, IdentityKeyVersion = 1, IsActive = true
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var invReady = await LegacyPhoneVerificationPreflight.InspectSafeAsync(db);
        check(invReady.LegacyUnverifiedPatientsBlocked == 0 &&
              invReady.LegacyUnverifiedPatientsEligible == 2 &&
              invReady.TotalLegacyCandidates == 6 &&
              invReady.CanTransitionSafely,
            "Sau khi đối soát đủ định danh, kiểm kê xác nhận đủ điều kiện an toàn (CanTransitionSafely = true)");

        // 3. Diễn tập đường OTP và Password Recovery cho Admin ở trạng thái LegacyVerification
        var preTransitionAdminJwt = tokenService.GenerateAccessToken(legacyAdmin);
        await db.Users.Where(u => u.UserId == legacyAdmin.UserId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.RequiresPhoneActivation, true)
                .SetProperty(u => u.PhoneActivationReason, PhoneActivationReason.LegacyVerification)
                .SetProperty(u => u.TokenVersion, u => u.TokenVersion + 1));

        // 3.1. Không cấp JWT sau mật khẩu khi đang chờ OTP; JWT cũ bị từ chối
        using (var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(legacyAdmin.Phone, "LegacyPass123!")))
        {
            var body = await loginRes.Content.ReadAsStringAsync();
            check(loginRes.StatusCode == HttpStatusCode.Unauthorized &&
                  body.Contains("AUTH_ACTIVATION_REQUIRED") &&
                  !body.Contains("accessToken", StringComparison.OrdinalIgnoreCase),
                "Admin ở trạng thái LegacyVerification nhập đúng mật khẩu chỉ nhận yêu cầu OTP, không được cấp JWT");
        }

        using (var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me"))
        {
            meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", preTransitionAdminJwt);
            using var meRes = await client.SendAsync(meReq);
            check(meRes.StatusCode == HttpStatusCode.Unauthorized, "JWT cũ của Admin bị từ chối ngay khi yêu cầu xác minh OTP");
        }

        // Ngay cả khi ký JWT mới với TokenVersion hiện tại nhưng RequiresPhoneActivation=true vẫn bị chặn bởi AccessTokenVersionValidation
        var adminReloaded = await db.Users.AsNoTracking().Include(u => u.Role).SingleAsync(u => u.UserId == legacyAdmin.UserId);
        var unverifiedJwt = tokenService.GenerateAccessToken(adminReloaded);
        using (var staffReq = new HttpRequestMessage(HttpMethod.Get, "/api/staff"))
        {
            staffReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", unverifiedJwt);
            using var staffRes = await client.SendAsync(staffReq);
            check(staffRes.StatusCode == HttpStatusCode.Unauthorized, "JWT ký khi RequiresPhoneActivation=true vẫn bị từ chối trên API Admin");
        }

        // 3.2. Khi SMS chưa sẵn sàng -> fail-closed cả Activation và Recovery
        activationSms.IsConfigured = false;
        recoverySms.IsConfigured = false;
        using (var actUnavailable = await client.PostAsJsonAsync("/api/auth/activation/request", new ActivationRequest(legacyAdmin.Phone, "LegacyPass123!")))
        {
            check(actUnavailable.StatusCode == HttpStatusCode.ServiceUnavailable, "Kênh SMS kích hoạt tắt trả về 503 ServiceUnavailable (Fail-closed)");
        }
        using (var recUnavailable = await client.PostAsJsonAsync("/api/auth/password-recovery/request", new RecoveryRequest(legacyAdmin.Phone)))
        {
            check(recUnavailable.StatusCode == HttpStatusCode.ServiceUnavailable, "Kênh SMS khôi phục mật khẩu tắt trả về 503 ServiceUnavailable (Fail-closed)");
        }
        activationSms.IsConfigured = true;
        recoverySms.IsConfigured = true;

        // 3.3. Kiểm tra OTP sai 5 lần khóa challenge và OTP hết hạn sau 5 phút
        clock.Now = clock.Now.AddSeconds(65);
        ActivationChallenge wrongChallenge;
        using (var reqRes = await client.PostAsJsonAsync("/api/auth/activation/request", new ActivationRequest(legacyAdmin.Phone, "LegacyPass123!")))
        {
            check(reqRes.StatusCode == HttpStatusCode.Accepted, "Yêu cầu OTP cho Admin LegacyVerification được chấp nhận");
            wrongChallenge = (await reqRes.Content.ReadFromJsonAsync<ActivationChallenge>())!;
        }
        var actualCode = activationSms.Dispatches[legacyAdmin.Phone].Code;
        var wrongCode = actualCode == "000000" ? "999999" : "000000";
        for (var i = 0; i < 5; i++)
        {
            using var vRes = await client.PostAsJsonAsync("/api/auth/activation/verify", new ActivationVerification(wrongChallenge.ChallengeId, wrongCode));
            check(vRes.StatusCode == HttpStatusCode.BadRequest, $"Admin nhập OTP sai lần {i + 1} bị từ chối");
        }
        using (var sixthRes = await client.PostAsJsonAsync("/api/auth/activation/verify", new ActivationVerification(wrongChallenge.ChallengeId, actualCode)))
        {
            check(sixthRes.StatusCode == HttpStatusCode.BadRequest, "Lần thử thứ 6 với đúng mã OTP vẫn bị từ chối do challenge đã khóa");
        }

        clock.Now = clock.Now.AddSeconds(65);
        ActivationChallenge expChallenge;
        using (var reqRes = await client.PostAsJsonAsync("/api/auth/activation/request", new ActivationRequest(legacyAdmin.Phone, "LegacyPass123!")))
        {
            expChallenge = (await reqRes.Content.ReadFromJsonAsync<ActivationChallenge>())!;
        }
        var expCode = activationSms.Dispatches[legacyAdmin.Phone].Code;
        clock.Now = clock.Now.AddMinutes(5);
        using (var expRes = await client.PostAsJsonAsync("/api/auth/activation/verify", new ActivationVerification(expChallenge.ChallengeId, expCode)))
        {
            check(expRes.StatusCode == HttpStatusCode.BadRequest, "OTP hết hạn sau đúng 5 phút bị từ chối");
        }

        // 3.4. Tài khoản Admin bị khóa (IsActive = false) không thể nhận OTP hay reset mật khẩu
        await db.Users.Where(u => u.UserId == legacyAdmin.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        clock.Now = clock.Now.AddSeconds(65);
        var beforeDispatches = activationSms.Dispatches.Count;
        activationSms.Dispatches.Clear();
        using (var lockedActRes = await client.PostAsJsonAsync("/api/auth/activation/request", new ActivationRequest(legacyAdmin.Phone, "LegacyPass123!")))
        {
            check(lockedActRes.StatusCode == HttpStatusCode.Accepted && !activationSms.Dispatches.ContainsKey(legacyAdmin.Phone),
                "Tài khoản Admin đang bị khóa (IsActive=false) không phát sinh gửi OTP");
        }
        await db.Users.Where(u => u.UserId == legacyAdmin.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, true));

        // 3.5. Đường khôi phục mật khẩu (Password Recovery) của Admin hoạt động khi đang ở LegacyVerification
        // nhưng không tự bỏ qua bước xác minh OTP và không ghi giả PhoneVerifiedAtUtc
        clock.Now = clock.Now.AddSeconds(65);
        RecoveryResponse recChallenge;
        using (var recReq = await client.PostAsJsonAsync("/api/auth/password-recovery/request", new RecoveryRequest(legacyAdmin.Phone)))
        {
            check(recReq.StatusCode == HttpStatusCode.Accepted, "Admin gửi yêu cầu khôi phục mật khẩu thành công");
            recChallenge = (await recReq.Content.ReadFromJsonAsync<RecoveryResponse>())!;
        }
        var recCode = recoverySms.Codes[legacyAdmin.Phone];
        using (var recReset = await client.PostAsJsonAsync("/api/auth/password-recovery/reset",
            new ResetPasswordRequest(recChallenge.ChallengeId, legacyAdmin.Phone, recCode, "AdminRecovered99!")))
        {
            check(recReset.StatusCode == HttpStatusCode.OK, "Admin đặt lại mật khẩu bằng OTP khôi phục thành công");
        }
        using (var recReplay = await client.PostAsJsonAsync("/api/auth/password-recovery/reset",
            new ResetPasswordRequest(recChallenge.ChallengeId, legacyAdmin.Phone, recCode, "AdminRecovered99!")))
        {
            check(recReplay.StatusCode == HttpStatusCode.BadRequest, "Chặn dùng lại (replay) mã OTP khôi phục mật khẩu của Admin");
        }

        var adminAfterRecovery = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == legacyAdmin.UserId);
        check(adminAfterRecovery.RequiresPhoneActivation &&
              adminAfterRecovery.PhoneActivationReason == PhoneActivationReason.LegacyVerification &&
              adminAfterRecovery.PhoneVerifiedAtUtc == null &&
              hasher.Verify("AdminRecovered99!", adminAfterRecovery.PasswordHash),
            "Khôi phục mật khẩu cập nhật hash nhưng giữ nguyên yêu cầu xác minh OTP và không ghi giả PhoneVerifiedAtUtc");

        // 3.6. Admin hoàn tất xác minh OTP LegacyVerification (giữ nguyên mật khẩu hiện tại, không bắt buộc đổi lại)
        clock.Now = clock.Now.AddSeconds(65);
        ActivationChallenge validChallenge;
        using (var reqRes = await client.PostAsJsonAsync("/api/auth/activation/request", new ActivationRequest(legacyAdmin.Phone, "AdminRecovered99!")))
        {
            check(reqRes.StatusCode == HttpStatusCode.Accepted, "Admin yêu cầu OTP xác minh với mật khẩu đã khôi phục thành công");
            validChallenge = (await reqRes.Content.ReadFromJsonAsync<ActivationChallenge>())!;
        }
        var validCode = activationSms.Dispatches[legacyAdmin.Phone].Code;
        ActivationGrant validGrant;
        using (var verRes = await client.PostAsJsonAsync("/api/auth/activation/verify", new ActivationVerification(validChallenge.ChallengeId, validCode)))
        {
            check(verRes.StatusCode == HttpStatusCode.OK, "Admin xác minh mã OTP thành công");
            validGrant = (await verRes.Content.ReadFromJsonAsync<ActivationGrant>())!;
            check(!validGrant.MustChangePassword, "LegacyVerification không ép đổi lại mật khẩu hiện tại (MustChangePassword = false)");
        }
        using (var verReplay = await client.PostAsJsonAsync("/api/auth/activation/verify", new ActivationVerification(validChallenge.ChallengeId, validCode)))
        {
            check(verReplay.StatusCode == HttpStatusCode.BadRequest, "Chặn replay mã OTP kích hoạt của Admin");
        }

        using (var compRes = await client.PostAsJsonAsync("/api/auth/activation/complete",
            new ActivationCompletion(validGrant.CompletionToken, KeepDefaultPassword: true, NewPassword: null, Confirmed: true)))
        {
            check(compRes.StatusCode == HttpStatusCode.OK, "Admin hoàn tất xác minh số điện thoại giữ mật khẩu hiện tại thành công");
        }
        using (var compReplay = await client.PostAsJsonAsync("/api/auth/activation/complete",
            new ActivationCompletion(validGrant.CompletionToken, KeepDefaultPassword: true, NewPassword: null, Confirmed: true)))
        {
            check(compReplay.StatusCode == HttpStatusCode.BadRequest, "Chặn replay completion token của Admin");
        }

        var adminVerified = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == legacyAdmin.UserId);
        check(!adminVerified.RequiresPhoneActivation &&
              adminVerified.PhoneActivationReason == null &&
              adminVerified.PhoneVerifiedAtUtc.HasValue &&
              hasher.Verify("AdminRecovered99!", adminVerified.PasswordHash),
            "Admin sau khi hoàn tất OTP có PhoneVerifiedAtUtc thật, xóa cờ chờ kích hoạt và giữ đúng mật khẩu");

        check(await db.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityId == legacyAdmin.UserId &&
                a.Action == "MOD_AUTH_STAFF_ACTIVATED" &&
                a.Detail != null &&
                a.Detail.Contains("DL-366/DL-367") &&
                a.Detail.Contains("\"changedPassword\":false")),
            "Audit log ghi nhận xác minh LegacyVerification theo quyết định DL-366/DL-367");

        // 3.7. Admin đăng nhập và gọi API quản trị bình thường sau khi xác minh OTP
        using (var finalLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(legacyAdmin.Phone, "AdminRecovered99!")))
        {
            check(finalLogin.StatusCode == HttpStatusCode.OK, "Admin đăng nhập thành công sau khi hoàn tất OTP");
            var loginDto = (await finalLogin.Content.ReadFromJsonAsync<LoginResponse>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginDto.AccessToken);

            using var meRes = await client.GetAsync("/api/auth/me");
            var meDto = (await meRes.Content.ReadFromJsonAsync<MeResponse>())!;
            check(meRes.StatusCode == HttpStatusCode.OK && meDto.PhoneVerified && meDto.RoleCode == RoleCodes.Admin,
                "GET /api/auth/me xác nhận Admin đã xác minh số điện thoại (PhoneVerified = true)");

            using var staffListRes = await client.GetAsync("/api/staff");
            check(staffListRes.StatusCode == HttpStatusCode.OK,
                "Admin truy cập bình thường vào endpoint quản trị /api/staff mà không bị khóa đường vào");
        }

        await app.StopAsync();
    }
}
