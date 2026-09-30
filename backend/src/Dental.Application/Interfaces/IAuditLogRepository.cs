using Dental.Domain.Entities;

namespace Dental.Application.Interfaces;

/// <summary>Chỉ cung cấp truy vấn đọc nhật ký kiểm toán.</summary>
public interface IAuditLogRepository
{
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPageAsync(
        int? performedByUserId,
        string? performedByName,
        string? action,
        string? entityType,
        DateTime? createdFromUtc,
        DateTime? createdBeforeUtc,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
