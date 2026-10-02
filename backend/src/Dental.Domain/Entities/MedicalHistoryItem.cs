namespace Dental.Domain.Entities;

/// <summary>Mục dị ứng hoặc bệnh lý thuộc một bản chụp tiền sử.</summary>
public sealed class MedicalHistoryItem
{
    public int ItemId { get; set; }
    public int RecordId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
    public string? Detail { get; set; }

    public MedicalHistoryRecord Record { get; set; } = null!;
}
