namespace Dental.Domain.Constants;

public static class FdiCodes
{
    public static readonly string[] Conditions =
        ["CARIES", "RESTORED", "MISSING", "CROWN", "PULPITIS", "IMPLANT", "CALCULUS", "NORMAL"];

    public static bool IsTooth(int number)
        => number / 10 is >= 1 and <= 4 && number % 10 is >= 1 and <= 8
        || number / 10 is >= 5 and <= 8 && number % 10 is >= 1 and <= 5;

    public static bool IsSurface(string? surface)
    {
        if (surface is null or "All") return true;
        if (surface.Length is < 1 or > 20) return false;
        var parts = surface.Split('-');
        if (parts.Any(p => p.Length == 0)) return false;
        var letters = string.Concat(parts);
        return letters.All(c => "BLMDO".Contains(c)) && letters.Distinct().Count() == letters.Length;
    }

    public static string? NormalizeSurface(string? surface)
        => surface is null or "All" ? null : string.Concat("BLMDO".Where(surface.Contains));
}
