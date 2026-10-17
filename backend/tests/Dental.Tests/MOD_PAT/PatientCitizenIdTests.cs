using System.Text.Json;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_PAT;
public sealed class PatientCitizenIdTests
{
    [Fact]
    public void HashMaskAndCompareFollowDl240()
    {
        const string value = "987654321098";
        var hash = CitizenIdIdentity.Hash(value);
        Assert.Equal(64, hash.Length); Assert.DoesNotContain(value, hash);
        Assert.Equal("987******098", CitizenIdIdentity.Mask(value));
        Assert.True(CitizenIdIdentity.Matches(hash, value));
        Assert.False(CitizenIdIdentity.Matches(hash, "987654321099"));
        Assert.False(CitizenIdIdentity.Matches(null, value));
        Assert.False(CitizenIdIdentity.Matches("short", value));
        var json = JsonSerializer.Serialize(new CitizenIdStatus(1, CitizenIdIdentity.Mask(value), 1));
        Assert.DoesNotContain(value, json); Assert.DoesNotContain(hash, json);
    }
    [Theory]
    [InlineData("")] [InlineData("123")] [InlineData("987654321098 ")] [InlineData("１２３４５６７８９０１２")]
    [InlineData("9876543210987")] [InlineData("98765432109a")] [InlineData(null)]
    public void InvalidCitizenIdRejected(string? value) => Assert.False(new CitizenIdRequestValidator().Validate(new CitizenIdRequest(value!, 0)).IsValid);
    [Fact]
    public void TwelveDigitsWithLeadingZeroAndVersionZeroAccepted()
    {
        Assert.True(new CitizenIdRequestValidator().Validate(new CitizenIdRequest("000000000001", 0)).IsValid);
        Assert.False(new CitizenIdRequestValidator().Validate(new CitizenIdRequest("000000000001", -1)).IsValid);
    }
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task WriteRoleGuardBeforeRepository(string? role)
    {
        var repo = Substitute.For<IPatientCitizenIdRepository>();
        var result = await new PatientCitizenIdService(repo).SaveAsync(1, 1, role, new("987654321098", 0), null);
        Assert.True(result.IsFailure);
        await repo.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default!, default!, default, default);
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task InvalidInputNeverCallsRepository(string role)
    {
        var repo = Substitute.For<IPatientCitizenIdRepository>(); var service = new PatientCitizenIdService(repo);
        Assert.True((await service.SaveAsync(1, 1, role, new("bad", 0), null)).IsFailure);
        Assert.True((await service.SaveAsync(0, 1, role, new("987654321098", 0), null)).IsFailure);
        Assert.True((await service.SaveAsync(1, 0, role, new("987654321098", 0), null)).IsFailure);
        await repo.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default!, default!, default, default);
    }
    [Fact]
    public async Task UnknownReaderRejectedBeforeRepository()
    {
        var repo = Substitute.For<IPatientCitizenIdRepository>(); var service = new PatientCitizenIdService(repo);
        Assert.True((await service.ReadAsync(1, 1, "UNKNOWN")).IsFailure);
        Assert.True((await service.ReadAsync(0, 1, RoleCodes.Admin)).IsFailure);
        await repo.DidNotReceiveWithAnyArgs().ReadAsync(default, default, default!, default);
    }
}
