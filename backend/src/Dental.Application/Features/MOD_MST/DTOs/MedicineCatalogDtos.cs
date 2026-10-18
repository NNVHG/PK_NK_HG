using System.Text.Json.Serialization;
namespace Dental.Application.Features.MOD_MST.DTOs;
public sealed record MedicineQuery(string? Keyword = null, bool IncludeInactive = false, int Page = 1, int PageSize = 20);
public sealed record MedicineWriteRequest(string Code, string Name, string? ActiveIngredient, string? Strength,
    string Unit, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal UnitPrice,
    string? DefaultUsageInstruction = null, bool IsActive = false, int ExpectedVersion = 0);
public sealed record MedicineResponse(int Id, string Code, string Name, string? ActiveIngredient, string? Strength,
    string Unit, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal UnitPrice,
    string? DefaultUsageInstruction, bool IsActive, int Version, DateTime CreatedAt, DateTime? UpdatedAt);
