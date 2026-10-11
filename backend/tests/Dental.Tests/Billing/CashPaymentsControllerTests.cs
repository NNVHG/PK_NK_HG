using Dental.Api.Controllers;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Billing;

public sealed class CashPaymentsControllerTests
{
    private readonly ICashPaymentRepository repository = Substitute.For<ICashPaymentRepository>();
    private readonly ICurrentUser user = Substitute.For<ICurrentUser>();
    private CashPaymentsController Controller()
        => new(new CashPaymentService(repository, new CashPaymentRequestValidator()), repository, user)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    [Theory]
    [InlineData("GEN_001", 404)]
    [InlineData("GEN_004", 409)]
    [InlineData("BIL_031", 409)]
    [InlineData("BIL_032", 400)]
    [InlineData("BIL_006", 409)]
    public async Task MapsRepositoryErrors(string code, int status)
    {
        user.UserId.Returns(9); user.RoleCode.Returns(RoleCodes.Receptionist);
        repository.ReceiveAsync(1, 9, Arg.Any<CashPaymentRequest>(), null, Arg.Any<CancellationToken>())
            .Returns(Result<PaymentTransaction>.Failure(new Error(code, "Test failure")));
        var result = Assert.IsType<ObjectResult>(await Controller().Receive(1, new(10, 20, Guid.NewGuid()), default));
        Assert.Equal(status, result.StatusCode);
    }

    [Theory]
    [InlineData(RoleCodes.Patient)]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Assistant)]
    public async Task NonCashierPostForbiddenWithoutPersistence(string role)
    {
        user.UserId.Returns(9); user.RoleCode.Returns(role);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Controller().Receive(1, new(10, 20, Guid.NewGuid()), default)).StatusCode);
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task InvalidTenderedReturns400WithoutPersistence()
    {
        user.UserId.Returns(9); user.RoleCode.Returns(RoleCodes.Admin);
        Assert.Equal(400, Assert.IsType<ObjectResult>(await Controller().Receive(1, new(10, 9, Guid.NewGuid()), default)).StatusCode);
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task MissingIdentityUnauthorized()
    {
        Assert.IsType<UnauthorizedResult>(await Controller().Receive(1, new(10, 20, Guid.NewGuid()), default));
        Assert.IsType<UnauthorizedResult>(await Controller().History(1, default));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task HistoryMissingInvoiceReturns404()
    {
        user.UserId.Returns(9);
        Assert.IsType<NotFoundResult>(await Controller().History(1, default));
        await repository.DidNotReceive().GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistingInvoiceWithoutPaymentsReturnsEmptyHistory()
    {
        user.UserId.Returns(9); repository.InvoiceExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetAsync(1, Arg.Any<CancellationToken>()).Returns(Array.Empty<PaymentTransaction>());
        var response = Assert.IsType<OkObjectResult>(await Controller().History(1, default));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<CashPaymentResponse>>(response.Value));
    }

    [Fact]
    public void UsesCashierPolicyAndNoStoreHistory()
    {
        Assert.Equal(Dental.Api.Policies.Policies.CashPaymentWrite,
            Assert.Single(typeof(CashPaymentsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy);
        var cache = Assert.Single(typeof(CashPaymentsController).GetMethod("History")!.GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>());
        Assert.True(cache.NoStore); Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }
}
