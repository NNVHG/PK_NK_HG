namespace Dental.Application.Features.MOD_RX.DTOs;
public sealed record PatientPrescriptionQuery(int Page = 1, int PageSize = 10);
public sealed record PatientPrescriptionSummary(int Id, int PatientId, int VisitId, int Version,
    string PrescriptionCode, DateTime CreatedAt, string DoctorName, string VisitDiagnosis,
    int MedicineCount, bool IsCancelled, DateTime? DispensedAt, bool CanPrint);
public sealed record PatientPrescriptionDetail(int Id, int PatientId, int VisitId, int Version,
    string PrescriptionCode, DateTime CreatedAt, string DoctorName, string VisitDiagnosis,
    string Diagnosis, string? DoctorAdvice, DateOnly? RevisitDate, bool IsCancelled,
    DateTime? DispensedAt, bool CanPrint, IReadOnlyList<PrescriptionPrintLine> Items);
