using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Application.Features.MOD_FDI.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;
using Assignment = Dental.Domain.Entities.VisitService;

namespace Dental.Tests.MOD_FDI;

public sealed class ServiceAssignmentTests
{
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IServiceCatalogRepository catalog = Substitute.For<IServiceCatalogRepository>();
    private readonly IVisitServiceRepository assignments = Substitute.For<IVisitServiceRepository>();
    private readonly IUnitOfWork unit = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private FdiServiceAssignmentService Service => new(visits, patients, catalog, assignments, unit, audit, new());

    public ServiceAssignmentTests()
    {
        unit.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<IReadOnlyList<AssignedServiceResponse>>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result<IReadOnlyList<AssignedServiceResponse>>>>>(0)(call.ArgAt<CancellationToken>(1)));
    }

    private void Ready(bool active = true)
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 1, Status = "InProgress", DentistId = 2 });
        catalog.GetByIdAsync(3, true, Arg.Any<CancellationToken>()).Returns(new DentalService
        {
            DentalServiceId = 3, Code = "SVC", Name = "Test service", IsActive = active,
            Prices = new List<ServicePrice>
            {
                new() { ServicePriceId = 4, Amount = 100000, EffectiveFrom = DateTime.UtcNow.AddDays(-2) },
                new() { ServicePriceId = 5, Amount = 120000, EffectiveFrom = DateTime.UtcNow.AddDays(-1) },
                new() { ServicePriceId = 6, Amount = 999000, EffectiveFrom = DateTime.UtcNow.AddDays(1) }
            }
        });
    }

    [Fact]
    public void Validator_AcceptsExactly52Teeth()
    {
        var valid = Enumerable.Range(1, 4).SelectMany(q => Enumerable.Range(1, 8).Select(t => q * 10 + t))
            .Concat(Enumerable.Range(5, 4).SelectMany(q => Enumerable.Range(1, 5).Select(t => q * 10 + t))).ToHashSet();
        var validator = new AssignServicesRequestValidator();
        for (var tooth = -10; tooth <= 100; tooth++)
            Assert.Equal(valid.Contains(tooth), validator.Validate(new AssignServicesRequest(3, [tooth], null)).IsValid);
        Assert.True(validator.Validate(new AssignServicesRequest(3, valid.ToArray(), null)).IsValid);
    }

    [Theory]
    [InlineData(null, true)] [InlineData("B", true)] [InlineData("L", true)] [InlineData("M", true)]
    [InlineData("D", true)] [InlineData("O", true)] [InlineData("MOD", false)] [InlineData("All", false)]
    [InlineData("", false)] [InlineData("buccal", false)]
    public void Validator_UsesSingleSurfaceForServices(string? surface, bool valid)
        => Assert.Equal(valid, new AssignServicesRequestValidator().Validate(new AssignServicesRequest(3, [11], surface)).IsValid);

    [Fact]
    public void Validator_RejectsDuplicatesWholeJawSurfaceAndInvalidQuantity()
    {
        var validator = new AssignServicesRequestValidator();
        Assert.False(validator.Validate(new AssignServicesRequest(3, [11, 11], null)).IsValid);
        Assert.False(validator.Validate(new AssignServicesRequest(3, [], "B")).IsValid);
        Assert.False(validator.Validate(new AssignServicesRequest(0, [11], null)).IsValid);
        Assert.False(validator.Validate(new AssignServicesRequest(3, [11], null, 0)).IsValid);
        Assert.False(validator.Validate(new AssignServicesRequest(3, [11], null, 101)).IsValid);
        Assert.True(validator.Validate(new AssignServicesRequest(3, null, null, 100)).IsValid);
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)]
    [InlineData(null)] [InlineData("UNKNOWN")]
    public async Task Assign_RejectsRolesBeforeTransaction(string? role)
    {
        Assert.Equal(Error.Forbidden, (await Service.AssignAsync(1, new(3, [11], null), 2, role, null)).Error);
        await visits.DidNotReceiveWithAnyArgs().GetForUpdateAsync(default, default);
    }

    [Theory]
    [InlineData("Created", false)] [InlineData("Completed", false)]
    [InlineData("Cancelled", false)] [InlineData("InProgress", true)]
    public async Task Assign_RejectsClosedLockedVisits(string status, bool locked)
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { DentistId = 2, Status = status, IsLocked = locked });
        Assert.Equal(FdiServiceAssignmentService.Closed, (await Service.AssignAsync(1, new(3, [11], null), 2, RoleCodes.Dentist, null)).Error);
        await assignments.DidNotReceiveWithAnyArgs().AddRangeAsync(default!, default);
    }

    [Fact]
    public async Task Assign_RejectsMissingVisitWrongDentistAndInvalidInput()
    {
        Assert.Equal(Error.NotFound, (await Service.AssignAsync(1, new(3, [11], null), 2, RoleCodes.Admin, null)).Error);
        Ready();
        Assert.Equal(Error.Forbidden, (await Service.AssignAsync(1, new(3, [11], null), 99, RoleCodes.Dentist, null)).Error);
        Assert.Equal(Error.Validation, (await Service.AssignAsync(1, new(3, [19], null), 2, RoleCodes.Admin, null)).Error);
        await unit.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Assign_RejectsInactiveMissingOrFutureOnlyPrice()
    {
        Ready(false);
        Assert.Equal(FdiServiceAssignmentService.PriceUnavailable, (await Service.AssignAsync(1, new(3, [11], null), 2, RoleCodes.Admin, null)).Error);
        catalog.GetByIdAsync(3, true, Arg.Any<CancellationToken>()).Returns(new DentalService { IsActive = true });
        Assert.Equal(FdiServiceAssignmentService.PriceUnavailable, (await Service.AssignAsync(1, new(3, [11], null), 2, RoleCodes.Admin, null)).Error);
        catalog.GetByIdAsync(3, true, Arg.Any<CancellationToken>()).Returns(new DentalService { IsActive = true,
            Prices = [new ServicePrice { Amount = 1, EffectiveFrom = DateTime.UtcNow.AddDays(1) }] });
        Assert.Equal(FdiServiceAssignmentService.PriceUnavailable, (await Service.AssignAsync(1, new(3, [11], null), 2, RoleCodes.Admin, null)).Error);
        await unit.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)]
    public async Task Assign_BatchUsesOneTransactionAndCurrentSnapshot(string role)
    {
        Ready();
        var result = await Service.AssignAsync(1, new(3, [16, 17, 51], "O", 2), 2, role, null);
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
        Assert.All(result.Value, row => { Assert.Equal(120000, row.UnitPrice); Assert.Equal(240000, row.TotalAmount); Assert.Equal(5, row.ServicePriceId); Assert.Equal("O", row.Surface); });
        Assert.Equal(new int?[] { 16, 17, 51 }, result.Value.Select(x => x.ToothNumber));
        await assignments.Received(1).AddRangeAsync(Arg.Is<IReadOnlyList<Assignment>>(x => x.Count == 3 && x.All(y => y.VisitId == 1 && y.CreatedByUserId == 2)), Arg.Any<CancellationToken>());
        await unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await unit.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<IReadOnlyList<AssignedServiceResponse>>>>>(), Arg.Any<CancellationToken>());
        await audit.Received(1).LogAsync("MOD_FDI_SERVICE_ASSIGNED", "Visit", 1, 2,
            "{\"action\":\"assign_services\",\"count\":3}", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assign_WholeJawHasNullToothAndRetainsSnapshotAfterCatalogChanges()
    {
        Ready();
        var result = await Service.AssignAsync(1, new(3, null, null), 2, RoleCodes.Admin, null);
        var original = await catalog.GetByIdAsync(3, true);
        original!.Name = "Changed"; original.Prices.Single(x => x.ServicePriceId == 5).Amount = 300000;
        var row = Assert.Single(result.Value!);
        Assert.Null(row.ToothNumber); Assert.Null(row.Surface); Assert.Equal(120000, row.UnitPrice); Assert.Equal("Test service", row.ServiceName);
    }

    [Fact]
    public async Task Assign_SaveFailureDoesNotAuditSuccess()
    {
        Ready();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<int>(new InvalidOperationException("Test failure")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.AssignAsync(1, new(3, [11, 12], null), 2, RoleCodes.Admin, null));
        await audit.DidNotReceiveWithAnyArgs().LogAsync(default!, default!, default, default, default, default, default);
    }

    [Fact]
    public async Task Assign_TiedEffectiveDateUsesNewestPriceId()
    {
        Ready();
        var service = await catalog.GetByIdAsync(3, true);
        service!.Prices.Add(new ServicePrice { ServicePriceId = 7, Amount = 125000,
            EffectiveFrom = service.Prices.Single(x => x.ServicePriceId == 5).EffectiveFrom });
        var row = Assert.Single((await Service.AssignAsync(1, new(3, [11], null), 2, RoleCodes.Admin, null)).Value!);
        Assert.Equal(7, row.ServicePriceId); Assert.Equal(125000, row.UnitPrice);
    }

    [Fact]
    public async Task Assign_RejectsAmountsOutsideStorageLimits()
    {
        Ready();
        var service = await catalog.GetByIdAsync(3, true);
        var price = service!.Prices.Single(x => x.ServicePriceId == 5);
        price.Amount = 999999999999999999m;
        Assert.Equal(FdiServiceAssignmentService.AmountTooLarge,
            (await Service.AssignAsync(1, new(3, [11], null, 2), 2, RoleCodes.Admin, null)).Error);
        price.Amount = -1;
        Assert.Equal(FdiServiceAssignmentService.AmountTooLarge,
            (await Service.AssignAsync(1, new(3, [11], null), 2, RoleCodes.Admin, null)).Error);
        await assignments.DidNotReceiveWithAnyArgs().AddRangeAsync(default!, default);
    }

    [Fact]
    public async Task Get_EnforcesPatientOwnership()
    {
        visits.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { PatientId = 7 });
        patients.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(new Patient { UserId = 2 });
        Assert.Equal(Error.NotFound, (await Service.GetAsync(1, 99, RoleCodes.Patient)).Error);
        await assignments.DidNotReceiveWithAnyArgs().GetForVisitAsync(default, default);
        assignments.GetForVisitAsync(1, Arg.Any<CancellationToken>()).Returns(new[] { new Assignment { VisitId = 1, UnitPrice = 120000, Quantity = 1 } });
        Assert.Single((await Service.GetAsync(1, 2, RoleCodes.Patient)).Value!);
    }

    [Theory]
    [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)]
    public async Task Get_AllowsStaff(string role)
    {
        visits.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit());
        assignments.GetForVisitAsync(1, Arg.Any<CancellationToken>()).Returns(Array.Empty<Assignment>());
        Assert.True((await Service.GetAsync(1, 2, role)).IsSuccess);
    }

    [Fact]
    public async Task Get_RejectsUnknownRoleAndMissingVisit()
    {
        Assert.Equal(Error.Forbidden, (await Service.GetAsync(1, 2, null)).Error);
        Assert.Equal(Error.NotFound, (await Service.GetAsync(1, 2, RoleCodes.Admin)).Error);
    }
}
