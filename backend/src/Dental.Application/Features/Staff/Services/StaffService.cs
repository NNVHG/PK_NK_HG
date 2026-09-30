using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Staff.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.Staff.Services;

public sealed class StaffService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogger _auditLogger;

    public StaffService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IAuditLogger auditLogger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _auditLogger = auditLogger;
    }

    public async Task<Result<PagedResult<StaffAccountResponse>>> GetStaffAsync(
        StaffQueryRequest request,
        CancellationToken ct = default)
    {
        var roleCode = NormalizeRoleCode(request.RoleCode);
        var (users, totalCount) = await _userRepository.GetStaffPageAsync(
            roleCode,
            request.IsActive,
            request.Search,
            request.Page,
            request.PageSize,
            ct);

        var page = PagedResult<StaffAccountResponse>.Create(
            users.Select(ToResponse).ToList(),
            totalCount,
            request.Page,
            request.PageSize);

        return Result<PagedResult<StaffAccountResponse>>.Success(page);
    }

    public async Task<Result<StaffAccountResponse>> CreateStaffAsync(
        int actorUserId,
        CreateStaffRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var roleCode = NormalizeRoleCode(request.RoleCode);
        if (!IsStaffRole(roleCode))
            return Result<StaffAccountResponse>.Failure(Error.InvalidStaffRole);

        var phone = request.Phone.Trim();
        if (await _userRepository.PhoneExistsAsync(phone, ct: ct))
            return Result<StaffAccountResponse>.Failure(Error.StaffPhoneExists);

        var role = await _userRepository.FindRoleByCodeAsync(roleCode, ct);
        if (role is null)
            return Result<StaffAccountResponse>.Failure(Error.InvalidStaffRole);

        var user = new User
        {
            Phone = phone,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Email = NormalizeOptional(request.Email),
            DateOfBirth = request.DateOfBirth,
            Gender = NormalizeOptional(request.Gender),
            RoleId = role.RoleId,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await WriteAuditAsync(
            AuditActions.Create,
            "create_staff",
            actorUserId,
            user,
            roleBefore: null,
            roleAfter: role.RoleCode,
            isActiveBefore: null,
            isActiveAfter: user.IsActive,
            changedFields: [nameof(User.FullName), nameof(User.Phone), nameof(User.Email), nameof(User.DateOfBirth), nameof(User.Gender), "RoleCode"],
            ipAddress,
            ct);

        return Result<StaffAccountResponse>.Success(ToResponse(user));
    }

    public async Task<Result<StaffAccountResponse>> UpdateStaffAsync(
        int actorUserId,
        int staffUserId,
        UpdateStaffRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(staffUserId, ct);
        if (user is null || !IsStaffRole(user.Role.RoleCode))
            return Result<StaffAccountResponse>.Failure(Error.NotFound);

        var roleCode = NormalizeRoleCode(request.RoleCode);
        if (!IsStaffRole(roleCode))
            return Result<StaffAccountResponse>.Failure(Error.InvalidStaffRole);

        var phone = request.Phone.Trim();
        if (await _userRepository.PhoneExistsAsync(phone, staffUserId, ct))
            return Result<StaffAccountResponse>.Failure(Error.StaffPhoneExists);

        var role = await _userRepository.FindRoleByCodeAsync(roleCode, ct);
        if (role is null)
            return Result<StaffAccountResponse>.Failure(Error.InvalidStaffRole);

        var roleBefore = user.Role.RoleCode;
        var isActive = user.IsActive;
        if (user.IsActive && roleBefore == RoleCodes.Admin && roleCode != RoleCodes.Admin &&
            await _userRepository.CountActiveAdminsAsync(ct) <= 1)
        {
            return Result<StaffAccountResponse>.Failure(Error.LastActiveAdmin);
        }

        var changedFields = new List<string>();
        SetIfChanged(user.FullName, request.FullName.Trim(), value => user.FullName = value, nameof(User.FullName), changedFields);
        SetIfChanged(user.Phone, phone, value => user.Phone = value, nameof(User.Phone), changedFields);
        SetIfChanged(user.Email, NormalizeOptional(request.Email), value => user.Email = value, nameof(User.Email), changedFields);
        SetIfChanged(user.DateOfBirth, request.DateOfBirth, value => user.DateOfBirth = value, nameof(User.DateOfBirth), changedFields);
        SetIfChanged(user.Gender, NormalizeOptional(request.Gender), value => user.Gender = value, nameof(User.Gender), changedFields);

        if (roleBefore != roleCode)
        {
            user.RoleId = role.RoleId;
            user.Role = role;
            changedFields.Add("RoleCode");
        }

        if (changedFields.Count > 0)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
            await WriteAuditAsync(
                AuditActions.Update,
                "update_staff",
                actorUserId,
                user,
                roleBefore,
                user.Role.RoleCode,
                isActive,
                user.IsActive,
                changedFields,
                ipAddress,
                ct);
        }

        return Result<StaffAccountResponse>.Success(ToResponse(user));
    }

    public Task<Result<StaffAccountResponse>> LockStaffAsync(
        int actorUserId,
        int staffUserId,
        string? ipAddress,
        CancellationToken ct = default)
        => SetStaffActiveAsync(actorUserId, staffUserId, false, ipAddress, ct);

    public Task<Result<StaffAccountResponse>> UnlockStaffAsync(
        int actorUserId,
        int staffUserId,
        string? ipAddress,
        CancellationToken ct = default)
        => SetStaffActiveAsync(actorUserId, staffUserId, true, ipAddress, ct);

    private async Task<Result<StaffAccountResponse>> SetStaffActiveAsync(
        int actorUserId,
        int staffUserId,
        bool isActive,
        string? ipAddress,
        CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(staffUserId, ct);
        if (user is null || !IsStaffRole(user.Role.RoleCode))
            return Result<StaffAccountResponse>.Failure(Error.NotFound);

        if (!isActive && actorUserId == staffUserId)
            return Result<StaffAccountResponse>.Failure(Error.CannotLockSelf);

        var roleCode = user.Role.RoleCode;
        var isActiveBefore = user.IsActive;
        if (!isActive && user.IsActive && roleCode == RoleCodes.Admin &&
            await _userRepository.CountActiveAdminsAsync(ct) <= 1)
        {
            return Result<StaffAccountResponse>.Failure(Error.LastActiveAdmin);
        }

        if (user.IsActive == isActive)
            return Result<StaffAccountResponse>.Success(ToResponse(user));

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        var action = isActive ? "unlock_staff" : "lock_staff";
        await WriteAuditAsync(
            AuditActions.Update,
            action,
            actorUserId,
            user,
            roleCode,
            roleCode,
            isActiveBefore,
            user.IsActive,
            [nameof(User.IsActive)],
            ipAddress,
            ct);

        return Result<StaffAccountResponse>.Success(ToResponse(user));
    }

    private Task WriteAuditAsync(
        string auditAction,
        string operation,
        int actorUserId,
        User target,
        string? roleBefore,
        string? roleAfter,
        bool? isActiveBefore,
        bool? isActiveAfter,
        IReadOnlyCollection<string> changedFields,
        string? ipAddress,
        CancellationToken ct)
    {
        var detail = JsonSerializer.Serialize(new
        {
            action = operation,
            actorUserId,
            targetUserId = target.UserId,
            roleBefore,
            roleAfter,
            isActiveBefore,
            isActiveAfter,
            changedFields,
        });

        return _auditLogger.LogAsync(
            action: auditAction,
            entityType: "User",
            entityId: target.UserId,
            userId: actorUserId,
            detail: detail,
            ipAddress: ipAddress,
            ct: ct);
    }

    private static StaffAccountResponse ToResponse(User user)
        => new(
            user.UserId,
            user.Phone,
            user.FullName,
            user.Email,
            user.DateOfBirth,
            user.Gender,
            user.Role.RoleCode,
            user.Role.RoleName,
            user.IsActive);

    private static string NormalizeRoleCode(string? roleCode)
        => roleCode?.Trim().ToUpperInvariant() ?? string.Empty;

    private static bool IsStaffRole(string? roleCode)
        => RoleCodes.StaffRoles.Contains(roleCode ?? string.Empty);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void SetIfChanged<T>(
        T current,
        T next,
        Action<T> setter,
        string fieldName,
        ICollection<string> changedFields)
    {
        if (!EqualityComparer<T>.Default.Equals(current, next))
        {
            setter(next);
            changedFields.Add(fieldName);
        }
    }
}
