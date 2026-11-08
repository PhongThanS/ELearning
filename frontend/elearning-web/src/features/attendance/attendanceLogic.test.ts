import type { AttendanceEntry } from "../../types/api";
import {
  appendNote,
  buildMonthlyReport,
  buildZaloReport,
  computeStats,
  homeworkSummary,
  matchesFilter,
  monthlyStats,
  parseScore,
  phoneDigits,
  scoreVariant,
  vnDate,
} from "./attendanceLogic";

const entry = (over: Partial<AttendanceEntry>): AttendanceEntry => ({ status: null, homeworkStatus: null, homeworkScore: null, note: null, ...over });

describe("attendanceLogic — điểm danh", () => {
  it("thống kê sĩ số: có mặt + muộn là tham gia", () => {
    const stats = computeStats([entry({ status: "PRESENT" }), entry({ status: "LATE" }), entry({ status: "UNEXCUSED" }), entry({})]);
    expect(stats).toMatchObject({ total: 4, attended: 2, rate: 50, unexcused: 1, unmarked: 1 });
  });

  it("lọc BTVN cần lưu ý: làm thiếu, không làm hoặc điểm < 5", () => {
    expect(matchesFilter(entry({ homeworkStatus: "PARTIAL" }), "HW_ISSUE")).toBe(true);
    expect(matchesFilter(entry({ homeworkScore: 4.5 }), "HW_ISSUE")).toBe(true);
    expect(matchesFilter(entry({ homeworkStatus: "DONE", homeworkScore: 7 }), "HW_ISSUE")).toBe(false);
    expect(matchesFilter(entry({}), "UNMARKED")).toBe(true);
  });

  it("điểm làm tròn bước 0,5, kẹp 0–10, nhận dấu phẩy", () => {
    expect(parseScore("7,3")).toBe(7.5);
    expect(parseScore("12")).toBe(10);
    expect(parseScore(" ")).toBeNull();
    expect(scoreVariant(8)).toBe("success");
    expect(scoreVariant(4.5)).toBe("danger");
    expect(homeworkSummary([entry({ homeworkScore: 8 }), entry({ homeworkScore: 7 })]).average).toBe(7.5);
  });

  it("nhận xét nhanh không lặp lại", () => {
    expect(appendNote(null, "Có tiến bộ")).toBe("Có tiến bộ.");
    expect(appendNote("Có tiến bộ.", "Có tiến bộ")).toBe("Có tiến bộ.");
    expect(appendNote("Tốt", "Có tiến bộ")).toBe("Tốt. Có tiến bộ.");
  });

  it("số điện thoại cho link Zalo", () => {
    expect(phoneDigits("+84 912 345 678")).toBe("0912345678");
    expect(phoneDigits("0912.345.678")).toBe("0912345678");
    expect(phoneDigits("123")).toBeNull();
  });

  it("báo cáo Zalo chi tiết liệt kê vắng và tuyên dương, xếp điểm giảm dần", () => {
    const text = buildZaloReport(
      [
        { ...entry({ status: "PRESENT", homeworkScore: 6 }), fullName: "An" },
        { ...entry({ status: "PRESENT", homeworkScore: 9 }), fullName: "Bình" },
        { ...entry({ status: "UNEXCUSED" }), fullName: "Chi" },
      ],
      { className: "Toán 12A", date: "2026-11-08", topic: "Tích phân", nextHomework: "Bài 1-5", style: "detailed", includeScoreList: true, sortByScore: true },
    );
    expect(text).toContain("📅 Ngày học: 08/11/2026");
    expect(text).toContain("SĨ SỐ: 2/3 HS tham gia (67%)");
    expect(text).toContain("Vắng không phép (1): Chi");
    expect(text).toContain("- Bình (9đ)");
    expect(text.indexOf("1. Bình")).toBeGreaterThan(0);
    expect(text).toContain("DẶN DÒ BTVN BUỔI TỚI: Bài 1-5");
  });

  it("tổng kết tháng", () => {
    const items = [
      { sessionDate: "2026-11-03", topic: null, status: "PRESENT" as const, homeworkStatus: "DONE" as const, homeworkScore: 8, note: null },
      { sessionDate: "2026-11-05", topic: null, status: "UNEXCUSED" as const, homeworkStatus: "MISSING" as const, homeworkScore: 4, note: null },
    ];
    expect(monthlyStats(items)).toMatchObject({ average: 6, max: 8, min: 4, attendanceRate: 50, homeworkRate: 50 });
    const text = buildMonthlyReport({
      studentId: "s", userName: "an", fullName: "An", parentName: null, parentPhone: null, classroomName: "Toán 12A",
      year: 2026, month: 11, averageScore: 6, classRank: 3, rankedCount: 10, items,
    });
    expect(text).toContain("TỔNG KẾT THÁNG 11/2026 — An");
    expect(text).toContain("xếp hạng 3/10");
  });

  it("dịch ngày", () => {
    expect(vnDate(1, "2026-11-30")).toBe("2026-12-01");
    expect(vnDate(-1, "2026-03-01")).toBe("2026-02-28");
  });
});
