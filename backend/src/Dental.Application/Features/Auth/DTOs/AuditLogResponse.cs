namespace Dental.Application.Features.Auth.DTOs;

public sealed record AuditLogResponse(
    long LogId,
    int? UserId,
    string? UserName,
    string Action,
    string EntityType,
    int? EntityId,
    string? Detail,
    string? IpAddress,
    DateTime CreatedAt);
