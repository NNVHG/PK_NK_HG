using Dental.Application.Common;
using Dental.Application.Features.MOD_INV;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_INV;

public sealed class StockIssueTests
{
    private readonly IStockIssueRepository repo = Substitute.For<IStockIssueRepository>();
    private static StockIssueRequest Input() => new(Guid.NewGuid(), true, 7, null, [new(1, 5, 1)]);
    [Theory]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Patient)]
    [InlineData(null)]
    public async Task NonWritersCannotConfirmOrRead(string? role)
    {
        var s = new StockIssueService(repo);
        Assert.Equal(Error.Forbidden, (await s.ConfirmAsync(Input(), 1, role)).Error);
        Assert.Equal(Error.Forbidden, (await s.TargetsAsync(null, 0, 1, role)).Error);
        Assert.Equal(Error.Forbidden, (await s.ListAsync(7, 0, 1, role)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Fact]
    public async Task MissingActorDeniedBeforeRepository()
    {
        var s = new StockIssueService(repo);
        Assert.Equal(Error.Unauthorized, (await s.ConfirmAsync(Input(), null, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Unauthorized, (await s.TargetsAsync(null, 0, 0, RoleCodes.Admin)).Error);
        Assert.Equal(Error.Unauthorized, (await s.ListAsync(7, 0, null, RoleCodes.Admin)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveQuantity(int qty) => Assert.False(new StockIssueValidator().Validate(Input() with { Lines = [new(1, qty, 1)] }).IsValid);
    [Fact]
    public void ValidatorBoundsAndConfirmation()
    {
        var v = new StockIssueValidator(); var r = Input();
        foreach (var bad in new[] { r with { RequestId = Guid.Empty }, r with { Confirmed = false }, r with { VisitId = 0 }, r with { Notes = new string('x', 501) }, r with { Lines = [] }, r with { Lines = null! }, r with { Lines = [null!] }, r with { Lines = [new(0, 1, 1)] }, r with { Lines = [new(1, 1, 0)] }, r with { Lines = [new(1, 1, 1), new(1, 2, 1)] }, r with { Lines = Enumerable.Range(1, 51).Select(x => new StockIssueLineRequest(x, 1, 1)).ToList() } }) Assert.False(v.Validate(bad).IsValid);
        Assert.True(v.Validate(r with { Lines = [new(1, int.MaxValue, 1)] }).IsValid);
    }
    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Assistant)]
    public async Task WriterNormalizesNotesAndPreservesKeyAndVisit(string role)
    {
        var r = Input() with { Notes = "  Thực tế  " };
        await new StockIssueService(repo).ConfirmAsync(r, 5, role);
        await repo.Received(1).ConfirmAsync(Arg.Is<StockIssueRequest>(x => x.RequestId == r.RequestId && x.VisitId == 7 && x.Notes == "Thực tế" && x.Lines[0].Quantity == 5), 5, role, Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task InvalidQueriesDoNotRead()
    {
        var s = new StockIssueService(repo);
        Assert.True((await s.TargetsAsync(0, 0, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.TargetsAsync(null, -1, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.ListAsync(0, 0, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.ListAsync(1, -1, 1, RoleCodes.Admin)).IsFailure);
        Assert.True((await s.ConfirmAsync(Input() with { Confirmed = false }, 1, RoleCodes.Admin)).IsFailure);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Fact]
    public async Task BlankNotesBecomeNull() { await new StockIssueService(repo).ConfirmAsync(Input() with { Notes = "  " }, 1, RoleCodes.Admin); await repo.Received().ConfirmAsync(Arg.Is<StockIssueRequest>(r => r.Notes == null), 1, RoleCodes.Admin, Arg.Any<CancellationToken>()); }
}
