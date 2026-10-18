using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_RX;
public sealed class PrescriptionHandoverTests
{
 [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Dentist)] [InlineData(null)] [InlineData("UNKNOWN")]
 public async Task NonWritersNeverReachRepository(string? role)
 {
  var repo=Substitute.For<IPrescriptionHandoverRepository>();var svc=new PrescriptionHandoverService(repo);
  Assert.True((await svc.DispenseAsync(1,3,role,new(1,true))).IsFailure);
  Assert.True((await svc.UndoAsync(1,3,role,new(1,true,"Synthetic reason"))).IsFailure);Assert.Empty(repo.ReceivedCalls());
 }
 [Theory] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)]
 public async Task NonAdminCannotOverrideExpiry(string role)
 {
  var repo=Substitute.For<IPrescriptionHandoverRepository>();
  Assert.True((await new PrescriptionHandoverService(repo).DispenseAsync(1,3,role,new(1,true,OverrideExpired:true,OverrideReason:"Synthetic reason"))).IsFailure);Assert.Empty(repo.ReceivedCalls());
 }
 [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(int.MaxValue)]
 public void InvalidVersions(int version)
 { Assert.False(new PrescriptionDispenseValidator().Validate(new PrescriptionDispenseRequest(version,true)).IsValid);Assert.False(new PrescriptionUndoDispenseValidator().Validate(new PrescriptionUndoDispenseRequest(version,true,"Synthetic reason")).IsValid); }
 [Theory] [InlineData("")] [InlineData("    ")] [InlineData("tiny")] [InlineData("bad\nreason")]
 public void InvalidUndoReason(string reason) => Assert.False(new PrescriptionUndoDispenseValidator().Validate(new PrescriptionUndoDispenseRequest(1,true,reason)).IsValid);
 [Fact] public void ConfirmationAndReceiverValidation()
 {
  var v=new PrescriptionDispenseValidator();Assert.False(v.Validate(new PrescriptionDispenseRequest(1,false)).IsValid);
  Assert.False(v.Validate(new PrescriptionDispenseRequest(1,true,ReceiverName:"Hidden")).IsValid);
  Assert.False(v.Validate(new PrescriptionDispenseRequest(1,true,true,"Synthetic","bad")).IsValid);
  Assert.False(v.Validate(new PrescriptionDispenseRequest(1,true,true,new string('x',101))).IsValid);
  Assert.True(v.Validate(new PrescriptionDispenseRequest(1,true,true,"Synthetic","0990000123")).IsValid);
  Assert.True(v.Validate(new PrescriptionDispenseRequest(1,true,true)).IsValid); // Server applies age-specific required fields.
  Assert.False(v.Validate(new PrescriptionDispenseRequest(1,true,OverrideExpired:true)).IsValid);
  Assert.True(v.Validate(new PrescriptionDispenseRequest(1,true,OverrideExpired:true,OverrideReason:"Synthetic reason")).IsValid);
  Assert.False(new PrescriptionUndoDispenseValidator().Validate(new PrescriptionUndoDispenseRequest(1,true,new string('x',501))).IsValid);
 }
 [Fact] public async Task InvalidTargetsNeverDelegate()
 {
  var repo=Substitute.For<IPrescriptionHandoverRepository>();var svc=new PrescriptionHandoverService(repo);
  Assert.True((await svc.ReadAsync(0,3,RoleCodes.Admin)).IsFailure);
  Assert.True((await svc.DispenseAsync(1,0,RoleCodes.Admin,new(1,true))).IsFailure);
  Assert.True((await svc.UndoAsync(-1,3,RoleCodes.Admin,new(1,true,"Synthetic reason"))).IsFailure);Assert.Empty(repo.ReceivedCalls());
 }
}
