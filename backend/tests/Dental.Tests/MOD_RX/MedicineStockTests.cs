using Dental.Application.Common;
using Dental.Application.Features.MOD_RX;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_RX;

public sealed class MedicineStockTests
{
    private readonly IMedicineStockRepository repository = Substitute.For<IMedicineStockRepository>();
    private MedicineStockService Service() => new(repository);
    private static MedicineStockReceiptRequest Request() => new(Guid.NewGuid(), 20, 1, "Supplier", null, true);
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task OnlyAdminCanReadReceiptsOrReceive(string? role)
    {
        Assert.Equal(Error.Forbidden, (await Service().ReadAsync(7, 0, 99, role)).Error);
        Assert.Equal(Error.Forbidden, (await Service().ReceiveAsync(7, Request(), 99, role)).Error); Assert.Empty(repository.ReceivedCalls());
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(null)]
    public async Task MissingActorNeverCallsRepository(int? actor)
    {
        Assert.Equal(Error.Forbidden, (await Service().ReadAsync(7, 0, actor, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Forbidden, (await Service().ReceiveAsync(7, Request(), actor, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Forbidden, (await Service().AvailabilityAsync(7, [1], actor, RoleCodes.Dentist)).Error); Assert.Empty(repository.ReceivedCalls());
    }
    [Fact] public async Task AdminReceiptIsNormalizedAndUsesExactRequestIdForRetry()
    {
        var input = Request() with { SupplierName = " Supplier ", Notes = "  " };
        await Service().ReceiveAsync(7, input, 99, RoleCodes.Admin);
        await repository.Received(1).ReceiveAsync(7, Arg.Is<MedicineStockReceiptRequest>(r => r.RequestId == input.RequestId && r.SupplierName == "Supplier" && r.Notes == null && r.Quantity == 20 && r.Confirmed), 99, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task QuantityMustBePositiveInteger(int quantity)
    {
        var request = Request() with { Quantity = quantity };
        Assert.False(new MedicineStockReceiptValidator().Validate(request).IsValid);
        Assert.Equal(Error.Validation, (await Service().ReceiveAsync(7, request, 99, RoleCodes.Admin)).Error); Assert.Empty(repository.ReceivedCalls());
    }
    [Fact] public async Task ConfirmationRequestVersionAndTextLimitsAreRequired()
    {
        foreach (var input in new[] { Request() with { RequestId = Guid.Empty }, Request() with { Confirmed = false }, Request() with { ExpectedMedicineVersion = 0 },
            Request() with { SupplierName = " " }, Request() with { SupplierName = new string('x', 201) }, Request() with { SupplierName = "Bad\nName" }, Request() with { Notes = new string('x', 501) } })
            Assert.Equal(Error.Validation, (await Service().ReceiveAsync(7, input, 99, RoleCodes.Admin)).Error);
        Assert.Empty(repository.ReceivedCalls());
        Assert.True(new MedicineStockReceiptValidator().Validate(Request() with { Quantity = int.MaxValue, SupplierName = new string('x', 200), Notes = new string('x', 500) }).IsValid);
    }
    [Fact] public async Task ReadAndReceiptIdentifiersAreBounded()
    {
        Assert.Equal(Error.Validation, (await Service().ReadAsync(0, 0, 99, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Validation, (await Service().ReadAsync(7, -1, 99, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Validation, (await Service().ReceiveAsync(0, Request(), 99, RoleCodes.Admin)).Error); Assert.Empty(repository.ReceivedCalls());
    }
    [Fact] public async Task AdminReadsActualRepositoryWithoutMutation()
    {
        var detail = new MedicineStockDetail(7, "M", "Medicine", "Viên", 1, true, 0, [], false);
        repository.ReadAsync(7, 0, 99, Arg.Any<CancellationToken>()).Returns(Result<MedicineStockDetail>.Success(detail));
        Assert.Same(detail, (await Service().ReadAsync(7, 0, 99, RoleCodes.Admin)).Value);
        await repository.DidNotReceive().ReceiveAsync(Arg.Any<int>(), Arg.Any<MedicineStockReceiptRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task DraftStockLookupOnlyDentistRole(string? role)
    {
        Assert.Equal(Error.Forbidden, (await Service().AvailabilityAsync(7, [1], 99, role)).Error); Assert.Empty(repository.ReceivedCalls());
    }
    [Fact] public async Task DraftStockQueryBoundsAndUniqueness()
    {
        foreach (var ids in new[] { Array.Empty<int>(), new[] { 1, 1 }, new[] { 0 }, Enumerable.Range(1, 11).ToArray() })
            Assert.Equal(Error.Validation, (await Service().AvailabilityAsync(7, ids, 99, RoleCodes.Dentist)).Error);
        Assert.Equal(Error.Validation, (await Service().AvailabilityAsync(0, [1], 99, RoleCodes.Dentist)).Error); Assert.Empty(repository.ReceivedCalls());
    }
    [Fact] public async Task LookupForDraftDelegatesOnlyReadAndDoesNotReserve()
    {
        await Service().AvailabilityAsync(7, [1, 2], 99, RoleCodes.Dentist);
        await repository.Received(1).AvailabilityAsync(7, Arg.Is<int[]>(ids => ids.SequenceEqual(new[] { 1, 2 })), 99, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().ReceiveAsync(Arg.Any<int>(), Arg.Any<MedicineStockReceiptRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
