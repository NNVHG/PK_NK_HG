using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Services;

public static class ClinicScheduleReader
{
    public static readonly string[] Keys = ["Clinic:WorkStartTime", "Clinic:LunchStartTime", "Clinic:LunchEndTime", "Clinic:WorkEndTime", "Appointment:DefaultSlotDurationMinutes"];
    public static async Task<ScheduleSnapshot> ReadAsync(DentalDbContext db, CancellationToken ct)
    {
        var values = await db.SystemConfigs.AsNoTracking().Where(x => Keys.Contains(x.Key)).ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        var raw = Keys.Select(key => new { key, value = values.GetValueOrDefault(key) }).ToArray();
        var revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(raw)));
        var fallback = ClinicSchedule.Default;
        string Value(int i, TimeOnly defaultValue) => values.GetValueOrDefault(Keys[i]) ?? defaultValue.ToString("HH:mm", CultureInfo.InvariantCulture);
        var durationText = values.GetValueOrDefault(Keys[4]);
        var duration = durationText is null ? fallback.DurationMinutes : int.TryParse(durationText, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
        var valid = ClinicSchedule.TryParse(Value(0, fallback.WorkStart), Value(1, fallback.LunchStart), Value(2, fallback.LunchEnd), Value(3, fallback.WorkEnd), duration, out var schedule);
        return new(valid ? schedule : fallback, revision, !valid || values.Count < Keys.Length);
    }
}
