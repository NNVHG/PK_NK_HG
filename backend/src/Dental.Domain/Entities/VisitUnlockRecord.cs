namespace Dental.Domain.Entities;

/// <summary>MOD_PAT: lý do mở khóa lưu riêng, không đưa nội dung y khoa vào audit JSON.</summary>
public sealed class VisitUnlockRecord
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int ActorUserId { get; set; }
    public int? CancelledInvoiceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Visit Visit { get; set; } = null!;
    public User Actor { get; set; } = null!;
    public Invoice? CancelledInvoice { get; set; }
}
