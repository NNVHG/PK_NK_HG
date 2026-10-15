namespace Dental.Domain.Constants;

public sealed record DentalConditionServiceSuggestion(string ServiceCode, string Label);

/// <summary>Static Phase 1 suggestions approved by DL-188 and DL-192.</summary>
public static class DentalConditionServiceMappings
{
    private static readonly IReadOnlyDictionary<string, DentalConditionServiceSuggestion[]> Mapping =
        new Dictionary<string, DentalConditionServiceSuggestion[]>(StringComparer.Ordinal)
        {
            ["CARIES"] = [new("SERVICE_FILL_COMPOSITE", "Trám răng Composite")],
            ["PULPITIS"] = [new("SERVICE_ROOT_CANAL", "Điều trị nội nha")],
            ["CALCULUS"] = [new("SERVICE_SCALING", "Cạo vôi răng và đánh bóng")],
            ["GINGIVITIS"] = [new("SERVICE_SCALING", "Cạo vôi răng và đánh bóng")],
            ["PERIODONTITIS"] = [new("SERVICE_PERIODONTAL", "Nạo túi nha chu")],
            ["IMPACTED"] = [new("SERVICE_EXTRACT_SURGICAL", "Nhổ răng tiểu phẫu")]
        };

    public static bool TryGet(string conditionCode, out IReadOnlyList<DentalConditionServiceSuggestion> suggestions)
    {
        if (Mapping.TryGetValue(conditionCode, out var found))
        {
            suggestions = found;
            return true;
        }

        suggestions = Array.Empty<DentalConditionServiceSuggestion>();
        return false;
    }
}
