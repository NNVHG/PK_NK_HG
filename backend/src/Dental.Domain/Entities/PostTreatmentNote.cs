using System.Text.Json.Serialization;

namespace Dental.Domain.Entities;

/// <summary>
/// Ghi chú lâm sàng hậu điều trị sau khi ca khám hoàn tất và đã thanh toán viện phí (DL-227, DL-265, F_PAT_08).
/// Lưu dạng JSONB append-only trên Visits, không được sửa hoặc xóa.
/// </summary>
public sealed class PostTreatmentNote
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int CreatedByUserId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? RequestId { get; set; }

    [JsonIgnore]
    public Visit Visit { get; set; } = null!;
    [JsonIgnore]
    public User CreatedByUser { get; set; } = null!;
}
