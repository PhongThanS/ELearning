// Cấu hình chung cho mọi kịch bản; mọi giá trị ghi đè bằng biến môi trường (k6 run -e KEY=value).

function int(name, fallback) {
  const raw = __ENV[name];
  if (raw === undefined || raw === '') {
    return fallback;
  }
  const value = parseInt(raw, 10);
  if (Number.isNaN(value) || value < 0) {
    throw new Error(`${name} phải là số nguyên không âm, nhận được "${raw}"`);
  }
  return value;
}

/** Origin công khai (Nginx). Đi qua Nginx để đo cả proxy, như học viên thật. */
export const BASE_URL = (__ENV.BASE_URL || 'http://localhost:8080').replace(/\/+$/, '');

export const ADMIN_USER = __ENV.ADMIN_USER || 'admin';
export const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || 'Admin@123456';

/** Học viên tải: <USER_PREFIX>0001 … thuộc nhóm GROUP_CODE, dùng lại giữa các lần chạy. */
export const USER_PREFIX = (__ENV.USER_PREFIX || 'lt.hv.').toLowerCase();
export const GROUP_CODE = __ENV.GROUP_CODE || 'LOADTEST';
export const STUDENT_PASSWORD = __ENV.STUDENT_PASSWORD || 'LoadTest@2026';

/** Số học viên ảo; chỉ tiêu ở docs/01-tong-quan.md mục 6 là 500. */
export const VUS = int('VUS', 500);

/** Số câu của đề tải (tối đa 50 để lưu cả bài trong một lô). */
export const QUESTION_COUNT = Math.min(int('QUESTION_COUNT', 20), 50);

export { int };

export function userName(index) {
  return `${USER_PREFIX}${String(index).padStart(4, '0')}`;
}

/**
 * SMOKE=1 (CI, vài VU trên máy chung): bỏ ngưỡng độ trễ, chỉ giữ ngưỡng về tính đúng
 * (không 5xx, không sai lượt thi, mọi check đạt). Chạy đo thật thì không đặt SMOKE.
 */
export function thresholds(latency, correctness) {
  return __ENV.SMOKE === '1' ? correctness : Object.assign({}, latency, correctness);
}
