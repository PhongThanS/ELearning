import { describe, expect, it } from "vitest";
import { questionCodeExample } from "./MarkdownImportDialog";

describe("questionCodeExample", () => {
  it("mã chuyên đề-mức độ 2 số-số thứ tự 6 số", () => {
    expect(questionCodeExample("12.HS", 1, 1)).toBe("12.HS-01-000001");
    expect(questionCodeExample("12.HS", 0, 123)).toBe("12.HS-00-000123");
  });

  it("ký tự không dùng được trong mã câu hỏi đổi thành gạch dưới", () => {
    expect(questionCodeExample("C#+", 4, 2)).toBe("C__-04-000002");
  });
});
