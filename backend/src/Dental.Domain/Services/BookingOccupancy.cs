using Dental.Domain.Entities;
namespace Dental.Domain.Services;

/// <summary>Khoảng nửa mở: lịch kết thúc đúng lúc lịch khác bắt đầu không trùng.</summary>
public static class BookingOccupancy
{
    public static long Start(DateOnly day, TimeOnly time) => (long)day.DayNumber * TimeSpan.TicksPerDay + time.Ticks;
    public static bool Overlaps(Appointment old, DateOnly day, TimeOnly time, int duration)
    {
        var start = Start(day, time); var oldStart = Start(old.AppointmentDate, old.SlotTime);
        return oldStart < start + duration * TimeSpan.TicksPerMinute && start < oldStart + old.SlotDurationMinutes * TimeSpan.TicksPerMinute;
    }
    public static int Peak(IEnumerable<Appointment> bookings, DateOnly day, TimeOnly time, int duration)
    {
        var start = Start(day, time); var end = start + duration * TimeSpan.TicksPerMinute;
        var events = new List<(long Time, int Delta)>();
        foreach (var a in bookings)
        {
            var from = Math.Max(start, Start(a.AppointmentDate, a.SlotTime));
            var to = Math.Min(end, Start(a.AppointmentDate, a.SlotTime) + a.SlotDurationMinutes * TimeSpan.TicksPerMinute);
            if (from >= to) continue;
            events.Add((from, 1)); events.Add((to, -1));
        }
        var occupied = 0; var peak = 0;
        foreach (var group in events.GroupBy(x => x.Time).OrderBy(x => x.Key)) { occupied += group.Sum(x => x.Delta); peak = Math.Max(peak, occupied); }
        return peak;
    }
}
