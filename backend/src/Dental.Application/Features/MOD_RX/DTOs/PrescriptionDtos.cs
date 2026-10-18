using System.Text.Json.Serialization;
using Dental.Domain.Entities;
namespace Dental.Application.Features.MOD_RX.DTOs;
public sealed record PrescriptionLineRequest(int MedicineId, int MedicineVersion, int Quantity, string UsageInstructions,
    int? TreatmentDays = null, bool AllergyConfirmed = false, string? AllergyOverrideReason = null);
public sealed record PrescriptionWriteRequest(int ExpectedVersion, string Diagnosis, IReadOnlyList<PrescriptionLineRequest> Items,
    string? DoctorAdvice = null, DateOnly? RevisitDate = null, bool DuplicateIngredientConfirmed = false, string? PreviewToken = null);
public sealed record PrescriptionMedicine(int Id, int Version, string Code, string Name, string? ActiveIngredient, string? Strength,
    string Unit, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal UnitPrice, string? DefaultUsageInstruction);
public sealed record PrescriptionLineResponse(int Id, int MedicineId, int MedicineVersion, string MedicineCode, string MedicineName,
    string? ActiveIngredient, string? Strength, string Unit, int Quantity,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal UnitPrice,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal TotalPrice,
    string UsageInstructions, int? TreatmentDays, string? AllergyOverrideReason, bool MedicineActive);
public sealed record PrescriptionResponse(int Id, int VisitId, int DoctorId, int? InvoiceId, string PrescriptionCode,
    string Diagnosis, string? DoctorAdvice, DateOnly? RevisitDate, bool IsCancelled, DateTime? DispensedAt,
    DateTime CreatedAt, DateTime? UpdatedAt, int Version, IReadOnlyList<PrescriptionLineResponse> Items);
public sealed record PrescriptionAllergy(int Id, string Name, string? Detail);
public sealed record PrescriptionContext(int VisitId, int PatientId, int? DentistId, string VisitStatus, bool VisitLocked,
    string VisitDiagnosis, bool CanEdit, int? InvoiceId, string? InvoiceCode, string? InvoiceStatus,
    int? AllergyRecordId, IReadOnlyList<PrescriptionAllergy> Allergies, PrescriptionResponse? Prescription)
{
    public bool CanCancel { get; init; }
}
public sealed record PrescriptionWarning(int MedicineId, IReadOnlyList<int> AllergyIds, IReadOnlyList<string> AllergyNames);
public sealed record PrescriptionPreview(string Token, IReadOnlyList<PrescriptionWarning> AllergyWarnings,
    IReadOnlyList<string> DuplicateIngredients,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal MedicineTotal,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal ServicesTotal,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal DiscountAmount,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal FinalAmount);
public sealed record RepeatPrescription(int SourcePrescriptionId, string SourceCode, IReadOnlyList<RepeatPrescriptionLine> Items);
public sealed record RepeatPrescriptionLine(int MedicineId, int Quantity, string UsageInstructions, int? TreatmentDays,
    PrescriptionMedicine? CurrentMedicine);
public sealed record PrescriptionSnapshot(Visit Visit, Invoice? Invoice, Prescription? Prescription, bool HasReceiptHistory,
    int? AllergyRecordId, IReadOnlyList<MedicalHistoryItem> Allergies, IReadOnlyList<Medicine> Medicines, IReadOnlyList<VisitService> Services)
{
    public int CancellationGeneration { get; init; }
}
public sealed record PrescriptionPlan(IReadOnlyList<PrescriptionItem> Items, PrescriptionPreview Preview);
public sealed record PrescriptionLookupLink(string PrescriptionCode, int Version, string RelativePath);
public sealed record PrescriptionCancellationRequest(int ExpectedVersion);
public sealed record PrescriptionCancellationResult(int PrescriptionId, int VisitId, int Version, int InvoiceId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal InvoiceTotal);
