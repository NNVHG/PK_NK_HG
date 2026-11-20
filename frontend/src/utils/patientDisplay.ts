export function patientGenderValue(value: string | null): string {
  switch (value) {
    case 'Nam':
    case 'Male': return 'Male';
    case 'Nữ':
    case 'Female': return 'Female';
    case 'Khác':
    case 'Other': return 'Other';
    default: return '';
  }
}

export function formatPatientGender(value: string | null): string {
  switch (patientGenderValue(value)) {
    case 'Male': return 'Nam';
    case 'Female': return 'Nữ';
    case 'Other': return 'Khác';
    default: return 'Chưa cập nhật';
  }
}

export function isDemoPatient(name: string): boolean {
  return name.endsWith(' (DEMO)');
}

export function patientDisplayName(name: string): string {
  if (!isDemoPatient(name)) return name;
  const nameWithoutMarker = name.slice(0, -' (DEMO)'.length);
  return nameWithoutMarker === nameWithoutMarker.toLocaleUpperCase('vi')
    ? nameWithoutMarker.toLocaleLowerCase('vi').replace(/(^|[\s-])\p{L}/gu, char => char.toLocaleUpperCase('vi'))
    : nameWithoutMarker;
}

export function patientDisplayAddress(name: string, address: string | null): string {
  if (isDemoPatient(name) && address === 'Địa chỉ kiểm thử, không dùng liên hệ') return '';
  return address?.trim() || '';
}
