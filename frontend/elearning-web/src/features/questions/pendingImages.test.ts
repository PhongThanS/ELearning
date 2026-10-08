import { imageMarkdown, referencedPending, removeImageRefs, replacePendingIds, validateImage, type PendingImage } from "./pendingImages";

const image = (id: string): PendingImage => ({ id, file: new File(["x"], `${id}.png`, { type: "image/png" }), previewUrl: "data:image/png;base64,eA==" });

describe("pendingImages (ảnh chỉ lên server khi bấm Lưu)", () => {
  it("chỉ lấy ảnh chờ lưu còn được tham chiếu, không trùng", () => {
    const pending = [image("pending-aaa"), image("pending-bbb"), image("pending-ccc")];
    const texts = [`Đề ${imageMarkdown("pending-aaa")} và ${imageMarkdown("pending-aaa")}`, "", `Lựa chọn ![x](media:PENDING-CCC)`];
    expect(referencedPending(texts, pending).map((p) => p.id)).toEqual(["pending-aaa", "pending-ccc"]);
  });

  it("thay mã tạm bằng id thật, giữ nguyên ảnh đã lưu và mã chưa có id", () => {
    const text = "A ![a](media:pending-aaa) B ![b](media:0f1e2d3c) C ![c](media:pending-zzz)";
    expect(replacePendingIds(text, { "pending-aaa": "11111111-2222-3333-4444-555555555555" })).toBe(
      "A ![a](media:11111111-2222-3333-4444-555555555555) B ![b](media:0f1e2d3c) C ![c](media:pending-zzz)",
    );
  });

  it("bỏ ảnh: xóa mọi chỗ đã chèn ảnh đó", () => {
    expect(removeImageRefs("Hình ![sơ đồ](media:pending-aaa) và ![](media:pending-aaa)", "pending-aaa")).toBe("Hình và");
    expect(removeImageRefs("Giữ ![x](media:pending-bbb)", "pending-aaa")).toBe("Giữ ![x](media:pending-bbb)");
  });

  it("kiểm tra loại và dung lượng ngay trên máy", () => {
    expect(validateImage(new File(["x"], "a.svg", { type: "image/svg+xml" }))).toBe("type");
    expect(validateImage(new File([new Uint8Array(2 * 1024 * 1024 + 1)], "a.png", { type: "image/png" }))).toBe("size");
    expect(validateImage(new File(["x"], "a.webp", { type: "image/webp" }))).toBeNull();
  });
});
