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

    /// <summary>Required audit inside a transaction: propagate failure so the operation cannot commit without its audit.</summary>
    Task LogRequiredAsync(string action, string entityType, int? entityId = null, int? userId = null,
        string? detail = null, string? ipAddress = null, CancellationToken ct = default)
        => throw new NotSupportedException("Required transactional audit is not supported by this logger.");
}
