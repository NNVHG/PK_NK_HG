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

public sealed class MedicalSupplyCatalogTests
{
    private readonly IMedicalSupplyCatalogRepository repo = Substitute.For<IMedicalSupplyCatalogRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly MedicalSupply item = new()
    {
        Id = 7,
        Code = "VT-GANGTAY",
        Name = "Găng tay y tế",
        Unit = "Hộp",
        MinimumStockThreshold = 10,
        Description = "Hộp 100 chiếc",
        IsActive = true,
        Version = 1,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        CreatedByUserId = 1
    };

    private MedicalSupplyCatalogService Service() => new(repo, uow, audit, new VietnamClock(TimeProvider.System));
    [Fact]
    public async Task UnitCannotChangeAfterStockDocument()
    {
        repo.HasStockHistoryAsync(7, Arg.Any<CancellationToken>()).Returns(true);
        var result = await Service().SaveAsync(7, 1, RoleCodes.Admin, Input() with { Unit = "Cái" });
        Assert.Equal("MST_093", result.Error.Code);
        Assert.Equal("Hộp", item.Unit);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
    private static MedicalSupplyWriteRequest Input() => new("vt-gangtay", "Găng tay y tế", "Hộp", 10, "Hộp 100 chiếc", IsActive: true, ExpectedVersion: 1);

    public MedicalSupplyCatalogTests()
    {
        repo.FindAsync(7, Arg.Any<CancellationToken>()).Returns(item);
        repo.GetPageAsync(Arg.Any<MedicalSupplyQuery>(), Arg.Any<CancellationToken>()).Returns(PagedResult<MedicalSupply>.Create([item], 1, 1, 20));
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<MedicalSupplyResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<MedicalSupplyResponse>>>>()(CancellationToken.None));
    }

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Patient)]
    [InlineData(null)]
    public async Task NonReadersCannotReadOrManage(string? role)
    {
        Assert.Equal(Error.Forbidden, (await Service().GetAsync(role, new())).Error);
        Assert.Equal(Error.Forbidden, (await Service().SaveAsync(null, 1, role, Input())).Error);
        Assert.Equal(Error.Forbidden, (await Service().DeactivateAsync(7, 1, 1, role)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Fact]
    public async Task DentistCanReadActiveOnlyAndCannotManage()
    {
        Assert.Equal(7, Assert.Single((await Service().GetAsync(RoleCodes.Dentist, new())).Value!.Items).Id);
        Assert.Equal(Error.Forbidden, (await Service().GetAsync(RoleCodes.Dentist, new(IncludeInactive: true))).Error);
        Assert.Equal(Error.Forbidden, (await Service().SaveAsync(null, 1, RoleCodes.Dentist, Input())).Error);
        Assert.Equal(Error.Forbidden, (await Service().SaveAsync(7, 1, RoleCodes.Dentist, Input())).Error);
        Assert.Equal(Error.Forbidden, (await Service().DeactivateAsync(7, 1, 1, RoleCodes.Dentist)).Error);
    }

    [Fact]
    public async Task AdminCanReadIncludingInactive()
    {
        Assert.Equal(7, Assert.Single((await Service().GetAsync(RoleCodes.Admin, new(IncludeInactive: true))).Value!.Items).Id);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 51)]
    [InlineData(1000001, 20)]
    public void PagingBounded(int page, int size) =>
        Assert.False(new MedicalSupplyQueryValidator().Validate(new MedicalSupplyQuery(Page: page, PageSize: size)).IsValid);

    [Fact]
    public async Task InvalidQueryNeverReads()
    {
        Assert.True((await Service().GetAsync(RoleCodes.Admin, new(Keyword: new string('x', 201)))).IsFailure);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Fact]
    public void WriteValidationEnforcesBoundsAndThreshold()
    {
        var v = new MedicalSupplyWriteValidator();
        foreach (var bad in new[]
        {
            Input() with { Code = "bad code" },
            Input() with { Code = new string('A', 51) },
            Input() with { Name = " " },
            Input() with { Name = new string('x', 201) },
            Input() with { Unit = " " },
            Input() with { Unit = new string('u', 51) },
            Input() with { MinimumStockThreshold = -1 },
            Input() with { MinimumStockThreshold = 1000001 },
            Input() with { Description = new string('d', 501) }
        })
        {
            Assert.False(v.Validate(bad).IsValid);
        }
        Assert.True(v.Validate(Input() with { MinimumStockThreshold = 0, Description = null }).IsValid);
    }

    [Fact]
    public async Task CreateCanonicalizesCodeTextAndAuditsOnlyMetadata()
    {
        var r = await Service().SaveAsync(null, 1, RoleCodes.Admin, new(" vt-bonggon ", " Bông gòn nha khoa ", " Gói ", 5, " Mô tả quy cách riêng "));
        Assert.True(r.IsSuccess);
        Assert.Equal("VT-BONGGON", r.Value!.Code);
        Assert.Equal("Bông gòn nha khoa", r.Value.Name);
        Assert.Equal("Gói", r.Value.Unit);
        Assert.Equal(5, r.Value.MinimumStockThreshold);
        Assert.Equal("Mô tả quy cách riêng", r.Value.Description);
        Assert.Equal(1, r.Value.CreatedByUserId);
        await repo.Received(1).AddAsync(
            Arg.Is<MedicalSupply>(s => s.Code == "VT-BONGGON" && s.SearchText.Contains("bong gon") && s.CreatedAt.Kind == DateTimeKind.Utc && s.CreatedByUserId == 1),
            Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync(
            "MOD_MST_MEDICAL_SUPPLY_CREATED",
            "MedicalSupply",
            Arg.Any<int?>(),
            1,
            Arg.Is<string>(s => !s.Contains("Mô tả quy cách riêng") && s.Contains("MinimumStockThreshold")),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateRejectedBeforeSave()
    {
        repo.CodeExistsAsync("VT-GANGTAY", null, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal("MST_091", (await Service().SaveAsync(null, 1, RoleCodes.Admin, Input())).Error.Code);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StaleUpdateDoesNotChangeItem()
    {
        Assert.Equal("MST_092", (await Service().SaveAsync(7, 1, RoleCodes.Admin, Input() with { ExpectedVersion = 2, Name = "Changed" })).Error.Code);
        Assert.Equal("Găng tay y tế", item.Name);
    }

    [Fact]
    public async Task UpdateIncrementsVersionPreservesCreationAndSetsUpdatedMetadata()
    {
        var created = item.CreatedAt;
        var r = await Service().SaveAsync(7, 9, RoleCodes.Admin, Input() with { MinimumStockThreshold = 25, Description = " " });
        Assert.True(r.IsSuccess);
        Assert.Equal(2, item.Version);
        Assert.Equal(created, item.CreatedAt);
        Assert.Equal(1, item.CreatedByUserId);
        Assert.NotNull(item.UpdatedAt);
        Assert.Equal(9, item.UpdatedByUserId);
        Assert.Equal(25, item.MinimumStockThreshold);
        Assert.Null(item.Description);
    }

    [Fact]
    public async Task SoftDeletePreservesThresholdAndIdentity()
    {
        var r = await Service().DeactivateAsync(7, 1, 9, RoleCodes.Admin);
        Assert.True(r.IsSuccess);
        Assert.False(item.IsActive);
        Assert.Equal(7, item.Id);
        Assert.Equal(10, item.MinimumStockThreshold);
        Assert.Equal(2, item.Version);
        Assert.Equal(9, item.UpdatedByUserId);
    }

    [Fact]
    public async Task UpdateAuditKeepsStructuredBeforeAfterMetadataOutOfIpAddress()
    {
        var result = await Service().SaveAsync(7, 9, RoleCodes.Admin, Input() with { MinimumStockThreshold = 25 });
        Assert.True(result.IsSuccess);
        var args = Assert.Single(audit.ReceivedCalls()).GetArguments();
        Assert.Equal("MOD_MST_MEDICAL_SUPPLY_UPDATED", args[0]);
        Assert.Null(args[5]);
        var json = Assert.IsType<string>(args[4]);
        using var detail = JsonDocument.Parse(json);
        var before = detail.RootElement.GetProperty("Before");
        var after = detail.RootElement.GetProperty("After");
        Assert.Equal(10, before.GetProperty("MinimumStockThreshold").GetInt32());
        Assert.Equal(25, after.GetProperty("MinimumStockThreshold").GetInt32());
        Assert.Equal(1, before.GetProperty("Version").GetInt32());
        Assert.Equal(2, after.GetProperty("Version").GetInt32());
        Assert.True(before.GetProperty("IsActive").GetBoolean());
        Assert.True(after.GetProperty("IsActive").GetBoolean());
        Assert.DoesNotContain("\"Name\"", json);
        Assert.DoesNotContain("\"Description\"", json);
    }

    [Fact]
    public async Task DeactivationAuditKeepsStructuredBeforeAfterMetadataOutOfIpAddress()
    {
        var result = await Service().DeactivateAsync(7, 1, 9, RoleCodes.Admin);
        Assert.True(result.IsSuccess);
        var args = Assert.Single(audit.ReceivedCalls()).GetArguments();
        Assert.Equal("MOD_MST_MEDICAL_SUPPLY_DEACTIVATED", args[0]);
        Assert.Null(args[5]);
        var json = Assert.IsType<string>(args[4]);
        using var detail = JsonDocument.Parse(json);
        var before = detail.RootElement.GetProperty("Before");
        var after = detail.RootElement.GetProperty("After");
        Assert.True(before.GetProperty("IsActive").GetBoolean());
        Assert.False(after.GetProperty("IsActive").GetBoolean());
        Assert.Equal(1, before.GetProperty("Version").GetInt32());
        Assert.Equal(2, after.GetProperty("Version").GetInt32());
        Assert.Equal(10, before.GetProperty("MinimumStockThreshold").GetInt32());
        Assert.Equal(10, after.GetProperty("MinimumStockThreshold").GetInt32());
        Assert.DoesNotContain("\"Name\"", json);
        Assert.DoesNotContain("\"Description\"", json);
    }

    [Fact]
    public async Task RepeatedDeactivateDoesNotDuplicateAudit()
    {
        item.IsActive = false;
        Assert.True((await Service().DeactivateAsync(7, 1, 1, RoleCodes.Admin)).IsSuccess);
        Assert.Empty(audit.ReceivedCalls());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidVersionDoesNotWrite(int version)
    {
        Assert.True((await Service().SaveAsync(7, 1, RoleCodes.Admin, Input() with { ExpectedVersion = version })).IsFailure);
        Assert.True((await Service().DeactivateAsync(7, version, 1, RoleCodes.Admin)).IsFailure);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Fact]
    public async Task MissingIdReturns404()
    {
        Assert.Equal(Error.NotFound, (await Service().SaveAsync(8, 1, RoleCodes.Admin, Input())).Error);
        Assert.Equal(Error.NotFound, (await Service().DeactivateAsync(8, 1, 1, RoleCodes.Admin)).Error);
    }

    [Fact]
    public async Task AuditFailureIsNotSwallowed()
    {
        audit.LogRequiredAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("Synthetic supply audit fault")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().SaveAsync(7, 1, RoleCodes.Admin, Input()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().DeactivateAsync(7, 2, 1, RoleCodes.Admin));
    }

    [Fact]
    public async Task RaceMapsTo409WithoutRetry()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<MedicalSupplyResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Result<MedicalSupplyResponse>>(new ConcurrentOperationException()));
        Assert.Equal("MST_092", (await Service().SaveAsync(7, 1, RoleCodes.Admin, Input())).Error.Code);
        Assert.Equal("MST_092", (await Service().DeactivateAsync(7, 1, 1, RoleCodes.Admin)).Error.Code);
    }
}
