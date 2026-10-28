export type CheckInField = 'citizenId' | 'nationalityCode' | 'phone' | 'notes';
export type CheckInErrors = Partial<Record<CheckInField, string>>;

export function validateCheckIn(citizenId: string, phone: string, notes: string, documentType: 'citizenId' | 'passport' = 'citizenId', nationalityCode = ''): CheckInErrors {
  const errors: CheckInErrors = {};
  if (documentType === 'passport') {
    if (!/^[A-Za-z0-9]{6,12}$/.test(citizenId.trim())) errors.citizenId = 'Số hộ chiếu phải gồm 6–12 chữ cái hoặc chữ số.';
    if (!/^[A-Za-z]{2,3}$/.test(nationalityCode.trim())) errors.nationalityCode = 'Mã quốc gia cấp hộ chiếu phải gồm 2–3 chữ cái.';
  } else if (!/^[0-9]{12}$/.test(citizenId.trim())) errors.citizenId = 'CCCD phải gồm 12 chữ số để đối chiếu tại quầy.';
  if (!/^0[0-9]{9}$/.test(phone.trim())) errors.phone = 'Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.';
  if (notes.length > 500) errors.notes = 'Ghi chú không được vượt quá 500 ký tự.';
  return errors;
}

export function checkInProblem(problem: unknown): { fields: CheckInErrors; message: string } {
  const fields: CheckInErrors = {};
  const fallback = 'Không thể tiếp đón. Vui lòng tải lại và đối chiếu trạng thái lượt khám.';
  if (!problem || typeof problem !== 'object') return { fields, message: fallback };
  const data = problem as { message?: unknown; errors?: unknown };
  const messages: string[] = [];
  function add(key: string, value: unknown) {
    if (typeof value !== 'string' || !value.trim()) return;
    messages.push(value);
    const field = key.toLowerCase();
    if (field === 'citizenid') fields.citizenId ??= value;
    if (field === 'nationalitycode') fields.nationalityCode ??= value;
    if (field === 'phone') fields.phone ??= value;
    if (field === 'notes') fields.notes ??= value;
  }
  if (Array.isArray(data.errors)) {
    for (const error of data.errors) {
      if (error && typeof error === 'object') add(String(error.field ?? ''), error.message);
    }
  } else if (data.errors && typeof data.errors === 'object') {
    for (const [key, errors] of Object.entries(data.errors)) {
      for (const message of Array.isArray(errors) ? errors : [errors]) add(key, message);
    }
  }
  return { fields, message: messages.length ? Array.from(new Set(messages)).join(' ') : (typeof data.message === 'string' ? data.message : fallback) };
}
