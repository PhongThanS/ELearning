// Gọi API như SPA: JSON, header chống CSRF, access token trong bộ nhớ, refresh token ở cookie (D-15).
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE_URL } from './config.js';

/** 5xx hoặc lỗi kết nối (status 0). Chỉ tiêu: không có lỗi 5xx (docs/08-kiem-thu.md mục 7). */
export const serverErrors = new Counter('server_errors');

/** 429 từ API hoặc Nginx: dấu hiệu rate limit chưa đủ cho một phòng thi dùng chung IP (docs/07-bao-mat.md mục 5). */
export const rateLimited = new Counter('rate_limited');

const HEADERS = { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' };

// Cookie refresh có cờ Secure nên chỉ gửi lại được qua HTTPS; qua HTTP thì đăng nhập lại khi token sắp hết hạn.
const CAN_REFRESH = BASE_URL.startsWith('https://');

function track(res) {
  if (res.status === 0 || res.status >= 500) {
    serverErrors.add(1);
  } else if (res.status === 429) {
    rateLimited.add(1);
  }
  return res;
}

/** Phần `data` của ApiResponse, hoặc null nếu body không phải JSON. */
export function data(res) {
  try {
    const body = res.json();
    return body && typeof body === 'object' ? body.data : null;
  } catch (_) {
    return null;
  }
}

/** Mã lỗi đầu tiên của ApiResponse (ví dụ MAX_ATTEMPTS_EXCEEDED), hoặc chuỗi rỗng. */
export function errorCode(res) {
  try {
    const errors = res.json('errors');
    return Array.isArray(errors) && errors.length > 0 ? errors[0].code : '';
  } catch (_) {
    return '';
  }
}

function params(token, name) {
  const headers = token ? Object.assign({ Authorization: `Bearer ${token}` }, HEADERS) : HEADERS;
  return { headers, tags: { name } };
}

/** Một request; `name` là tag để đặt ngưỡng riêng, ví dụ http_req_duration{name:start}. */
export function request(method, path, payload, token, name) {
  const body = payload === undefined ? null : JSON.stringify(payload);
  return track(http.request(method, `${BASE_URL}${path}`, body, params(token, name || path)));
}

/** Tham số cho http.batch (nhiều request song song của cùng một người dùng). */
export function batchItem(method, path, payload, token, name) {
  return {
    method,
    url: `${BASE_URL}${path}`,
    body: payload === undefined ? null : JSON.stringify(payload),
    params: params(token, name || path),
  };
}

export function trackAll(responses) {
  responses.forEach(track);
  return responses;
}

/** Phiên của một người dùng trong một VU (mỗi VU có cookie jar riêng). */
export class Session {
  constructor(userName, password) {
    this.userName = userName;
    this.password = password;
    this.token = null;
    this.expiresAt = 0;
    this.user = null;
  }

  login() {
    const res = request('POST', '/api/auth/login', { userName: this.userName, password: this.password }, null, 'login');
    const ok = check(res, { 'login: 200': (r) => r.status === 200 });
    if (!ok) {
      console.warn(`Đăng nhập ${this.userName} thất bại: ${res.status} ${errorCode(res)}`);
      return false;
    }
    this.accept(data(res));
    return true;
  }

  accept(auth) {
    this.token = auth.accessToken;
    this.expiresAt = Date.parse(auth.expiresAt);
    this.user = auth.user;
  }

  /** Làm mới access token khi còn dưới 60 giây, như interceptor của frontend. */
  ensureFresh() {
    if (this.token && Date.now() < this.expiresAt - 60 * 1000) {
      return true;
    }
    if (this.token && CAN_REFRESH) {
      const res = request('POST', '/api/auth/refresh', undefined, null, 'refresh');
      if (res.status === 200) {
        this.accept(data(res));
        return true;
      }
    }
    return this.login();
  }

  call(method, path, payload, name) {
    this.ensureFresh();
    return request(method, path, payload, this.token, name);
  }

  /** Như call, nhưng chờ theo Retry-After khi bị 429; dùng cho phần chuẩn bị dữ liệu (setup / teardown). */
  send(method, path, payload) {
    for (let attempt = 1; ; attempt++) {
      const res = this.call(method, path, payload, `setup ${method} ${path.split('?')[0]}`);
      if (res.status !== 429 || attempt >= 20) {
        return res;
      }
      sleep(Math.max(1, parseInt(res.headers['Retry-After'] || '5', 10)));
    }
  }

  /** Như send, lỗi thì dừng kịch bản. Trả về phần `data`. */
  must(method, path, payload) {
    const res = this.send(method, path, payload);
    if (res.status >= 200 && res.status < 300) {
      return data(res);
    }
    throw new Error(`${method} ${path} → ${res.status} ${res.body}`);
  }

  /** Mọi phần tử của một danh sách phân trang (pageSize tối đa 100, docs/05-api.md mục 5). */
  all(path, maxItems) {
    const items = [];
    const separator = path.includes('?') ? '&' : '?';
    for (let page = 1; ; page++) {
      const result = this.must('GET', `${path}${separator}page=${page}&pageSize=100`);
      items.push(...result.items);
      if (result.items.length < 100 || items.length >= result.totalCount || items.length >= maxItems) {
        return items;
      }
    }
  }
}

/** Phiên admin cho setup; production bắt buộc đổi mật khẩu khởi tạo trước khi chạy load test. */
export function adminSession(userName, password) {
  const admin = new Session(userName, password);
  if (!admin.login()) {
    throw new Error(`Không đăng nhập được admin "${userName}" tại ${BASE_URL}`);
  }
  if (admin.user.mustChangePassword) {
    throw new Error(`Admin "${userName}" phải đổi mật khẩu trước (MustChangePassword). Đăng nhập UI đổi mật khẩu rồi chạy lại.`);
  }
  return admin;
}

/** Thời điểm tuyệt đối (ms) → ngủ tới đó; dùng để dồn request vào một cửa sổ chung giữa các VU. */
export function sleepUntil(epochMs) {
  const seconds = (epochMs - Date.now()) / 1000;
  if (seconds > 0) {
    sleep(seconds);
  }
}
