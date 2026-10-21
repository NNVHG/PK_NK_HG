using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_IMG;

public sealed class DentalImageClinicalTests
{
    private static ImageClinicalState State(string status = VisitStatuses.InProgress) => new(new DentalImage { Id = 7, VisitId = 8, Version = 1 }, new Visit { VisitId = 8, DentistId = 3, Status = status }, false);
    [Theory]
    [InlineData(VisitStatuses.InProgress)]
    [InlineData(VisitStatuses.Correcting)]
    public void OpenClinicalModesAllowReplace(string status) { var s = State(status); Assert.True(ImageClinicalRules.MayReplace(s)); Assert.False(ImageClinicalRules.MayAppend(s)); }
    [Theory]
    [InlineData(VisitStatuses.Created)]
    [InlineData(VisitStatuses.Cancelled)]
    public void OtherModesNeverAllowClinicalWrite(string status) { var s = State(status); Assert.False(ImageClinicalRules.MayReplace(s)); Assert.False(ImageClinicalRules.MayAppend(s)); }
    [Fact]
    public void CompletedAllowsAppendOnlyEvenAfterPayment()
    { var s = State(VisitStatuses.Completed) with { HasPayments = true }; s.Visit!.IsLocked = true; Assert.False(ImageClinicalRules.MayReplace(s)); Assert.True(ImageClinicalRules.MayAppend(s)); }
    [Fact]
    public void LockedOrPaymentHistoryBlocksOriginalOverwrite()
    { var s = State(); Assert.False(ImageClinicalRules.MayReplace(s with { HasPayments = true })); s.Visit!.IsLocked = true; Assert.False(ImageClinicalRules.MayReplace(s)); }
    [Fact]
    public void Q336aOnlyAdminOwnsUnattachedClinicalRecord()
    { var s = State() with { Visit = null }; s.Image.VisitId = null; Assert.True(ImageClinicalRules.MayReplace(s)); Assert.True(ImageClinicalRules.Owns(s, 1, RoleCodes.Admin)); Assert.False(ImageClinicalRules.Owns(s, 3, RoleCodes.Dentist)); Assert.False(ImageClinicalRules.MayAppend(s)); }
    [Fact]
    public void OwnershipDoesNotFollowUploaderOrReaderRole()
    { var s = State(); s.Image.UploadedByUserId = 4; Assert.True(ImageClinicalRules.Owns(s, 3, RoleCodes.Dentist)); Assert.False(ImageClinicalRules.Owns(s, 4, RoleCodes.Dentist)); Assert.False(ImageClinicalRules.Owns(s, 3, RoleCodes.Assistant)); Assert.False(ImageClinicalRules.Owns(s, 3, RoleCodes.Receptionist)); }
    [Fact]
    public void MissingLinkedVisitDeletedAndVersionExhaustionCannotWrite()
    { var s = State() with { Visit = null }; Assert.False(ImageClinicalRules.MayReplace(s)); s = State(); s.Image.IsDeleted = true; Assert.False(ImageClinicalRules.MayReplace(s)); Assert.False(ImageClinicalRules.MayAppend(s)); s = State(VisitStatuses.Completed); s.Image.Version = int.MaxValue; Assert.False(ImageClinicalRules.MayAppend(s)); }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void VersionBoundaries(int version)
    { Assert.False(new ImageClinicalWriteValidator().Validate(new ImageClinicalWriteRequest(version, null, null, true)).IsValid); Assert.False(new ImageClinicalAppendValidator().Validate(new ImageClinicalAppendRequest(version, "note", Guid.NewGuid(), true)).IsValid); }
    [Fact]
    public void OriginalTextBoundsClearAndConfirmation()
    { var v = new ImageClinicalWriteValidator(); Assert.True(v.Validate(new ImageClinicalWriteRequest(1, null, null, true)).IsValid); Assert.True(v.Validate(new ImageClinicalWriteRequest(1, new string('x', 1000), new string('x', 255), true)).IsValid); foreach (var r in new[] { new ImageClinicalWriteRequest(1, "n", "d", false), new(1, new string('x', 1001), null, true), new(1, null, new string('x', 256), true), new(1, "\0", null, true) }) Assert.False(v.Validate(r).IsValid); }
    [Fact]
    public void AppendRequiresNonblankBoundedNoteRequestIdAndConfirmation()
    { var v = new ImageClinicalAppendValidator(); var r = new ImageClinicalAppendRequest(1, "note", Guid.NewGuid(), true); Assert.True(v.Validate(r).IsValid); foreach (var bad in new[] { r with { Note = " " }, r with { Note = new string('x', 1001) }, r with { Note = "\0" }, r with { Confirmed = false }, r with { RequestId = Guid.Empty } }) Assert.False(v.Validate(bad).IsValid); }
    [Theory]
    [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Patient)]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(null)]
    public async Task NonClinicalRolesNeverReachWriteRepository(string? role)
    { var repo = Substitute.For<IDentalImageClinicalRepository>(); var service = new DentalImageClinicalService(repo); Assert.Equal(Error.Forbidden, (await service.ReplaceAsync(7, 3, role, new(1, null, null, true), null)).Error); Assert.Equal(Error.Forbidden, (await service.AppendAsync(7, 3, role, new(1, "note", Guid.NewGuid(), true), null)).Error); Assert.Empty(repo.ReceivedCalls()); }
    [Fact]
    public async Task InvalidHistoryAndPayloadNeverReachRepository()
    { var repo = Substitute.For<IDentalImageClinicalRepository>(); var service = new DentalImageClinicalService(repo); Assert.True((await service.HistoryAsync(7, 3, RoleCodes.Dentist, 0, 20)).IsFailure); Assert.True((await service.HistoryAsync(7, 3, RoleCodes.Dentist, 1, 51)).IsFailure); Assert.True((await service.ReplaceAsync(7, 3, RoleCodes.Admin, new(1, null, null, false), null)).IsFailure); Assert.Empty(repo.ReceivedCalls()); }
    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Dentist)]
    public async Task ExactWriteDelegatesWithReasonableExistingPolicy(string role)
    { var repo = Substitute.For<IDentalImageClinicalRepository>(); var r = new ImageClinicalWriteRequest(2, "note", "diagnosis", true); await new DentalImageClinicalService(repo).ReplaceAsync(7, 3, role, r, "synthetic"); await repo.Received().ReplaceAsync(7, 3, role, r, "synthetic", default); }
    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task AppendedClinicalRecordsCannotBeMutated(EntityState state)
    { await using var db = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=localhost;Database=unused;Username=unused").Options); db.Entry(new DentalImageClinicalAddendum { Id = 1 }).State = state; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); }
}
