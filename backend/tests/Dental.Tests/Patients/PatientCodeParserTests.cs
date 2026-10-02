using Dental.Application.Features.Patient;
using Dental.Domain.Entities;
using Xunit;

namespace Dental.Tests.Patients;

public sealed class PatientCodeParserTests
{
    [Theory]
    [InlineData(1, "BN000001")]
    [InlineData(42, "BN000042")]
    [InlineData(999999, "BN999999")]
    public void PatientCode_FormatsNumberWithPrefixAndSixDigits(int patientNumber, string expected)
    {
        var patient = new Patient { PatientNumber = patientNumber };

        Assert.Equal(expected, patient.PatientCode);
    }

    [Theory]
    [InlineData("BN000001", 1)]
    [InlineData("BN000042", 42)]
    [InlineData("BN999999", 999999)]
    [InlineData(" BN000042 ", 42)]
    public void TryParse_ValidPatientCode_ReturnsPatientNumber(string patientCode, int expected)
    {
        var parsed = PatientCodeParser.TryParse(patientCode, out var patientNumber);

        Assert.True(parsed);
        Assert.Equal(expected, patientNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BN000000")]
    [InlineData("BN00001")]
    [InlineData("BN0000001")]
    [InlineData("bn000001")]
    [InlineData("BN00A001")]
    [InlineData("PT000001")]
    public void TryParse_InvalidPatientCode_ReturnsFalse(string? patientCode)
    {
        var parsed = PatientCodeParser.TryParse(patientCode, out var patientNumber);

        Assert.False(parsed);
        Assert.Equal(0, patientNumber);
    }

    [Theory]
    [InlineData("BN", 1, 999999)]
    [InlineData("bn0001", 100, 199)]
    [InlineData("BN000042", 42, 42)]
    [InlineData("BN99999", 999990, 999999)]
    public void TryParseSearchRange_ValidCodePrefix_ReturnsPatientNumberRange(
        string keyword,
        int expectedMinimum,
        int expectedMaximum)
    {
        var parsed = PatientCodeParser.TryParseSearchRange(keyword, out var minimum, out var maximum);

        Assert.True(parsed);
        Assert.Equal(expectedMinimum, minimum);
        Assert.Equal(expectedMaximum, maximum);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BN1234567")]
    [InlineData("BN12A")]
    [InlineData("PAT001")]
    public void TryParseSearchRange_InvalidPrefix_ReturnsFalse(string? keyword)
    {
        var parsed = PatientCodeParser.TryParseSearchRange(keyword, out var minimum, out var maximum);

        Assert.False(parsed);
        Assert.Equal(0, minimum);
        Assert.Equal(0, maximum);
    }
}
