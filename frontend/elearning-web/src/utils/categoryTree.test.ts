import { describe, expect, it } from "vitest";
import { categoryLabel, orderCategories } from "./categoryTree";

const c = (id: string, name: string, parentId: string | null = null, parentName: string | null = null) => ({ id, name, parentId, parentName });

describe("orderCategories", () => {
  it("đặt chuyên đề con ngay dưới cha, giữ thứ tự chuyên đề gốc", () => {
    const items = [c("b", "Bất phương trình"), c("x2", "Đơn điệu", "h", "Hàm số"), c("h", "Hàm số"), c("x1", "Cực trị", "h", "Hàm số"), c("o", "Mồ côi", "z", "Đã ẩn")];
    expect(orderCategories(items).map((i) => i.id)).toEqual(["b", "h", "x1", "x2", "o"]);
  });

  it("tên hiển thị của chuyên đề con ghi kèm cha", () => {
    expect(categoryLabel(c("x1", "Cực trị", "h", "Hàm số"))).toBe("Hàm số › Cực trị");
    expect(categoryLabel(c("h", "Hàm số"))).toBe("Hàm số");
  });
});
