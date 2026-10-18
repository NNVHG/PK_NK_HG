namespace Dental.Domain.Entities;

/// <summary>MOD_MST: master data only in Phase 1, no inventory quantities.</summary>
public sealed class Medicine
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string? Strength { get; set; }
    public string Unit { get; set; } = "Viên";
    public decimal UnitPrice { get; set; }
    public string? DefaultUsageInstruction { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
    public string SearchText { get; set; } = string.Empty;
}
