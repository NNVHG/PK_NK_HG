using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;
public sealed class ClinicPrintSettingsRepository(DentalDbContext db) : IClinicPrintSettingsRepository
{
    private Task<bool> Admin(int id, CancellationToken ct) => db.Users.AnyAsync(u => u.UserId == id && u.IsActive && u.Role.RoleCode == RoleCodes.Admin, ct);
    private async Task<ClinicPrintSettings> Read(CancellationToken ct)
    {
        var values = await db.SystemConfigs.AsNoTracking().Where(x => x.Key == "Clinic:Address" || x.Key == "Clinic:Phone").ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        var address = values.GetValueOrDefault("Clinic:Address"); var phone = values.GetValueOrDefault("Clinic:Phone");
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { address, phone }))));
        return new(address, phone, revision);
    }
    public async Task<Result<ClinicPrintSettings>> ReadAsync(int actor, CancellationToken ct) =>
        !await Admin(actor, ct) ? Result<ClinicPrintSettings>.Failure(Error.Forbidden) : Result<ClinicPrintSettings>.Success(await Read(ct));
    public async Task<Result<ClinicPrintSettings>> SaveAsync(int actor, ClinicPrintSettingsRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            if (!await Admin(actor, ct)) return Result<ClinicPrintSettings>.Failure(Error.Forbidden);
            if ((await Read(ct)).Revision != request.ExpectedRevision) return Result<ClinicPrintSettings>.Failure(Error.Conflict);
            foreach (var pair in new[] { ("Clinic:Address", request.Address.Trim()), ("Clinic:Phone", request.Phone.Trim()) })
            {
                var row = await db.SystemConfigs.SingleOrDefaultAsync(x => x.Key == pair.Item1, ct);
                if (row is null) { row = new SystemConfig { Key = pair.Item1 }; db.SystemConfigs.Add(row); }
                row.Value = pair.Item2; row.DataType = "String"; row.UpdatedAt = DateTime.UtcNow; row.UpdatedByUserId = actor;
            }
            db.AuditLogs.Add(new() { UserId = actor, Action = "MOD_MST_CLINIC_PRINT_SETTINGS_UPDATED", EntityType = "SystemConfig", IpAddress = ip, CreatedAt = DateTime.UtcNow,
                Detail = JsonSerializer.Serialize(new { fields = new[] { "Clinic:Address", "Clinic:Phone" } }) });
            await db.SaveChangesAsync(ct); var result = await Read(ct); await tx.CommitAsync(ct); committed = true;
            return Result<ClinicPrintSettings>.Success(result);
        }
        catch (Exception e) when (IsConflict(e))
        { return Result<ClinicPrintSettings>.Failure(Error.Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool IsConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException || current is PostgresException { SqlState: "23505" or "40001" or "40P01" }) return true;
        return false;
    }
}
