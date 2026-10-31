import { beforeEach, describe, expect, it, vi } from "vitest";
import { isChunkLoadError, reloadOnceForNewVersion } from "./chunkReload";

const stale = new TypeError("Failed to fetch dynamically imported module: http://localhost:8080/assets/QuestionPages-yUrstGCo.js");

describe("chunkReload", () => {
  beforeEach(() => window.sessionStorage.clear());

  it("nhận ra lỗi file trang cũ sau deploy, bỏ qua lỗi khác", () => {
    expect(isChunkLoadError(stale)).toBe(true);
    expect(isChunkLoadError(new Error("Importing a module script failed."))).toBe(true);
    expect(isChunkLoadError(new Error("Network Error"))).toBe(false);
  });

  it("tải lại đúng một lần trong 30 giây, không lặp vô hạn", () => {
    const reload = vi.fn();

    expect(reloadOnceForNewVersion(stale, 1_000_000, reload)).toBe(true);
    expect(reloadOnceForNewVersion(stale, 1_010_000, reload)).toBe(false);
    expect(reloadOnceForNewVersion(stale, 1_040_000, reload)).toBe(true);
    expect(reloadOnceForNewVersion(new Error("Network Error"), 2_000_000, reload)).toBe(false);
    expect(reload).toHaveBeenCalledTimes(2);
  });
});
