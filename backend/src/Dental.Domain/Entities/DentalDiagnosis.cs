namespace Dental.Domain.Entities;
public sealed class DentalDiagnosis
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string SearchText { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
}
