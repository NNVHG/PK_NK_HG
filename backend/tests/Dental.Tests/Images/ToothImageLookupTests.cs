using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Images;
public sealed class ToothImageLookupTests
{
    [Theory][InlineData(RoleCodes.Assistant)][InlineData("UNKNOWN")]
    public async Task NonReadersCannotResolveVisit(string role)
    {
        var repo=Substitute.For<IDentalImageRepository>();var service=new DentalImageService(repo,Substitute.For<IDentalImageStorage>());
        Assert.Equal(Error.Forbidden,(await service.VisitContextAsync(7,3,role)).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Theory][InlineData(RoleCodes.Admin)][InlineData(RoleCodes.Dentist)][InlineData(RoleCodes.Receptionist)][InlineData(RoleCodes.Patient)]
    public async Task ReadRolesDelegateExactActorVisitAndRole(string role)
    {
        var repo=Substitute.For<IDentalImageRepository>();await new DentalImageService(repo,Substitute.For<IDentalImageStorage>()).VisitContextAsync(7,3,role);
        await repo.Received().VisitContextAsync(7,3,role,default);
    }
    [Fact]public async Task InvalidVisitDoesNotReachRepository()
    {
        var repo=Substitute.For<IDentalImageRepository>();var r=await new DentalImageService(repo,Substitute.For<IDentalImageStorage>()).VisitContextAsync(0,3,RoleCodes.Admin);
        Assert.Equal(Error.Validation,r.Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Fact]public void LookupContextContainsOnlyIds()=>Assert.Equal(new[]{"PatientId","VisitId"},typeof(ImageVisitContext).GetProperties().Select(p=>p.Name).Order().ToArray());
}
