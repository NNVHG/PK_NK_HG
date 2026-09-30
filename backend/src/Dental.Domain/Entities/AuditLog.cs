namespace Dental.Domain.Entities;

/// <summary>
/// Nhật ký kiểm toán bất biến (immutable).
/// KHÔNG được UPDATE hoặc DELETE bất kỳ dòng nào.
/// DbContext override SaveChanges để enforce điều này.
/// </summary>
public class AuditLog
{
    public long LogId { get; set; }

    /// <summary>null nếu hành động do hệ thống tự động hoặc đăng nhập thất bại (chưa biết user)</summary>
    public int? UserId { get; set; }

    /// <summary>Ví dụ: LOGIN, LOGIN_FAILED, CREATE, UPDATE, DELETE</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Ví dụ: User, Patient, Invoice</summary>
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    /// <summary>
    /// Chi tiết dạng JSON.
    /// KHÔNG chứa mật khẩu, token. SĐT che một phần (09****678).
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>IP của request</summary>
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}
