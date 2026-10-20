using Dental.Application.Common;
using Dental.Application.Features.MOD_INV;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_INV;

public sealed class StockReceiptTests
{
    private readonly IStockReceiptRepository repo = Substitute.For<IStockReceiptRepository>();
    private static StockReceiptRequest Input() => new(Guid.NewGuid(), true, "Nhà cung cấp", null, [new(1, 5, 1)]);
    [Theory]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Patient)]
    [InlineData(null)]
    public async Task NonWritersCannotConfirmOrReadReceipts(string? role)
    {
        var service = new StockReceiptService(repo);
        Assert.Equal(Error.Forbidden, (await service.ConfirmAsync(Input(), 1, role)).Error);
        Assert.Equal(Error.Forbidden, (await service.ListAsync(0, 1, role)).Error);
        Assert.Equal(Error.Forbidden, (await service.GetAsync(1, 1, role)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory]
    [InlineData(RoleCodes.Patient)]
    [InlineData(RoleCodes.Receptionist)]
    public async Task NonReadersCannotReadStock(string role) => Assert.Equal(Error.Forbidden, (await new StockReceiptService(repo).StockAsync("", 1, 1, role)).Error);
    [Fact]
    public async Task MissingActorDeniedBeforeRepository()
    {
        var s = new StockReceiptService(repo);
        Assert.Equal(Error.Unauthorized, (await s.ConfirmAsync(Input(), null, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Unauthorized, (await s.StockAsync("", 1, 0, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Unauthorized, (await s.ListAsync(0, null, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Unauthorized, (await s.GetAsync(1, 0, RoleCodes.Admin)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveQuantity(int quantity) => Assert.False(new StockReceiptValidator().Validate(Input() with { Lines = [new(1, quantity, 1)] }).IsValid);
    [Fact]
    public void ValidatorBoundsAndConfirmation()
    {
        var v = new StockReceiptValidator(); var r = Input();
        foreach (var bad in new[] { r with { RequestId = Guid.Empty }, r with { Confirmed = false }, r with { SupplierName = "" }, r with { SupplierName = "a\nb" }, r with { SupplierName = new string('x', 201) }, r with { Notes = new string('x', 501) }, r with { Lines = [] }, r with { Lines = null! }, r with { Lines = [null!] }, r with { Lines = [new(0, 1, 1)] }, r with { Lines = [new(1, 1, 0)] }, r with { Lines = [new(1, 1, 1), new(1, 2, 1)] }, r with { Lines = Enumerable.Range(1, 51).Select(x => new StockReceiptLineRequest(x, 1, 1)).ToList() } }) Assert.False(v.Validate(bad).IsValid);
        Assert.True(v.Validate(r with { Lines = [new(1, int.MaxValue, 1)] }).IsValid);
    }
    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Assistant)]
    public async Task WriterNormalizesAndKeepsIdempotencyKey(string role)
    {
        var r = Input() with { SupplierName = "  NCC  ", Notes = "   " };
        await new StockReceiptService(repo).ConfirmAsync(r, 5, role);
        await repo.Received(1).ConfirmAsync(Arg.Is<StockReceiptRequest>(x => x.RequestId == r.RequestId && x.SupplierName == "NCC" && x.Notes == null && x.Lines[0].Quantity == 5), 5, role, Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task InvalidInputsDoNotReachRepository()
    {
        var s = new StockReceiptService(repo);
        Assert.True((await s.ConfirmAsync(Input() with { Confirmed = false }, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.StockAsync(new string('x', 201), 1, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.StockAsync("", 0, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.StockAsync("", 100001, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.ListAsync(-1, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.GetAsync(0, 1, RoleCodes.Admin)).IsFailure);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Dentist)]
    public async Task StockReadersUseMinimalStockQuery(string role)
    {
        await new StockReceiptService(repo).StockAsync("  Găng  ", 2, 4, role);
        await repo.Received(1).StockAsync("Găng", 2, 4, role, Arg.Any<CancellationToken>());
    }
}
