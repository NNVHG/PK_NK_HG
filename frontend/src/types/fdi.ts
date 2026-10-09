// Tiêu chuẩn FDI và định nghĩa mặt răng (AGENTS.md §2.4, DL-007, DL-018)

export type DentitionType = 'adult' | 'child' | 'mixed';

export type ToothSurface = 'buccal' | 'lingual' | 'mesial' | 'distal' | 'occlusal';

export interface SelectedToothSurface {
  toothNumber: number;
  surface?: ToothSurface;
}

export interface SurfaceMetadata {
  key: ToothSurface;
  code: string;
  nameVi: string;
  descriptionVi: string;
}

export interface ToothInfo {
  number: number;
  nameVi: string;
  quadrant: number;
  arch: 'upper' | 'lower';
  side: 'right' | 'left';
  isPrimary: boolean;
}

// Bảng tra thông tin mặt răng (AGENTS.md §2.4: buccal, lingual, mesial, distal, occlusal)
export const SURFACE_METADATA: Record<ToothSurface, SurfaceMetadata> = {
  buccal: {
    key: 'buccal',
    code: 'B',
    nameVi: 'Mặt ngoài',
    descriptionVi: 'Mặt ngoài (má/môi)'
  },
  lingual: {
    key: 'lingual',
    code: 'L',
    nameVi: 'Mặt trong',
    descriptionVi: 'Mặt trong (lưỡi/vòm miệng)'
  },
  mesial: {
    key: 'mesial',
    code: 'M',
    nameVi: 'Mặt gần',
    descriptionVi: 'Mặt gần (hướng về đường giữa)'
  },
  distal: {
    key: 'distal',
    code: 'D',
    nameVi: 'Mặt xa',
    descriptionVi: 'Mặt xa (hướng ra ngoài/xa đường giữa)'
  },
  occlusal: {
    key: 'occlusal',
    code: 'O',
    nameVi: 'Mặt nhai',
    descriptionVi: 'Mặt nhai / rìa cắn'
  }
};

export const ALL_SURFACES: ToothSurface[] = [
  'buccal',
  'lingual',
  'mesial',
  'distal',
  'occlusal'
];

// Danh sách mã FDI chuẩn (ISO 3950)
export const ADULT_UPPER_RIGHT = [18, 17, 16, 15, 14, 13, 12, 11] as const;
export const ADULT_UPPER_LEFT = [21, 22, 23, 24, 25, 26, 27, 28] as const;
export const ADULT_LOWER_RIGHT = [48, 47, 46, 45, 44, 43, 42, 41] as const;
export const ADULT_LOWER_LEFT = [31, 32, 33, 34, 35, 36, 37, 38] as const;

export const ADULT_TEETH: number[] = [
  ...ADULT_UPPER_RIGHT,
  ...ADULT_UPPER_LEFT,
  ...ADULT_LOWER_RIGHT,
  ...ADULT_LOWER_LEFT
];

export const CHILD_UPPER_RIGHT = [55, 54, 53, 52, 51] as const;
export const CHILD_UPPER_LEFT = [61, 62, 63, 64, 65] as const;
export const CHILD_LOWER_RIGHT = [85, 84, 83, 82, 81] as const;
export const CHILD_LOWER_LEFT = [71, 72, 73, 74, 75] as const;

export const CHILD_TEETH: number[] = [
  ...CHILD_UPPER_RIGHT,
  ...CHILD_UPPER_LEFT,
  ...CHILD_LOWER_RIGHT,
  ...CHILD_LOWER_LEFT
];

