import type { InvoiceDraft } from '@/services/invoiceDraft';

export function validDiscountValue(type: string, value: string | number): boolean {
  const text = String(value);
  if (type === 'Percentage') return /^\d+(\.\d{1,2})?$/.test(text) && Number(text) > 0 && Number(text) <= 100;
  return type === 'FixedAmount' && /^\d+$/.test(text) && Number.isSafeInteger(Number(text)) && Number(text) > 0;
}

/** Positive amounts only: exact integer arithmetic matches DL-140 AwayFromZero at 1 VND. */
export function calculateInvoiceDiscount(invoice: InvoiceDraft, type: string, value: string | number): number | null {
  if (!validDiscountValue(type, value) || !Number.isSafeInteger(invoice.totalAmount) || invoice.totalAmount < 0) return null;
  let subtotal = 0n, services = 0n;
  for (const item of invoice.items) {
    if (!['Service', 'Medicine'].includes(item.itemType) || !Number.isSafeInteger(item.quantity) || item.quantity <= 0 ||
        !Number.isSafeInteger(item.unitPrice) || item.unitPrice < 0 || !Number.isSafeInteger(item.totalAmount)) return null;
    const amount = BigInt(item.quantity) * BigInt(item.unitPrice);
    if (amount !== BigInt(item.totalAmount)) return null;
    subtotal += amount;
    if (item.itemType === 'Service') services += amount;
  }
  if (subtotal !== BigInt(invoice.totalAmount)) return null;
  const [whole, fraction = ''] = String(value).split('.');
  const basisPoints = BigInt(whole!) * 100n + BigInt(fraction.padEnd(2, '0'));
  const amount = type === 'Percentage' ? (services * basisPoints + 5000n) / 10000n : BigInt(value);
  return amount <= subtotal && amount <= BigInt(Number.MAX_SAFE_INTEGER) ? Number(amount) : null;
}
