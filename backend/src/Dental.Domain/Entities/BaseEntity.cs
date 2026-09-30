namespace Dental.Domain.Entities;

/// <summary>Thực thể gốc: chứa trường audit chung cho mọi entity.</summary>
public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
