export const AFTER_HOURS_BOOKING_NOTICE =
  'Nha khoa đã hết giờ làm việc hôm nay, hãy đặt lịch cho ngày mai để hàm răng chắc khỏe.';

export interface VietnamNow {
  date: string;
  time: string;
}

export interface BookingDateWindow {
  today: string;
  minDate: string;
  defaultDate: string;
  isAfterHours: boolean;
}

const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

/** Returns current date (YYYY-MM-DD) and time (HH:mm) in Asia/Ho_Chi_Minh. */
export function getVietnamNow(now: Date = new Date()): VietnamNow {
  const date = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).format(now);
  const time = new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23'
  }).format(now);
  return { date, time };
}

/** Adds calendar days to a validated YYYY-MM-DD date string using UTC arithmetic. */
export function addIsoDays(iso: string, offset: number): string {
  if (!ISO_DATE_RE.test(iso)) return iso;
  const [year, month, day] = iso.split('-').map(Number);
  const utc = new Date(Date.UTC(year, month - 1, day + offset));
  if (Number.isNaN(utc.getTime())) return iso;
  const y = utc.getUTCFullYear();
  const m = String(utc.getUTCMonth() + 1).padStart(2, '0');
  const d = String(utc.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function hasRemainingSlotsToday(slots: readonly string[], nowTime: string): boolean {
  return slots.some(slot => slot > nowTime);
}

/**
 * Returns true when referenceToday equals current Vietnam date and every bookable slot has started/passed.
 * Checking referenceToday === now.date preserves unit tests that supply future mock dates (e.g. 2099-01-01).
 */
export function isBookingDayClosed(
  referenceToday: string,
  slots: readonly string[],
  now: VietnamNow = getVietnamNow()
): boolean {
  return (
    Boolean(referenceToday) &&
    ISO_DATE_RE.test(referenceToday) &&
    referenceToday === now.date &&
    slots.length > 0 &&
    !hasRemainingSlotsToday(slots, now.time)
  );
}

export function resolveBookingDateWindow(
  referenceToday: string,
  slots: readonly string[],
  now: VietnamNow = getVietnamNow()
): BookingDateWindow {
  const closed = isBookingDayClosed(referenceToday, slots, now);
  if (closed) {
    const tomorrow = addIsoDays(referenceToday, 1);
    return {
      today: referenceToday,
      minDate: tomorrow,
      defaultDate: tomorrow,
      isAfterHours: true
    };
  }
  return {
    today: referenceToday,
    minDate: referenceToday,
    defaultDate: referenceToday,
    isAfterHours: false
  };
}
