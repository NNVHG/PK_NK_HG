namespace Dental.Application.Features.VitalSigns.DTOs;

public sealed record VitalSignRecordResponse(
    int VitalSignRecordId,
    int PatientId,
    int VisitId,
    int? SystolicBp,
    int? DiastolicBp,
    int? PulseBpm,
    decimal? TemperatureC,
    string? Note,
    int RecordedByUserId,
    DateTime CreatedAt);
