import { useRef } from "react";
import { Button, Spinner } from "react-bootstrap";
import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { mediaApi } from "../../services/api";
import { useToast } from "../../components/common/toast";
import type { MediaUpload } from "../../types/api";
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
 * Nút chọn/tải ảnh lên:
 * - Nếu truyền onInserted: tải ảnh trực tiếp lên server qua mediaApi và trả về MediaUpload
 * - Nếu truyền onPicked: kiểm tra file và trả về PendingImage để tải lên sau
 */
export function ImageInsertButton({
  target,
  onInserted,
  onPicked,
  size = "sm",
}: {
  /** Tên phần nhận ảnh cho trình đọc màn hình, ví dụ "đề bài" hay "phần giải thích". */
  target: string;
  onInserted?: (upload: MediaUpload) => void;
  onPicked?: (image: PendingImage) => void;
  size?: "sm" | "lg";
}) {
  const { t } = useTranslation();
  const toast = useToast();
  const input = useRef<HTMLInputElement>(null);

  const upload = useMutation({
    mutationFn: (file: File) => mediaApi.upload(file),
    onSuccess: (result) => {
      onInserted?.(result);
      toast.success(t("media.inserted"));
    },
    onError: (error) => toast.error(error),
  });

  const pick = async (file: File) => {
    if (onInserted) {
      upload.mutate(file);
      return;
    }
    const problem = validateImage(file);
    if (problem) {
      toast.error(t(problem === "type" ? "media.invalidType" : "media.tooLarge"));
      return;
    }
    try {
      onPicked?.({ id: newPendingId(), file, previewUrl: await readAsDataUrl(file) });
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
        disabled={upload.isPending}
        aria-label={t("media.insertInto", { target })}
        title={t("media.hint")}
        onClick={() => input.current?.click()}
      >
        {upload.isPending ? (
          <>
            <Spinner size="sm" animation="border" className="me-1" aria-hidden="true" />
            {t("media.uploading")}
          </>
        ) : (
          <>🖼 {t("media.insert")}</>
        )}
      </Button>
    </>
  );
}
