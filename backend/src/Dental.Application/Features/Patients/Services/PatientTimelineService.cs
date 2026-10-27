using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;

namespace Dental.Application.Features.Patients.Services;

public sealed class PatientTimelineService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IVisitRepository _visitRepository;
    private readonly IPatientGuardianRepository? _guardianRepository;

    public PatientTimelineService(
        IPatientRepository patientRepository,
        IVisitRepository visitRepository)
    {
        _patientRepository = patientRepository;
        _visitRepository = visitRepository;
    }

    public PatientTimelineService(
        IPatientRepository patientRepository,
        IVisitRepository visitRepository,
        IPatientGuardianRepository guardianRepository)
    {
        _patientRepository = patientRepository;
        _visitRepository = visitRepository;
        _guardianRepository = guardianRepository;
    }

    public async Task<Result<PagedResult<PatientTimelineItemResponse>>> GetAsync(
        int patientId,
        int currentUserId,
        string? currentRoleCode,
        PatientTimelineQueryRequest request,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return Result<PagedResult<PatientTimelineItemResponse>>.Failure(Error.NotFound);
        }

        if (currentRoleCode == RoleCodes.Patient)
        {
            var isSelf = patient.UserId == currentUserId;
            var isGuardian = false;
            if (!isSelf && _guardianRepository is not null)
            {
                isGuardian = await _guardianRepository.HasActiveGuardianshipAsync(currentUserId, patientId, ct);
            }

            if (!isSelf && !isGuardian)
            {
                return Result<PagedResult<PatientTimelineItemResponse>>.Failure(Error.NotFound);
            }
        }

        var timeline = await _visitRepository.GetPatientTimelineAsync(
            patientId, request.Page, request.PageSize, ct);

        return Result<PagedResult<PatientTimelineItemResponse>>.Success(timeline);
    }
}
