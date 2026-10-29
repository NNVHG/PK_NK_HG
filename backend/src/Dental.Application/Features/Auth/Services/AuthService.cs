using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.Auth.Services;

/// <summary>Nghiệp vụ xác thực: đăng nhập, lấy thông tin bản thân.</summary>
public sealed class AuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogger _auditLogger;
    private readonly ILoginLockoutService? _lockoutService;
    private readonly IKeyedIdentityHasher? _identityHasher;

    public AuthService(
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditLogger auditLogger,
        IPatientRepository patientRepository,
        ILoginLockoutService? lockoutService = null,
        IKeyedIdentityHasher? identityHasher = null)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditLogger = auditLogger;
        _lockoutService = lockoutService;
        _identityHasher = identityHasher;
    }

    /// <summary>DL326: tài khoản chỉ được tạo sau duyệt lịch; giữ lỗi rõ ràng cho caller cũ.</summary>
    public Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken ct = default)
        => Task.FromResult(Result<RegisterResponse>.Failure(Dental.Application.Features.MOD_AUTH.PatientActivationService.RegistrationDisabled));

    /// <summary>
    /// Đăng nhập bằng SĐT nhân viên, CCCD hoặc Hộ chiếu bệnh nhân + mật khẩu (DL-360, DL-364, DL-365).
    /// Quy tắc bảo mật (UAT AUTH-09, DL-365):
    ///   - Kiểm tra khóa tạm thời do nhập sai quá 5 lần trong 15 phút (DL-365).
    ///   - Sai SĐT hoặc sai mật khẩu → cùng thông báo chung (chống dò tài khoản).
    ///   - CHỈ khi mật khẩu ĐÚNG mà IsActive = false → báo "tài khoản bị khóa".
    /// </summary>
    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var identifier = (request.Phone ?? string.Empty).Trim();
        var lockoutKey = request.DocumentType == IdentityDocumentType.Passport
            ? $"PASSPORT:{request.NationalityCode?.Trim().ToUpperInvariant()}:{identifier.ToUpperInvariant()}"
            : request.DocumentType == IdentityDocumentType.CitizenId || identifier.Length == 12
                ? $"CCCD:{identifier}"
                : $"PHONE:{identifier}";

        // 1. Kiểm tra khóa tạm thời chống brute-force (DL-365)
        if (_lockoutService is not null && _lockoutService.IsLocked(lockoutKey, out _))
        {
            await _auditLogger.LogAsync(
                action: AuditActions.LoginFailed,
                entityType: "User",
                detail: "{\"reason\":\"temporary_lockout\"}",
                ipAddress: ipAddress,
                ct: ct);

            return Result<LoginResponse>.Failure(Error.TemporaryLockout);
        }

        User? user = null;
        if (request.DocumentType == IdentityDocumentType.Passport)
        {
            // Bệnh nhân dùng Hộ chiếu (DL-360, DL-364)
            if (_identityHasher is null) return Result<LoginResponse>.Failure(Error.ServerError);
            var hash = _identityHasher.Hash(identifier, IdentityDocumentType.Passport, request.NationalityCode);
            user = await _userRepo.FindByIdentityHashAsync(hash, ct);
        }
        else if (identifier.Length == 12 && request.DocumentType != IdentityDocumentType.Passport)
        {
            // CCCD 12 chữ số
            var keyedHash = _identityHasher?.Hash(identifier, IdentityDocumentType.CitizenId);
            var legacyHash = Dental.Application.Features.MOD_PAT.CitizenIdIdentity.Hash(identifier);

            user = keyedHash is not null ? await _userRepo.FindByIdentityHashAsync(keyedHash, ct) : null;
            user ??= await _userRepo.FindByCitizenIdHashAsync(legacyHash, ct);
        }
        else
        {
            // SĐT nhân viên hoặc tài khoản cũ
            user = await _userRepo.FindByPhoneAsync(identifier, ct);
        }

        // Không tìm thấy User → ghi nhận lần sai + trả lỗi chung (chống dò tài khoản)
        if (user is null)
        {
            var nowLocked = false;
            _lockoutService?.RecordFailedAttempt(lockoutKey, out nowLocked, out _);

            await _auditLogger.LogAsync(
                action: AuditActions.LoginFailed,
                entityType: "User",
                detail: "{\"reason\":\"phone_not_found\"}",
                ipAddress: ipAddress,
                ct: ct);

            return Result<LoginResponse>.Failure(nowLocked ? Error.TemporaryLockout : Error.InvalidCredentials);
        }

        // Kiểm tra mật khẩu
        var passwordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            var nowLocked = false;
            _lockoutService?.RecordFailedAttempt(lockoutKey, out nowLocked, out _);

            await _auditLogger.LogAsync(
                action: AuditActions.LoginFailed,
                entityType: "User",
                userId: user.UserId,
                detail: nowLocked ? "{\"reason\":\"wrong_password_locked\"}" : "{\"reason\":\"wrong_password\"}",
                ipAddress: ipAddress,
                ct: ct);

            if (nowLocked)
                return Result<LoginResponse>.Failure(Error.TemporaryLockout);

            return Result<LoginResponse>.Failure(Error.InvalidCredentials);
        }

        // Mật khẩu đúng nhưng tài khoản bị khóa vĩnh viễn (IsActive = false) → báo rõ (UAT AUTH-09)
        if (!user.IsActive)
        {
            await _auditLogger.LogAsync(
                action: AuditActions.LoginFailed,
                entityType: "User",
                userId: user.UserId,
                detail: "{\"reason\":\"account_locked\"}",
                ipAddress: ipAddress,
                ct: ct);

            return Result<LoginResponse>.Failure(Error.AccountLocked);
        }

        // Mật khẩu đúng -> Xóa bộ đếm sai (DL-365)
        _lockoutService?.ResetAttempts(lockoutKey);

        if (user.RequiresPhoneActivation)
            return Result<LoginResponse>.Failure(new Error("AUTH_ACTIVATION_REQUIRED", "Cần xác minh số điện thoại bằng OTP trước khi đăng nhập."));

        // Đăng nhập thành công
        var token = _tokenService.GenerateAccessToken(user);

        await _auditLogger.LogAsync(
            action: AuditActions.Login,
            entityType: "User",
            entityId: user.UserId,
            userId: user.UserId,
            detail: null,
            ipAddress: ipAddress,
            ct: ct);

        var response = new LoginResponse(
            AccessToken: token,
            UserId: user.UserId,
            FullName: user.FullName,
            RoleCode: user.Role.RoleCode,
            RoleName: user.Role.RoleName
        );

        return Result<LoginResponse>.Success(response);
    }

    /// <summary>Lấy thông tin bản thân (GET /api/auth/me).</summary>
    public async Task<Result<MeResponse>> GetMeAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userRepo.FindByIdAsync(userId, ct);

        if (user is null)
            return Result<MeResponse>.Failure(Error.NotFound);

        var response = new MeResponse(
            UserId: user.UserId,
            Phone: user.Phone,
            FullName: user.FullName,
            Email: user.Email,
            DateOfBirth: user.DateOfBirth,
            Gender: user.Gender,
            RoleCode: user.Role.RoleCode,
            RoleName: user.Role.RoleName,
            IsActive: user.IsActive,
            PhoneVerified: user.PhoneVerifiedAtUtc.HasValue && !user.RequiresPhoneActivation
        );

        return Result<MeResponse>.Success(response);
    }

    /// <summary>Cập nhật hồ sơ của người dùng hiện tại.</summary>
    public async Task<Result<MeResponse>> UpdateProfileAsync(
        int userId,
        UpdateProfileRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var user = await _userRepo.FindByIdAsync(userId, ct);
        if (user is null)
            return Result<MeResponse>.Failure(Error.NotFound);

        // DL-334/335: đổi số phải qua OTP và xác nhận chính chủ; profile PUT không được đi tắt.
        if (request.Phone != user.Phone)
            return Result<MeResponse>.Failure(Error.PhoneChangeNotAllowed);

        var fullName = request.FullName.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var changedFields = new List<string>();

        if (user.FullName != fullName)
        {
            user.FullName = fullName;
            changedFields.Add(nameof(User.FullName));
        }

        if (user.Email != email)
        {
            user.Email = email;
            changedFields.Add(nameof(User.Email));
        }

        if (user.DateOfBirth != request.DateOfBirth)
        {
            user.DateOfBirth = request.DateOfBirth;
            changedFields.Add(nameof(User.DateOfBirth));
        }

        var gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        if (user.Gender != gender)
        {
            user.Gender = gender;
            changedFields.Add(nameof(User.Gender));
        }

        if (changedFields.Count > 0)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);

            var detail = JsonSerializer.Serialize(new
            {
                action = "update_profile",
                changedFields,
            });

            await _auditLogger.LogAsync(
                action: AuditActions.Update,
                entityType: "User",
                entityId: user.UserId,
                userId: user.UserId,
                detail: detail,
                ipAddress: ipAddress,
                ct: ct);
        }

        var response = new MeResponse(
            UserId: user.UserId,
            Phone: user.Phone,
            FullName: user.FullName,
            Email: user.Email,
            DateOfBirth: user.DateOfBirth,
            Gender: user.Gender,
            RoleCode: user.Role.RoleCode,
            RoleName: user.Role.RoleName,
            IsActive: user.IsActive,
            PhoneVerified: user.PhoneVerifiedAtUtc.HasValue && !user.RequiresPhoneActivation
        );

        return Result<MeResponse>.Success(response);
    }

    /// <summary>Đổi mật khẩu người dùng (POST /api/auth/change-password).</summary>
    public async Task<Result<bool>> ChangePasswordAsync(
        int userId,
        ChangePasswordRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        try
        {
            return await _unitOfWork.ExecuteSerializableTransactionAsync(
                transactionCt => ChangePasswordCoreAsync(userId, request, ipAddress, transactionCt), ct);
        }
        catch (ConcurrentOperationException)
        {
            return Result<bool>.Failure(Error.Conflict);
        }
    }

    private async Task<Result<bool>> ChangePasswordCoreAsync(
        int userId, ChangePasswordRequest request, string? ipAddress, CancellationToken ct)
    {
        var user = await _userRepo.FindByIdAsync(userId, ct);
        if (user is null)
            return Result<bool>.Failure(Error.NotFound);

        // Kiểm tra mật khẩu cũ
        if (!_passwordHasher.Verify(request.OldPassword, user.PasswordHash))
        {
            await _auditLogger.LogAsync(
                action: AuditActions.Update,
                entityType: "User",
                entityId: user.UserId,
                userId: user.UserId,
                detail: "{\"action\":\"change_password_failed\",\"reason\":\"wrong_old_password\"}",
                ipAddress: ipAddress,
                ct: ct);

            return Result<bool>.Failure(Error.WrongOldPassword);
        }

        // Mật khẩu mới không được trùng mật khẩu cũ
        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            return Result<bool>.Failure(Error.SameNewPassword);
        }

        // DL-336/Q351b: self-change keeps TokenVersion and existing JWT expiry; no token is issued here.
        // Recovery and phone-change revocation remain independent.
        // Băm và lưu mật khẩu mới
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogRequiredAsync(
            action: AuditActions.Update,
            entityType: "User",
            entityId: user.UserId,
            userId: user.UserId,
            detail: "{\"action\":\"change_password_success\"}",
            ipAddress: ipAddress,
            ct: ct);

        return Result<bool>.Success(true);
    }

    /// <summary>Đăng xuất ghi nhận audit log (POST /api/auth/logout).</summary>
    public async Task<Result<bool>> LogoutAsync(
        int userId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var user = await _userRepo.FindByIdAsync(userId, ct);
        if (user is null)
            return Result<bool>.Failure(Error.NotFound);

        await _auditLogger.LogAsync(
            action: AuditActions.Logout,
            entityType: "User",
            entityId: user.UserId,
            userId: user.UserId,
            detail: "{\"action\":\"user_logout\"}",
            ipAddress: ipAddress,
            ct: ct);

        return Result<bool>.Success(true);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

