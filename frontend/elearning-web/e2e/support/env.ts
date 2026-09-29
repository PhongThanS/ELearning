/** Cấu hình E2E; mọi giá trị ghi đè được bằng biến môi trường. */
export const API_PORT = Number(process.env.E2E_API_PORT ?? 5236);
export const WEB_PORT = Number(process.env.E2E_WEB_PORT ?? 5273);
export const BASE_URL = process.env.E2E_BASE_URL ?? `http://localhost:${WEB_PORT}`;

/** Tài khoản admin do seed tạo ở môi trường Development (appsettings.Development.json). */
export const ADMIN_USER = process.env.E2E_ADMIN_USER ?? "admin";
export const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD ?? "Admin@123456";

/** Mật khẩu cho học viên do test tạo. */
export const STUDENT_PASSWORD = "HocVien@E2E2026";

/** Hậu tố duy nhất cho dữ liệu của một lần chạy (mã đề, nhóm, user không trùng giữa các lần chạy). */
export function uniqueId(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`.toUpperCase();
}
