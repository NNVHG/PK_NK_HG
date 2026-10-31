using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
internal static class TestChairFixtures
{
    public static VisitChairService Service(DentalDbContext db, VietnamClock? clock = null)
        => new(new StaffWorkspaceRepository(db), new DentalChairRepository(db), new VisitRepository(db), new UserRepository(db), clock ?? new VietnamClock(TimeProvider.System));
    public static async Task EnsureAsync(DentalDbContext db, int dentistId, int actorId, DateOnly? workDate = null)
    {
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned test database required");
        if (await db.StaffWorkspaces.AnyAsync(w => w.UserId == dentistId)) return;
        var chair = new DentalChair { ChairNumber = "TEST-" + Guid.NewGuid().ToString("N"), RoomName = "Synthetic room", Status = DentalChairStatus.Available, IsActive = true, CreatedByUserId = actorId };
        db.DentalChairs.Add(chair); await db.SaveChangesAsync();
        var today = workDate ?? new VietnamClock(SmokeTestTimeProvider.Instance).Today;
        db.StaffWorkspaces.Add(new() { UserId = dentistId, ChairId = chair.Id, WorkDate = today, UpdatedByUserId = actorId }); await db.SaveChangesAsync();
    }
}
