using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;
using DiagnosisService = Dental.Application.Features.Visits.Services.VisitService;

namespace Dental.Tests.Billing;

public sealed class VisitCorrectionTests
{
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly Visit visit = new() { VisitId = 1, DentistId = 2, Status = VisitStatuses.Correcting };
    public VisitCorrectionTests()
    {
        visits.GetForUpdateAsync(1, Arg.Any<CancellationToken>()).Returns(visit);
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<VisitResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<VisitResponse>>>>()(CancellationToken.None));
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<ToothConditionResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<ToothConditionResponse>>>>()(CancellationToken.None));
    }
    [Theory] [InlineData(RoleCodes.Admin, 99)] [InlineData(RoleCodes.Dentist, 2)]
    public async Task CorrectionAllowsDiagnosisOnlyForExistingClinicalWriters(string role, int actor)
    {
        var service = new DiagnosisService(Substitute.For<IPatientRepository>(), visits, uow, audit);
        var result = await service.UpdateDiagnosisAsync(1, new("Synthetic correction", "Synthetic clinical note"), actor, role, null);
        Assert.True(result.IsSuccess); Assert.Equal(VisitStatuses.Correcting, result.Value!.Status);
        await audit.Received(1).LogRequiredAsync(AuditActions.VisitDiagnosisUpdated, "Visit", 1, actor,
            Arg.Is<string>(s => !s.Contains("Synthetic")), null, Arg.Any<CancellationToken>());
        Assert.NotNull(visit.UpdatedAt);
    }
    [Theory] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)]
    public async Task CorrectionDoesNotExpandDiagnosisRoles(string role)
    {
        var result = await new DiagnosisService(Substitute.For<IPatientRepository>(), visits, uow, audit)
            .UpdateDiagnosisAsync(1, new("Synthetic correction"), 2, role, null);
        Assert.Equal(Error.Forbidden, result.Error); Assert.Null(visit.Diagnosis); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(VisitStatuses.Correcting)] [InlineData(VisitStatuses.InProgress)]
    public async Task LockedStateAlwaysBlocksDiagnosisEvenIfStateIsEditable(string status)
    {
        visit.Status = status; visit.IsLocked = true;
        var result = await new DiagnosisService(Substitute.For<IPatientRepository>(), visits, uow, audit)
            .UpdateDiagnosisAsync(1, new("Synthetic correction"), 2, RoleCodes.Dentist, null);
        Assert.True(result.IsFailure); Assert.Null(visit.Diagnosis); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task ToothCorrectionTouchesVisitAndRequiresAuditInTransaction()
    {
        var conditions = Substitute.For<IToothConditionRepository>();
        var result = await new ToothConditionService(visits, Substitute.For<IPatientRepository>(), conditions, uow, audit, new())
            .AddAsync(1, new(11, null, "CARIES", "Synthetic note"), 2, RoleCodes.Dentist, null);
        Assert.True(result.IsSuccess); Assert.NotNull(visit.UpdatedAt);
        await audit.Received(1).LogRequiredAsync("MOD_FDI_CONDITION_CREATED", "ToothCondition", Arg.Any<int?>(), 2,
            Arg.Is<string>(s => !s.Contains("Synthetic")), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Receptionist)] [InlineData(null)]
    public async Task UnsupportedRoleCannotFinalizeCorrection(string? role)
    {
        var invoices = Substitute.For<IInvoiceRepository>();
        var service = new InvoiceDraftService(invoices, visits, Substitute.For<IPatientRepository>(), audit,
            new(new FixedTimeProvider(DateTimeOffset.UtcNow)));
        Assert.Equal(Error.Forbidden, (await service.CompleteCorrectionAsync(1, 2, role, null)).Error);
        Assert.Empty(invoices.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)]
    public async Task FinalizationUsesDedicatedRepositoryPathAndPropagatesConflict(string role)
    {
        var invoices = Substitute.For<IInvoiceRepository>();
        invoices.CompleteCorrectionAsync(1, 2, role, Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<InvoiceDraftSynchronization>.Failure(Error.Conflict));
        var service = new InvoiceDraftService(invoices, visits, Substitute.For<IPatientRepository>(), audit, new(TimeProvider.System));
        Assert.Equal(Error.Conflict, (await service.CompleteCorrectionAsync(1, 2, role, null)).Error);
        await invoices.DidNotReceiveWithAnyArgs().SaveCompletedVisitDraftAsync(default!, default, default, default);
        Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task ConcurrentDiagnosisChangeReturnsReloadConflict()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<VisitResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<VisitResponse>>(new ConcurrentOperationException()));
        var result = await new DiagnosisService(Substitute.For<IPatientRepository>(), visits, uow, audit)
            .UpdateDiagnosisAsync(1, new("Synthetic correction"), 2, RoleCodes.Dentist, null);
        Assert.Equal("PAT_113", result.Error.Code); Assert.Empty(audit.ReceivedCalls());
    }
}
