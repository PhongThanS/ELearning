import { defineConfig, devices } from "@playwright/test";
import { API_PORT, BASE_URL, WEB_PORT } from "./e2e/support/env";

/**
 * E2E (docs/08-kiem-thu.md mục 6).
 * - Mặc định tự khởi động backend riêng (cổng 5236, database ELearningE2E, dữ liệu dev seed)
 *   và bản build production của frontend qua `vite preview` (cổng 5273).
 * - Đặt E2E_BASE_URL để chạy trên môi trường có sẵn (staging); khi đó không khởi động server.
 * - PW_CHANNEL=msedge / chrome: dùng trình duyệt đã cài trên máy thay cho Chromium của Playwright.
 */
const external = !!process.env.E2E_BASE_URL;
const channel = process.env.PW_CHANNEL;

export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.e2e.ts",
  // Các kịch bản dùng chung một backend; chạy tuần tự cho dễ đọc log và tránh giới hạn tần suất.
  workers: 1,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  timeout: 120_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: BASE_URL,
    locale: "vi-VN",
    timezoneId: "Asia/Ho_Chi_Minh",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    // Quay video cần ffmpeg của Playwright (CI cài bằng --with-deps); máy dev dùng trace là đủ.
    video: process.env.CI ? "retain-on-failure" : "off",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], ...(channel ? { channel } : {}) },
    },
  ],
  webServer: external
    ? undefined
    : [
        {
          command: "node e2e/scripts/start-api.mjs",
          url: `http://localhost:${API_PORT}/health/ready`,
          timeout: 240_000,
          reuseExistingServer: !process.env.CI,
          stdout: "pipe",
          stderr: "pipe",
        },
        {
          command: `npm run build && npx vite preview --port ${WEB_PORT} --strictPort`,
          url: `http://localhost:${WEB_PORT}`,
          timeout: 180_000,
          reuseExistingServer: !process.env.CI,
          env: { VITE_API_TARGET: `http://localhost:${API_PORT}` },
        },
      ],
});
