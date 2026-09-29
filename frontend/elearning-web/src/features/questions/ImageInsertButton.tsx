import { useRef } from "react";
import { Button, Spinner } from "react-bootstrap";
import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { mediaApi } from "../../services/api";
import { useToast } from "../../components/common/toast";
import type { MediaUpload } from "../../types/api";

/** Loại ảnh server nhận (D-27); server vẫn kiểm tra lại theo nội dung file. */
const ACCEPT = "image/png,image/jpeg,image/gif,image/webp";

/** Chèn chuỗi vào vị trí con trỏ (hoặc cuối), thêm khoảng trắng để ảnh không dính vào chữ. */
export function insertAt(text: string, insert: string, position: number | null | undefined): string {
  const at = position == null || position < 0 || position > text.length ? text.length : position;
  const before = text.slice(0, at);
  const after = text.slice(at);
  const lead = before && !/\s$/.test(before) ? " " : "";
  const trail = after && !/^\s/.test(after) ? " " : "";
  return `${before}${lead}${insert}${trail}${after}`;
}

/** Nút tải ảnh lên và trả về chuỗi Markdown `![](media:<id>)` để chèn vào nội dung. */
export function ImageInsertButton({
  target,
  onInserted,
  size = "sm",
}: {
  /** Tên phần nhận ảnh cho trình đọc màn hình, ví dụ "đề bài" hay "lựa chọn A". */
  target: string;
  onInserted: (upload: MediaUpload) => void;
  size?: "sm" | "lg";
}) {
  const { t } = useTranslation();
  const toast = useToast();
  const input = useRef<HTMLInputElement>(null);
  const upload = useMutation({
    mutationFn: (file: File) => mediaApi.upload(file),
    onSuccess: (result) => {
      onInserted(result);
      toast.success(t("media.inserted"));
    },
    onError: (error) => toast.error(error),
  });

  return (
    <>
      <input
        ref={input}
        type="file"
        accept={ACCEPT}
        className="d-none"
        aria-hidden="true"
        tabIndex={-1}
        onChange={(e) => {
          const file = e.target.files?.[0];
          e.target.value = "";
          if (file) {
            upload.mutate(file);
          }
        }}
      />
      <Button
        variant="outline-secondary"
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
