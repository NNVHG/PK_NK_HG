using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Bản chụp tiền sử bệnh và dị ứng tại một lần khám.</summary>
public sealed class MedicalHistoryRecord : BaseEntity
{
    public int RecordId { get; set; }
    public int PatientId { get; set; }
    public int VisitId { get; set; }
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }

    public Patient Patient { get; set; } = null!;
    public Visit Visit { get; set; } = null!;
    public User RecordedByUser { get; set; } = null!;
    public ICollection<MedicalHistoryItem> Items { get; set; } = [];
}
