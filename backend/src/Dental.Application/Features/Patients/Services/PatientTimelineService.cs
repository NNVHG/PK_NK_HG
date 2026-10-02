using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;

namespace Dental.Application.Features.Patients.Services;

public sealed class PatientTimelineService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IVisitRepository _visitRepository;

    public PatientTimelineService(
        IPatientRepository patientRepository,
        IVisitRepository visitRepository)
    {
        _patientRepository = patientRepository;
        _visitRepository = visitRepository;
    }

    public async Task<Result<PagedResult<PatientTimelineItemResponse>>> GetAsync(
        int patientId,
        int currentUserId,
        string? currentRoleCode,
        PatientTimelineQueryRequest request,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null ||
            (currentRoleCode == RoleCodes.Patient && patient.UserId != currentUserId))
        {
            return Result<PagedResult<PatientTimelineItemResponse>>.Failure(Error.NotFound);
        }

        var timeline = await _visitRepository.GetPatientTimelineAsync(
            patientId, request.Page, request.PageSize, ct);

        return Result<PagedResult<PatientTimelineItemResponse>>.Success(timeline);
    }
}
