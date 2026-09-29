import { matchesAny, normalizeAnswer } from "./answerNormalizer";
import { formatDateTime, formatDuration, markdownExcerpt, utcIsoToVnLocal, vnLocalToUtcIso } from "./format";

describe("answerNormalizer (khớp thuật toán server)", () => {
  it("NFC: chuỗi tổ hợp khớp chuỗi dựng sẵn", () => {
    expect(normalizeAnswer("Hà Nội".normalize("NFD"), false, false)).toBe(normalizeAnswer("Hà Nội", false, false));
  });

  it("gộp khoảng trắng và không phân biệt hoa thường", () => {
    expect(normalizeAnswer("  HÀ \t  Nội ", false, false)).toBe("hà nội");
  });

  it("bỏ dấu kể cả đ/Đ", () => {
    expect(matchesAny("da nang", ["Đà Nẵng"], false, true)).toBe(true);
    expect(matchesAny("da nang", ["Đà Nẵng"], false, false)).toBe(false);
  });

  it("phân biệt hoa thường khi bật", () => {
    expect(matchesAny("async", ["Async"], true, false)).toBe(false);
  });
});

describe("format", () => {
  it("hiển thị theo giờ Việt Nam dd/MM/yyyy HH:mm", () => {
    expect(formatDateTime("2026-09-29T02:05:00.000Z")).toBe("29/09/2026 09:05");
    expect(formatDateTime(null)).toBe("—");
  });

  it("ô datetime-local hiểu là giờ Việt Nam", () => {
    expect(vnLocalToUtcIso("2026-09-29T09:05")).toBe("2026-09-29T02:05:00.000Z");
    expect(utcIsoToVnLocal("2026-09-29T02:05:00.000Z")).toBe("2026-09-29T09:05");
    expect(vnLocalToUtcIso("")).toBeNull();
  });

  it("rút gọn Markdown thành văn bản thuần", () => {
    expect(markdownExcerpt("Đoạn code sau in ra gì? ```csharp\nvar x = 1;\n```")).toBe("Đoạn code sau in ra gì? var x = 1;");
    expect(markdownExcerpt("Những từ khóa nào là **access modifier** trong `C#`?")).toBe("Những từ khóa nào là access modifier trong C#?");
    expect(markdownExcerpt("# Tiêu đề\n- [liên kết](https://x.vn)")).toBe("Tiêu đề liên kết");
  });

  it("thời lượng", () => {
    expect(formatDuration(65)).toBe("01:05");
    expect(formatDuration(3725)).toBe("1:02:05");
  });
});
