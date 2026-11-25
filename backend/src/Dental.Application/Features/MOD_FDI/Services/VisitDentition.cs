using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_FDI.Services;

internal static class VisitDentition
{
    private static readonly VietnamClock Clock = new(TimeProvider.System);

    internal static readonly Error ToothOutsideDentition = new("FDI_007", "Răng đã chọn không thuộc bộ răng của bệnh nhân trong lần khám này.");

    internal static bool Contains(Visit visit, DateOnly dateOfBirth, int toothNumber)
    {
        var visitDate = DateOnly.FromDateTime(Clock.ToVietnamTime(visit.StartedAt ?? visit.CreatedAt));
        var isChild = PatientAccountAge.IsUnderMinimumAge(dateOfBirth, visitDate);
        return FdiCodes.IsTooth(toothNumber) && (toothNumber >= 50) == isChild;
    }
}
