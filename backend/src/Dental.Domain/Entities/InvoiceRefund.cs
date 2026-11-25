using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dental.Domain.Entities;

public static class InvoiceRefundStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Stale = "Stale";
}

/// <summary>MOD_BIL: đề xuất hoàn tiền / phiếu hoàn tiền hợp nhất (Pending/Stale/Rejected hoặc Approved bất biến).</summary>
public sealed class InvoiceRefund
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int Revision { get; set; }
    public Guid RequestId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string Snapshot { get; set; } = "";
    public string Status { get; set; } = InvoiceRefundStatuses.Approved;
    public int ActorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ReviewerId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
    public int? PaymentTransactionId { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal DebtReduction { get; set; }
    public decimal TotalBefore { get; set; }
    public decimal DiscountBefore { get; set; }
    public decimal PaidBefore { get; set; }
    public decimal TotalAfter { get; set; }
    public decimal DiscountAfter { get; set; }
    public decimal PaidAfter { get; set; }
    public string ReasonCode { get; set; } = "";
    public string ReasonNotes { get; set; } = "";
    public string PaymentMethod { get; set; } = "Cash";
    public bool LateApproval { get; set; }
    public bool ReserveConfirmed { get; set; }
    public string ItemsJson { get; set; } = "[]";

    public Invoice Invoice { get; set; } = null!;
    public User Actor { get; set; } = null!;
    public User? Reviewer { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
    [NotMapped]
    public ICollection<InvoiceRefundItem> Items { get; set; } = new List<InvoiceRefundItem>();

    public List<InvoiceRefundItem> GetItems()
    {
        if (string.IsNullOrWhiteSpace(ItemsJson) || ItemsJson == "[]")
            return [.. Items];

        var list = JsonSerializer.Deserialize<List<InvoiceRefundItem>>(ItemsJson) ?? [];
        foreach (var item in list)
            item.InvoiceRefundId = Id;
        return list;
    }

    public InvoiceRefund HydrateItems()
    {
        if (!string.IsNullOrWhiteSpace(ItemsJson) && ItemsJson != "[]")
            Items = GetItems();
        return this;
    }

    public void SyncItemsJson()
    {
        var list = Items.ToList();
        var nextId = 1;
        foreach (var item in list)
        {
            item.InvoiceRefundId = Id;
            if (item.Id <= 0)
                item.Id = nextId++;
            else
                nextId = Math.Max(nextId, item.Id + 1);
        }

        Items = list;
        ItemsJson = JsonSerializer.Serialize(list);
    }
}

public sealed class InvoiceRefundItem
{
    public int Id { get; set; }
    public int InvoiceRefundId { get; set; }
    public int InvoiceItemId { get; set; }
    public int Quantity { get; set; }

    [JsonIgnore]
    public InvoiceRefund InvoiceRefund { get; set; } = null!;
    [JsonIgnore]
    public InvoiceItem InvoiceItem { get; set; } = null!;
}
