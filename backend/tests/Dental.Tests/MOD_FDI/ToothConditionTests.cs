using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Application.Features.MOD_FDI.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_FDI;

public sealed class ToothConditionTests
{
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IToothConditionRepository conditions = Substitute.For<IToothConditionRepository>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    public ToothConditionTests()
    {
        unitOfWork.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<ToothConditionResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<ToothConditionResponse>>>>()(CancellationToken.None));
        unitOfWork.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<bool>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<bool>>>>()(CancellationToken.None));
    }
    private ToothConditionService Service => new(visits, patients, conditions, unitOfWork, audit, new());

    [Theory] [InlineData(null, null)] [InlineData("", null)] [InlineData("   ", null)]
    [InlineData("  Ghi chú chuyên môn\nDòng thứ hai  ", "Ghi chú chuyên môn\nDòng thứ hai")]
    public async Task Add_PreservesNoteWithoutLoggingClinicalText(string? input, string? expected)
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 1, Status = VisitStatuses.InProgress, DentistId = 2 });
        var result = await Service.AddAsync(1, new(11, "O", "CARIES", input), 2, RoleCodes.Dentist, null);
        Assert.True(result.IsSuccess); Assert.Equal(expected, result.Value!.Note);
        await conditions.Received(1).AddAsync(Arg.Is<ToothCondition>(x => x.Note == expected && x.VisitId == 1 && x.ToothNumber == 11 && x.Surface == "O"), Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync("MOD_FDI_CONDITION_CREATED", "ToothCondition", Arg.Any<int?>(), 2,
            "{\"action\":\"condition_created\"}", null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(500, true)] [InlineData(501, false)]
    public void Validator_EnforcesNoteLimit(int length, bool valid)
        => Assert.Equal(valid, new ToothConditionRequestValidator().Validate(new ToothConditionRequest(11, null, "CARIES", new string('x', length))).IsValid);
    [Fact] public async Task Add_OverlongNoteDoesNotReadOrSave()
    {
        Assert.Equal(Error.Validation, (await Service.AddAsync(1, new(11, null, "CARIES", new string('x', 501)), 2, RoleCodes.Admin, null)).Error);
        Assert.Empty(visits.ReceivedCalls()); Assert.Empty(conditions.ReceivedCalls()); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task Get_ReturnsPersistedClinicalNote()
    {
        visits.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { PatientId = 5 });
        conditions.GetForVisitAsync(1, Arg.Any<CancellationToken>()).Returns(new[] { new ToothCondition { Id = 3, VisitId = 1, ToothNumber = 11, ConditionCode = "CARIES", Note = "Synthetic note" } });
        Assert.Equal("Synthetic note", Assert.Single((await Service.GetAsync(1, 2, RoleCodes.Admin)).Value!).Note);
    }

    [Fact]
    public void Validator_AcceptsExactly52FdiTeeth()
    {
        var expected = Enumerable.Range(1, 4).SelectMany(q => Enumerable.Range(1, 8).Select(t => q * 10 + t))
            .Concat(Enumerable.Range(5, 4).SelectMany(q => Enumerable.Range(1, 5).Select(t => q * 10 + t))).ToHashSet();
        var validator = new ToothConditionRequestValidator();
        for (var number = -10; number <= 100; number++)
            Assert.Equal(expected.Contains(number), validator.Validate(new ToothConditionRequest(number, null, "CARIES")).IsValid);
        Assert.Equal(52, expected.Count);
    }

    [Theory]
    [InlineData(null, true)] [InlineData("All", true)] [InlineData("B", true)]
    [InlineData("L", true)] [InlineData("M", true)] [InlineData("D", true)] [InlineData("O", true)]
    [InlineData("MOD", true)] [InlineData("B-L", true)] [InlineData("BLMDO", true)]
    [InlineData("", false)] [InlineData("BB", false)] [InlineData("B-", false)]
    [InlineData("-B", false)] [InlineData("B--L", false)] [InlineData("buccal", false)]
    [InlineData("X", false)] [InlineData("ALL", false)]
    public void Validator_ChecksSurface(string? surface, bool valid)
        => Assert.Equal(valid, new ToothConditionRequestValidator().Validate(new ToothConditionRequest(11, surface, "CARIES")).IsValid);

    [Fact]
    public void Validator_AcceptsApprovedCodesOnly()
    {
        var validator = new ToothConditionRequestValidator();
        foreach (var code in FdiCodes.Conditions) Assert.True(validator.Validate(new ToothConditionRequest(11, null, code)).IsValid);
        foreach (var code in new[] { "Caries", "BRIDGE", "", "UNKNOWN" })
            Assert.False(validator.Validate(new ToothConditionRequest(11, null, code)).IsValid);
    }

    [Theory]
    [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)]
    [InlineData(null)] [InlineData("UNKNOWN")]
    public async Task Add_RejectsOtherRolesBeforeLookingUpVisit(string? role)
    {
        var result = await Service.AddAsync(1, new(11, null, "CARIES"), 2, role, null);
        Assert.Equal(Error.Forbidden, result.Error);
        await visits.DidNotReceiveWithAnyArgs().GetForUpdateAsync(default, default);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData("Created", false)] [InlineData("Completed", false)]
    [InlineData("Cancelled", false)] [InlineData("InProgress", true)]
    public async Task Add_RejectsClosedOrLockedVisit(string status, bool locked)
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 1, Status = status, IsLocked = locked });
        var result = await Service.AddAsync(1, new(11, null, "CARIES"), 2, RoleCodes.Admin, null);
        Assert.Equal(ToothConditionService.VisitClosed, result.Error);
        await conditions.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Add_RejectsUnassignedDentist()
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { Status = VisitStatuses.InProgress, DentistId = 3 });
        Assert.Equal(Error.Forbidden, (await Service.AddAsync(1, new(11, null, "CARIES"), 2, RoleCodes.Dentist, null)).Error);
    }

    [Fact]
    public async Task Add_MissingVisitOrInvalidRequestDoesNotSave()
    {
        Assert.Equal(Error.NotFound, (await Service.AddAsync(1, new(11, null, "CARIES"), 2, RoleCodes.Admin, null)).Error);
        Assert.Equal(Error.Validation, (await Service.AddAsync(1, new(19, null, "CARIES"), 2, RoleCodes.Admin, null)).Error);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)]
    public async Task Add_SavesVisitSnapshotAndSafeAudit(string role)
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 1, Status = VisitStatuses.InProgress, DentistId = 2 });
        var result = await Service.AddAsync(1, new(51, "B-L", "RESTORED"), 2, role, null);
        Assert.True(result.IsSuccess);
        Assert.Equal("BL", result.Value!.Surface);
        Assert.Equal(1, result.Value!.VisitId);
        await conditions.Received(1).AddAsync(Arg.Is<ToothCondition>(x => x.ToothNumber == 51 && x.Note == null && x.CreatedAt.Kind == DateTimeKind.Utc), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync("MOD_FDI_CONDITION_CREATED", "ToothCondition", Arg.Any<int?>(), 2,
            "{\"action\":\"condition_created\"}", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ChangesOnlyCurrentVisitSnapshotAndAuditsWithoutClinicalText()
    {
        var row = new ToothCondition { Id = 4, VisitId = 1, ToothNumber = 11, Surface = "O", ConditionCode = "CARIES", Note = "old" };
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { VisitId = 1, Status = VisitStatuses.InProgress, DentistId = 2 });
        conditions.GetForUpdateAsync(1, 4, Arg.Any<CancellationToken>()).Returns(row);

        var result = await Service.UpdateAsync(1, 4, new(12, "B-L", "RESTORED", "  treatment done  "), 2, RoleCodes.Dentist, null);

        Assert.True(result.IsSuccess);
        Assert.Equal((12, "BL", "RESTORED", "treatment done"),
            (row.ToothNumber, row.Surface, row.ConditionCode, row.Note));
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync("MOD_FDI_CONDITION_UPDATED", "ToothCondition", 4, 2,
            "{\"action\":\"condition_updated\"}", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_RejectsSnapshotFromAnotherVisit()
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { Status = VisitStatuses.InProgress, DentistId = 2 });
        conditions.GetForUpdateAsync(1, 4, Arg.Any<CancellationToken>()).Returns((ToothCondition?)null);

        var result = await Service.UpdateAsync(1, 4, new(11, null, "RESTORED"), 2, RoleCodes.Dentist, null);

        Assert.Equal(Error.NotFound, result.Error);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData("Completed", false)] [InlineData("InProgress", true)]
    public async Task Delete_RejectsClosedOrLockedVisit(string status, bool locked)
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { Status = status, IsLocked = locked, DentistId = 2 });

        var result = await Service.DeleteAsync(1, 4, 2, RoleCodes.Dentist, null);

        Assert.Equal(ToothConditionService.VisitClosed, result.Error);
        await conditions.DidNotReceiveWithAnyArgs().GetForUpdateAsync(default, default, default);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Delete_RemovesOnlyCurrentVisitSnapshotAndAuditsSafeReference()
    {
        var row = new ToothCondition { Id = 4, VisitId = 1, ToothNumber = 11, ConditionCode = "CARIES", Note = "private clinical note" };
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { Status = VisitStatuses.InProgress, DentistId = 2 });
        conditions.GetForUpdateAsync(1, 4, Arg.Any<CancellationToken>()).Returns(row);

        var result = await Service.DeleteAsync(1, 4, 2, RoleCodes.Dentist, null);

        Assert.True(result.IsSuccess);
        conditions.Received(1).Remove(row);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync("MOD_FDI_CONDITION_DELETED", "ToothCondition", 4, 2,
            "{\"action\":\"condition_deleted\"}", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_RejectsOtherPatientsWithNotFound()
    {
        visits.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { PatientId = 5 });
        patients.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new Patient { UserId = 99 });
        Assert.Equal(Error.NotFound, (await Service.GetAsync(1, 2, RoleCodes.Patient)).Error);
        await conditions.DidNotReceiveWithAnyArgs().GetForVisitAsync(default, default);
    }

    [Theory]
    [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Receptionist)]
    public async Task Get_ReturnsOnlyRequestedVisit(string role)
    {
        visits.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Visit { PatientId = 5 });
        patients.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new Patient { UserId = 2 });
        conditions.GetForVisitAsync(1, Arg.Any<CancellationToken>()).Returns(new[] { new ToothCondition { Id = 3, VisitId = 1, ToothNumber = 11, ConditionCode = "CARIES" } });
        var result = await Service.GetAsync(1, 2, role);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, Assert.Single(result.Value!).VisitId);
    }

    [Theory]
    [InlineData(null)] [InlineData("UNKNOWN")]
    public async Task Get_RejectsUnknownRole(string? role)
        => Assert.Equal(Error.Forbidden, (await Service.GetAsync(1, 2, role)).Error);
}
