namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed record InvoicePortalProfileResponse(
    int PatientId, string PatientCode, string FullName, DateOnly DateOfBirth, bool IsActive);
