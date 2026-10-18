using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Features.MOD_MST.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_MST;
public sealed class DiagnosisCatalogTests
{
    private readonly IDiagnosisCatalogRepository repo = Substitute.For<IDiagnosisCatalogRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly DentalDiagnosis item = new() { Id = 7, Code = "TEST", Name = "Synthetic name", Category = "Khác", Version = 1 };
    private DiagnosisCatalogService Service() => new(repo, uow, audit);
    public DiagnosisCatalogTests()
    {
        repo.FindAsync(7, Arg.Any<CancellationToken>()).Returns(item);
        repo.GetPageAsync(Arg.Any<DiagnosisQuery>(), Arg.Any<CancellationToken>()).Returns(PagedResult<DentalDiagnosis>.Create(new[] { item }, 1, 1, 20));
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<DiagnosisResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<DiagnosisResponse>>>>()(CancellationToken.None));
    }
    [Theory] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Assistant)] [InlineData(null)]
    public async Task NonReadersCannotAccess(string? role)
    { Assert.Equal(Error.Forbidden, (await Service().GetAsync(role, new())).Error); Assert.Equal(Error.Forbidden, (await Service().SaveAsync(null, 1, role, new("T", "Name", null, "Khác"))).Error); Assert.Empty(repo.ReceivedCalls()); }
    [Fact] public async Task DentistCanReadOnlyActiveAndCannotWrite()
    {
        Assert.True((await Service().GetAsync(RoleCodes.Dentist, new())).IsSuccess);
        Assert.Equal(Error.Forbidden, (await Service().GetAsync(RoleCodes.Dentist, new(IncludeInactive: true))).Error);
        Assert.Equal(Error.Forbidden, (await Service().SaveAsync(7, 1, RoleCodes.Dentist, new("T", "Name", null, "Khác", ExpectedVersion: 1))).Error);
    }
    [Theory] [InlineData("  Viêm TỦY  ", "viem tuy")] [InlineData("Đau Răng", "dau rang")] [InlineData("K02.1", "k02.1")]
    public void AccentNormalization(string input, string expected) => Assert.Equal(expected, DiagnosisSearch.Normalize(input));
    [Fact] public async Task CreateNormalizesCodeTextAndAuditsOnlyMetadata()
    {
        var result = await Service().SaveAsync(null, 1, RoleCodes.Admin, new(" low ", " Tên bệnh ", " ", " Nhóm mới "));
        Assert.True(result.IsSuccess); Assert.Equal("LOW", result.Value!.Code); Assert.Null(result.Value.Description);
        await repo.Received(1).AddAsync(Arg.Is<DentalDiagnosis>(x => x.SearchText == "low ten benh" && x.Category == "Nhóm mới"), Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync("MOD_MST_DIAGNOSIS_CREATED", "DentalDiagnosis", Arg.Any<int?>(), 1, Arg.Is<string>(s => !s.Contains("Tên bệnh")), null, Arg.Any<CancellationToken>());
    }
    [Fact] public async Task DuplicateDoesNotSave()
    { repo.CodeExistsAsync("TEST", null, Arg.Any<CancellationToken>()).Returns(true); Assert.Equal("MST_071", (await Service().SaveAsync(null, 1, RoleCodes.Admin, new("test", "Name", null, "Khác"))).Error.Code); await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); }
    [Fact] public async Task StaleVersionDoesNotMutate()
    { Assert.Equal("MST_072", (await Service().SaveAsync(7, 1, RoleCodes.Admin, new("TEST", "Changed", null, "Khác", ExpectedVersion: 2))).Error.Code); Assert.Equal("Synthetic name", item.Name); }
    [Fact] public async Task DeactivationRetainsIdentityAndIncrementsVersion()
    { var r = await Service().SaveAsync(7, 1, RoleCodes.Admin, new("TEST", item.Name, null, "Khác", false, 1)); Assert.True(r.IsSuccess); Assert.False(item.IsActive); Assert.Equal(7, item.Id); Assert.Equal(2, item.Version); }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task InvalidVersionRejected(int version) => Assert.True((await Service().SaveAsync(7, 1, RoleCodes.Admin, new("T", "Name", null, "Khác", ExpectedVersion: version))).IsFailure);
    [Theory] [InlineData(0, 20)] [InlineData(1, 51)] [InlineData(1, 0)]
    public void QueryBounded(int page, int size) => Assert.False(new DiagnosisQueryValidator().Validate(new DiagnosisQuery(Page: page, PageSize: size)).IsValid);
    [Fact] public void TextBoundsValidated()
    {
        var v = new DiagnosisWriteValidator(); Assert.False(v.Validate(new DiagnosisWriteRequest("T", new string('a', 201), null, "Khác")).IsValid);
        Assert.False(v.Validate(new DiagnosisWriteRequest("T", "Name", new string('a', 501), "Khác")).IsValid);
        Assert.False(v.Validate(new DiagnosisWriteRequest("T", "Name", null, " ")).IsValid);
    }
    [Fact] public async Task AuditFailurePropagatesWithinTransaction()
    {
        audit.LogRequiredAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromException(new InvalidOperationException("Synthetic audit fault")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SaveAsync(null, 1, RoleCodes.Admin, new("T", "Name", null, "Khác")));
    }
}
