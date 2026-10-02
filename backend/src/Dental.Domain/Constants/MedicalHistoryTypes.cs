namespace Dental.Domain.Constants;

/// <summary>Loại mục trong bản chụp tiền sử bệnh.</summary>
public static class MedicalHistoryTypes
{
    public const string Allergy = "Allergy";
    public const string Condition = "Condition";

    public static readonly string[] All = [Allergy, Condition];
}
