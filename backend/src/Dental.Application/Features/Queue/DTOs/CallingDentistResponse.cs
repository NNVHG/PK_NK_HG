namespace Dental.Application.Features.Queue.DTOs;

public sealed record CallingDentistResponse(int DentistId, string DentistName, string? ChairNumber = null, string? RoomName = null);
