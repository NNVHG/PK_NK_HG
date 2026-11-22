using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Errors;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using PatientEntity = Dental.Domain.Entities.Patient;

namespace Dental.Application.Features.Patients.Services;

public sealed class PatientsService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    private readonly IPatientGuardianRepository? _guardianRepository;
    private readonly VietnamClock _vietnamClock;

    public PatientsService(
        IPatientRepository patientRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _patientRepository = patientRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _vietnamClock = new VietnamClock(TimeProvider.System);
    }

    public PatientsService(
        IPatientRepository patientRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        IPatientGuardianRepository guardianRepository,
        VietnamClock? vietnamClock = null)
    {
        _patientRepository = patientRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _guardianRepository = guardianRepository;
        _vietnamClock = vietnamClock ?? new VietnamClock(TimeProvider.System);
    }


    public async Task<Result<IReadOnlyList<PatientDuplicateInfo>>> CheckDuplicatesAsync(
        PatientDuplicateCheckRequest request,
        CancellationToken ct = default)
    {
        var patients = await _patientRepository.FindPossibleDuplicatesAsync(
            request.Phone,
            request.FullName,
            request.DateOfBirth,
            ct);

        return Result<IReadOnlyList<PatientDuplicateInfo>>.Success(patients.Select(ToDuplicateInfo).ToList());
    }

    public async Task<Result<PagedResult<PatientListItemResponse>>> GetPatientsAsync(
        PatientQueryRequest request,
        CancellationToken ct = default)
    {
        var patients = await _patientRepository.SearchAsync(
            request.Keyword,
            request.Page,
            request.PageSize,
            ct);

        var activeWardIds = await GetActiveGuardianWardIdsAsync(patients.Items, ct);

        var page = PagedResult<PatientListItemResponse>.Create(
            patients.Items.Select(patient => ToListItemResponse(
                patient,
                activeWardIds.Contains(patient.PatientId))).ToList(),
            patients.TotalCount,
            patients.Page,
            patients.PageSize);

        return Result<PagedResult<PatientListItemResponse>>.Success(page);
    }

    public async Task<Result<PatientResponse>> CreatePatientAsync(
        int creatorUserId,
        CreatePatientRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var possibleDuplicates = await _patientRepository.FindPossibleDuplicatesAsync(
            request.Phone,
            request.FullName,
            request.DateOfBirth,
            ct);

        if (possibleDuplicates.Count > 0 && !request.ConfirmNotDuplicate)
        {
            var duplicateInfo = possibleDuplicates.Select(ToDuplicateInfo).ToList();
            return Result<PatientResponse>.Failure(new PatientPossibleDuplicateError(duplicateInfo));
        }

        var patient = new PatientEntity
        {
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = NormalizeOptional(request.Gender),
            Phone = request.Phone.Trim(),
            Email = NormalizeOptional(request.Email),
            Address = NormalizeOptional(request.Address),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _patientRepository.AddAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var detail = JsonSerializer.Serialize(new
        {
            createdByUserId = creatorUserId,
            patientCode = patient.PatientCode,
            confirmedNotDuplicate = request.ConfirmNotDuplicate,
        });

        await _auditLogger.LogAsync(
            action: AuditActions.PatientCreated,
            entityType: "Patient",
            entityId: patient.PatientId,
            userId: creatorUserId,
            detail: detail,
            ipAddress: ipAddress,
            ct: ct);

        return Result<PatientResponse>.Success(ToResponse(patient, hasActiveGuardian: false));
    }

    public async Task<Result<PatientListItemResponse>> GetMyPatientAsync(int userId, CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByUserIdAsync(userId, ct);
        if (patient is null || !patient.IsActive) return Result<PatientListItemResponse>.Failure(Error.NotFound);
        var activeWardIds = await GetActiveGuardianWardIdsAsync([patient], ct);
        return Result<PatientListItemResponse>.Success(ToListItemResponse(
            patient,
            activeWardIds.Contains(patient.PatientId)));
    }

    public async Task<Result<PatientResponse>> GetPatientByIdAsync(
        int patientId,
        int? currentUserId,
        string? currentRoleCode,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null)
            return Result<PatientResponse>.Failure(Error.NotFound);

        var isGuardian = false;
        if (currentRoleCode == RoleCodes.Patient)
        {
            var isSelf = currentUserId.HasValue && patient.UserId == currentUserId.Value;
            if (!isSelf && currentUserId.HasValue && _guardianRepository is not null)
            {
                isGuardian = await _guardianRepository.HasActiveGuardianshipAsync(currentUserId.Value, patientId, ct);
            }

            if (!isSelf && !isGuardian)
            {
                return Result<PatientResponse>.Failure(Error.NotFound);
            }
        }

        var hasActiveGuardian = isGuardian ||
            (await GetActiveGuardianWardIdsAsync([patient], ct)).Contains(patient.PatientId);
        return Result<PatientResponse>.Success(ToResponse(patient, hasActiveGuardian));
    }

    public async Task<Result<PatientResponse>> UpdatePatientAsync(
        int actorUserId,
        int patientId,
        UpdatePatientRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null)
            return Result<PatientResponse>.Failure(Error.NotFound);

        if (PatientAccountAge.IsUnderMinimumAge(request.DateOfBirth, _vietnamClock.Today) &&
            (patient.UserId.HasValue || !string.IsNullOrWhiteSpace(patient.CitizenIdHash)))
        {
            return Result<PatientResponse>.Failure(new Error(
                "PAT_CHILD_AGE_CONFLICT",
                "Hồ sơ đã có tài khoản hoặc CCCD không thể đổi ngày sinh xuống dưới 14 tuổi."));
        }

        var fullName = request.FullName.Trim();
        var phone = request.Phone.Trim();
        var email = NormalizeOptional(request.Email);
        var address = NormalizeOptional(request.Address);
        var gender = NormalizeOptional(request.Gender);
        var identityChanged = patient.Phone != phone ||
                              patient.FullName != fullName ||
                              patient.DateOfBirth != request.DateOfBirth;

        if (identityChanged)
        {
            var possibleDuplicates = await _patientRepository.FindPossibleDuplicatesAsync(
                phone,
                fullName,
                request.DateOfBirth,
                ct,
                patientId);

            if (possibleDuplicates.Count > 0 && !request.ConfirmNotDuplicate)
            {
                var duplicateInfo = possibleDuplicates.Select(ToDuplicateInfo).ToList();
                return Result<PatientResponse>.Failure(new PatientPossibleDuplicateError(duplicateInfo));
            }
        }

        var changedFields = new List<string>();
        SetIfChanged(patient.FullName, fullName, value => patient.FullName = value, nameof(PatientEntity.FullName), changedFields);
        SetIfChanged(patient.DateOfBirth, request.DateOfBirth, value => patient.DateOfBirth = value, nameof(PatientEntity.DateOfBirth), changedFields);
        SetIfChanged(patient.Gender, gender, value => patient.Gender = value, nameof(PatientEntity.Gender), changedFields);
        SetIfChanged(patient.Phone, phone, value => patient.Phone = value, nameof(PatientEntity.Phone), changedFields);
        SetIfChanged(patient.Email, email, value => patient.Email = value, nameof(PatientEntity.Email), changedFields);
        SetIfChanged(patient.Address, address, value => patient.Address = value, nameof(PatientEntity.Address), changedFields);

        if (changedFields.Count > 0)
        {
            patient.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);

            var detail = JsonSerializer.Serialize(new { changedFields });
            await _auditLogger.LogAsync(
                action: AuditActions.PatientUpdated,
                entityType: "Patient",
                entityId: patient.PatientId,
                userId: actorUserId,
                detail: detail,
                ipAddress: ipAddress,
                ct: ct);
        }

        return Result<PatientResponse>.Success(await ToResponseAsync(patient, ct));
    }

    private async Task<IReadOnlySet<int>> GetActiveGuardianWardIdsAsync(
        IEnumerable<PatientEntity> patients,
        CancellationToken ct)
    {
        if (_guardianRepository is null)
            return new HashSet<int>();

        var patientIds = patients
            .Where(patient => patient.PatientId > 0)
            .Select(patient => patient.PatientId)
            .Distinct()
            .ToArray();

        if (patientIds.Length == 0)
            return new HashSet<int>();

        return await _guardianRepository.GetWardPatientIdsWithActiveGuardianAsync(patientIds, ct)
            ?? new HashSet<int>();
    }

    private async Task<PatientResponse> ToResponseAsync(PatientEntity patient, CancellationToken ct)
    {
        var activeWardIds = await GetActiveGuardianWardIdsAsync([patient], ct);
        return ToResponse(patient, activeWardIds.Contains(patient.PatientId));
    }

    private PatientListItemResponse ToListItemResponse(PatientEntity patient, bool hasActiveGuardian)
    {
        var isUnderFourteen = PatientAccountAge.IsUnderMinimumAge(patient.DateOfBirth, _vietnamClock.Today);
        var hasUserAccount = HasActiveUserAccount(patient);
        return new PatientListItemResponse(
            patient.PatientId,
            patient.PatientCode,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender,
            patient.Phone,
            hasUserAccount,
            hasActiveGuardian,
            isUnderFourteen,
            isUnderFourteen && !hasActiveGuardian);
    }

    private PatientResponse ToResponse(PatientEntity patient, bool hasActiveGuardian = false)
    {
        var isUnderFourteen = PatientAccountAge.IsUnderMinimumAge(patient.DateOfBirth, _vietnamClock.Today);
        var hasUserAccount = HasActiveUserAccount(patient);
        return new PatientResponse(
            patient.PatientId,
            patient.PatientCode,
            patient.FullName,
            patient.DateOfBirth,
            patient.Gender,
            patient.Phone,
            patient.Email,
            patient.Address,
            patient.IsActive,
            hasUserAccount,
            hasActiveGuardian,
            isUnderFourteen,
            isUnderFourteen && !hasActiveGuardian);
    }

    private static bool HasActiveUserAccount(PatientEntity patient)
        => patient.UserId.HasValue && (patient.User is null || patient.User.IsActive);

    private static PatientDuplicateInfo ToDuplicateInfo(PatientEntity patient)
        => new(
            patient.PatientCode,
            patient.FullName,
            patient.DateOfBirth,
            MaskPhone(patient.Phone));

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 5)
            return "***";

        return phone[..2] + new string('*', phone.Length - 5) + phone[^3..];
    }

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
