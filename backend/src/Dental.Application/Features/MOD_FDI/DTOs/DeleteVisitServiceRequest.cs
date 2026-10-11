namespace Dental.Application.Features.MOD_FDI.DTOs;
public sealed record DeleteVisitServiceRequest(int VisitId, int AssignmentId, string Reason);
public sealed record DeleteVisitServiceBody(string Reason);
