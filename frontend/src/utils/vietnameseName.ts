function capitalizeWord(word: string): string {
  if (!word) return '';
  const chars = Array.from(word);
  const first = chars[0].toLocaleUpperCase('vi-VN');
  const rest = chars.slice(1).join('').toLocaleLowerCase('vi-VN');
  return first + rest;
}

/**
 * Chuẩn hóa tên tiếng Việt viết hoa chữ cái đầu mỗi từ.
 * Khi đang nhập (finalize = false), chỉ chuẩn hóa các từ đã hoàn tất trước khoảng trắng
 * để không làm gián đoạn bộ gõ tiếng Việt (Telex/VNI).
 */
export function capitalizeVietnameseName(value: string, finalize = false): string {
  if (!value) return '';

  if (finalize) {
    return value
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .map(capitalizeWord)
      .join(' ');
  }

  const normalizedLeading = value.replace(/^\s+/, '');
  if (!normalizedLeading) return '';

  const parts = normalizedLeading.split(' ');
  if (parts.length <= 1) {
    return normalizedLeading;
  }

  const completed = parts.slice(0, -1).map(part => (part ? capitalizeWord(part) : ''));
  const trailing = parts[parts.length - 1];
  return [...completed, trailing].join(' ');
}
