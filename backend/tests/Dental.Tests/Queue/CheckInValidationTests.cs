using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Validators;
using Xunit;

namespace Dental.Tests.Queue;

public sealed class CheckInValidationTests
{
    private static CheckInRequest Request(string? notes) => new(1, Notes: notes, CitizenId: "000000000101", Phone: "0900000091");

    [Theory][InlineData(null)][InlineData("")][InlineData("   ")]
    public void OptionalReceptionNotesMayBeEmpty(string? notes)
        => Assert.True(new CheckInRequestValidator().Validate(Request(notes)).IsValid);

    [Theory][InlineData(500, true)][InlineData(501, false)]
    public void ReceptionNotesKeepTheirLengthLimit(int length, bool valid)
    {
        var result = new CheckInRequestValidator().Validate(Request(new string('x', length)));
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.All(result.Errors, e => Assert.Equal("Notes", e.PropertyName));
    }

    [Fact] public void BlankNotesDoNotHideMissingIdentityFields()
    {
        var result = new CheckInRequestValidator().Validate(new CheckInRequest(1, Notes: ""));
        Assert.Equal(new[] { "CitizenId", "Phone" }, result.Errors.Select(e => e.PropertyName));
        Assert.All(result.Errors, e => Assert.StartsWith("Vui lòng nhập", e.ErrorMessage));
    }

    [Theory][InlineData("123", "0900000091", "CitizenId")][InlineData("000000000101", "123", "Phone")]
    public void MalformedIdentityHasOneVietnameseFieldError(string citizen, string phone, string field)
    {
        var result = new CheckInRequestValidator().Validate(Request(null) with { CitizenId = citizen, Phone = phone });
        var error = Assert.Single(result.Errors); Assert.Equal(field, error.PropertyName);
        Assert.Contains("chữ số", error.ErrorMessage);
    }
}
