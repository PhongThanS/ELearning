import { attachImages, imageStem } from "./MarkdownImportPage";

describe("attachImages — ghép dòng ảnh trong file .md với ảnh đã chọn", () => {
  it("khớp đúng tên trước, không có thì khớp tên bỏ đuôi (.png trong .md ↔ .jpg lấy từ Word)", () => {
    const md = "![Hình](hinh-p1-c3.png)\n![Lời giải](hinh-p1-c3-2.png)\n![Khác](hinh-p1-c9.png)";
    const out = attachImages(md, [
      { id: "pending-a", name: "hinh-p1-c3.jpg" },
      { id: "pending-b", name: "hinh-p1-c3-2.png" },
    ]);
    expect(out).toBe("![Hình](media:pending-a)\n![Lời giải](media:pending-b)\n![Khác](hinh-p1-c9.png)");
  });

  it("AI đặt sai số thứ tự hình trong câu (Gemini: hinh-p1-c2-2 trong khi Word chỉ có hinh-p1-c2) → ghép ảnh cùng câu chưa dùng", () => {
    const md = "![Lời giải](hinh-p1-c2-2.png)\n![Câu 3](hinh-p1-c3.png)\n![Câu 3 lời giải](hinh-p1-c3-5.png)\n![Câu 9](hinh-p1-c9-2.png)";
    const out = attachImages(md, [
      { id: "c2", name: "hinh-p1-c2.jpg" },
      { id: "c3", name: "hinh-p1-c3.jpg" },
      { id: "c3b", name: "hinh-p1-c3-2.jpg" },
      { id: "c10", name: "hinh-p1-c10.jpg" },
    ]);
    // c3 đúng tên; c3-5 lấy ảnh còn lại của câu 3; c9-2 không có ảnh nào của câu 9 → giữ nguyên để báo thiếu (không lấy ảnh câu 10)
    expect(out).toBe("![Lời giải](media:c2)\n![Câu 3](media:c3)\n![Câu 3 lời giải](media:c3b)\n![Câu 9](hinh-p1-c9-2.png)");
  });

  it("AI đánh lại số câu từ 1 mỗi phần (Gemini: hinh-p2-c2) trong khi Word đánh liên tục (Câu 14) → ghép theo vị trí câu", () => {
    const md = [
      "@part: I",
      "@question: 1",
      "@explanation:",
      "![](hinh-p1-c1.png)",
      "@question: 2",
      "@content: x",
      "@part: II",
      "@question: 1",
      "@content: y",
      "@question: 2",
      "@content:",
      "![Đề](hinh-p2-c2.png)",
      "@explanation:",
      "![Lời giải](hinh-p2-c2-2.png)",
      "@question: 3",
      "@explanation:",
      "![](hinh-p2-c3-2.png)",
    ].join("\n");
    const out = attachImages(md, [
      { id: "p1c1", name: "hinh-p1-c1.jpg" },
      { id: "q4", name: "hinh-p2-c4.jpg" },
      { id: "q4b", name: "hinh-p2-c4-2.jpg" },
      { id: "q5", name: "hinh-p2-c5.jpg" },
    ]);
    expect(out).toContain("![Đề](media:q4)");
    expect(out).toContain("![Lời giải](media:q4b)");
    expect(out).toContain("![](media:q5)");
  });

  it("đúng tên thì giữ nguyên, kể cả khi đề đánh số lại từ 1 mỗi phần", () => {
    const md = "@part: I\n@question: 1\n@content: a\n@part: II\n@question: 1\n@content:\n![](hinh-p2-c1.png)";
    const out = attachImages(md, [
      { id: "p2c1", name: "hinh-p2-c1.png" },
      { id: "p2c2", name: "hinh-p2-c2.png" },
    ]);
    expect(out).toContain("![](media:p2c1)");
  });

  it("tên bỏ đuôi, chữ thường, bỏ thư mục", () => {
    expect(imageStem("images/HINH-P2-C14.JPG")).toBe("hinh-p2-c14");
  });
});
