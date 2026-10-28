namespace Dental.Domain.Constants;

/// <summary>
/// Các hằng số và regex chuẩn định danh giấy tờ cá nhân (DL-360, DL-364).
/// </summary>
public static class IdentityDocumentConstants
{
    public const string CitizenIdRegex = @"^[0-9]{12}$";
    public const string PassportNumberRegex = @"^[A-Za-z0-9]{6,12}$";
    public const string NationalityCodeRegex = @"^[A-Za-z]{2,3}$";

    public const string DefaultCountryCode = "VN";
    public const int CurrentHmacKeyVersion = 1;
}
