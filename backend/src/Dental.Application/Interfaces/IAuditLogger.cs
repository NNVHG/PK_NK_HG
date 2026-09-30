namespace Dental.Application.Interfaces;

public interface IAuditLogger
{
    /// <summary>Ghi AuditLog bất đồng bộ — không ném exception khi ghi thất bại (chỉ log warning).</summary>
    Task LogAsync(
        string action,
        string entityType,
        int? entityId = null,
        int? userId = null,
        string? detail = null,
        string? ipAddress = null,
        CancellationToken ct = default);
}
