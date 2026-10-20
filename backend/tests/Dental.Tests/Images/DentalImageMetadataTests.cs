using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Images;

public sealed class DentalImageMetadataTests
{
    [Theory]
    [InlineData("Panorama", null, true)]
    [InlineData("Periapical", 11, true)]
    [InlineData("Bitewing", 85, true)]
    [InlineData("Cephalometric", 48, true)]
    [InlineData("Intraoral", 51, true)]
    [InlineData("Other", 11, false)]
    [InlineData("Panoramic", 11, false)]
    [InlineData("Intraoral", 19, false)]
    [InlineData("Intraoral", 56, false)]
    [InlineData("Intraoral", 0, false)]
    public void UsesApprovedCategoriesAndFdi(string category, int? tooth, bool expected)
        => Assert.Equal(expected, new UpdateDentalImageMetadataValidator().Validate(new UpdateDentalImageMetadataRequest(1, category, tooth, null, true)).IsValid);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void NotesBoundaries(int length, bool expected)
        => Assert.Equal(expected, new UpdateDentalImageMetadataValidator().Validate(new UpdateDentalImageMetadataRequest(1, "Intraoral", null, new string('x', length), true)).IsValid);

    [Fact]
    public void NullAndBlankNotesCanClearButNulIsRejected()
    {
        var v = new UpdateDentalImageMetadataValidator();
        foreach (var notes in new string?[] { null, "", "  " }) Assert.True(v.Validate(new UpdateDentalImageMetadataRequest(1, "Intraoral", null, notes, true)).IsValid);
        Assert.False(v.Validate(new UpdateDentalImageMetadataRequest(1, "Intraoral", null, "a\0b", true)).IsValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(1, false)]
    public void RequiresExactVersionAndConfirmation(int version, bool confirmed)
        => Assert.False(new UpdateDentalImageMetadataValidator().Validate(new UpdateDentalImageMetadataRequest(version, "Intraoral", null, null, confirmed)).IsValid);

    [Theory]
    [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Patient)]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(null)]
    public async Task NonClinicalRolesCannotReachRepository(string? role)
    {
        var repo = Substitute.For<IDentalImageMetadataRepository>(); var service = new DentalImageMetadataService(repo);
        Assert.Equal(Error.Forbidden, (await service.ReadAsync(7, 3, role)).Error);
        Assert.Equal(Error.Forbidden, (await service.UpdateAsync(7, 3, role, new(1, "Intraoral", null, null, true), null)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Fact]
    public async Task InvalidPayloadNeverReachesRepository()
    {
        var repo = Substitute.For<IDentalImageMetadataRepository>(); var service = new DentalImageMetadataService(repo);
        Assert.True((await service.ReadAsync(0, 3, RoleCodes.Admin)).IsFailure);
        Assert.True((await service.UpdateAsync(7, 3, RoleCodes.Admin, new(1, "Other", null, null, true), null)).IsFailure);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Dentist)]
    public async Task ExactActorVersionAndPayloadAreDelegated(string role)
    {
        var repo = Substitute.For<IDentalImageMetadataRepository>(); var request = new UpdateDentalImageMetadataRequest(4, "Panorama", null, "note", true);
        await new DentalImageMetadataService(repo).UpdateAsync(7, 3, role, request, "synthetic");
        await repo.Received().UpdateAsync(7, 3, role, request, "synthetic", default);
    }

    [Fact]
    public void MutationDtoDoesNotAcceptPatientVisitFileOrClinicalFields()
    {
        Assert.Equal(new[] { "Category", "Confirmed", "ExpectedVersion", "Notes", "ToothNumber" }, typeof(UpdateDentalImageMetadataRequest).GetProperties().Select(p => p.Name).Order().ToArray());
        Assert.DoesNotContain(typeof(DentalImageMetadataContext).GetProperties(), p => p.Name is "ClinicalNotes" or "StoragePath");
    }
}
