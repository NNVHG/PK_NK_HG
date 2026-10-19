namespace Dental.Domain.Entities;

public sealed class MedicineStockBalance
{
    public int MedicineId { get; set; }
    public int OnHand { get; set; }
    public long Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
}

// One medicine per receipt keeps the MVP entry flow small. Confirmed documents are append-only.
public sealed class MedicineStockReceipt
{
    public int Id { get; set; }
    public Guid RequestId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public int MedicineId { get; set; }
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public int ConfirmedByUserId { get; set; }
}

public sealed class MedicineStockIssue
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public int PrescriptionItemId { get; set; }
    public int MedicineId { get; set; }
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int DispenseEventId { get; set; }
    public PrescriptionDispenseEvent DispenseEvent { get; set; } = null!;
    public DateTime IssuedAt { get; set; }
    public int IssuedByUserId { get; set; }
}