// Tên giải phẫu răng tiếng Việt
const TOOTH_NAMES: Record<number, string> = {
  18: 'Răng khôn trên phải (Răng 8)',
  17: 'Răng cối lớn thứ hai trên phải (Răng 7)',
  16: 'Răng cối lớn thứ nhất trên phải (Răng 6)',
  15: 'Răng tiền hàm thứ hai trên phải (Răng 5)',
  14: 'Răng tiền hàm thứ nhất trên phải (Răng 4)',
  13: 'Răng nanh trên phải (Răng 3)',
  12: 'Răng cửa bên trên phải (Răng 2)',
  11: 'Răng cửa giữa trên phải (Răng 1)',
  21: 'Răng cửa giữa trên trái (Răng 1)',
  22: 'Răng cửa bên trên trái (Răng 2)',
  23: 'Răng nanh trên trái (Răng 3)',
  24: 'Răng tiền hàm thứ nhất trên trái (Răng 4)',
  25: 'Răng tiền hàm thứ hai trên trái (Răng 5)',
  26: 'Răng cối lớn thứ nhất trên trái (Răng 6)',
  27: 'Răng cối lớn thứ hai trên trái (Răng 7)',
  28: 'Răng khôn trên trái (Răng 8)',
  31: 'Răng cửa giữa dưới trái (Răng 1)',
  32: 'Răng cửa bên dưới trái (Răng 2)',
  33: 'Răng nanh dưới trái (Răng 3)',
  34: 'Răng tiền hàm thứ nhất dưới trái (Răng 4)',
  35: 'Răng tiền hàm thứ hai dưới trái (Răng 5)',
  36: 'Răng cối lớn thứ nhất dưới trái (Răng 6)',
  37: 'Răng cối lớn thứ hai dưới trái (Răng 7)',
  38: 'Răng khôn dưới trái (Răng 8)',
  41: 'Răng cửa giữa dưới phải (Răng 1)',
  42: 'Răng cửa bên dưới phải (Răng 2)',
  43: 'Răng nanh dưới phải (Răng 3)',
  44: 'Răng tiền hàm thứ nhất dưới phải (Răng 4)',
  45: 'Răng tiền hàm thứ hai dưới phải (Răng 5)',
  46: 'Răng cối lớn thứ nhất dưới phải (Răng 6)',
  47: 'Răng cối lớn thứ hai dưới phải (Răng 7)',
  48: 'Răng khôn dưới phải (Răng 8)',
  55: 'Răng cối sữa 2 trên phải (Răng V)',
  54: 'Răng cối sữa 1 trên phải (Răng IV)',
  53: 'Răng nanh sữa trên phải (Răng III)',
  52: 'Răng cửa bên sữa trên phải (Răng II)',
  51: 'Răng cửa giữa sữa trên phải (Răng I)',
  61: 'Răng cửa giữa sữa trên trái (Răng I)',
  62: 'Răng cửa bên sữa trên trái (Răng II)',
  63: 'Răng nanh sữa trên trái (Răng III)',
  64: 'Răng cối sữa 1 trên trái (Răng IV)',
  65: 'Răng cối sữa 2 trên trái (Răng V)',
  71: 'Răng cửa giữa sữa dưới trái (Răng I)',
  72: 'Răng cửa bên sữa dưới trái (Răng II)',
  73: 'Răng nanh sữa dưới trái (Răng III)',
  74: 'Răng cối sữa 1 dưới trái (Răng IV)',
  75: 'Răng cối sữa 2 dưới trái (Răng V)',
  81: 'Răng cửa giữa sữa dưới phải (Răng I)',
  82: 'Răng cửa bên sữa dưới phải (Răng II)',
  83: 'Răng nanh sữa dưới phải (Răng III)',
  84: 'Răng cối sữa 1 dưới phải (Răng IV)',
  85: 'Răng cối sữa 2 dưới phải (Răng V)'
};

export function isValidFdiTooth(num: number): boolean {
  return ADULT_TEETH.includes(num) || CHILD_TEETH.includes(num);
}

export function isUpperArch(toothNumber: number): boolean {
  const q = Math.floor(toothNumber / 10);
  return q === 1 || q === 2 || q === 5 || q === 6;
}

export function isPatientRight(toothNumber: number): boolean {
  const q = Math.floor(toothNumber / 10);
  return q === 1 || q === 4 || q === 5 || q === 8;
}

export function isPrimaryTooth(toothNumber: number): boolean {
  const q = Math.floor(toothNumber / 10);
  return q >= 5 && q <= 8;
}

export function getToothNameVi(toothNumber: number): string {
  return TOOTH_NAMES[toothNumber] ?? `Răng FDI ${toothNumber}`;
}

export function getToothInfo(toothNumber: number): ToothInfo {
  const quadrant = Math.floor(toothNumber / 10);
  return {
    number: toothNumber,
    nameVi: getToothNameVi(toothNumber),
    quadrant,
    arch: isUpperArch(toothNumber) ? 'upper' : 'lower',
    side: isPatientRight(toothNumber) ? 'right' : 'left',
    isPrimary: isPrimaryTooth(toothNumber)
  };
}

export function getSurfaceAtPosition(
  toothNumber: number,
  position: 'top' | 'bottom' | 'left' | 'right' | 'center'
): ToothSurface {
  if (position === 'center') return 'occlusal';

  const isUpper = isUpperArch(toothNumber);
  const isRight = isPatientRight(toothNumber);

  if (position === 'top') {
    return isUpper ? 'buccal' : 'lingual';
  }
  if (position === 'bottom') {
    return isUpper ? 'lingual' : 'buccal';
  }
  if (position === 'left') {
    return isRight ? 'distal' : 'mesial';
  }
  if (position === 'right') {
    return isRight ? 'mesial' : 'distal';
  }

  return 'occlusal';
}

export function getPositionForSurface(
  toothNumber: number,
  surface: ToothSurface
): 'top' | 'bottom' | 'left' | 'right' | 'center' {
  if (surface === 'occlusal') return 'center';

  const isUpper = isUpperArch(toothNumber);
  const isRight = isPatientRight(toothNumber);

  if (surface === 'buccal') {
    return isUpper ? 'top' : 'bottom';
  }
  if (surface === 'lingual') {
    return isUpper ? 'bottom' : 'top';
  }
  if (surface === 'mesial') {
    return isRight ? 'right' : 'left';
  }
  if (surface === 'distal') {
    return isRight ? 'left' : 'right';
  }

  return 'center';
}
