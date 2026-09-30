namespace Dental.Domain.Constants;

/// <summary>Hằng số tên hành động ghi AuditLog — nhất quán toàn hệ thống.</summary>
public static class AuditActions
{
    public const string Login       = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout      = "LOGOUT";
    public const string Create      = "CREATE";
    public const string Update      = "UPDATE";
    public const string Delete      = "DELETE";
}
