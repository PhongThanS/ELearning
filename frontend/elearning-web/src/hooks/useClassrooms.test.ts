import { describe, expect, it } from "vitest";
import { matchClassroom } from "./useClassrooms";

describe("matchClassroom", () => {
  const lop = { code: "LOP-10A1-7395", name: "Lớp 10A1 Chuyên Toán" };

  it("tìm theo mã lớp, không phân biệt hoa thường", () => {
    expect(matchClassroom(lop, "lop-10a1")).toBe(true);
    expect(matchClassroom(lop, "7395")).toBe(true);
  });

  it("tìm theo tên lớp, gõ không dấu vẫn khớp", () => {
    expect(matchClassroom(lop, "chuyên toán")).toBe(true);
    expect(matchClassroom(lop, "chuyen toan")).toBe(true);
  });

  it("từ khóa trống thì khớp tất cả; sai thì không khớp", () => {
    expect(matchClassroom(lop, "  ")).toBe(true);
    expect(matchClassroom(lop, "11B2")).toBe(false);
  });
});
