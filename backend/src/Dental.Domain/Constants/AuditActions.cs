namespace Dental.Domain.Constants;

/// <summary>Hằng số tên hành động ghi AuditLog — nhất quán toàn hệ thống.</summary>
public static class AuditActions
{
    public const string Login       = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout      = "LOGOUT";
    public const string Create      = "CREATE";
    public const string Update      = "UPDATE";
    public const string Delete      = "DELETE";
    public const string PatientCreated = "PATIENT_CREATED";
    public const string PatientUpdated = "PATIENT_UPDATED";
    public const string UserRegistered = "USER_REGISTERED";
    public const string VisitCreated = "VISIT_CREATED";
    public const string VisitDiagnosisUpdated = "VISIT_DIAGNOSIS_UPDATED";
    public const string MedicalHistoryRecorded = "MEDICAL_HISTORY_RECORDED";
    public const string VitalSignsRecorded = "VITAL_SIGNS_RECORDED";
    public const string ServicePriceCreated = "SERVICE_PRICE_CREATED";
    public const string ServiceDeactivated = "SERVICE_DEACTIVATED";
    public const string ServiceActivated = "SERVICE_ACTIVATED";
    public const string AppointmentCreated = "APPOINTMENT_CREATED";
    public const string AppointmentCancelled = "APPOINTMENT_CANCELLED";
    public const string AppointmentRescheduled = "APPOINTMENT_RESCHEDULED";
    public const string QueueCheckedIn = "QUEUE_CHECKED_IN";
    public const string QueueStatusChanged = "QUEUE_STATUS_CHANGED";

}
