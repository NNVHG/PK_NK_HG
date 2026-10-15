using Dental.Application.Common;
using Dental.Application.Features.ServiceCatalog.DTOs;
using Dental.Application.Features.ServiceCatalog.Services;
using Dental.Application.Features.ServiceCatalog.Validators;
using Dental.Application.Interfaces;
using NSubstitute;
using Xunit;

namespace Dental.Tests.ServiceCatalog;

public sealed class ServiceCatalogSearchTests
{
    [Theory] [InlineData(0, false)] [InlineData(1, true)] [InlineData(1000000, true)] [InlineData(1000001, false)] [InlineData(int.MaxValue, false)]
    public void PaginationBoundPreventsOffsetOverflow(int page, bool valid)
        => Assert.Equal(valid, new ServiceCatalogQueryRequestValidator().Validate(new ServiceCatalogQueryRequest { Page = page }).IsValid);
    [Theory] [InlineData(100, true)] [InlineData(101, false)]
    public void KeywordBoundMatchesUI(int length, bool valid)
        => Assert.Equal(valid, new ServiceCatalogQueryRequestValidator().Validate(new ServiceCatalogQueryRequest { Keyword = new string('x', length) }).IsValid);
    [Theory] [InlineData(0, false)] [InlineData(100, true)] [InlineData(101, false)]
    public void PageSizeBound(int size, bool valid)
        => Assert.Equal(valid, new ServiceCatalogQueryRequestValidator().Validate(new ServiceCatalogQueryRequest { PageSize = size }).IsValid);
    [Theory] [InlineData("  TRÁM  ", "TRÁM")] [InlineData("   ", "")]
    public async Task SearchTrimsKeywordAndPreservesActiveFilterAndPagination(string keyword, string expected)
    {
        var repository = Substitute.For<IServiceCatalogRepository>(); var uow = Substitute.For<IUnitOfWork>(); var audit = Substitute.For<IAuditLogger>();
        var page = PagedResult<ServiceCatalogListItemResponse>.Create(Array.Empty<ServiceCatalogListItemResponse>(), 0, 2, 20);
        repository.GetPageAsync(Arg.Any<string>(), true, 2, 20, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(page);
        var result = await new ServiceCatalogService(repository, uow, audit).GetPageAsync(new() { Keyword = keyword, IsActive = true, Page = 2 });
        Assert.Same(page, result);
        await repository.Received(1).GetPageAsync(expected, true, 2, 20, Arg.Is<DateTime>(t => t.Kind == DateTimeKind.Utc), Arg.Any<CancellationToken>());
        Assert.Empty(uow.ReceivedCalls()); Assert.Empty(audit.ReceivedCalls());
    }
}
