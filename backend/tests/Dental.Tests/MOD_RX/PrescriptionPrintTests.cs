using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Features.Staff.DTOs;
using Dental.Application.Features.Staff.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_RX;
public sealed class PrescriptionPrintTests
{
    [Theory] [InlineData(null)] [InlineData("UNKNOWN")]
    public async Task UnknownPrintActorNeverQueriesRepository(string? role)
    {
        var repo=Substitute.For<IPrescriptionRepository>();Assert.Equal(Error.Forbidden,(await new PrescriptionService(repo).PreparePrintAsync(7,9,3,role,new(1))).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)]
    public async Task PrintReadersDelegateOwnershipToRepository(string role)
    {
        var repo=Substitute.For<IPrescriptionRepository>();var request=new PrescriptionPrintRequest(2);repo.PreparePrintAsync(7,9,3,role,request,null,default).Returns(Result<PrescriptionPrintDocument>.Failure(Error.NotFound));
        Assert.Equal(Error.NotFound,(await new PrescriptionService(repo).PreparePrintAsync(7,9,3,role,request)).Error);await repo.Received().PreparePrintAsync(7,9,3,role,request,null,default);
    }
    [Fact] public async Task InvalidPrintTargetOrVersionNeverReachesRepository()
    {
        var repo=Substitute.For<IPrescriptionRepository>();var service=new PrescriptionService(repo);
        foreach(var ids in new[] { (0,9,3,1),(7,0,3,1),(7,9,0,1),(7,9,3,0),(7,9,3,-1) })Assert.True((await service.PreparePrintAsync(ids.Item1,ids.Item2,ids.Item3,RoleCodes.Dentist,new(ids.Item4))).IsFailure);
        Assert.Empty(repo.ReceivedCalls());Assert.True(new PrescriptionPrintValidator().Validate(new PrescriptionPrintRequest(int.MaxValue)).IsValid);
    }
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task OnlyAdminManagesClinicPrintSettings(string? role)
    {
        var repo=Substitute.For<IClinicPrintSettingsRepository>();var service=new ClinicPrintSettingsService(repo);
        Assert.Equal(Error.Forbidden,(await service.ReadAsync(3,role)).Error);Assert.Equal(Error.Forbidden,(await service.SaveAsync(3,role,new("Synthetic address","0123456789",new string('A',64)),null)).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData("", "0123456789")] [InlineData(" ","0123456789")] [InlineData("Address\nInjected","0123456789")] [InlineData("Address","short")] [InlineData("Address","<script>")]
    public void InvalidClinicSettingsRejected(string address,string phone) => Assert.False(new ClinicPrintSettingsValidator().Validate(new ClinicPrintSettingsRequest(address,phone,new string('A',64))).IsValid);
    [Fact] public void ClinicBoundsRevisionAndValidFreeText()
    {
        var validator=new ClinicPrintSettingsValidator();Assert.True(validator.Validate(new ClinicPrintSettingsRequest("Synthetic address","+84 901 234 567",new string('A',64))).IsValid);
        Assert.False(validator.Validate(new ClinicPrintSettingsRequest(new string('x',501),"0123456789",new string('A',64))).IsValid);
        Assert.False(validator.Validate(new ClinicPrintSettingsRequest("Address","0123456789","revision")).IsValid);
    }
    [Theory] [InlineData(null,true)] [InlineData("",true)] [InlineData("Synthetic approved title",true)] [InlineData("title\nInjected",false)]
    public void StaffAcademicTitleValidatedWithoutInventingDefault(string? title,bool valid)
    {
        var create=new CreateStaffRequest("Synthetic doctor","0999999991",null,RoleCodes.Dentist,null,null,null,title);
        var update=new UpdateStaffRequest("Synthetic doctor","0999999991",null,null,null,RoleCodes.Dentist,title);
        Assert.Equal(valid,new CreateStaffRequestValidator().Validate(create).IsValid);Assert.Equal(valid,new UpdateStaffRequestValidator().Validate(update).IsValid);
    }
    [Fact] public void AcademicTitleMaximum100()
    {
        var request=new UpdateStaffRequest("Synthetic doctor","0999999991",null,null,null,RoleCodes.Dentist,new string('x',101));Assert.False(new UpdateStaffRequestValidator().Validate(request).IsValid);
    }
}
