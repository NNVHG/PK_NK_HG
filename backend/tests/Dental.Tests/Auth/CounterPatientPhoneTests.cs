using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Auth;
public sealed class CounterPatientPhoneTests
{
    [Theory]
    [InlineData("0967000001", true, true)]
    [InlineData("0967000001", false, false)]
    [InlineData("0967000001\n", true, false)]
    [InlineData("0１２３４５６７８９", true, false)]
    [InlineData("0967", true, false)]
    [InlineData("+84967000001", true, false)]
    [InlineData(null, true, false)]
    public void Validate(string? phone, bool consent, bool valid) => Assert.Equal(valid, new CounterPhoneRequestValidator().Validate(new CounterPhoneRequest(phone!, consent)).IsValid);
    [Theory]
    [InlineData(0,"0967000001",true)]
    [InlineData(-1,"0967000001",true)]
    [InlineData(1,"bad",true)]
    [InlineData(1,"0967000001",false)]
    public async Task InvalidDoesNotCallRepository(int appointment, string phone, bool consent)
    {
        var repo=Substitute.For<ICounterPatientPhoneRepository>();var service=new CounterPatientPhoneService(repo);
        Assert.Equal(Error.Validation,(await service.CreateAsync(4,appointment,new(phone,consent),null,default)).Error);
        await repo.DidNotReceiveWithAnyArgs().CreateAsync(default,default,default!,default,default);
    }
    [Fact] public async Task ValidRequestCarriesActorAppointmentAndConsent()
    {
        var repo=Substitute.For<ICounterPatientPhoneRepository>();var request=new CounterPhoneRequest("0967000001",true);
        repo.CreateAsync(4,8,request,null,default).Returns(Result<CounterPhoneReceipt>.Success(new(Guid.NewGuid(),true)));
        Assert.True((await new CounterPatientPhoneService(repo).CreateAsync(4,8,request,null,default)).Value!.RequiresOwnerConfirmation);
        await repo.Received(1).CreateAsync(4,8,request,null,default);
    }
}
