using System.Globalization;

namespace Dental.Application.Features.Patient;

/// <summary>Chuyển mã hiển thị BN###### thành số bệnh nhân dùng trong truy vấn.</summary>
public static class PatientCodeParser
{
    public static bool TryParse(string? patientCode, out int patientNumber)
    {
        patientNumber = 0;
        if (patientCode is null)
            return false;

        var code = patientCode.Trim();
        if (code.Length != 8 || !code.StartsWith("BN", StringComparison.Ordinal))
            return false;

        return int.TryParse(
                   code.AsSpan(2),
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out patientNumber)
               && patientNumber > 0;
    }

    public static bool TryParseSearchRange(string? keyword, out int minimum, out int maximum)
    {
        minimum = 0;
        maximum = 0;
        var code = keyword?.Trim();
        if (code is null || !code.StartsWith("BN", StringComparison.OrdinalIgnoreCase))
            return false;

        var digits = code.AsSpan(2);
        if (digits.Length > 6 || !AllDigits(digits))
            return false;

        if (digits.Length == 0)
        {
            minimum = 1;
            maximum = 999999;
            return true;
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var prefix))
            return false;

        var multiplier = (int)Math.Pow(10, 6 - digits.Length);
        minimum = Math.Max(1, prefix * multiplier);
        maximum = prefix * multiplier + multiplier - 1;
        return minimum <= maximum;
    }

    private static bool AllDigits(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
                return false;
        }

        return true;
    }
}
