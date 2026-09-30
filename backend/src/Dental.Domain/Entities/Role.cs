using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>
/// Vai trò người dùng.
/// RoleCode là chuỗi không dấu dùng trong JWT và [Authorize].
/// RoleName là tên hiển thị tiếng Việt.
/// </summary>
public class Role : BaseEntity
{
    public int RoleId { get; set; }

    /// <summary>Không dấu, ví dụ: ADMIN, DENTIST</summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>Tên hiển thị, ví dụ: Quản trị viên, Nha sĩ</summary>
    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; } = [];
}
