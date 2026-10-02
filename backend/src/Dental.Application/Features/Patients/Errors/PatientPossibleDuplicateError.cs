using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;

namespace Dental.Application.Features.Patients.Errors;

public sealed record PatientPossibleDuplicateError(IReadOnlyList<PatientDuplicateInfo> Duplicates)
    : Error("PATIENT_POSSIBLE_DUPLICATE", "Có hồ sơ bệnh nhân có thể trùng. Vui lòng kiểm tra trước khi tiếp tục.");
