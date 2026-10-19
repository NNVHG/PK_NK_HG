using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Entities;
using NSubstitute;
namespace Dental.Tests.Common;
internal static class TestVisitChairs
{
    public static IVisitChairCoordinator Create()
    {
        var coordinator = Substitute.For<IVisitChairCoordinator>();
        coordinator.CheckReadyAsync(Arg.Any<Visit>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        coordinator.AcquireAsync(Arg.Any<Visit>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        coordinator.TransferAsync(Arg.Any<Visit>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        coordinator.ReleaseAsync(Arg.Any<Visit>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return coordinator;
    }
}
