import { useRef } from "react";
import { Button } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import { useToast } from "../../components/common/toast";
import { IMAGE_TYPES, newPendingId, readAsDataUrl, validateImage, type PendingImage } from "./pendingImages";

/** Chèn chuỗi vào vị trí con trỏ (hoặc cuối), thêm khoảng trắng để ảnh không dính vào chữ. */
export function insertAt(text: string, insert: string, position: number | null | undefined): string {
  const at = position == null || position < 0 || position > text.length ? text.length : position;
  const before = text.slice(0, at);
  const after = text.slice(at);
  const lead = before && !/\s$/.test(before) ? " " : "";
  const trail = after && !/^\s/.test(after) ? " " : "";
  return `${before}${lead}${insert}${trail}${after}`;
}

/**
 * Nút chọn ảnh: kiểm tra loại / dung lượng ngay trên máy và trả về ảnh chờ lưu để xem trước.
 * Ảnh chỉ được tải lên server khi người dùng bấm Lưu câu hỏi (xem pendingImages.ts).
 */
export function ImageInsertButton({
  target,
  onPicked,
  size = "sm",
}: {
  /** Tên phần nhận ảnh cho trình đọc màn hình, ví dụ "đề bài" hay "lựa chọn A". */
  target: string;
  onPicked: (image: PendingImage) => void;
  size?: "sm" | "lg";
}) {
  const { t } = useTranslation();
  const toast = useToast();
  const input = useRef<HTMLInputElement>(null);

  const pick = async (file: File) => {
    const problem = validateImage(file);
    if (problem) {
      toast.error(t(problem === "type" ? "media.invalidType" : "media.tooLarge"));
      return;
    }
    try {
      onPicked({ id: newPendingId(), file, previewUrl: await readAsDataUrl(file) });
      toast.info(t("media.picked"));
    } catch (error) {
      toast.error(error);
    }
  };

  return (
    <>
      <input
        ref={input}
        type="file"
        accept={IMAGE_TYPES.join(",")}
        className="d-none"
        aria-hidden="true"
        tabIndex={-1}
        onChange={(e) => {
          const file = e.target.files?.[0];
          e.target.value = "";
          if (file) {
            void pick(file);
          }
        }}
      />
      <Button
        variant="outline-primary"
        size={size}
        aria-label={t("media.insertInto", { target })}
        title={t("media.hint")}
        onClick={() => input.current?.click()}
      >
        🖼 {t("media.insert")}
      </Button>
    </>
  );
}
