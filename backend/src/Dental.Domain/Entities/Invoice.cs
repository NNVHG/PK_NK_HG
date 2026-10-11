using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

public sealed class Invoice
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Visit Visit { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public sealed class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int SourceVisitServiceId { get; set; }
    public string ItemType { get; set; } = "Service";
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ToothNumber { get; set; }
    public string? Surface { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public Invoice Invoice { get; set; } = null!;
}

public sealed class InvoiceNumberCounter
{
    public DateOnly InvoiceDate { get; set; }
    public int LastNumber { get; set; }
}
