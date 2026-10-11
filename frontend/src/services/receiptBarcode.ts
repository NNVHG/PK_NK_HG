// Code 39's standard character patterns, cross-checked against ZXing Code39Reader:
// https://github.com/zxing/zxing/blob/master/core/src/main/java/com/google/zxing/oned/Code39Reader.java
const alphabet = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-';
const patterns = [0x034, 0x121, 0x061, 0x160, 0x031, 0x130, 0x070, 0x025, 0x124, 0x064,
  0x109, 0x049, 0x148, 0x019, 0x118, 0x058, 0x00d, 0x10c, 0x04c, 0x01c,
  0x103, 0x043, 0x142, 0x013, 0x112, 0x052, 0x007, 0x106, 0x046, 0x016,
  0x181, 0x0c1, 0x1c0, 0x091, 0x190, 0x0d0, 0x085];
export function MOD_BIL_receiptBarcode(code: string) {
  if (!/^[A-Z0-9-]{1,50}$/.test(code)) return null;
  const symbols = [0x094, ...[...code].map(character => patterns[alphabet.indexOf(character)]!), 0x094];
  const bars: { x: number; width: number }[] = []; let x = 10;
  symbols.forEach((pattern, symbolIndex) => {
    for (let element = 0; element < 9; element++) {
      const width = pattern & (1 << (8 - element)) ? 2 : 1;
      if (element % 2 === 0) bars.push({ x, width }); x += width;
    }
    if (symbolIndex < symbols.length - 1) x++;
  });
  return { bars, width: x + 10 };
}
