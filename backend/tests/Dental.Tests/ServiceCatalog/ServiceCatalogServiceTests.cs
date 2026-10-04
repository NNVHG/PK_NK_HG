using Dental.Application.Common;
using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Features.ServiceCatalog.Services;
using Dental.Application.Features.ServiceCatalog.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.ServiceCatalog;

public sealed class ServiceCatalogServiceTests
{
    private readonly IServiceCatalogRepository _repository = Substitute.For<IServiceCatalogRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private ServiceCatalogService CreateService() => new(_repository, _unitOfWork, _auditLogger);

    [Fact]
    public async Task CreateAsync_ValidRequestCreatesServiceAndInitialPrice_AuditsOnlyFieldNamesAndCode()
    {
        DentalService? savedService = null;
        _repository.CodeExistsAsync("CLEANING", Arg.Any<CancellationToken>()).Returns(false);
        _repository.AddServiceWithInitialPriceAsync(
                Arg.Any<DentalService>(), Arg.Any<ServicePrice>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                savedService = call.Arg<DentalService>();
                savedService.DentalServiceId = 14;
                var price = call.Arg<ServicePrice>();
                price.ServicePriceId = 31;
                savedService.Prices = [price];
                return true;
            });
        _repository.GetByIdAsync(14, true, Arg.Any<CancellationToken>())
            .Returns(call => savedService);

        var result = await CreateService().CreateAsync(
            7,
            new CreateDentalServiceRequest(" cleaning ", "Vệ sinh răng", "Mô tả", 30, 250000, DateTimeOffset.UtcNow.AddDays(-1)),
            "127.0.0.1");

