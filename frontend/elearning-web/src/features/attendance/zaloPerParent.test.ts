import { describe, expect, it } from "vitest";
import { ZALO_PRESETS, fillZaloTemplate, zaloChatUrl } from "./zaloPerParent";

const ctx = { className: "Toán 9A", date: "2026-11-21", topic: "Hệ phương trình", nextHomework: "Bài 1–5 trang 20" };
const student = {
  fullName: "Nguyễn Văn An",
  parentName: "Nguyễn Văn Bình",
  status: "PRESENT" as const,
  homeworkStatus: "DONE" as const,
  homeworkScore: 8.5,
  note: "Tiến bộ",
};

describe("fillZaloTemplate", () => {
  it("điền tên, lớp, ngày và dữ liệu buổi học của từng học sinh", () => {
    const text = fillZaloTemplate(ZALO_PRESETS.report.text, student, ctx);
    expect(text).toContain("Kính gửi phụ huynh Nguyễn Văn Bình,");
    expect(text).toContain("Lớp Toán 9A gửi kết quả buổi học ngày 21/11/2026 của em Nguyễn Văn An:");
    expect(text).toContain("(8,5 điểm)");
    expect(text).toContain("- Nhận xét: Tiến bộ");
    expect(text).toContain("- BTVN buổi tới: Bài 1–5 trang 20");
    expect(text).not.toMatch(/\{[^}]+\}/);
  });

  it("thiếu tên phụ huynh thì gọi theo tên học sinh; thiếu dữ liệu ghi —; chỗ trống sai giữ nguyên", () => {
    const text = fillZaloTemplate(
      "Kính gửi phụ huynh {phụ huynh}. Nhận xét: {nhận xét}. {Điểm Danh} {không có}",
      { ...student, parentName: null, note: null, status: null },
      { ...ctx, topic: "" },
    );
    expect(text).toBe("Kính gửi phụ huynh em Nguyễn Văn An. Nhận xét: —. Chưa điểm danh {không có}");
  });
});

describe("zaloChatUrl", () => {
  it("chuẩn hóa số điện thoại thành link zalo.me", () => {
    expect(zaloChatUrl("0912 345 678")).toBe("https://zalo.me/0912345678");
    expect(zaloChatUrl("+84912345678")).toBe("https://zalo.me/0912345678");
    expect(zaloChatUrl(null)).toBeNull();
    expect(zaloChatUrl("123")).toBeNull();
  });
});
