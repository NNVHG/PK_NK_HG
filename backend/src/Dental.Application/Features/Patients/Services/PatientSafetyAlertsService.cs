using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Interfaces;

namespace Dental.Application.Features.Patients.Services;

public sealed class PatientSafetyAlertsService
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMedicalHistoryRepository _medicalHistoryRepository;

    public PatientSafetyAlertsService(
        IPatientRepository patientRepository,
        IMedicalHistoryRepository medicalHistoryRepository)
    {
        _patientRepository = patientRepository;
        _medicalHistoryRepository = medicalHistoryRepository;
    }

    public async Task<Result<PatientSafetyAlertsResponse>> GetAsync(
        int patientId,
        CancellationToken ct = default)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId, ct);
        if (patient is null)
            return Result<PatientSafetyAlertsResponse>.Failure(Error.NotFound);

        var latestRecord = await _medicalHistoryRepository.GetLatestByPatientIdAsync(patientId, ct);
        if (latestRecord is null)
        {
            return Result<PatientSafetyAlertsResponse>.Success(
                new PatientSafetyAlertsResponse(false, []));
        }

        var alerts = latestRecord.Items
            .Where(item => item.IsCritical)
            .OrderBy(item => item.ItemId)
            .Select(item => new PatientSafetyAlertItemResponse(
                item.Type,
                item.Name,
                item.Detail,
                latestRecord.CreatedAt,
                latestRecord.VisitId))
            .ToList();

        return Result<PatientSafetyAlertsResponse>.Success(
            new PatientSafetyAlertsResponse(true, alerts));
    }
}
