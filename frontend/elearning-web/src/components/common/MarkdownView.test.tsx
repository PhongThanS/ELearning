import { render, screen } from "@testing-library/react";
import { MarkdownView } from "./MarkdownView";
import { MediaUrls } from "./MediaUrls";
import { insertAt } from "../../features/questions/ImageInsertButton";

const ID = "0f8fad5b-d9cb-469f-a165-70867728950e";
const SIGNED = `/api/media/${ID}?exp=1790000000&sig=abc`;

describe("MarkdownView ảnh (D-27)", () => {
  it("hiển thị media:<id> bằng URL đã ký trong bảng ảnh", () => {
    render(
      <MediaUrls value={{ [ID]: SIGNED }}>
        <MarkdownView content={`Hình: ![sơ đồ lớp](media:${ID.toUpperCase()})`} />
      </MediaUrls>,
    );

    const image = screen.getByRole("img", { name: "sơ đồ lớp" });
    expect(image).toHaveAttribute("src", SIGNED);
    expect(image).toHaveAttribute("loading", "lazy");
  });

  it("ảnh không có trong bảng (ví dụ giải thích chưa được xem) không được tải", () => {
    render(<MarkdownView content={`![](media:${ID})`} media={{}} />);

    expect(screen.queryByRole("img")).toBeNull();
    expect(screen.getByText("(ảnh không hiển thị được)")).toBeInTheDocument();
  });

  it("không tải ảnh ngoài và không biến media: thành liên kết", () => {
    render(
      <MediaUrls value={{ [ID]: SIGNED }}>
        <MarkdownView content={`![x](https://evil.example/track.png) [mở](media:${ID}) [web](https://example.com)`} />
      </MediaUrls>,
    );

    expect(screen.queryByRole("img")).toBeNull();
    expect(screen.getByText("mở").closest("a")).not.toHaveAttribute("href", SIGNED);
    expect(screen.getByRole("link", { name: "web" })).toHaveAttribute("href", "https://example.com");
  });

  it("chế độ inline (lựa chọn đáp án) vẫn hiển thị ảnh", () => {
    render(<MarkdownView content={`Hình ![A](media:${ID})`} inline media={{ [ID]: SIGNED }} />);

    expect(screen.getByRole("img", { name: "A" })).toHaveAttribute("src", SIGNED);
  });
});

describe("insertAt", () => {
  it("chèn tại con trỏ và tách khỏi chữ bằng khoảng trắng", () => {
    expect(insertAt("Hình và chữ", "![](media:x)", 4)).toBe("Hình ![](media:x) và chữ");
    expect(insertAt("abc", "IMG", null)).toBe("abc IMG");
    expect(insertAt("", "IMG", 0)).toBe("IMG");
    expect(insertAt("abc ", "IMG", 99)).toBe("abc IMG");
  });
});
