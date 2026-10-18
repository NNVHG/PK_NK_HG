using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>MOD_MST / DL-070 / DL-103: master data only in Phase 1; no UnitPrice or stock transactions.</summary>
public sealed class MedicalSupply : BaseEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "Cái";
    public int MinimumStockThreshold { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public int? UpdatedByUserId { get; set; }
    public int Version { get; set; } = 1;
    public string SearchText { get; set; } = string.Empty;
}
