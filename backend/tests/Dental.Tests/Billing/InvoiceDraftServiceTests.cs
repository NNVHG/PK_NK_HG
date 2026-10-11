using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Billing;

public sealed class InvoiceDraftServiceTests
{
    private readonly IInvoiceRepository invoices = Substitute.For<IInvoiceRepository>();
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IPatientRepository patients = Substitute.For<IPatientRepository>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private InvoiceDraftService Service() => new(invoices, visits, patients, audit,
        new VietnamClock(new FixedTimeProvider(new DateTimeOffset(2026, 10, 10, 18, 0, 0, TimeSpan.Zero))));
    private static Visit Completed() => new() { VisitId = 8, PatientId = 6, DentistId = 5,
        Status = VisitStatuses.Completed, IsLocked = true };

    [Theory]
    [InlineData(RoleCodes.Patient)]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Assistant)]
    public async Task Generate_ForbiddenRoles_DoNotPersist(string role)
    {
        var result = await Service().GenerateAsync(Completed(), 5, role, null);
        Assert.Equal(Error.Forbidden, result.Error);
        Assert.Empty(invoices.ReceivedCalls());
        Assert.Empty(audit.ReceivedCalls());
    }

    [Fact]
    public async Task Generate_OtherDentist_IsForbidden()
    {
        Assert.Equal(Error.Forbidden, (await Service().GenerateAsync(Completed(), 9, RoleCodes.Dentist, null)).Error);
        Assert.Empty(invoices.ReceivedCalls());
    }

    [Theory]
    [InlineData(VisitStatuses.InProgress, false)]
    [InlineData(VisitStatuses.Completed, false)]
    [InlineData(VisitStatuses.Cancelled, true)]
    public async Task Generate_RequiresCompletedLockedVisit(string status, bool locked)
    {
        var visit = Completed(); visit.Status = status; visit.IsLocked = locked;
        Assert.Equal("BIL_001", (await Service().GenerateAsync(visit, 5, RoleCodes.Dentist, null)).Error.Code);
        Assert.Empty(invoices.ReceivedCalls());
    }

    [Theory]
    [InlineData(RoleCodes.Admin, 99)]
    [InlineData(RoleCodes.Dentist, 5)]
    public async Task Generate_UsesVietnamDate_AndMinimalAudit(string role, int userId)
    {
        var visit = Completed();
        invoices.SaveCompletedVisitDraftAsync(visit, userId, new DateOnly(2026, 10, 11), Arg.Any<CancellationToken>())
            .Returns(Result<Invoice>.Success(new Invoice { Id = 4 }));
        Assert.True((await Service().GenerateAsync(visit, userId, role, null)).IsSuccess);
        await audit.Received(1).LogAsync("MOD_BIL_DRAFT_CREATED", "Invoice", 4, userId,
            "{\"action\":\"draft_created\"}", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_Conflict_DoesNotAuditSuccess()
    {
        invoices.SaveCompletedVisitDraftAsync(Arg.Any<Visit>(), 5, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result<Invoice>.Failure(new Error("BIL_006", "Conflict")));
        Assert.Equal("BIL_006", (await Service().GenerateAsync(Completed(), 5, RoleCodes.Dentist, null)).Error.Code);
        Assert.Empty(audit.ReceivedCalls());
    }

    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Patient)]
    public async Task Get_AllowedRoles_ReturnSavedSnapshot(string role)
    {
        visits.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(Completed());
        patients.GetByIdAsync(6, Arg.Any<CancellationToken>()).Returns(new Patient { UserId = 5 });
        invoices.GetForVisitAsync(8, Arg.Any<CancellationToken>()).Returns(new Invoice {
            Id = 4, VisitId = 8, Status = InvoiceStatus.PendingPayment, TotalAmount = 200000, PaidAmount = 0,
            Items = [new InvoiceItem { Code = "OLD", Name = "Snapshot", Quantity = 2, UnitPrice = 100000, TotalAmount = 200000 }] });
        var result = await Service().GetAsync(8, 5, role);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Status);
        Assert.Equal(200000m, result.Value.RemainingAmount);
        Assert.Equal("OLD", Assert.Single(result.Value.Items).Code);
    }

    [Fact]
    public async Task Get_OtherPatient_HidesVisit_AndDoesNotReadInvoice()
    {
        visits.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(Completed());
        patients.GetByIdAsync(6, Arg.Any<CancellationToken>()).Returns(new Patient { UserId = 90 });
        Assert.Equal(Error.NotFound, (await Service().GetAsync(8, 5, RoleCodes.Patient)).Error);
        Assert.Empty(invoices.ReceivedCalls());
    }

    [Fact]
    public async Task Get_Assistant_IsForbidden()
    {
        Assert.Equal(Error.Forbidden, (await Service().GetAsync(8, 5, RoleCodes.Assistant)).Error);
        Assert.Empty(visits.ReceivedCalls());
    }

    [Fact]
    public async Task Get_NoInvoice_ReturnsNotFound()
    {
        visits.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(Completed());
        Assert.Equal(Error.NotFound, (await Service().GetAsync(8, 5, RoleCodes.Admin)).Error);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Validator_RequiresPositiveVisitId(int id, bool valid)
        => Assert.Equal(valid, new InvoiceVisitRequestValidator().Validate(new InvoiceVisitRequest(id)).IsValid);

    [Fact]
    public void Enum_MatchesDl144()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, Enum.GetValues<InvoiceStatus>().Select(x => (int)x));
        Assert.Equal("PendingPayment", Enum.GetName((InvoiceStatus)1));
    }

    [Fact]
    public void Snapshot_PreservesRecordedPrice_AndIsIndependentOfSource()
    {
        var row = new VisitService { Id = 9, ServiceCode = "S01", ServiceName = "Snapshot",
            Quantity = 3, UnitPrice = 12345, ToothNumber = 16, Surface = "O" };
        var result = InvoiceServiceSnapshot.Create([row]);
        var item = Assert.Single(result.Value!);
        row.UnitPrice = 99999; row.ServiceName = "Changed";
        Assert.Equal(37035m, item.TotalAmount);
        Assert.Equal(12345m, item.UnitPrice);
        Assert.Equal("Snapshot", item.Name);
        Assert.Equal(16, item.ToothNumber);
        Assert.Equal("O", item.Surface);
        Assert.Equal(9, item.SourceVisitServiceId);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, -1)]
    [InlineData(1, 0.5)]
    public void Snapshot_RejectsInvalidMoney(int quantity, double price)
        => Assert.True(InvoiceServiceSnapshot.Create([new VisitService { Quantity = quantity, UnitPrice = (decimal)price }]).IsFailure);

    [Fact]
    public void Snapshot_RejectsOverflowBeforeMultiplication()
        => Assert.True(InvoiceServiceSnapshot.Create([new VisitService { Quantity = int.MaxValue, UnitPrice = decimal.MaxValue }]).IsFailure);

    [Fact]
    public void Snapshot_RejectsAccumulatedOverflow()
        => Assert.True(InvoiceServiceSnapshot.Create([
            new VisitService { Quantity = 1, UnitPrice = InvoiceServiceSnapshot.MaximumAmount },
            new VisitService { Quantity = 1, UnitPrice = 1 }]).IsFailure);

    [Fact]
    public void Snapshot_HandlesEmptyVisit_AndFreeGeneralService()
    {
        Assert.Empty(InvoiceServiceSnapshot.Create([]).Value!);
        var item = Assert.Single(InvoiceServiceSnapshot.Create([new VisitService { Quantity = 1, UnitPrice = 0 }]).Value!);
        Assert.Null(item.ToothNumber); Assert.Equal(0m, item.TotalAmount);
    }
}
