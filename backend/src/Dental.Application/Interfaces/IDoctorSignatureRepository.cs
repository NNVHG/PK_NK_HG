using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
namespace Dental.Application.Interfaces;
public interface IDoctorSignatureRepository
{
    Task<Result<DoctorSignatureStatus>> ReadAsync(int doctorId, int actorId, string role, CancellationToken ct);
    Task<Result<byte[]>> FileAsync(int doctorId, int version, int actorId, string role, CancellationToken ct);
    Task<Result<DoctorSignatureStatus>> SaveAsync(int doctorId, int actorId, string role, int expectedVersion, byte[] image, string? ip, CancellationToken ct);
}
