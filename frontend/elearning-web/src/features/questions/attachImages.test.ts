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

  it("tên bỏ đuôi, chữ thường, bỏ thư mục", () => {
    expect(imageStem("images/HINH-P2-C14.JPG")).toBe("hinh-p2-c14");
  });
});
