const currency = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' });
const compact = new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 });
const dateDisplay = { timeZone: 'UTC' } as const;
const shortDate = new Intl.DateTimeFormat('vi-VN', { ...dateDisplay, day: '2-digit', month: '2-digit' });
const longDate = new Intl.DateTimeFormat('vi-VN', { ...dateDisplay, dateStyle: 'full' });
const weekday = new Intl.DateTimeFormat('vi-VN', { ...dateDisplay, weekday: 'short' });
const vietnamDate = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short' });

export interface RevenueChartDay {
  date: string;
  revenue: number | null;
  transactionCount: number | null;
}

export function recentRevenueDates(now = new Date()): string[] {
  const date = new Date(vietnamDate.format(now) + 'T00:00:00Z');
  date.setUTCDate(date.getUTCDate() - 6);
  return Array.from({ length: 7 }, () => {
    const key = date.toISOString().slice(0, 10);
    date.setUTCDate(date.getUTCDate() + 1);
    return key;
  });
}

export const formatRevenue = (value: number) => currency.format(value);
export const formatRevenueTick = (value: number) => compact.format(value);

export function revenueDateLabels(date: string) {
  const day = new Date(date + 'T00:00:00Z');
  return { short: shortDate.format(day), long: longDate.format(day), weekday: weekday.format(day) };
}

export function createRevenueScale(values: (number | null)[]) {
  let lowest = 0;
  let highest = 0;
  for (const value of values) {
    if (value === null || !Number.isFinite(value)) continue;
    lowest = Math.min(lowest, value);
    highest = Math.max(highest, value);
  }

  const roughStep = Math.max(1, (highest - lowest) / 4);
  const power = 10 ** Math.floor(Math.log10(roughStep));
  const step = ([1, 2, 5, 10].find(factor => roughStep <= factor * power) ?? 10) * power;
  const lowerTick = Math.floor(lowest / step);
  const upperTick = Math.max(lowerTick + 1, Math.ceil(highest / step));
  const min = lowerTick * step;
  const max = upperTick * step;
  const position = (value: number) => (value - min) / (max - min) * 100;
  const ticks = Array.from({ length: upperTick - lowerTick + 1 }, (_, index) => (lowerTick + index) * step);
  const zero = position(0);

  return {
    min, max, zero, ticks, position,
    bar(value: number) {
      const end = position(value);
      return { start: Math.min(zero, end), size: Math.abs(end - zero) };
    },
  };
}
