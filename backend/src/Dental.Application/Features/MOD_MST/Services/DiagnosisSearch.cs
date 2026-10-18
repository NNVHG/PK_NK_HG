using System.Globalization;
using System.Text;
namespace Dental.Application.Features.MOD_MST.Services;
public static class DiagnosisSearch
{
    public static string Normalize(string text) => string.Concat(text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);
}
