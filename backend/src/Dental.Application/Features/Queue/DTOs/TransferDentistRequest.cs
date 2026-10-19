namespace Dental.Application.Features.Queue.DTOs;

public sealed record TransferDentistRequest(int TargetDentistId, bool Confirmed, string? Reason = null,
    int ExpectedDentistId = 0, int ExpectedStatus = 0);
