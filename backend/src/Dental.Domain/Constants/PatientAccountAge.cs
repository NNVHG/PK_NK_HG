namespace Dental.Domain.Constants;

public static class PatientAccountAge
{
    public const int MinimumAge = 14;

    public static DateOnly LatestEligibleBirthDate(DateOnly today)
    {
        var cutoff = today.AddYears(-MinimumAge);
        // AddYears treats February 28 as the anniversary of February 29 in a non-leap year.
        if (today.Month == 2 && today.Day == 28 && !DateTime.IsLeapYear(today.Year) &&
            DateTime.IsLeapYear(cutoff.Year))
            return cutoff.AddDays(1);
        return cutoff;
    }

    public static bool IsUnderMinimumAge(DateOnly dateOfBirth, DateOnly today)
        => dateOfBirth > LatestEligibleBirthDate(today);
}
