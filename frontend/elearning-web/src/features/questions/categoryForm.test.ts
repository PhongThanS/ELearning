import { describe, expect, it } from "vitest";
import { nextChildCode, validateCategory } from "./categoryForm";

describe("nextChildCode", () => {
  it("đánh số 01 cho chuyên đề con đầu tiên", () => {
    expect(nextChildCode("ASPNET", ["ASPNET"])).toBe("ASPNET-01");
  });

  it("bỏ qua mã đã dùng, không phân biệt hoa thường", () => {
    expect(nextChildCode("ASPNET", ["ASPNET", "aspnet-01", "ASPNET-02"])).toBe("ASPNET-03");
  });

  it("bỏ dấu phân cách thừa ở cuối mã cha", () => {
    expect(nextChildCode("ASPNET.", [])).toBe("ASPNET-01");
  });
});

describe("validateCategory", () => {
  it("chấp nhận tên và mã hợp lệ", () => {
    expect(validateCategory({ code: "TOAN-12", name: "Đại số 12" })).toEqual({});
  });

  it("từ chối tên chỉ có số", () => {
    expect(validateCategory({ code: "ASPNET-01", name: "12" }).name).toBeTruthy();
  });

  it("từ chối tên quá ngắn", () => {
    expect(validateCategory({ code: "ASPNET-01", name: " a " }).name).toBeTruthy();
  });

  it("từ chối mã kết thúc bằng dấu chấm", () => {
    expect(validateCategory({ code: "ASPNET.", name: "ASP.NET Core" }).code).toBeTruthy();
  });

  it("không kiểm tra mã khi sửa (mã đã khóa)", () => {
    expect(validateCategory({ id: "x", code: "ASPNET.", name: "ASP.NET Core" })).toEqual({});
  });
});
