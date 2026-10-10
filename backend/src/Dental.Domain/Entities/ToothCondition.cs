namespace Dental.Domain.Entities;

/// <summary>MOD_FDI: ghi nhận tình trạng răng trong một lần khám; không ghi đè lần khám cũ.</summary>
public sealed class ToothCondition
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int ToothNumber { get; set; }
    public string? Surface { get; set; }
    public string ConditionCode { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public Visit Visit { get; set; } = null!;
}
