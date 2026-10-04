using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.ServiceCatalog.Services;

public sealed class ServiceCatalogService
{
    private readonly IServiceCatalogRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public ServiceCatalogService(
        IServiceCatalogRepository repository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public Task<PagedResult<ServiceCatalogListItemResponse>> GetPageAsync(
        ServiceCatalogQueryRequest request, CancellationToken ct = default)
        => _repository.GetPageAsync(
            request.Keyword?.Trim(), request.IsActive, request.Page, request.PageSize, DateTime.UtcNow, ct);

    public async Task<Result<ServiceCatalogDetailResponse>> GetByIdAsync(
        int serviceId, bool includePriceHistory, CancellationToken ct = default)
    {
        var service = await _repository.GetByIdAsync(serviceId, includePrices: true, ct);
        if (service is null)
            return Result<ServiceCatalogDetailResponse>.Failure(Error.NotFound);

        var currentPrice = GetCurrentPrice(service.Prices, DateTime.UtcNow);
        var history = includePriceHistory
            ? service.Prices
                .OrderByDescending(price => price.EffectiveFrom)
                .ThenByDescending(price => price.ServicePriceId)
                .Select(ToPriceResponse)
                .ToList()
            : null;

        return Result<ServiceCatalogDetailResponse>.Success(new ServiceCatalogDetailResponse(
            service.DentalServiceId,
            service.Code,
            service.Name,
            service.Description,
            service.DurationMinutes,
            service.IsActive,
            currentPrice?.Amount,
            currentPrice?.EffectiveFrom,
            history));
    }

    public async Task<Result<ServiceCatalogDetailResponse>> CreateAsync(
        int actorUserId,
        CreateDentalServiceRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repository.CodeExistsAsync(code, ct))
            return Result<ServiceCatalogDetailResponse>.Failure(Error.ServiceCodeExists);

        var now = DateTime.UtcNow;
        var service = new DentalService
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            DurationMinutes = request.DurationMinutes,
            IsActive = true,
            CreatedAt = now,
        };
        var price = new ServicePrice
        {
            Service = service,
            Amount = request.InitialPrice,
            EffectiveFrom = request.EffectiveFrom.UtcDateTime,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
        };

        if (!await _repository.AddServiceWithInitialPriceAsync(service, price, ct))
            return Result<ServiceCatalogDetailResponse>.Failure(Error.ServiceCodeExists);

        await WriteAuditAsync(
            AuditActions.Create,
            service,
            actorUserId,
            [nameof(DentalService.Code), nameof(DentalService.Name), nameof(DentalService.Description), nameof(DentalService.DurationMinutes), nameof(DentalService.IsActive), nameof(ServicePrice.Amount), nameof(ServicePrice.EffectiveFrom)],
            "create_service",
            ipAddress,
            ct);

