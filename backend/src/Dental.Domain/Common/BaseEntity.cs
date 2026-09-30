namespace Dental.Domain.Common;

/// <summary>
/// Entity gốc chứa các trường dùng chung (audit timestamps).
/// Không implement soft-delete ở tầng base — từng entity tự khai báo IsActive nếu cần.
/// </summary>
public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
