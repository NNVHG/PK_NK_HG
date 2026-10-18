using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_RX;
public sealed class PatientPrescriptionHistoryTests
{
 [Theory] [InlineData(null)] [InlineData("UNKNOWN")]
 public async Task UnknownReaderNeverQueriesRepository(string? role)
 {
  var repo=Substitute.For<IPrescriptionRepository>();var svc=new PrescriptionService(repo);
  Assert.True((await svc.PatientHistoryAsync(7,3,role,new())).IsFailure);
  Assert.True((await svc.PatientHistoryDetailAsync(7,9,3,role)).IsFailure);Assert.Empty(repo.ReceivedCalls());
 }
 [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)]
 public async Task KnownReadersDelegateScopeToRepository(string role)
 {
  var repo=Substitute.For<IPrescriptionRepository>();var svc=new PrescriptionService(repo);var query=new PatientPrescriptionQuery();
  repo.PatientHistoryAsync(7,3,role,query,default).Returns(Result<PagedResult<PatientPrescriptionSummary>>.Failure(Error.NotFound));
  repo.PatientHistoryDetailAsync(7,9,3,role,default).Returns(Result<PatientPrescriptionDetail>.Failure(Error.NotFound));
  Assert.Equal(Error.NotFound,(await svc.PatientHistoryAsync(7,3,role,query)).Error);
  Assert.Equal(Error.NotFound,(await svc.PatientHistoryDetailAsync(7,9,3,role)).Error);
  await repo.Received().PatientHistoryDetailAsync(7,9,3,role,default);
 }
 [Theory] [InlineData(0,10)] [InlineData(-1,10)] [InlineData(1000001,10)] [InlineData(1,0)] [InlineData(1,51)]
 public void InvalidPageBounds(int page,int size) => Assert.False(new PatientPrescriptionQueryValidator().Validate(new PatientPrescriptionQuery(page,size)).IsValid);
 [Fact] public async Task InvalidTargetsAndActorsNeverDelegate()
 {
  var repo=Substitute.For<IPrescriptionRepository>();var svc=new PrescriptionService(repo);
  Assert.True((await svc.PatientHistoryAsync(0,3,RoleCodes.Admin,new())).IsFailure);
  Assert.True((await svc.PatientHistoryAsync(7,0,RoleCodes.Admin,new())).IsFailure);
  Assert.True((await svc.PatientHistoryDetailAsync(7,0,3,RoleCodes.Dentist)).IsFailure);
  Assert.True((await svc.PatientHistoryDetailAsync(-1,9,3,RoleCodes.Dentist)).IsFailure);Assert.Empty(repo.ReceivedCalls());
 }
}
