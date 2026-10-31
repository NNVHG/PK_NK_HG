// Thành phần giao diện dùng chung cho bản mẫu.
(function () {
  const D = window.DEMO;
  const vnd = (n) => n.toLocaleString('vi-VN') + ' ₫';
  const esc = (s) => String(s);

  // ---------- Monochrome Black & White Minimalist SVG Icon System ----------
  const ICONS = {
    home: '<path d="M3 10.5L12 3l9 7.5V20a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 20v-9.5z"/><path d="M9 21v-7h6v7"/>',
    tooth: '<path d="M6.5 4c2.5-1.5 5 0 5.5 1 .5-1 3-2.5 5.5-1 3 1.8 3.5 6 3 9.5-.7 4.5-2 9.5-3.5 9.5-1.2 0-2-3.5-3-5-.5-.8-1.5-.8-2 0-1 1.5-1.8 5-3 5-1.5 0-2.8-5-3.5-9.5-.5-3.5 0-7.7 3-9.5z"/>',
    tag: '<path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/><circle cx="7" cy="7" r="1.5" fill="currentColor"/>',
    doctors: '<path d="M16 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="10" cy="7" r="4"/><path d="M19 8v6M16 11h6"/>',
    pill: '<rect x="3" y="10.5" width="18" height="7" rx="3.5" transform="rotate(-45 12 14)"/><line x1="8.5" y1="10.5" x2="15.5" y2="17.5"/>',
    login: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
    calendar: '<rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>',
    folder: '<path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/>',
    card: '<rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/>',
    user: '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
    search: '<circle cx="11" cy="11" r="7"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>',
    sparkle: '<path d="M12 2v4M12 18v4M2 12h4M18 12h4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"/>',
    tools: '<path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>',
    crown: '<path d="M2 4l3 12h14l3-12-5 6-5-6-5 6-5-6z"/><line x1="5" y1="18" x2="19" y2="18"/>',
    smile: '<circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><circle cx="9" cy="9" r="1" fill="currentColor"/><circle cx="15" cy="9" r="1" fill="currentColor"/>',
    baby: '<circle cx="12" cy="12" r="9"/><circle cx="9" cy="10" r="1" fill="currentColor"/><circle cx="15" cy="10" r="1" fill="currentColor"/><path d="M9.5 15a3.5 3.5 0 0 0 5 0"/>',
    bandage: '<rect x="3" y="8" width="18" height="8" rx="2" transform="rotate(45 12 12)"/><circle cx="12" cy="12" r="1" fill="currentColor"/>',
    xray: '<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/>',
    bell: '<path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/>',
    clipboard: '<path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><rect x="8" y="2" width="8" height="4" rx="1"/><polyline points="9 14 11 16 15 12"/>',
    clock: '<circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>',
    inbox: '<polyline points="22 12 16 12 14 15 10 15 8 12 2 12"/><path d="M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/>',
    chair: '<path d="M6 19v2M18 19v2M4 11h16M7 11V6a2 2 0 0 1 2-2h6a2 2 0 0 1 2 2v5M6 15h12v4H6z"/>',
    package: '<line x1="16.5" y1="9.4" x2="7.5" y2="4.21"/><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><polyline points="3.27 6.96 12 12.01 20.73 6.96"/><line x1="12" y1="22.08" x2="12" y2="12"/>',
    flask: '<path d="M9 3h6M10 3v5.5L4.5 18a2 2 0 0 0 1.7 3h11.6a2 2 0 0 0 1.7-3L14 8.5V3"/>',
    receipt: '<path d="M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1z"/><line x1="8" y1="8" x2="16" y2="8"/><line x1="8" y1="12" x2="16" y2="12"/><line x1="8" y1="16" x2="12" y2="16"/>',
    chart: '<line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/>',
    shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"/>',
    printer: '<polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/>',
    pen: '<path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"/>',
    eye: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
    money: '<line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/>',
    camera: '<path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/>',
    phone: '<rect x="5" y="2" width="14" height="20" rx="2"/><line x1="12" y1="18" x2="12.01" y2="18"/>',
    check: '<polyline points="20 6 9 17 4 12"/>',
    close: '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
    users: '<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>',
    microscope: '<path d="M6 18h8M3 22h18M14 22a7 7 0 1 0 0-14h-1M9 14h2M9 12a2 2 0 0 1-2-2V6h6v4a2 2 0 0 1-2 2zM12 6V3"/>',
    door: '<path d="M18 20V4a2 2 0 0 0-2-2H8a2 2 0 0 0-2 2v16"/><path d="M2 20h20"/><circle cx="14" cy="12" r="1" fill="currentColor"/>',
    archive: '<polyline points="21 8 21 21 3 21 3 8"/><rect x="1" y="3" width="22" height="5"/><line x1="10" y1="12" x2="14" y2="12"/>',
    stethoscope: '<path d="M4.5 3v5a4.5 4.5 0 0 0 9 0V3M18 13.5v2.5a5 5 0 0 1-10 0V11M18 13.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z"/>',
    bank: '<line x1="3" y1="21" x2="21" y2="21"/><line x1="3" y1="10" x2="21" y2="10"/><polyline points="12 3 2 10 22 10 12 3"/><line x1="6" y1="10" x2="6" y2="21"/><line x1="10" y1="10" x2="10" y2="21"/><line x1="14" y1="10" x2="14" y2="21"/><line x1="18" y1="10" x2="18" y2="21"/>'
  };

  const EMOJI_MAP = {
    '🏠': 'home', '🦷': 'tooth', '🏷️': 'tag', '🏷': 'tag', '👨‍⚕️': 'doctors', '👩‍⚕️': 'doctors',
    '💊': 'pill', '🔐': 'login', '📅': 'calendar', '📁': 'folder', '💳': 'card',
    '👤': 'user', '🧾': 'receipt', '⏳': 'clock', '📥': 'inbox', '🗓️': 'calendar',
    '👥': 'users', '🗺️': 'tooth', '🔬': 'microscope', '🧑‍⚕️': 'doctors', '🪑': 'chair',
    '📦': 'package', '🧪': 'flask', '🩺': 'stethoscope', '📋': 'clipboard',
    '🧤': 'archive', '🚪': 'door', '⚙️': 'settings', '🖨️': 'printer', '✍️': 'pen',
    '📈': 'chart', '🕵️': 'eye', '🔔': 'bell', '🩻': 'xray', '🔍': 'search',
    '✨': 'sparkle', '🛠️': 'tools', '👑': 'crown', '😁': 'smile', '🧒': 'baby',
    '🩹': 'bandage', '😊': 'smile', '💰': 'money', '💵': 'money', '🏦': 'bank',
    '📷': 'camera', '📱': 'phone', '✓': 'check', '✕': 'close', '✖': 'close', '🕘': 'clock', '✅': 'check'
  };

  const ic = (name, size = 18, cls = '') => {
    const k = EMOJI_MAP[name] || name;
    const p = ICONS[k];
    if (!p) return name;
    return `<svg class="ic-svg ${cls}" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${p}</svg>`;
  };

  const badge = (text, tone = 'blue') => `<span class="badge b-${tone}">${text}</span>`;
  const fn = (...codes) => codes.map((c) => `<span class="tag-fn">${c}</span>`).join(' ');

  const statusMap = {
    waiting: ['Đang chờ', 'amber'], called: ['Đã gọi số', 'blue'], 'in-exam': ['Đang khám', 'violet'],
    xray: ['Chụp X-quang', 'gold'], done: ['Hoàn tất', 'green'],
    draft: ['Nháp', 'gray'], partial: ['Trả một phần', 'amber'], paid: ['Đã thanh toán', 'green'], cancelled: ['Đã hủy', 'red'],
    'checked-in': ['Đã tiếp đón', 'green'], confirmed: ['Đã xác nhận', 'blue'], pending: ['Chờ duyệt', 'amber'],
    ready: ['Sẵn sàng', 'green'], busy: ['Đang điều trị', 'red'], clean: ['Chờ khử khuẩn', 'gold'], off: ['Bảo trì', 'gray']
  };
  const status = (k) => { const [t, c] = statusMap[k] || [k, 'gray']; return badge(t, c); };

  const table = (cols, rows) => `
    <div class="table-wrap"><table>
      <thead><tr>${cols.map((c) => `<th${c.r ? ' class="right"' : ''}>${c.t || c}</th>`).join('')}</tr></thead>
      <tbody>${rows.map((r) => `<tr>${r.map((v, i) => `<td${cols[i] && cols[i].r ? ' class="right"' : ''}>${v}</td>`).join('')}</tr>`).join('')}</tbody>
    </table></div>`;

  const card = (title, body, actions = '', pad = true) => `
    <section class="card">
      ${title ? `<div class="card-head"><h3>${title}</h3><div class="row">${actions}</div></div>` : ''}
      <div class="${pad ? 'card-body' : ''}">${body}</div>
    </section>`;

  const stat = (icKey, val, lbl, bg) => `<div class="card stat"><div class="ic" style="${bg ? 'background:' + bg : ''}">${ic(icKey, 20)}</div><div><div class="val">${val}</div><div class="lbl">${lbl}</div></div></div>`;

  const field = (label, input, opts = {}) => `<div class="field ${opts.full ? 'full' : ''}"><label>${label}${opts.req ? ' <span style="color:var(--red)">*</span>' : ''}</label>${input}${opts.hint ? `<div class="hint">${opts.hint}</div>` : ''}${opts.err ? `<div class="err">${opts.err}</div>` : ''}</div>`;
  const input = (ph = '', val = '', type = 'text', extra = '') => `<input class="input ${extra}" type="${type}" placeholder="${ph}" value="${val}">`;
  const select = (opts) => `<select class="select">${opts.map((o) => `<option>${o}</option>`).join('')}</select>`;
  const textarea = (ph = '', val = '') => `<textarea class="textarea" placeholder="${ph}">${val}</textarea>`;

  const pageHead = (title, sub, crumbs, actions = '', codes = []) => `
    <div class="page-head">
      <div>
        <div class="crumbs">${crumbs || ''}</div>
        <div class="row"><h1>${title}</h1>${fn(...codes)}</div>
        ${sub ? `<div class="sub">${sub}</div>` : ''}
      </div>
      <div class="page-actions">${actions}</div>
    </div>`;

  const safetyBanner = (p) => `
    <div class="alert alert-red safety">
      <div class="ai">⚠️</div>
      <div style="flex:1">
        <div class="row between"><strong>Cảnh báo an toàn lâm sàng — ${p.name}</strong>${fn('F_PAT_06')}</div>
        <div class="small" style="margin-top:4px">
          <b>Dị ứng thuốc:</b> Penicillin (phát ban, khó thở) · <b>Bệnh nền:</b> Tăng huyết áp, đang dùng Amlodipine 5mg ·
          <b>Huyết áp hôm nay:</b> <span class="badge b-red">158/96 mmHg</span> đo lúc 08:47
        </div>
        <div class="small" style="margin-top:4px">Cân nhắc trước khi tiêm tê có Epinephrine. Cảnh báo chỉ hỗ trợ, không thay quyết định chuyên môn của Nha sĩ.</div>
      </div>
    </div>`;

  // ---------- Tooth & FDI chart (Anatomical teeth + 5-surface FDI diagram) ----------
  const condColor = (code) => (D.conditions.find((c) => c.code === code) || { color: '#fff' }).color;
  const toothAnatSvg = (num, state, child) => {
    const q = Math.floor(num / 10);
    const pos = num % 10;
    const isUpper = q === 1 || q === 2 || q === 5 || q === 6;
    const isMolar = child ? pos >= 4 : pos >= 6;
    const isPremolar = !child && (pos === 4 || pos === 5);
    const isCanine = pos === 3;
    const missing = state === 'MIS';
    const isImplant = state === 'IMP';
    const isCrown = state === 'CRN';
    const isRct = state === 'RCT';
    const isCaries = state === 'CAR';
    const isFilling = state === 'FIL';

    let rootPath = '';
    let canalPath = '';
    if (isMolar) {
      if (isUpper) {
        rootPath = 'M9,34 C7,18 9,6 13,5 C15,5 16,17 17,24 C19,14 20,3 22,3 C24,3 25,14 27,24 C28,17 29,5 31,5 C35,6 37,18 35,34 Z';
        canalPath = 'M13,8 C14,18 16,28 18,38 M22,6 L22,38 M31,8 C30,18 28,28 26,38 M16,38 H28';
      } else {
        rootPath = 'M9,34 C7,18 10,5 15,4 C18,5 19,17 22,24 C25,17 26,5 29,4 C34,5 37,18 35,34 Z';
        canalPath = 'M15,7 C16,18 17,28 19,39 M29,7 C28,18 27,28 25,39 M17,38 H27';
      }
    } else if (isPremolar) {
      rootPath = 'M13,34 C12,19 14,6 17.5,5 C19.5,5 20.5,14 22,22 C23.5,14 24.5,5 26.5,5 C30,6 32,19 31,34 Z';
      canalPath = 'M17.5,8 C18,18 19,28 20,39 M26.5,8 C26,18 25,28 24,39 M19,38 H25';
    } else if (isCanine) {
      rootPath = 'M15,34 C14,14 18,2 22,1.5 C26,2 30,14 29,34 Z';
      canalPath = 'M22,5 L22,42';
    } else {
      const rw = pos === 1 ? 7.5 : 6.2;
      rootPath = `M${22 - rw},34 C${22 - rw},16 ${20 - rw * 0.2},4 22,3 C${24 + rw * 0.2},4 ${22 + rw},16 ${22 + rw},34 Z`;
      canalPath = 'M22,6 L22,41';
    }

    let crownPath = '';
    let groovePath = '';
    if (isMolar) {
      crownPath = 'M6,33 C3,39 3,52 7,59 C10,63 14,63 17,60 C19,63 25,63 27,60 C30,63 34,63 37,59 C41,52 41,39 38,33 C31,30 13,30 6,33 Z';
      groovePath = 'M11,48 Q22,51 33,48 M18,41 C19,48 19,53 17,59 M26,41 C25,48 25,53 27,59';
    } else if (isPremolar) {
      crownPath = 'M9,33 C6,38 6,52 10,59 C13,63 18,62 22,59 C26,62 31,63 34,59 C38,52 38,38 35,33 C29,31 15,31 9,33 Z';
      groovePath = 'M13,49 Q22,52 31,49 M22,43 L22,57';
    } else if (isCanine) {
      crownPath = 'M10,33 C8,38 9,51 12,57 L22,65 L32,57 C35,51 36,38 34,33 C29,31 15,31 10,33 Z';
      groovePath = 'M22,42 L22,60';
    } else {
      const cw = pos === 1 ? 11.5 : 9.5;
      crownPath = `M${22 - cw},33 C${20 - cw},37 ${20 - cw},51 ${22 - cw + 1},59 C${22 - cw * 0.5},62 ${22 + cw * 0.5},62 ${22 + cw - 1},59 C${24 + cw},51 ${24 + cw},37 ${22 + cw},33 C28,31 16,31 ${22 - cw},33 Z`;
      groovePath = `M${22 - cw + 4},57 H${22 + cw - 4}`;
    }

    const crownFill = isCrown ? '#F3CA6E' : '#FFFFFF';
    const crownStroke = isCrown ? '#B7831E' : '#64748B';
    const rootFill = '#F6E7CB';
    const rootStroke = '#948368';
    const flip = isUpper ? '' : 'transform="translate(0,68) scale(1,-1)"';

    return `<svg class="tooth-anat" viewBox="0 0 44 68" aria-hidden="true">
      <g ${flip} opacity="${missing ? '0.28' : '1'}" ${missing ? 'stroke-dasharray="3 2"' : ''}>
        ${isImplant ? `
          <rect x="16" y="6" width="12" height="27" rx="3" fill="#BAE6FD" stroke="#0284C7" stroke-width="1.5"/>
          <path d="M15,11 H29 M15,16 H29 M15,21 H29 M15,26 H29 M17,31 H27" stroke="#0369A1" stroke-width="1.6" stroke-linecap="round"/>
        ` : `<path d="${rootPath}" fill="${rootFill}" stroke="${rootStroke}" stroke-width="1.4" stroke-linejoin="round"/>`}
        <path d="M7,33 Q22,36 37,33" fill="none" stroke="#F4A5AE" stroke-width="2.6" stroke-linecap="round" opacity=".75"/>
        <path d="${crownPath}" fill="${crownFill}" stroke="${crownStroke}" stroke-width="1.5" stroke-linejoin="round"/>
        <path d="${groovePath}" fill="none" stroke="${isCrown ? '#C59328' : '#CBD5E1'}" stroke-width="1.1" stroke-linecap="round"/>
        <path d="M13,38 C12,43 12,48 13,53" fill="none" stroke="#FFFFFF" stroke-width="2" stroke-linecap="round" opacity=".7"/>
        ${isRct ? `<path d="${canalPath}" fill="none" stroke="#8B5CF6" stroke-width="2.2" stroke-linecap="round"/><ellipse cx="22" cy="42" rx="5" ry="3.5" fill="#8B5CF6"/>` : ''}
        ${isCaries ? `<ellipse cx="24" cy="49" rx="${isMolar ? 7 : 5}" ry="4.5" fill="#EF4444" stroke="#991B1B" stroke-width="1"/><circle cx="19" cy="46" r="2.5" fill="#EF4444"/>` : ''}
        ${isFilling ? `<ellipse cx="22" cy="48" rx="${isMolar ? 7.5 : 5}" ry="4.5" fill="#3B82F6" stroke="#1D4ED8" stroke-width="1"/>` : ''}
      </g>
      ${missing ? `<path d="M9,12 L35,56 M35,12 L9,56" stroke="#EF4444" stroke-width="2.6" stroke-linecap="round"/>` : ''}
    </svg>`;
  };
  const toothSurfSvg = (state) => {
    const c = state && state !== 'MIS' ? condColor(state) : '#FFFFFF';
    const oFill = state && state !== 'MIS' ? c : '#F8FAFC';
    const bFill = state === 'CRN' ? c : '#FFFFFF';
    return `<svg class="tooth-surf" viewBox="0 0 32 32" aria-hidden="true">
      <polygon points="2,2 30,2 22,10 10,10" fill="${bFill}" stroke="#94A3B8" stroke-width="1"/>
      <polygon points="10,22 22,22 30,30 2,30" fill="${bFill}" stroke="#94A3B8" stroke-width="1"/>
      <polygon points="2,2 10,10 10,22 2,30" fill="${state === 'CAR' ? c : bFill}" stroke="#94A3B8" stroke-width="1"/>
      <polygon points="30,2 30,30 22,22 22,10" fill="${bFill}" stroke="#94A3B8" stroke-width="1"/>
      <rect x="10" y="10" width="12" height="12" fill="${oFill}" stroke="#64748B" stroke-width="1.1"/>
    </svg>`;
  };
  const fdiChart = (opts = {}) => {
    const child = opts.child;
    const st = child ? D.childToothState : D.toothState;
    const sel = opts.selected || (child ? 84 : 16);
    const quad = (q, n, rev) => {
      const arr = Array.from({ length: n }, (_, i) => q * 10 + i + 1);
      if (rev) arr.reverse();
      const isUp = q <= 2 || (q >= 5 && q <= 6);
      return `<div class="fdi-quad">${arr.map((t) => {
        const anat = toothAnatSvg(t, st[t], child);
        const surf = toothSurfSvg(st[t]);
        return `<button class="tooth ${t === sel ? 'sel' : ''}" data-tooth="${t}" title="Răng ${t}">
          ${isUp ? `<span class="num">${t}</span>${anat}${surf}` : `${surf}${anat}<span class="num">${t}</span>`}
        </button>`;
      }).join('')}</div>`;
    };
    const n = child ? 5 : 8;
    const [q1, q2, q3, q4] = child ? [5, 6, 7, 8] : [1, 2, 3, 4];
    return `<div class="fdi">
      <div class="small muted">Hàm trên (${child ? '20 răng sữa · 51–65' : '32 răng vĩnh viễn · 11–28'} · ký hiệu FDI)</div>
      <div class="fdi-jaw">${quad(q1, n, true)}${quad(q2, n, false)}</div>
      <div class="fdi-midline"></div>
      <div class="fdi-jaw lower">${quad(q4, n, true)}${quad(q3, n, false)}</div>
      <div class="small muted">Hàm dưới (${child ? '71–85' : '31–48'}) · Bên phải bệnh nhân ← → Bên trái bệnh nhân</div>
    </div>`;
  };
  const fdiLegend = () => `<div class="legend">${D.conditions.map((c) => `<span><i style="background:${c.color}"></i>${c.name}</span>`).join('')}</div>`;
  const surfaces = (on = ['O', 'M']) => `<div class="row">${[['B', 'Ngoài (Buccal)'], ['L', 'Trong (Lingual)'], ['M', 'Gần (Mesial)'], ['D', 'Xa (Distal)'], ['O', 'Nhai (Occlusal)']]
    .map(([k, t]) => `<button class="btn btn-sm surf-btn ${on.includes(k) ? 'btn-primary' : 'btn-outline'}" data-surf="${k}">${k} · ${t}</button>`).join('')}</div>`;

  // ---------- Navigation config ----------
  const NAV = {
    ADMIN: [
      ['Tổng quan', [['🏠', 'Bảng điều khiển', 'm-dashboard']]],
      ['Tiếp đón & lịch', [['🧾', 'Tiếp đón tại quầy', 'm-checkin'], ['⏳', 'Hàng đợi hôm nay', 'm-queue'], ['📥', 'Duyệt yêu cầu đặt lịch', 'm-booking-requests'], ['📅', 'Đặt lịch tại quầy', 'm-counter-booking'], ['🗓️', 'Bàn lịch hẹn', 'm-appointment-desk']]],
      ['Bệnh nhân & lâm sàng', [['👥', 'Hồ sơ bệnh nhân', 'm-patients'], ['🦷', 'Khám & chẩn đoán', 'm-diagnosis'], ['🗺️', 'Sơ đồ răng FDI', 'm-fdi'], ['🔬', 'Kiểm thử sơ đồ FDI', 'm-fdi-preview'], ['🧑‍⚕️', 'Không gian nha sĩ/phụ tá', 'm-workspace']]],
      ['Thu ngân', [['💳', 'Hóa đơn & thu tiền', 'm-invoices'], ['🧾', 'Biên lai thu tiền', 'm-receipt']]],
      ['Kho & tài nguyên', [['🪑', 'Trạng thái ghế/phòng', 'm-chair-status'], ['📦', 'Phiếu nhập kho', 'm-stock-receipts'], ['🧪', 'Xác nhận tiêu hao', 'm-stock-issues'], ['💊', 'Tồn kho thuốc', 'm-medicine-stock']]],
      ['Danh mục', [['🩺', 'Dịch vụ & bảng giá', 'm-services'], ['📋', 'Bệnh lý / triệu chứng', 'm-diagnoses'], ['💊', 'Danh mục thuốc', 'm-medicines'], ['🧤', 'Vật tư & ngưỡng tồn', 'm-supplies'], ['🚪', 'Phòng & ghế', 'm-chairs']]],
      ['Quản trị', [['👤', 'Nhân viên', 'm-staff'], ['🔐', 'Vai trò & quyền', 'm-roles'], ['⚙️', 'Cấu hình lịch khám', 'm-scheduling'], ['🖨️', 'Cấu hình in ấn', 'm-print-settings'], ['✍️', 'Chữ ký bác sĩ', 'm-signature'], ['📈', 'Báo cáo doanh thu', 'm-revenue'], ['🕵️', 'Nhật ký thao tác', 'm-audit']]]
    ],
    RECEPTIONIST: [
      ['Tổng quan', [['🏠', 'Bảng điều khiển', 'm-dashboard']]],
      ['Tiếp đón & lịch', [['🧾', 'Tiếp đón tại quầy', 'm-checkin'], ['⏳', 'Hàng đợi hôm nay', 'm-queue'], ['📥', 'Duyệt yêu cầu đặt lịch', 'm-booking-requests'], ['📅', 'Đặt lịch tại quầy', 'm-counter-booking'], ['🗓️', 'Bàn lịch hẹn', 'm-appointment-desk']]],
      ['Bệnh nhân', [['👥', 'Hồ sơ bệnh nhân', 'm-patients']]],
      ['Thu ngân', [['💳', 'Hóa đơn & thu tiền', 'm-invoices'], ['🧾', 'Biên lai', 'm-receipt']]]
    ],
    DENTIST: [
      ['Tổng quan', [['🏠', 'Bảng điều khiển', 'm-dashboard'], ['🧑‍⚕️', 'Không gian làm việc', 'm-workspace']]],
      ['Lâm sàng', [['⏳', 'Hàng đợi hôm nay', 'm-queue'], ['👥', 'Hồ sơ bệnh nhân', 'm-patients'], ['🦷', 'Khám & chẩn đoán', 'm-diagnosis'], ['🗺️', 'Sơ đồ răng FDI', 'm-fdi'], ['🔬', 'Kiểm thử sơ đồ FDI', 'm-fdi-preview'], ['🩺', 'Chỉ định dịch vụ', 'm-fdi-services'], ['🩻', 'Ảnh X-quang', 'm-images'], ['💊', 'Kê đơn thuốc', 'm-prescription']]],
      ['Khác', [['📦', 'Xem tồn vật tư', 'm-stock-receipts'], ['✍️', 'Chữ ký của tôi', 'm-signature']]]
    ],
    ASSISTANT: [
      ['Tổng quan', [['🏠', 'Bảng điều khiển', 'm-dashboard'], ['🧑‍⚕️', 'Không gian làm việc', 'm-workspace']]],
      ['Hỗ trợ lâm sàng', [['⏳', 'Hàng đợi hôm nay', 'm-queue'], ['👥', 'Hồ sơ bệnh nhân', 'm-patients'], ['🗺️', 'Sơ đồ răng FDI', 'm-fdi'], ['🩻', 'Ảnh X-quang', 'm-images']]],
      ['Kho & ghế', [['🪑', 'Trạng thái ghế/phòng', 'm-chair-status'], ['🧪', 'Xác nhận tiêu hao', 'm-stock-issues'], ['📦', 'Phiếu nhập kho', 'm-stock-receipts']]]
    ]
  };
  const PATIENT_NAV = [['🏠', 'Tổng quan', 'p-dashboard'], ['📁', 'Hồ sơ của tôi', 'p-record'], ['📅', 'Lịch hẹn', 'p-appointments'], ['💳', 'Hóa đơn', 'p-invoices'], ['👤', 'Tài khoản', 'p-profile']];

  const staffShell = (route, role, body) => {
    const r = D.roles[role];
    const groups = NAV[role] || NAV.ADMIN;
    return `<div class="shell" id="shell">
      <aside class="sidebar">
        <div class="brand"><a href="#/cover"><img src="assets/logo-ngang-nen-toi.svg" alt="Nha khoa Hoàng Gia"></a></div>
        ${groups.map(([g, items]) => `<div class="nav-group"><div class="nav-group-title">${g}</div>
          ${items.map(([itemIcon, t, id]) => `<a class="nav-item ${id === route ? 'active' : ''}" href="#/${id}?role=${role}"><span class="ico">${ic(itemIcon, 17)}</span>${t}</a>`).join('')}
        </div>`).join('')}
      </aside>
      <div class="main">
        <header class="topbar">
          <button class="btn btn-outline btn-sm menu-btn" onclick="document.getElementById('shell').classList.toggle('nav-open')">☰</button>
          <div class="search">${input('Tìm bệnh nhân theo tên, SĐT, mã hồ sơ…')}</div>
          <div class="spacer"></div>
          ${role === 'ADMIN' || role === 'RECEPTIONIST' ? badge('Ca sáng · 31/10/2026', 'blue') : badge('Phòng 01 · Ghế 1', 'blue')}
          <button class="btn btn-ghost btn-sm" title="Thông báo">${ic('bell', 16)}</button>
          <a class="user-chip" href="#/m-profile?role=${role}"><div class="avatar">${r.init}</div><div class="who"><div class="strong small">${r.user}</div><div class="small muted">${r.label}</div></div></a>
        </header>
        <main class="content">${body}</main>
      </div>
    </div>`;
  };

  const patientShell = (route, body) => `
    <header class="pp-header"><div class="inner">
      <a class="pub-logo" href="#/cover"><img src="assets/logo-ngang.svg" alt="Nha khoa Hoàng Gia"></a>
      <nav class="pp-nav">${PATIENT_NAV.map(([k, t, id]) => `<a class="${route.startsWith(id) ? 'on' : ''}" href="#/${id}"><span class="nav-ic">${ic(k, 16)}</span><span>${t}</span></a>`).join('')}</nav>
      <div class="pub-actions">
        <a class="btn btn-gold btn-sm" href="#/p-book">${ic('calendar', 14)} <span>Đặt lịch</span></a>
        <a class="avatar" href="#/p-profile" title="Tài khoản bệnh nhân" style="text-decoration:none">TL</a>
      </div>
    </div></header>
    <main class="pp-main">${body}</main>
    <nav class="bottom-nav">${PATIENT_NAV.map(([k, t, id]) => `<a class="${route.startsWith(id) ? 'on' : ''}" href="#/${id}"><span class="bnav-ic">${ic(k, 20)}</span><span>${t}</span></a>`).join('')}</nav>`;

  const publicShell = (body, activeRoute = 'home') => `
    <header class="pub-header"><div class="inner">
      <a class="pub-logo" href="#/home"><img src="assets/logo-ngang.svg" alt="Nha khoa Hoàng Gia"></a>
      <nav class="pub-nav">
        <a class="pub-link ${activeRoute === 'home' ? 'on' : ''}" href="#/home"><span class="nav-ic">${ic('home', 15)}</span><span>Trang chủ</span></a>
        <a class="pub-link" href="#/home" onclick="setTimeout(()=>{const el=document.getElementById('sec-services');if(el)el.scrollIntoView({behavior:'smooth'});},40)"><span class="nav-ic">${ic('tooth', 15)}</span><span>Dịch vụ</span></a>
        <a class="pub-link" href="#/home" onclick="setTimeout(()=>{const el=document.getElementById('sec-pricing');if(el)el.scrollIntoView({behavior:'smooth'});},40)"><span class="nav-ic">${ic('tag', 15)}</span><span>Bảng giá</span></a>
        <a class="pub-link" href="#/home" onclick="setTimeout(()=>{const el=document.getElementById('sec-doctors');if(el)el.scrollIntoView({behavior:'smooth'});},40)"><span class="nav-ic">${ic('doctors', 15)}</span><span>Đội ngũ</span></a>
        <a class="pub-link ${activeRoute === 'rx-public' ? 'on' : ''}" href="#/rx-public"><span class="nav-ic">${ic('pill', 15)}</span><span>Tra cứu đơn thuốc</span></a>
      </nav>
      <div class="pub-actions">
        <a class="btn btn-outline btn-sm" href="#/login">${ic('login', 14)} <span>Đăng nhập</span></a>
        <a class="btn btn-gold btn-sm" href="#/booking">${ic('calendar', 14)} <span>Đặt lịch ngay →</span></a>
      </div>
    </div></header>
    ${body}
    <footer class="footer"><div class="container">
      <div class="grid g3">
        <div class="stack"><img src="assets/logo-ngang-nen-toi.svg" alt=""><p class="small">Chăm sóc răng miệng toàn diện — Khám răng • Điều trị • Thẩm mỹ răng.</p></div>
        <div class="stack small"><strong style="color:#fff">Liên hệ</strong><span>📞 [ĐIỀN SĐT]</span><span>📍 [ĐIỀN ĐỊA CHỈ]</span><span>🕗 T2–T7: 7:30 – 20:00 · CN: 7:30 – 12:00</span></div>
        <div class="stack small"><strong style="color:#fff">Liên kết</strong><a href="#/booking" style="color:#C9D8EA">${ic('calendar', 13)} Đặt lịch khám</a><a href="#/rx-public" style="color:#C9D8EA">${ic('pill', 13)} Tra cứu đơn thuốc</a><a href="#/login" style="color:#C9D8EA">${ic('login', 13)} Cổng bệnh nhân</a></div>
      </div>
      <div class="small" style="margin-top:28px;opacity:.7">© 2026 Phòng khám Nha khoa Hoàng Gia.</div>
    </div></footer>`;

  window.UI = { D, vnd, esc, badge, fn, status, table, card, stat, field, input, select, textarea, pageHead, safetyBanner, toothAnatSvg, toothSurfSvg, fdiChart, fdiLegend, surfaces, condColor, staffShell, patientShell, publicShell, NAV, ic };
})();
