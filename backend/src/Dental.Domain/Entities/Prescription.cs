namespace Dental.Domain.Entities;
public sealed class Prescription
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int DoctorId { get; set; }
    public int? InvoiceId { get; set; }
    public string PrescriptionCode { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string? DoctorAdvice { get; set; }
    public DateOnly? RevisitDate { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime? DispensedAt { get; set; }
    public int? DispensedBy { get; set; }
    public int? AllergyRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int Version { get; set; } = 1;
    public Visit Visit { get; set; } = null!;
    public Invoice? Invoice { get; set; }
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}
public sealed class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public int MedicineId { get; set; }
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string? Strength { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => UnitPrice * Quantity;
    public string UsageInstructions { get; set; } = string.Empty;
    public int? TreatmentDays { get; set; }
    public string? AllergyOverrideReason { get; set; }
    public Prescription Prescription { get; set; } = null!;
}
public sealed class PrescriptionNumberCounter
{
    public DateOnly PrescriptionDate { get; set; }
    public int LastNumber { get; set; }
}
