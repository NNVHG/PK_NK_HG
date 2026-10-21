using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Services;
public static class BookingRequestReservations
{
    public static IQueryable<BookingRequest> Nearby(DentalDbContext db,DateOnly date)
    {
        var first=DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber,date.DayNumber-1));
        var last=DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber,date.DayNumber+1));
        return db.BookingRequests.AsNoTracking().Where(x=>x.Status=="Pending"&&x.AppointmentDate>=first&&x.AppointmentDate<=last);
    }
    public static Appointment Period(BookingRequest x)=>new(){AppointmentDate=x.AppointmentDate,SlotTime=x.SlotTime,SlotDurationMinutes=x.SlotDurationMinutes,DentistId=x.DentistId};
}
