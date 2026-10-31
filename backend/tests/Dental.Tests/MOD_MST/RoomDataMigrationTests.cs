using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dental.Tests.MOD_MST;

/// <summary>
/// Kiểm thử đơn vị cho E1 — dữ liệu phòng: preflight RoomName, chặn trùng tên/xung đột ca đang khám, và bảo vệ DB chính (DL-361, DL-367).
/// </summary>
public sealed class RoomDataMigrationTests
{
    [Theory]
    [InlineData("  Phòng   Khám   1  ", "Phòng Khám 1", "PHÒNG KHÁM 1")]
    [InlineData("phòng tiểu phẫu", "phòng tiểu phẫu", "PHÒNG TIỂU PHẪU")]
    [InlineData("   ", "", "")]
    [InlineData(null, "", "")]
    public void NormalizeAndCleanDisplayRoomName_CollapsesWhitespaceAndNormalizesCase(
        string? raw,
        string expectedClean,
        string expectedNormalized)
    {
        Assert.Equal(expectedClean, RoomMigrationPreflight.CleanDisplayRoomName(raw));
        Assert.Equal(expectedNormalized, RoomMigrationPreflight.NormalizeRoomName(raw));
    }

    [Fact]
    public void Analyze_CleanLegacyChairsWorkspacesAndVisits_AllowsSafeMigration()
    {
        var chairs = new[]
        {
            new RoomMigrationPreflight.ChairRoomSnapshot(1, "G01", "Phòng Khám 1", DentalChairStatus.Occupied, true, null),
            new RoomMigrationPreflight.ChairRoomSnapshot(2, "G02", "Phòng Khám 2", DentalChairStatus.Available, true, null),
            new RoomMigrationPreflight.ChairRoomSnapshot(3, "G03", "Phòng Tiểu Phẫu", DentalChairStatus.Maintenance, true, null),
        };
        var workspaces = new[]
        {
            new RoomMigrationPreflight.WorkspaceRoomSnapshot(10, RoleCodes.Dentist, true, 1, null, null),
            new RoomMigrationPreflight.WorkspaceRoomSnapshot(11, RoleCodes.Assistant, true, null, 10, null),
            new RoomMigrationPreflight.WorkspaceRoomSnapshot(12, RoleCodes.Assistant, true, null, 10, null),
        };
        var visits = new[]
        {
            new RoomMigrationPreflight.VisitRoomSnapshot(100, VisitStatuses.InProgress, 10, 1, null),
            new RoomMigrationPreflight.VisitRoomSnapshot(101, VisitStatuses.Completed, 10, 1, null),
            new RoomMigrationPreflight.VisitRoomSnapshot(102, VisitStatuses.Cancelled, 10, 2, null),
        };

        var report = RoomMigrationPreflight.Analyze(chairs, workspaces, visits);

        Assert.True(report.CanMigrateSafely);
        Assert.Equal(3, report.TotalChairs);
        Assert.Equal(0, report.AlreadyLinkedChairs);
        Assert.Equal(3, report.PendingChairsToMigrate);
        Assert.Equal(1, report.ActiveInProgressVisits);
        Assert.Equal(2, report.HistoricalVisitsWithChair);
        Assert.Equal(1, report.ActiveDentistWorkspaces);
        Assert.Empty(report.EmptyRoomNameChairIds);
        Assert.Empty(report.DuplicateRoomNameGroups);
        Assert.Empty(report.ConflictingActiveVisitIds);
        Assert.Empty(report.ConflictingWorkspaceUserIds);
    }

