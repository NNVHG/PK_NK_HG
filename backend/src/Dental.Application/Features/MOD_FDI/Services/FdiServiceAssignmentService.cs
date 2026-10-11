using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Assignment = Dental.Domain.Entities.VisitService;

namespace Dental.Application.Features.MOD_FDI.Services;

public sealed class FdiServiceAssignmentService(IVisitRepository visits, IPatientRepository patients,
    IServiceCatalogRepository catalog, IVisitServiceRepository assignments, IUnitOfWork unitOfWork,
    IAuditLogger audit, AssignServicesRequestValidator validator)
{
    public static readonly Error Closed = new("FDI_041", "Chỉ chỉ định dịch vụ khi lần khám đang diễn ra và chưa khóa.");
    public static readonly Error PriceUnavailable = new("FDI_042", "Dịch vụ không hoạt động hoặc chưa có giá hiệu lực.");
    public static readonly Error AmountTooLarge = new("FDI_043", "Thành tiền vượt giới hạn lưu trữ.");

    public async Task<Result<IReadOnlyList<AssignedServiceResponse>>> GetAsync(int visitId, int userId,
        string? role, CancellationToken ct = default)
    {
        if (role != RoleCodes.Patient && !RoleCodes.StaffRoles.Contains(role))
            return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(Error.Forbidden);
        var visit = await visits.GetByIdAsync(visitId, ct);
        if (visit is null) return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(Error.NotFound);
        if (role == RoleCodes.Patient && (await patients.GetByIdAsync(visit.PatientId, ct))?.UserId != userId)
            return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(Error.NotFound);
        return Result<IReadOnlyList<AssignedServiceResponse>>.Success(
            (await assignments.GetForVisitAsync(visitId, ct)).Select(Map).ToArray());
    }

    public async Task<Result<IReadOnlyList<AssignedServiceResponse>>> AssignAsync(int visitId,
        AssignServicesRequest request, int userId, string? role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist))
            return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(Error.Forbidden);
        if (!(await validator.ValidateAsync(request, ct)).IsValid)
            return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(Error.Validation);

        var result = await unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            var visit = await visits.GetForUpdateAsync(visitId, transactionCt);
            if (visit is null) return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(Error.NotFound);
            if (role == RoleCodes.Dentist && visit.DentistId != userId)
                return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(Error.Forbidden);
            if (visit.Status != VisitStatuses.InProgress || visit.IsLocked)
                return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(Closed);
            var service = await catalog.GetByIdAsync(request.ServiceId, true, transactionCt);
            var now = DateTime.UtcNow;
            var price = service?.Prices.Where(x => x.EffectiveFrom <= now)
                .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.ServicePriceId).FirstOrDefault();
            if (service is null || !service.IsActive || price is null)
                return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(PriceUnavailable);
            const decimal maxMoney = 999999999999999999m;
            if (price.Amount < 0 || price.Amount > maxMoney / request.Quantity)
                return Result<IReadOnlyList<AssignedServiceResponse>>.Failure(AmountTooLarge);
            var teeth = request.ToothNumbers is { Length: > 0 }
                ? request.ToothNumbers.Select(x => (int?)x) : new int?[] { null };
            var rows = teeth.Select(tooth => new Assignment
            {
                VisitId = visitId, ServiceId = service.DentalServiceId, ServicePriceId = price.ServicePriceId,
                ServiceCode = service.Code, ServiceName = service.Name, UnitPrice = price.Amount,
                ToothNumber = tooth, Surface = request.Surface, Quantity = request.Quantity,
                CreatedByUserId = userId, CreatedAt = now
            }).ToArray();
            await assignments.AddRangeAsync(rows, transactionCt);
            await unitOfWork.SaveChangesAsync(transactionCt);
            return Result<IReadOnlyList<AssignedServiceResponse>>.Success(rows.Select(Map).ToArray());
        }, ct);
        if (result.IsSuccess)
            await audit.LogAsync("MOD_FDI_SERVICE_ASSIGNED", "Visit", visitId, userId,
                JsonSerializer.Serialize(new { action = "assign_services", count = result.Value!.Count }), ip, ct);
        return result;
    }

    private static AssignedServiceResponse Map(Assignment row) => new(row.Id, row.VisitId, row.ServiceId,
        row.ServicePriceId, row.ServiceCode, row.ServiceName, row.ToothNumber, row.Surface,
        row.Quantity, row.UnitPrice, row.UnitPrice * row.Quantity, row.CreatedAt);
}
