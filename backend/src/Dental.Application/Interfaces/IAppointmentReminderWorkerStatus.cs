using Dental.Application.Features.Appointments.DTOs;
namespace Dental.Application.Interfaces;

public interface IAppointmentReminderWorkerStatus
{
    ReminderWorkerStatusResponse GetStatus();
}
