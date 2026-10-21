using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Images;

public sealed class DentalImageDeletionTests
{
    private static DentalImage Image() => new() { Id = 7, PatientId = 9, UploadedByUserId = 3, Version = 1 };
    [Theory]
    [InlineData(RoleCodes.Admin, 8, true)]
    [InlineData(RoleCodes.Dentist, 3, true)]
    [InlineData(RoleCodes.Assistant, 3, true)]
    [InlineData(RoleCodes.Dentist, 8, false)]
    [InlineData(RoleCodes.Assistant, 8, false)]
    [InlineData(RoleCodes.Receptionist, 3, false)]
    [InlineData(RoleCodes.Patient, 3, false)]
    public void OwnershipUsesUploaderInsteadOfTreatingDentist(string role, int actor, bool allowed)
        => Assert.Equal(allowed, DentalImageDeletionRules.Owns(Image(), actor, role));

    [Fact]
    public void IndependentImageCanBeDeletedWithoutAVisit()
        => Assert.True(DentalImageDeletionRules.IsOpen(Image(), null, false));

    [Theory]
    [InlineData(VisitStatuses.InProgress, true)]
    [InlineData(VisitStatuses.Created, false)]
    [InlineData(VisitStatuses.Completed, false)]
    [InlineData(VisitStatuses.Cancelled, false)]
    [InlineData(VisitStatuses.Correcting, false)]
    public void Q265aAllowsOnlyAnOpenInProgressVisit(string status, bool expected)
    {
        var image = Image(); image.VisitId = 8;
        Assert.Equal(expected, DentalImageDeletionRules.IsOpen(image, new Visit { Status = status }, false));
    }

    [Fact]
    public void LockPaymentsMissingVisitAndDeletedAlwaysPreventDeletion()
    {
        var image = Image(); image.VisitId = 8; var visit = new Visit { Status = VisitStatuses.InProgress };
        Assert.False(DentalImageDeletionRules.IsOpen(image, visit, true));
        visit.IsLocked = true; Assert.False(DentalImageDeletionRules.IsOpen(image, visit, false));
        Assert.False(DentalImageDeletionRules.IsOpen(image, null, false));
        image.IsDeleted = true; image.VisitId = null; Assert.False(DentalImageDeletionRules.IsOpen(image, null, false));
        image.IsDeleted = false; image.Version = int.MaxValue; Assert.False(DentalImageDeletionRules.IsOpen(image, null, false));
    }

    [Theory]
    [InlineData(0, "reason", true, false)]
    [InlineData(int.MaxValue, "reason", true, false)]
    [InlineData(1, "", true, false)]
    [InlineData(1, "  ", true, false)]
    [InlineData(1, "a\0b", true, false)]
    [InlineData(1, "reason", false, false)]
    [InlineData(1, "reason", true, true)]
    public void RequiresBoundedVersionReasonAndConfirmation(int version, string reason, bool confirmed, bool expected)
        => Assert.Equal(expected, new DeleteDentalImageValidator().Validate(new DeleteDentalImageRequest(version, reason, confirmed)).IsValid);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void ReasonLength(int length, bool expected)
        => Assert.Equal(expected, new DeleteDentalImageValidator().Validate(new DeleteDentalImageRequest(1, new string('a', length), true)).IsValid);

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Patient)]
    [InlineData(null)]
    public async Task ForbiddenRolesNeverReachRepository(string? role)
    {
        var repo = Substitute.For<IDentalImageDeletionRepository>(); var service = new DentalImageDeletionService(repo);
        Assert.Equal(Error.Forbidden, (await service.ListAsync(1, 0, 3, role)).Error);
        Assert.Equal(Error.Forbidden, (await service.DeleteAsync(7, 3, role, new(1, "reason", true), null)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Fact]
    public async Task InvalidIdsAndPayloadNeverReachRepository()
    {
        var repo = Substitute.For<IDentalImageDeletionRepository>(); var service = new DentalImageDeletionService(repo);
        Assert.True((await service.ListAsync(0, 0, 3, RoleCodes.Admin)).IsFailure);
        Assert.True((await service.ListAsync(1, -1, 3, RoleCodes.Admin)).IsFailure);
        Assert.True((await service.DeleteAsync(7, 3, RoleCodes.Admin, new(1, "reason", false), null)).IsFailure);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Assistant)]
    public async Task AllowedRolesDelegateExactActorAndRequest(string role)
    {
        var repo = Substitute.For<IDentalImageDeletionRepository>(); var request = new DeleteDentalImageRequest(2, "reason", true);
        await new DentalImageDeletionService(repo).DeleteAsync(7, 3, role, request, "synthetic");
        await repo.Received().DeleteAsync(7, 3, role, request, "synthetic", default);
    }

    [Fact]
    public void DeletionMetadataIsNullableForLegacyImagesAndHasRestrictedActorFk()
    {
        using var db = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var type = db.Model.FindEntityType(typeof(DentalImage))!;
        Assert.True(type.FindProperty(nameof(DentalImage.DeletedAt))!.IsNullable);
        Assert.True(type.FindProperty(nameof(DentalImage.DeletedByUserId))!.IsNullable);
        Assert.Equal(500, type.FindProperty(nameof(DentalImage.DeleteReason))!.GetMaxLength());
        Assert.Equal(DeleteBehavior.Restrict, type.GetForeignKeys().Single(f => f.Properties.Single().Name == nameof(DentalImage.DeletedByUserId)).DeleteBehavior);
        // Physical-file byte quotas intentionally include soft-deleted records; no global filter.
        Assert.Empty(type.GetDeclaredQueryFilters());
    }
}
