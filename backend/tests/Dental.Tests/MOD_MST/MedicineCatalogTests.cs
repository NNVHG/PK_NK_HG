using System.Text.Json;
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
public sealed class MedicineCatalogTests
{
    private readonly IMedicineCatalogRepository repo = Substitute.For<IMedicineCatalogRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly Medicine item = new() { Id = 7, Code = "TEST", Name = "Test drug", Unit = "Viên", UnitPrice = 1000, IsActive = true, Version = 1, CreatedAt = DateTime.UtcNow.AddDays(-1) };
    private MedicineCatalogService Service() => new(repo, uow, audit, new VietnamClock(TimeProvider.System));
    private static MedicineWriteRequest Input() => new("test", "Test drug", null, null, "Viên", 1000, IsActive: true, ExpectedVersion: 1);
    public MedicineCatalogTests()
    {
        repo.FindAsync(7, Arg.Any<CancellationToken>()).Returns(item);
        repo.GetPageAsync(Arg.Any<MedicineQuery>(), Arg.Any<CancellationToken>()).Returns(PagedResult<Medicine>.Create([item], 1, 1, 20));
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<MedicineResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<MedicineResponse>>>>()(CancellationToken.None));
    }
    [Theory] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task NonAdminCannotManageOrRead(string? role)
    {
        Assert.Equal(Error.Forbidden, (await Service().GetAsync(role, new())).Error);
        Assert.Equal(Error.Forbidden, (await Service().SaveAsync(null, 1, role, Input())).Error);
        Assert.Equal(Error.Forbidden, (await Service().DeactivateAsync(7, 1, 1, role)).Error); Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task DentistCanReadButCannotManage()
    {
        Assert.Equal(7, Assert.Single((await Service().GetAsync(RoleCodes.Dentist, new())).Value!.Items).Id);
        Assert.Equal(Error.Forbidden, (await Service().SaveAsync(null, 1, RoleCodes.Dentist, Input())).Error);
        Assert.Equal(Error.Forbidden, (await Service().DeactivateAsync(7, 1, 1, RoleCodes.Dentist)).Error);
    }
    [Theory] [InlineData("Viên")] [InlineData("Vỉ")] [InlineData("Gói")] [InlineData("Chai")] [InlineData("Ống")] [InlineData("Tuýp")] [InlineData("Hộp")]
    public void SevenUnitsAccepted(string unit) => Assert.True(new MedicineWriteValidator().Validate(Input() with { Unit = unit }).IsValid);
    [Theory] [InlineData(-1)] [InlineData(0.5)] [InlineData(1000000000000000000d)]
    public void InvalidVndRejected(double price) => Assert.False(new MedicineWriteValidator().Validate(Input() with { UnitPrice = (decimal)price }).IsValid);
    [Fact] public void Max18digitPriceAccepted() => Assert.True(new MedicineWriteValidator().Validate(Input() with { UnitPrice = 999999999999999999m }).IsValid);
    [Fact] public void ZeroAllowedOnlyWhileInactive()
    {
        var v = new MedicineWriteValidator(); Assert.True(v.Validate(Input() with { UnitPrice = 0, IsActive = false }).IsValid); Assert.False(v.Validate(Input() with { UnitPrice = 0 }).IsValid);
    }
    [Fact] public void BoundsAndUnitsValidated()
    {
        var v = new MedicineWriteValidator();
        foreach (var q in new[] { Input() with { Code = "bad code" }, Input() with { Name = new string('x', 201) }, Input() with { ActiveIngredient = new string('x', 201) }, Input() with { Strength = new string('x', 51) }, Input() with { Unit = "Tự do" }, Input() with { DefaultUsageInstruction = new string('x', 501) } }) Assert.False(v.Validate(q).IsValid);
    }
    [Theory] [InlineData(0, 20)] [InlineData(1, 51)] [InlineData(1000001, 20)]
    public void PagingBounded(int page, int size) => Assert.False(new MedicineQueryValidator().Validate(new MedicineQuery(Page: page, PageSize: size)).IsValid);
    [Fact] public async Task AdminReadsRealPagedRecords() => Assert.Equal(7, Assert.Single((await Service().GetAsync(RoleCodes.Admin, new())).Value!.Items).Id);
    [Fact] public async Task InvalidQueryNeverReads()
    { Assert.True((await Service().GetAsync(RoleCodes.Admin, new(Keyword: new string('x', 201)))).IsFailure); Assert.Empty(repo.ReceivedCalls()); }
    [Fact] public async Task CreateCanonicalizesAndAuditsWithoutClinicalInstruction()
    {
        var r = await Service().SaveAsync(null, 1, RoleCodes.Admin, Input() with { Code = " lower ", ActiveIngredient = " DƯỢC chất ", DefaultUsageInstruction = "Private test instruction" });
        Assert.True(r.IsSuccess); Assert.Equal("LOWER", r.Value!.Code);
        await repo.Received(1).AddAsync(Arg.Is<Medicine>(m => m.ActiveIngredient == "DƯỢC chất" && m.SearchText.Contains("duoc chat") && m.CreatedAt.Kind == DateTimeKind.Utc), Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync("MOD_MST_MEDICINE_CREATED", "Medicine", Arg.Any<int?>(), 1, Arg.Is<string>(s => !s.Contains("Private test instruction") && s.Contains("UnitPrice")), null, Arg.Any<CancellationToken>());
    }
    [Fact] public async Task DuplicateRejectedBeforeSave()
    { repo.CodeExistsAsync("TEST", null, Arg.Any<CancellationToken>()).Returns(true); Assert.Equal("MST_081", (await Service().SaveAsync(null, 1, RoleCodes.Admin, Input())).Error.Code); await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); }
    [Fact] public async Task StaleUpdateDoesNotChangeItem()
    { Assert.Equal("MST_082", (await Service().SaveAsync(7, 1, RoleCodes.Admin, Input() with { ExpectedVersion = 2, Name = "Changed" })).Error.Code); Assert.Equal("Test drug", item.Name); }
    [Fact] public async Task UpdateIncrementsVersionPreservesCreationAndSetsTimestamp()
    {
        var created = item.CreatedAt; var r = await Service().SaveAsync(7, 1, RoleCodes.Admin, Input() with { UnitPrice = 2000 });
        Assert.True(r.IsSuccess); Assert.Equal(2, item.Version); Assert.Equal(created, item.CreatedAt); Assert.NotNull(item.UpdatedAt); Assert.Equal(2000, item.UnitPrice);
    }
    [Fact] public async Task ActivationRequiresPriceBeforeMutation()
    { Assert.True((await Service().SaveAsync(7, 1, RoleCodes.Admin, Input() with { UnitPrice = 0, IsActive = true })).IsFailure); Assert.Equal(1000, item.UnitPrice); await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); }
    [Fact] public async Task SoftDeletePreservesPriceAndIdentity()
    { var r = await Service().DeactivateAsync(7, 1, 1, RoleCodes.Admin); Assert.True(r.IsSuccess); Assert.False(item.IsActive); Assert.Equal(7, item.Id); Assert.Equal(1000, item.UnitPrice); Assert.Equal(2, item.Version); }
    [Fact] public async Task RepeatedDeactivateDoesNotDuplicateAudit()
    { item.IsActive = false; Assert.True((await Service().DeactivateAsync(7, 1, 1, RoleCodes.Admin)).IsSuccess); Assert.Empty(audit.ReceivedCalls()); await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task InvalidVersionDoesNotWrite(int version)
    { Assert.True((await Service().SaveAsync(7, 1, RoleCodes.Admin, Input() with { ExpectedVersion = version })).IsFailure); Assert.True((await Service().DeactivateAsync(7, version, 1, RoleCodes.Admin)).IsFailure); Assert.Empty(repo.ReceivedCalls()); }
    [Fact] public async Task MissingIdReturns404()
    { Assert.Equal(Error.NotFound, (await Service().SaveAsync(8, 1, RoleCodes.Admin, Input())).Error); Assert.Equal(Error.NotFound, (await Service().DeactivateAsync(8, 1, 1, RoleCodes.Admin)).Error); }
    [Fact] public async Task AuditFailureIsNotSwallowed()
    {
        audit.LogRequiredAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromException(new InvalidOperationException("Synthetic medicine audit fault")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SaveAsync(7, 1, RoleCodes.Admin, Input()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().DeactivateAsync(7, 2, 1, RoleCodes.Admin));
    }
    [Fact] public async Task RaceMapsTo409WithoutRetry()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<MedicineResponse>>>>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromException<Result<MedicineResponse>>(new ConcurrentOperationException()));
        Assert.Equal("MST_082", (await Service().SaveAsync(7, 1, RoleCodes.Admin, Input())).Error.Code);
        Assert.Equal("MST_082", (await Service().DeactivateAsync(7, 1, 1, RoleCodes.Admin)).Error.Code);
    }
    [Fact] public void JsonMoneyIsExact18digitStringAndRequestAcceptsIt()
    {
        var response = new MedicineResponse(1, "TEST", "Test", null, null, "Viên", 999999999999999999m, null, true, 1, DateTime.UtcNow, null);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)); Assert.Contains("\"unitPrice\":\"999999999999999999\"", json);
        var request = JsonSerializer.Deserialize<MedicineWriteRequest>("{\"code\":\"TEST\",\"name\":\"Test\",\"unit\":\"Viên\",\"unitPrice\":\"999999999999999999\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(999999999999999999m, request!.UnitPrice);
    }
}
