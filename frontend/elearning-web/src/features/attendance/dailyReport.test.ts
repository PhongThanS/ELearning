import { describe, expect, it } from "vitest";
import { formatReportDate, formatReportScore, sortReportRows, splitVietnameseName } from "./dailyReport";

describe("báo cáo học tập ngày", () => {
  it("tách họ và tên đệm / tên theo chữ cuối", () => {
    expect(splitVietnameseName("Nguyễn Thị Minh Hà")).toEqual(["Nguyễn Thị Minh", "Hà"]);
    expect(splitVietnameseName("  Hà   Thùy  Dung ")).toEqual(["Hà Thùy", "Dung"]);
    expect(splitVietnameseName("Linh")).toEqual(["", "Linh"]);
  });

  it("định dạng điểm hai chữ số thập phân và ngày dd/MM/yyyy", () => {
    expect(formatReportScore(10)).toBe("10,00");
    expect(formatReportScore(9.5)).toBe("9,50");
    expect(formatReportScore(null)).toBe("—");
    expect(formatReportDate("2026-11-16")).toBe("16/11/2026");
  });

  it("xếp điểm cao → thấp, chưa có điểm xuống cuối, cùng điểm theo tên", () => {
    const rows = [
      { firstName: "Phúc", lastName: "Nguyễn An", score: 8.5 },
      { firstName: "Bình", lastName: "Trần", score: null },
      { firstName: "Linh", lastName: "Tạ Phương", score: 10 },
      { firstName: "Dung", lastName: "Hà Thùy", score: 10 },
    ];
    expect(sortReportRows(rows).map((r) => r.firstName)).toEqual(["Dung", "Linh", "Phúc", "Bình"]);
  });
});
