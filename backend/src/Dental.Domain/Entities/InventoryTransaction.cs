using System.Text.Json.Serialization;

namespace Dental.Domain.Entities;

public static class InventoryItemDomains
{
    public const string Supply = "Supply";
    public const string Medicine = "Medicine";
}

public static class InventoryTransactionTypes
{
    public const string Receipt = "Receipt";
    public const string Issue = "Issue";
}

/// <summary>MOD_INV & MOD_RX: chứng từ nhập/xuất kho hợp nhất cho vật tư và thuốc, bất biến sau xác nhận.</summary>
public sealed class InventoryTransaction
{
    public int Id { get; set; }
    public string ItemDomain { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public Guid? RequestId { get; set; }
    public string? PayloadHash { get; set; }
    public string? SupplierName { get; set; }
    public int? VisitId { get; set; }
    public int? PrescriptionId { get; set; }
    public int? DispenseEventId { get; set; }
    public string? Notes { get; set; }
    public int ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public List<InventoryTransactionLine> Lines { get; set; } = [];
}

public sealed class InventoryTransactionLine
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    [JsonIgnore]
    public InventoryTransaction Transaction { get; set; } = null!;
    public int ItemId { get; set; }
    public int? PrescriptionItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
