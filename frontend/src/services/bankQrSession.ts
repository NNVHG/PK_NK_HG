export const MOD_BIL_QR_TTL_SECONDS = 900;

type SessionClock = {
  now: () => number;
  schedule: (callback: () => void) => ReturnType<typeof setInterval>;
  cancel: (timer: ReturnType<typeof setInterval>) => void;
};

/** Absolute deadline handles delayed ticks in background tabs without extending TTL. */
export function MOD_BIL_createQrSession(onTick: (seconds: number) => void, onExpire: () => void,
  clock: SessionClock = { now: Date.now, schedule: callback => setInterval(callback, 1000), cancel: clearInterval }) {
  let deadline: number | null = null;
  let timer: ReturnType<typeof setInterval> | undefined;
  function stop() {
    if (timer !== undefined) clock.cancel(timer);
    timer = undefined; deadline = null;
  }
  function isActive() {
    if (deadline === null) return false;
    const remaining = Math.max(0, Math.ceil((deadline - clock.now()) / 1000));
    onTick(remaining);
    if (remaining > 0) return true;
    stop(); onExpire(); return false;
  }
  function start() {
    stop(); deadline = clock.now() + MOD_BIL_QR_TTL_SECONDS * 1000;
    onTick(MOD_BIL_QR_TTL_SECONDS);
    timer = clock.schedule(isActive);
  }
  return { start, stop, isActive };
}

export function MOD_BIL_formatQrCountdown(seconds: number) {
  return `${Math.floor(seconds / 60).toString().padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`;
}