        Assert.True(result.IsSuccess, result.Error.ToString());
        Assert.Equal("CLEANING", result.Value!.Code);
        Assert.Equal(250000, result.Value.CurrentPrice);
        Assert.Single(result.Value.PriceHistory!);
        await _repository.Received(1).AddServiceWithInitialPriceAsync(
            Arg.Is<DentalService>(service => service.Code == "CLEANING" && service.IsActive),
            Arg.Is<ServicePrice>(price => price.Amount == 250000 && price.CreatedByUserId == 7),
            Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.Create,
            entityType: nameof(DentalService),
            entityId: 14,
            userId: 7,
            detail: Arg.Is<string?>(detail =>
                detail != null && detail.Contains("CLEANING") && detail.Contains("DurationMinutes") &&
                detail.Contains("EffectiveFrom") && !detail.Contains("Vệ sinh răng") && !detail.Contains("250000")),
            ipAddress: "127.0.0.1",
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ReturnsConflictWithoutSaving()
    {
        _repository.CodeExistsAsync("CLEANING", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateService().CreateAsync(
            7,
            new CreateDentalServiceRequest("cleaning", "Vệ sinh răng", null, 30, 250000, DateTimeOffset.UtcNow),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.ServiceCodeExists, result.Error);
        await _repository.DidNotReceive().AddServiceWithInitialPriceAsync(
            Arg.Any<DentalService>(), Arg.Any<ServicePrice>(), Arg.Any<CancellationToken>());
        await _auditLogger.DidNotReceive().LogAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_CodeBecomesDuplicateBeforeInsert_ReturnsConflict()
    {
        _repository.CodeExistsAsync("CLEANING", Arg.Any<CancellationToken>()).Returns(false);
        _repository.AddServiceWithInitialPriceAsync(
                Arg.Any<DentalService>(), Arg.Any<ServicePrice>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateService().CreateAsync(
            7,
            new CreateDentalServiceRequest("cleaning", "Vệ sinh răng", null, 30, 250000, DateTimeOffset.UtcNow),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.ServiceCodeExists, result.Error);
        await _auditLogger.DidNotReceive().LogAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CreateDentalServiceRequestValidator_RejectsInvalidDurationAndNegativePrice()
    {
        var validator = new CreateDentalServiceRequestValidator();
        var result = validator.Validate(new CreateDentalServiceRequest(
            "CLEANING", "Vệ sinh răng", null, 0, -1, DateTimeOffset.UtcNow));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateDentalServiceRequest.DurationMinutes));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateDentalServiceRequest.InitialPrice));
    }

    [Fact]
    public void ServiceIdRequestValidator_RejectsNonPositiveRouteId()
    {
        var validator = new ServiceIdRequestValidator();

        Assert.False(validator.Validate(new ServiceIdRequest(0)).IsValid);
        Assert.True(validator.Validate(new ServiceIdRequest(1)).IsValid);
    }

    [Fact]
    public async Task UpdateAsync_ChangesEditableFieldsAndKeepsCodeImmutable()
    {
        var service = MakeService("CLEANING");
        _repository.GetByIdForUpdateAsync(14, Arg.Any<CancellationToken>()).Returns(service);
        _repository.GetByIdAsync(14, true, Arg.Any<CancellationToken>()).Returns(service);

        var result = await CreateService().UpdateAsync(
            7, 14, new UpdateDentalServiceRequest("Vệ sinh răng cập nhật", "Thông tin", 35), null);

        Assert.True(result.IsSuccess, result.Error.ToString());
        Assert.Equal("CLEANING", service.Code);
        Assert.Equal("Vệ sinh răng cập nhật", service.Name);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.Update,
            entityType: nameof(DentalService),
            entityId: 14,
            userId: 7,
            detail: Arg.Is<string?>(detail => detail != null && detail.Contains("CLEANING") &&
                detail.Contains("Name") && detail.Contains("DurationMinutes") && !detail.Contains("Vệ sinh răng cập nhật")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPriceAsync_AppendsPriceAndUsesNewestEffectivePrice()
    {
        var service = MakeService("CLEANING");
        service.Prices.Add(new ServicePrice
        {
            ServicePriceId = 2,
            ServiceId = 14,
            Amount = 200000,
            EffectiveFrom = DateTime.UtcNow.AddDays(-10),
            CreatedByUserId = 7,
        });
        _repository.GetByIdAsync(14, false, Arg.Any<CancellationToken>()).Returns(service);
        _repository.GetByIdAsync(14, true, Arg.Any<CancellationToken>()).Returns(service);
        _repository.GetLatestPriceEffectiveFromAsync(14, Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.AddDays(-10));
        _repository.AddPriceAsync(Arg.Any<ServicePrice>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var price = call.Arg<ServicePrice>();
                price.ServicePriceId = 3;
                service.Prices.Add(price);
                return Task.CompletedTask;
            });

        var result = await CreateService().AddPriceAsync(
            8, 14, new CreateServicePriceRequest(275000, DateTimeOffset.UtcNow.AddDays(-1)), null);

        Assert.True(result.IsSuccess, result.Error.ToString());
        Assert.Equal(2, service.Prices.Count);
        Assert.Equal(275000, result.Value!.Amount);
        Assert.Equal(275000, (await CreateService().GetActiveServiceWithCurrentPriceAsync(14)).Value!.CurrentPrice);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            action: AuditActions.ServicePriceCreated,
            entityType: nameof(DentalService),
            entityId: 14,
            userId: 8,
            detail: Arg.Is<string?>(detail => detail != null && detail.Contains("CLEANING") &&
                detail.Contains("add_service_price") && detail.Contains("Amount") && !detail.Contains("275000")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddPriceAsync_EffectiveFromBeforeLatest_ReturnsConflict()
    {
        var service = MakeService("CLEANING");
        var latest = DateTime.UtcNow.AddDays(-1);
        _repository.GetByIdAsync(14, false, Arg.Any<CancellationToken>()).Returns(service);
        _repository.GetLatestPriceEffectiveFromAsync(14, Arg.Any<CancellationToken>()).Returns(latest);

        var result = await CreateService().AddPriceAsync(
            7, 14, new CreateServicePriceRequest(100000, DateTimeOffset.UtcNow.AddDays(-2)), null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.ServicePriceEffectiveDateInvalid, result.Error);
        await _repository.DidNotReceive().AddPriceAsync(Arg.Any<ServicePrice>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_FuturePriceIsNotCurrent_AndHistoryIsAdminOnlyByArgument()
    {
        var service = MakeService("CLEANING");
        service.Prices =
        [
            new ServicePrice { ServicePriceId = 1, Amount = 200000, EffectiveFrom = DateTime.UtcNow.AddDays(-1) },
            new ServicePrice { ServicePriceId = 2, Amount = 300000, EffectiveFrom = DateTime.UtcNow.AddDays(1) },
        ];
        _repository.GetByIdAsync(14, true, Arg.Any<CancellationToken>()).Returns(service);

        var adminResult = await CreateService().GetByIdAsync(14, includePriceHistory: true);
        var staffResult = await CreateService().GetByIdAsync(14, includePriceHistory: false);

        Assert.Equal(200000, adminResult.Value!.CurrentPrice);
        Assert.Equal(2, adminResult.Value.PriceHistory!.Count);
        Assert.Null(staffResult.Value!.PriceHistory);
    }

    [Theory]
    [InlineData(false, "SERVICE_DEACTIVATED")]
    [InlineData(true, "SERVICE_ACTIVATED")]
    public async Task SetActiveAsync_AuditsStateTransition(bool targetState, string expectedAction)
    {
        var service = MakeService("CLEANING", isActive: !targetState);
        _repository.GetByIdForUpdateAsync(14, Arg.Any<CancellationToken>()).Returns(service);
        _repository.GetByIdAsync(14, true, Arg.Any<CancellationToken>()).Returns(service);

        var result = await CreateService().SetActiveAsync(7, 14, targetState, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetState, service.IsActive);
        await _auditLogger.Received(1).LogAsync(
            action: expectedAction,
            entityType: nameof(DentalService),
            entityId: 14,
            userId: 7,
            detail: Arg.Is<string?>(detail => detail != null && detail.Contains("CLEANING") &&
                detail.Contains("IsActive") && !detail.Contains("Vệ sinh răng")),
            ipAddress: Arg.Any<string?>(),
            ct: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActiveServiceWithCurrentPriceAsync_RejectsInactiveService()
    {
        _repository.GetByIdAsync(14, true, Arg.Any<CancellationToken>()).Returns(MakeService("CLEANING", false));

        var result = await CreateService().GetActiveServiceWithCurrentPriceAsync(14);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.ServiceInactive, result.Error);
    }

    [Fact]
    public async Task GetActiveServiceWithCurrentPriceAsync_RejectsServiceWithoutCurrentPrice()
    {
        var service = MakeService("CLEANING");
        service.Prices.Add(new ServicePrice
        {
            ServicePriceId = 1,
            Amount = 300000,
            EffectiveFrom = DateTime.UtcNow.AddDays(1),
        });
        _repository.GetByIdAsync(14, true, Arg.Any<CancellationToken>()).Returns(service);

        var result = await CreateService().GetActiveServiceWithCurrentPriceAsync(14);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.ServicePriceUnavailable, result.Error);
    }

    private static DentalService MakeService(string code, bool isActive = true)
        => new()
        {
            DentalServiceId = 14,
            Code = code,
            Name = "Vệ sinh răng",
            DurationMinutes = 30,
            IsActive = isActive,
        };
}
