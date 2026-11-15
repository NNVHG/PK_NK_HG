export function isUnderFourteen(dateOfBirth: string, today = new Date().toLocaleDateString('sv-SE', {
  timeZone: 'Asia/Ho_Chi_Minh'
})): boolean {
  const birthDate = dateOfBirth.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(birthDate) || !/^\d{4}-\d{2}-\d{2}$/.test(today)) return false;

  const [year, month, day] = birthDate.split('-').map(Number);
  const adultYear = year + 14;
  const lastDay = new Date(Date.UTC(adultYear, month, 0)).getUTCDate();
  const anniversary = `${String(adultYear).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(Math.min(day, lastDay)).padStart(2, '0')}`;
  return today < anniversary;
}