    [Fact]
    public void Analyze_DuplicateRoomNamesAcrossChairs_BlocksMigrationWithoutPickingWinner()
    {
        var chairs = new[]
        {
            new RoomMigrationPreflight.ChairRoomSnapshot(1, "G01", "Phòng Khám 1", DentalChairStatus.Available, true, null),
            new RoomMigrationPreflight.ChairRoomSnapshot(2, "G02", "  phòng   khám 1 ", DentalChairStatus.Available, true, null),
            new RoomMigrationPreflight.ChairRoomSnapshot(3, "G03", "Phòng Khám 2", DentalChairStatus.Available, true, null),
        };
        var workspaces = new[]
        {
            new RoomMigrationPreflight.WorkspaceRoomSnapshot(10, RoleCodes.Dentist, true, 1, null, null),
        };
        var visits = new[]
        {
            new RoomMigrationPreflight.VisitRoomSnapshot(200, VisitStatuses.InProgress, 10, 2, null),
        };

        var report = RoomMigrationPreflight.Analyze(chairs, workspaces, visits);

        Assert.False(report.CanMigrateSafely);
        var dup = Assert.Single(report.DuplicateRoomNameGroups);
        Assert.Equal("PHÒNG KHÁM 1", dup.NormalizedRoomName);
        Assert.Equal(new[] { 1, 2 }, dup.ChairIds);
        Assert.Equal(new[] { "G01", "G02" }, dup.ChairNumbers);
        Assert.Contains(200, report.ConflictingActiveVisitIds);
        Assert.Contains(10, report.ConflictingWorkspaceUserIds);
    }

    [Fact]
    public void Analyze_EmptyRoomNameOrChairNumber_BlocksMigration()
    {
        var chairs = new[]
        {
            new RoomMigrationPreflight.ChairRoomSnapshot(7, "G07", "   ", DentalChairStatus.Available, true, null),
            new RoomMigrationPreflight.ChairRoomSnapshot(8, " ", "Phòng 8", DentalChairStatus.Available, true, null),
        };

        var report = RoomMigrationPreflight.Analyze(chairs, [], []);

        Assert.False(report.CanMigrateSafely);
        Assert.Equal(new[] { 7, 8 }, report.EmptyRoomNameChairIds);
    }

    [Fact]
    public void Analyze_ActiveInProgressVisitOnMaintenanceOrInactiveChair_BlocksMigration()
    {
        var chairs = new[]
        {
            new RoomMigrationPreflight.ChairRoomSnapshot(1, "G01", "Phòng 1", DentalChairStatus.Maintenance, true, null),
            new RoomMigrationPreflight.ChairRoomSnapshot(2, "G02", "Phòng 2", DentalChairStatus.Inactive, false, null),
        };
        var visits = new[]
        {
            new RoomMigrationPreflight.VisitRoomSnapshot(301, VisitStatuses.InProgress, 10, 1, null),
            new RoomMigrationPreflight.VisitRoomSnapshot(302, VisitStatuses.InProgress, 11, 2, null),
            new RoomMigrationPreflight.VisitRoomSnapshot(303, VisitStatuses.Completed, 10, 1, null),
        };

        var report = RoomMigrationPreflight.Analyze(chairs, [], visits);

        Assert.False(report.CanMigrateSafely);
        Assert.Equal(new[] { 301, 302 }, report.ConflictingActiveVisitIds);
    }

    [Fact]
    public void Analyze_WorkspaceWithBothChairAndDentistOrDuplicateChairClaim_BlocksMigration()
    {
        var chairs = new[]
        {
            new RoomMigrationPreflight.ChairRoomSnapshot(1, "G01", "Phòng 1", DentalChairStatus.Available, true, null),
        };
        var workspaces = new[]
        {
            new RoomMigrationPreflight.WorkspaceRoomSnapshot(10, RoleCodes.Dentist, true, 1, null, null),
            new RoomMigrationPreflight.WorkspaceRoomSnapshot(11, RoleCodes.Dentist, true, 1, null, null),
            new RoomMigrationPreflight.WorkspaceRoomSnapshot(12, RoleCodes.Assistant, true, 1, 10, null),
        };

        var report = RoomMigrationPreflight.Analyze(chairs, workspaces, []);

        Assert.False(report.CanMigrateSafely);
        Assert.Equal(new[] { 10, 11, 12 }, report.ConflictingWorkspaceUserIds);
    }

    [Theory]
    [InlineData("dental_db")]
    [InlineData("dental_clinic")]
    [InlineData("DENTAL_DB")]
    public void AssertNotProductionDatabase_BlocksClinicDatabases(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalDbContext>()
            .UseNpgsql($"Host=localhost;Database={dbName};Username=postgres;Password=postgres")
            .Options;
        using var db = new DentalDbContext(options);

        Assert.Throws<InvalidOperationException>(() =>
            RoomDataMigrationRunner.AssertNotProductionDatabase(db));
    }
}
