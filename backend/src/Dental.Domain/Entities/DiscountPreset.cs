using System.Text.Json;

namespace Dental.Domain.Entities;

/// <summary>MOD_BIL: mẫu giảm giá do Admin cấu hình (DL-119), lưu trong SystemConfigs.</summary>
public sealed class DiscountPreset
{
    public const string ConfigKeyPrefix = "Billing:DiscountPreset:";
    public const string ConfigDataType = "DiscountPreset";

    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public bool IsActive { get; set; } = true;

    public static string BuildConfigKey(string code) => $"{ConfigKeyPrefix}{code}";

    public static bool IsDiscountPresetConfig(SystemConfig config)
        => config.DataType == ConfigDataType &&
           config.Key.StartsWith(ConfigKeyPrefix, StringComparison.Ordinal);

    public static DiscountPreset FromSystemConfig(SystemConfig config)
    {
        if (!IsDiscountPresetConfig(config))
            throw new InvalidOperationException("SystemConfig không thuộc danh mục mẫu giảm giá.");

        var payload = JsonSerializer.Deserialize<DiscountPresetPayload>(config.Value)
            ?? throw new InvalidOperationException("Dữ liệu mẫu giảm giá không hợp lệ.");

        return new DiscountPreset
        {
            Id = config.Id,
            Code = string.IsNullOrWhiteSpace(payload.Code) ? config.Key[ConfigKeyPrefix.Length..] : payload.Code,
            Name = payload.Name,
            DiscountType = payload.DiscountType,
            Value = payload.Value,
            IsActive = payload.IsActive,
        };
    }

    public void ApplyToSystemConfig(SystemConfig config, int? updatedByUserId = null, DateTime? updatedAt = null)
    {
        config.Key = BuildConfigKey(Code);
        config.DataType = ConfigDataType;
        config.Description = Name;
        config.Value = JsonSerializer.Serialize(new DiscountPresetPayload(Code, Name, DiscountType, Value, IsActive));
        config.UpdatedByUserId = updatedByUserId;
        config.UpdatedAt = updatedAt;
    }

    private sealed record DiscountPresetPayload(string Code, string Name, string DiscountType, decimal Value, bool IsActive);
}
