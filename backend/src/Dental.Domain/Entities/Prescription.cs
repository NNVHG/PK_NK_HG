using System.Text.Json;

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
    public bool ReceivedByProxy { get; set; }
    public string? DispenseNote { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public int? AllergyRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int Version { get; set; } = 1;
    public string? DispenseHistoryJson { get; set; }
    public Visit Visit { get; set; } = null!;
    public Invoice? Invoice { get; set; }
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();

    public List<PrescriptionDispenseEvent> GetDispenseHistory()
        => string.IsNullOrWhiteSpace(DispenseHistoryJson)
            ? []
            : JsonSerializer.Deserialize<List<PrescriptionDispenseEvent>>(DispenseHistoryJson) ?? [];

    public PrescriptionDispenseEvent AppendDispenseEvent(PrescriptionDispenseEvent evt)
    {
        var history = GetDispenseHistory();
        evt.PrescriptionId = Id;
        if (evt.Id <= 0)
            evt.Id = history.Count == 0 ? 1 : Math.Max(1, history.Max(x => x.Id) + 1);
        history.Add(evt);
        DispenseHistoryJson = JsonSerializer.Serialize(history);
        return evt;
    }
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
