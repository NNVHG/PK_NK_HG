using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;

namespace Dental.Application.Features.Auth.Services;

/// <summary>Nghiệp vụ xác thực: đăng nhập, lấy thông tin bản thân.</summary>
public sealed class AuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogger _auditLogger;

    public AuthService(
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditLogger auditLogger)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Đăng nhập bằng SĐT + mật khẩu.
    /// Quy tắc bảo mật (UAT AUTH-09):
    ///   - Sai SĐT hoặc sai mật khẩu → cùng thông báo chung (chống dò tài khoản).
    ///   - CHỈ khi mật khẩu ĐÚNG mà IsActive = false → báo "tài khoản bị khóa".
    /// </summary>
    public async Task<Result<LoginResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        // Che SĐT khi ghi log (VD: 09****678)
        var maskedPhone = MaskPhone(request.Phone);

        var user = await _userRepo.FindByPhoneAsync(request.Phone, ct);

        // Không tìm thấy SĐT → trả lỗi chung (không tiết lộ SĐT không tồn tại)
        if (user is null)
        {
            await _auditLogger.LogAsync(
                action: AuditActions.LoginFailed,
                entityType: "User",
                detail: $"{{\"maskedPhone\":\"{maskedPhone}\",\"reason\":\"phone_not_found\"}}",
                ipAddress: ipAddress,
                ct: ct);

            return Result<LoginResponse>.Failure(Error.InvalidCredentials);
        }

        // Kiểm tra mật khẩu
        var passwordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            await _auditLogger.LogAsync(
                action: AuditActions.LoginFailed,
                entityType: "User",
                userId: user.UserId,
                detail: $"{{\"maskedPhone\":\"{maskedPhone}\",\"reason\":\"wrong_password\"}}",
                ipAddress: ipAddress,
                ct: ct);

            return Result<LoginResponse>.Failure(Error.InvalidCredentials);
        }

        // Mật khẩu đúng nhưng tài khoản bị khóa → báo rõ (UAT AUTH-09)
        if (!user.IsActive)
        {
            await _auditLogger.LogAsync(
                action: AuditActions.LoginFailed,
                entityType: "User",
                userId: user.UserId,
                detail: $"{{\"maskedPhone\":\"{maskedPhone}\",\"reason\":\"account_locked\"}}",
                ipAddress: ipAddress,
                ct: ct);

            return Result<LoginResponse>.Failure(Error.AccountLocked);
        }

        // Đăng nhập thành công
        var token = _tokenService.GenerateAccessToken(user);

        await _auditLogger.LogAsync(
            action: AuditActions.Login,
            entityType: "User",
            entityId: user.UserId,
            userId: user.UserId,
            detail: $"{{\"maskedPhone\":\"{maskedPhone}\"}}",
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
            RoleCode: user.Role.RoleCode,
            RoleName: user.Role.RoleName,
            IsActive: user.IsActive
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

        // Băm và lưu mật khẩu mới
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(
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

    /// <summary>Che SĐT: 0912345678 → 09****678.</summary>
    private static string MaskPhone(string phone)
    {
        if (phone.Length < 5) return "***";
        return phone[..2] + new string('*', phone.Length - 5) + phone[^3..];
    }
}

