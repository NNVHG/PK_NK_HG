using Dental.Domain.Enums;

namespace Dental.Application.Features.Appointments.DTOs;

public sealed record CreateAppointmentRequest(
    int PatientId,
    DateOnly AppointmentDate,
    TimeOnly SlotTime,
    int? DentistId = null,
    string? Notes = null,
    // Retired by DL337; kept so old clients receive an explicit validation error.
    bool IsEmergency = false,
    string? EmergencyReason = null,
    int? ConfirmedDentistConflictAppointmentId = null,
    string? ExpectedScheduleRevision = null,
    int? ConfirmedDentistConflictRequestId = null,
    string? CitizenId = null,
    string? ContactPhone = null,
    string? GuardianFullName = null,
    string? GuardianRelationship = null,
    bool GuardianVerifiedAtCounter = false,
    IdentityDocumentType? DocumentType = null,
    string? NationalityCode = null
);
