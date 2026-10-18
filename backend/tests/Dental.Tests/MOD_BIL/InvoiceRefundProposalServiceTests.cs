using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_BIL;

public sealed class InvoiceRefundProposalServiceTests
{
    [Fact]
    public async Task ReceptionistCanCreateProposalButAdminCannotUseCashierSubmission()
    {
        var proposals = Substitute.For<IInvoiceRefundProposalRepository>();
        var refunds = Substitute.For<IInvoiceRefundRepository>();
        var service = new InvoiceRefundProposalService(proposals, new InvoiceRefundService(refunds));
        var request = new InvoiceRefundProposalCreateRequest([new(10, 1)], "BILLING_ERROR", "Sai số lượng dịch vụ");
        var expected = new InvoiceRefundProposalDto(5, 20, "INV-20", new('A', 64), Guid.NewGuid(), "Pending",
            DateTime.UtcNow, "Lễ tân", "BILLING_ERROR", request.ReasonNotes,
            [new(10, "Service", "SRV-10", "Khám", 1, "500000")], null, null);
        proposals.CreateAsync(20, 31, request, Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result<InvoiceRefundProposalDto>.Success(expected)));

        var accepted = await service.CreateAsync(20, 31, RoleCodes.Receptionist, request, default);
        var denied = await service.CreateAsync(20, 31, RoleCodes.Admin, request, default);
        var invalid = await service.CreateAsync(20, 31, RoleCodes.Receptionist,
            new InvoiceRefundProposalCreateRequest([], "BILLING_ERROR", "Sai số lượng dịch vụ"), default);

        Assert.True(accepted.IsSuccess);
        Assert.Equal("Pending", accepted.Value!.Status);
        Assert.Equal(Error.Forbidden.Code, denied.Error.Code);
        Assert.Equal(Error.Validation.Code, invalid.Error.Code);
        await proposals.Received(1).CreateAsync(20, 31, request, Arg.Any<CancellationToken>());
    }
}