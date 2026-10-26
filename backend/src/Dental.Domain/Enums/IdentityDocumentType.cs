namespace Dental.Domain.Enums;

/// <summary>
/// Loại giấy tờ định danh bệnh nhân (DL-360, DL-364).
/// </summary>
public enum IdentityDocumentType
{
    /// <summary>Căn cước công dân gắn chip (12 chữ số).</summary>
    CitizenId = 1,

    /// <summary>Hộ chiếu nước ngoài / ngoại quốc (kèm mã quốc tịch).</summary>
    Passport = 2
}
