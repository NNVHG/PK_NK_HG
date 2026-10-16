using Dental.Application.Features.MOD_RPT.DTOs;

namespace Dental.Application.Interfaces;

public interface IRevenueReportRepository
{
    Task<RevenueReportData> ReadAsync(RevenueReportFilter filter, CancellationToken ct = default);
}
