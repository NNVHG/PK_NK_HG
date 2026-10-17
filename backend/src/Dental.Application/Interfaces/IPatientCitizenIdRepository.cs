using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;

namespace Dental.Application.Interfaces;
public interface IPatientCitizenIdRepository
{
    Task<Result<CitizenIdStatus>> ReadAsync(int patientId, int actorId, string role, CancellationToken ct);
    Task<Result<CitizenIdStatus>> SaveAsync(int patientId, int actorId, string role, CitizenIdRequest request, string? ip, CancellationToken ct);
}
