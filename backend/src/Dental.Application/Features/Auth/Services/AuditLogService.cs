using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Interfaces;

namespace Dental.Application.Features.Auth.Services;

public sealed class AuditLogService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogService(IAuditLogRepository auditLogRepository)
        => _auditLogRepository = auditLogRepository;

    public async Task<PagedResult<AuditLogResponse>> GetPageAsync(
        AuditLogQueryRequest request,
        CancellationToken ct = default)
    {
        var performedBy = request.PerformedBy?.Trim();
        int? performedByUserId = int.TryParse(performedBy, out var userId) ? userId : null;
        var performedByName = performedByUserId.HasValue ? null : performedBy;
        var action = string.IsNullOrWhiteSpace(request.Action) ? null : request.Action.Trim().ToUpperInvariant();
        var entityType = string.IsNullOrWhiteSpace(request.EntityType) ? null : request.EntityType.Trim();
        DateTime? createdFromUtc = request.FromDate.HasValue
            ? new DateTimeOffset(request.FromDate.Value.ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime
            : null;
        DateTime? createdBeforeUtc = request.ToDate.HasValue
            ? new DateTimeOffset(request.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamOffset).UtcDateTime
            : null;

        var (items, totalCount) = await _auditLogRepository.GetPageAsync(
            performedByUserId,
            performedByName,
            action,
            entityType,
            createdFromUtc,
            createdBeforeUtc,
            request.Page,
            request.PageSize,
            ct);

        var responses = items.Select(log => new AuditLogResponse(
            log.LogId,
            log.UserId,
            log.User?.FullName,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.Detail,
            log.IpAddress,
            DateTime.SpecifyKind(log.CreatedAt, DateTimeKind.Utc))).ToList();

        return PagedResult<AuditLogResponse>.Create(responses, totalCount, request.Page, request.PageSize);
    }
}