        service.Prices = [price];
        return await GetByIdAsync(service.DentalServiceId, includePriceHistory: true, ct);
    }

    public async Task<Result<ServiceCatalogDetailResponse>> UpdateAsync(
        int actorUserId,
        int serviceId,
        UpdateDentalServiceRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var service = await _repository.GetByIdForUpdateAsync(serviceId, ct);
        if (service is null)
            return Result<ServiceCatalogDetailResponse>.Failure(Error.NotFound);

        var changedFields = new List<string>();
        SetIfChanged(service.Name, request.Name.Trim(), value => service.Name = value, nameof(DentalService.Name), changedFields);
        SetIfChanged(service.Description, NormalizeOptional(request.Description), value => service.Description = value, nameof(DentalService.Description), changedFields);
        SetIfChanged(service.DurationMinutes, request.DurationMinutes, value => service.DurationMinutes = value, nameof(DentalService.DurationMinutes), changedFields);

        if (changedFields.Count > 0)
        {
            service.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
            await WriteAuditAsync(AuditActions.Update, service, actorUserId, changedFields, "update_service", ipAddress, ct);
        }

        return await GetByIdAsync(serviceId, includePriceHistory: false, ct);
    }

    public async Task<Result<ServicePriceResponse>> AddPriceAsync(
        int actorUserId,
        int serviceId,
        CreateServicePriceRequest request,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var service = await _repository.GetByIdAsync(serviceId, includePrices: false, ct);
        if (service is null)
            return Result<ServicePriceResponse>.Failure(Error.NotFound);

        var latestEffectiveFrom = await _repository.GetLatestPriceEffectiveFromAsync(serviceId, ct);
        // [CẦN XÁC NHẬN] Tạm không cho lùi ngày áp dụng trước giá mới nhất; tài liệu chưa quy định giới hạn lùi ngày.
        if (latestEffectiveFrom is DateTime latest && request.EffectiveFrom.UtcDateTime < latest)
            return Result<ServicePriceResponse>.Failure(Error.ServicePriceEffectiveDateInvalid);

        var price = new ServicePrice
        {
            ServiceId = serviceId,
            Amount = request.Amount,
            EffectiveFrom = request.EffectiveFrom.UtcDateTime,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
        };
        await _repository.AddPriceAsync(price, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await WriteAuditAsync(
            AuditActions.ServicePriceCreated,
            service,
            actorUserId,
            [nameof(ServicePrice.Amount), nameof(ServicePrice.EffectiveFrom)],
            "add_service_price",
            ipAddress,
            ct);

        return Result<ServicePriceResponse>.Success(ToPriceResponse(price));
    }

    public async Task<Result<ServiceCatalogDetailResponse>> SetActiveAsync(
        int actorUserId,
        int serviceId,
        bool isActive,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var service = await _repository.GetByIdForUpdateAsync(serviceId, ct);
        if (service is null)
            return Result<ServiceCatalogDetailResponse>.Failure(Error.NotFound);

        if (service.IsActive != isActive)
        {
            service.IsActive = isActive;
            service.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
            await WriteAuditAsync(
                isActive ? AuditActions.ServiceActivated : AuditActions.ServiceDeactivated,
                service,
                actorUserId,
                [nameof(DentalService.IsActive)],
                isActive ? "activate_service" : "deactivate_service",
                ipAddress,
                ct);
        }

        return await GetByIdAsync(serviceId, includePriceHistory: false, ct);
    }

    /// <summary>Dùng bởi các luồng nghiệp vụ để từ chối dịch vụ ngừng bán hoặc chưa có giá hiện hành.</summary>
    public async Task<Result<ActiveServiceWithCurrentPriceResponse>> GetActiveServiceWithCurrentPriceAsync(
        int serviceId, CancellationToken ct = default)
    {
        var service = await _repository.GetByIdAsync(serviceId, includePrices: true, ct);
        if (service is null)
            return Result<ActiveServiceWithCurrentPriceResponse>.Failure(Error.NotFound);
        if (!service.IsActive)
            return Result<ActiveServiceWithCurrentPriceResponse>.Failure(Error.ServiceInactive);

        var price = GetCurrentPrice(service.Prices, DateTime.UtcNow);
        if (price is null)
            return Result<ActiveServiceWithCurrentPriceResponse>.Failure(Error.ServicePriceUnavailable);

        return Result<ActiveServiceWithCurrentPriceResponse>.Success(new ActiveServiceWithCurrentPriceResponse(
            service.DentalServiceId, service.Code, service.Name, service.DurationMinutes, price.Amount, price.EffectiveFrom));
    }

    private async Task WriteAuditAsync(
        string action,
        DentalService service,
        int actorUserId,
        IReadOnlyList<string> changedFields,
        string operation,
        string? ipAddress,
        CancellationToken ct)
    {
        await _auditLogger.LogAsync(
            action: action,
            entityType: nameof(DentalService),
            entityId: service.DentalServiceId,
            userId: actorUserId,
            detail: JsonSerializer.Serialize(new
            {
                serviceCode = service.Code,
                action = operation,
                changedFields,
            }),
            ipAddress: ipAddress,
            ct: ct);
    }

    private static ServicePrice? GetCurrentPrice(IEnumerable<ServicePrice> prices, DateTime nowUtc)
        => prices
            .Where(price => price.EffectiveFrom <= nowUtc)
            .OrderByDescending(price => price.EffectiveFrom)
            .ThenByDescending(price => price.ServicePriceId)
            .FirstOrDefault();

    private static ServicePriceResponse ToPriceResponse(ServicePrice price)
        => new(price.ServicePriceId, price.Amount, price.EffectiveFrom, price.CreatedByUserId, price.CreatedAt);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void SetIfChanged<T>(T current, T next, Action<T> set, string field, ICollection<string> changedFields)
    {
        if (!EqualityComparer<T>.Default.Equals(current, next))
        {
            set(next);
            changedFields.Add(field);
        }
    }
}
