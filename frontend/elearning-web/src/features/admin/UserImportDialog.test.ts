import { describe, expect, it } from "vitest";
import { accountsCsv } from "./UserImportDialog";

describe("accountsCsv", () => {
  it("có BOM, tiêu đề và mật khẩu tạm; escape dấu ngoặc kép", () => {
    const csv = accountsCsv([
      { rowNumber: 2, userName: "hs.a", fullName: 'Nguyễn "A"', roles: ["Học viên"], classes: ["10A1", "10A2"], temporaryPassword: "Xyz23456abcd", issues: [] },
      { rowNumber: 3, userName: "gv.b", fullName: "Lê B", roles: ["Giáo viên"], classes: [], temporaryPassword: null, issues: [] },
    ]);
    const lines = csv.split("\r\n");

    expect(csv.startsWith("﻿")).toBe(true);
    expect(lines[0]).toBe('﻿"Tên đăng nhập","Họ tên","Vai trò","Lớp","Mật khẩu tạm"');
    expect(lines[1]).toBe('"hs.a","Nguyễn ""A""","Học viên","10A1, 10A2","Xyz23456abcd"');
    expect(lines[2]).toBe('"gv.b","Lê B","Giáo viên","","(đã đặt trong file)"');
  });
});
