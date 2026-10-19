using System.Globalization;
namespace Dental.Domain.Constants;

/// <summary>MOD_MST: giờ vận hành DL174; lịch cũ giữ duration riêng theo DL316.</summary>
public sealed record ClinicSchedule(TimeOnly WorkStart, TimeOnly LunchStart, TimeOnly LunchEnd, TimeOnly WorkEnd, int DurationMinutes)
{
    public static readonly ClinicSchedule Default = new(new(8, 0), new(12, 0), new(13, 30), new(20, 0), 30);
    public TimeOnly[] Slots()
    {
        var result = new List<TimeOnly>();
        if (DurationMinutes is < 1 or > 1440) return [];
        foreach (var (start, end) in new[] { (WorkStart, LunchStart), (LunchEnd, WorkEnd) })
            for (var minute = start.Hour * 60 + start.Minute; minute + DurationMinutes <= end.Hour * 60 + end.Minute; minute += DurationMinutes)
                result.Add(new TimeOnly(minute / 60, minute % 60));
        return result.ToArray();
    }
    public bool IsValidSlot(TimeOnly time) => Slots().Contains(time);
    public static bool TryParse(string? start, string? lunchStart, string? lunchEnd, string? end, int duration, out ClinicSchedule schedule)
    {
        schedule = Default;
        if (!TimeOnly.TryParseExact(start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var s) ||
            !TimeOnly.TryParseExact(lunchStart, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ls) ||
            !TimeOnly.TryParseExact(lunchEnd, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var le) ||
            !TimeOnly.TryParseExact(end, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e) ||
            !(s < ls && ls < le && le < e) || duration is < 1 or > 1440) return false;
        var value = new ClinicSchedule(s, ls, le, e, duration);
        if (value.Slots().Length == 0) return false;
        schedule = value; return true;
    }
}
