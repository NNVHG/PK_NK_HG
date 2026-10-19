namespace Dental.Domain.Entities;

/// <summary>MOD_INV: phiếu nhập đã xác nhận, bất biến; số lượng theo Unit snapshot.</summary>
public sealed class StockReceipt
{
    public int Id { get; set; }
    public Guid RequestId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public List<StockReceiptLine> Lines { get; set; } = [];
}

public sealed class StockReceiptLine
{
    public int Id { get; set; }
    public int StockReceiptId { get; set; }
    public StockReceipt Receipt { get; set; } = null!;
    public int MedicalSupplyId { get; set; }
    public int Quantity { get; set; }
    public string SupplyCode { get; set; } = string.Empty;
    public string SupplyName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

public sealed class StockBalance
{
    public int MedicalSupplyId { get; set; }
    public int OnHand { get; set; }
    public long Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
}
