using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.Services;
namespace Dental.Application.Interfaces;
public interface IClinicPrintSettingsRepository
{
    Task<Result<ClinicPrintSettings>> ReadAsync(int actor, CancellationToken ct);
    Task<Result<ClinicPrintSettings>> SaveAsync(int actor, ClinicPrintSettingsRequest request, string? ip, CancellationToken ct);
}
