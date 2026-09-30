using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Services;

/// <summary>
/// Ghi AuditLog vào DB — bất đồng bộ, không ném exception khi thất bại
/// (để không ảnh hưởng luồng chính khi lỗi log).
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly DentalDbContext _db;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(DentalDbContext db, ILogger<AuditLogger> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        int? entityId = null,
        int? userId = null,
        string? detail = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        try
        {
            var log = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                UserId = userId,
                Detail = detail,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow,
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Lỗi ghi AuditLog không được ném ra ngoài — chỉ log warning
            _logger.LogWarning(ex, "Ghi AuditLog thất bại. Action={Action} EntityType={EntityType}", action, entityType);
        }
    }
}
