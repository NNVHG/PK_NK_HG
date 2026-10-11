using System.Reflection;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Billing;
public sealed class ReceiptPrintTests
{
    [Theory]
    [InlineData(0, 1, false)] [InlineData(1, 0, false)] [InlineData(-1, 1, false)] [InlineData(1, 1, true)]
    public void ValidatesRouteIds(int invoice, int payment, bool valid)
        => Assert.Equal(valid, new ReceiptRequestValidator().Validate(new ReceiptRequest(invoice, payment)).IsValid);
    [Theory]
    [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task ServiceRejectsNonCashier(string role)
    {
        var repo = Substitute.For<IReceiptRepository>();
        var result = await new ReceiptPrintService(repo, new()).GetAsync(new(1, 1), 1, role, true, null);
        Assert.Equal(Error.Forbidden, result.Error); Assert.Empty(repo.ReceivedCalls());
    }
    [Fact]
    public async Task ServiceRejectsMissingActorAndInvalidIdsBeforeRepository()
    {
        var repo = Substitute.For<IReceiptRepository>(); var service = new ReceiptPrintService(repo, new());
        Assert.True((await service.GetAsync(new(1, 1), 0, RoleCodes.Admin, true, null)).IsFailure);
        Assert.Equal(Error.Validation, (await service.GetAsync(new(0, 1), 1, RoleCodes.Admin, true, null)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory]
    [InlineData(RoleCodes.Admin, true)] [InlineData(RoleCodes.Receptionist, false)]
    public async Task ServiceDelegatesReadOrPrintAndReturnsRealFailure(string role, bool print)
    {
        var repo = Substitute.For<IReceiptRepository>(); var request = new ReceiptRequest(1, 2);
        repo.GetAsync(request).Returns(Result<ReceiptPrintDto>.Failure(Error.NotFound));
        repo.PrintAsync(request, 3, null).Returns(Result<ReceiptPrintDto>.Failure(Error.NotFound));
        var result = await new ReceiptPrintService(repo, new()).GetAsync(request, 3, role, print, null);
        Assert.Equal(Error.NotFound, result.Error);
        if (print) await repo.Received(1).PrintAsync(request, 3, null); else await repo.Received(1).GetAsync(request);
    }
    [Theory]
    [InlineData(1, false, false, true)] [InlineData(2, false, false, false)]
    [InlineData(-1, false, false, false)] [InlineData(1, true, false, false)] [InlineData(1, false, true, false)]
    public void OnlyOnePrintIncrementCanBypassFinancialImmutability(int increment, bool changeAmount, bool delete, bool allowed)
    {
        using var db = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var p = new PaymentTransaction { Id = 1, Amount = 10, PrintCount = 1 }; db.Attach(p);
        p.PrintCount += increment; if (changeAmount) p.Amount = 20; if (delete) db.Remove(p);
        db.ChangeTracker.DetectChanges();
        var guard = typeof(DentalDbContext).GetMethod("GuardAuditLogImmutability", BindingFlags.NonPublic | BindingFlags.Instance)!;
        if (allowed) guard.Invoke(db, null);
        else Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => guard.Invoke(db, null)).InnerException);
    }
}
