using System.Data;
using System.Globalization;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;

public sealed class SchedulingSettingsRepository(DentalDbContext db) : ISchedulingSettingsRepository
{
    private Task<bool> Admin(int actor, CancellationToken ct) => db.Users.AnyAsync(x => x.UserId == actor && x.IsActive && x.Role.RoleCode == RoleCodes.Admin, ct);
    public async Task<Result<ScheduleSnapshot>> ReadAsync(int actor, CancellationToken ct) => !await Admin(actor, ct) ? Result<ScheduleSnapshot>.Failure(Error.Forbidden) : Result<ScheduleSnapshot>.Success(await ClinicScheduleReader.ReadAsync(db, ct));
    public async Task<Result<ScheduleSnapshot>> SaveAsync(int actor, SchedulingSettingsRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            if (!await Admin(actor, ct)) return Result<ScheduleSnapshot>.Failure(Error.Forbidden);
            var prior = await ClinicScheduleReader.ReadAsync(db, ct);
            if (prior.Revision != request.ExpectedRevision) return Result<ScheduleSnapshot>.Failure(Error.Conflict);
            var values = new[] { request.WorkStartTime, request.LunchStartTime, request.LunchEndTime, request.WorkEndTime, request.SlotDurationMinutes.ToString(CultureInfo.InvariantCulture) };
            var before = await db.SystemConfigs.AsNoTracking().Where(x => ClinicScheduleReader.Keys.Contains(x.Key)).ToDictionaryAsync(x => x.Key, x => x.Value, ct);
            for (var i = 0; i < ClinicScheduleReader.Keys.Length; i++)
            {
                var key = ClinicScheduleReader.Keys[i]; var row = await db.SystemConfigs.SingleOrDefaultAsync(x => x.Key == key, ct);
                if (row is null) { row = new SystemConfig { Key = key }; db.SystemConfigs.Add(row); }
                row.Value = values[i]; row.DataType = i == 4 ? "Number" : "String";
                row.Description = i == 4 ? "Thời lượng slot đặt lịch (phút)" : "Giờ vận hành phòng khám theo DL174";
                row.UpdatedAt = DateTime.UtcNow; row.UpdatedByUserId = actor;
            }
            db.AuditLogs.Add(new AuditLog { UserId = actor, Action = "MOD_MST_SCHEDULE_UPDATED", EntityType = "SystemConfig", CreatedAt = DateTime.UtcNow, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { Before = before, After = ClinicScheduleReader.Keys.Select((key, i) => new { Key = key, Value = values[i] }) }) });
            await db.SaveChangesAsync(ct); var result = await ClinicScheduleReader.ReadAsync(db, ct);
            await tx.CommitAsync(ct); committed = true;
            return Result<ScheduleSnapshot>.Success(result);
        }
        catch (Exception e) when (Conflict(e)) { return Result<ScheduleSnapshot>.Failure(Error.Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool Conflict(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
            if (e is DbUpdateConcurrencyException || e is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
