using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task GetPageAsync_MapsFiltersAndReturnsPagedAuditRecords()
    {
        var repository = Substitute.For<IAuditLogRepository>();
        var actor = new User { UserId = 7, FullName = "Quản trị viên" };
        var log = new AuditLog
        {
            LogId = 101,
            UserId = actor.UserId,
            User = actor,
            Action = "UPDATE",
            EntityType = "User",
            EntityId = 22,
            Detail = "{\"action\":\"update_staff\"}",
            CreatedAt = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc),
        };
        repository.GetPageAsync(
            7,
            null,
            "UPDATE",
            "User",
            new DateTime(2026, 9, 29, 17, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
            2,
            10,
            Arg.Any<CancellationToken>())
            .Returns((new List<AuditLog> { log }, 11));

        var result = await new AuditLogService(repository).GetPageAsync(new AuditLogQueryRequest
        {
            Page = 2,
            PageSize = 10,
            PerformedBy = "7",
            Action = " update ",
            EntityType = " User ",
            FromDate = new DateOnly(2026, 9, 30),
            ToDate = new DateOnly(2026, 9, 30),
        });

        Assert.Equal(11, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal("Quản trị viên", Assert.Single(result.Items).UserName);
        Assert.Equal(101, result.Items[0].LogId);
        await repository.Received(1).GetPageAsync(
            7,
            null,
            "UPDATE",
            "User",
            new DateTime(2026, 9, 29, 17, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc),
            2,
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPageAsync_UsesPerformerNameWhenSearchIsNotUserId()
    {
        var repository = Substitute.For<IAuditLogRepository>();
        repository.GetPageAsync(
            null, "Lễ tân", null, null, null, null, 1, 20, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<AuditLog>(), 0));

        await new AuditLogService(repository).GetPageAsync(new AuditLogQueryRequest { PerformedBy = " Lễ tân " });

        await repository.Received(1).GetPageAsync(
            null, "Lễ tân", null, null, null, null, 1, 20, Arg.Any<CancellationToken>());
    }
}
