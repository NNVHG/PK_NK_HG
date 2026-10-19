namespace Dental.Domain.Entities;

/// <summary>MOD_INV: tiêu hao thực tế của một lần khám, chỉ ghi sau xác nhận.</summary>
public sealed class StockIssue
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public Guid RequestId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int ConfirmedByUserId { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public List<StockIssueLine> Lines { get; set; } = [];
}

public sealed class StockIssueLine
{
    public int Id { get; set; }
    public int StockIssueId { get; set; }
    public StockIssue Issue { get; set; } = null!;
    public int MedicalSupplyId { get; set; }
    public int Quantity { get; set; }
    public string SupplyCode { get; set; } = string.Empty;
    public string SupplyName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}
