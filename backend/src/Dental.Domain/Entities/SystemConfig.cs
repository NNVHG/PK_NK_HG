namespace Dental.Domain.Entities;

// MOD_MST/DL105; Key/Value retain their C# names for existing consumers.
public sealed class SystemConfig
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DataType { get; set; } = "String";
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
