namespace Dental.Application.Features.Queue.DTOs;

// MOD_CHK: read-only location of the destination dentist, never of the calling staff member.
public sealed record QueueDentistLocation(int DentistId, string ChairNumber, string RoomName);
