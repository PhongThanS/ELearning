/**
 * Ảnh chờ lưu trong trình soạn câu hỏi (D-27): chọn ảnh chỉ xem trước trên máy (data URL), chưa gửi lên server.
 * Nội dung tạm tham chiếu `media:pending-xxxx`; khi bấm Lưu, chỉ ảnh còn được dùng mới được tải lên, rồi mã tạm
 * được thay bằng id thật trước khi lưu câu hỏi. Ảnh bị xóa khỏi nội dung không bao giờ lên server.
 */
export const PENDING_PREFIX = "pending-";

/** Loại ảnh server nhận (server vẫn kiểm tra lại theo nội dung file). */
export const IMAGE_TYPES = ["image/png", "image/jpeg", "image/gif", "image/webp"];
export const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

export interface PendingImage {
  /** Mã tạm, ví dụ "pending-1a2b3c4d" (chữ thường để khớp cách MarkdownView tra bảng ảnh). */
  id: string;
  file: File;
  /** data URL để xem trước (CSP cho phép img-src data:). */
  previewUrl: string;
}

/** Lỗi nếu file không phải ảnh hợp lệ; null nếu hợp lệ. */
export function validateImage(file: File): "type" | "size" | null {
  if (!IMAGE_TYPES.includes(file.type)) {
    return "type";
  }
  return file.size > MAX_IMAGE_BYTES ? "size" : null;
}

export function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(reader.error ?? new Error("Không đọc được file ảnh."));
    reader.readAsDataURL(file);
  });
}

export function newPendingId(): string {
  return `${PENDING_PREFIX}${crypto.randomUUID().replaceAll("-", "").slice(0, 12)}`;
}

export function imageMarkdown(id: string, alt = ""): string {
  return `![${alt}](media:${id})`;
}

const PENDING_REF = /media:(pending-[0-9a-z]+)/gi;

/** Ảnh chờ lưu còn được tham chiếu trong ít nhất một đoạn văn bản (theo thứ tự xuất hiện, không trùng). */
export function referencedPending(texts: readonly string[], pending: readonly PendingImage[]): PendingImage[] {
  const used = new Set<string>();
  for (const text of texts) {
    for (const match of text.matchAll(PENDING_REF)) {
      used.add(match[1]!.toLowerCase());
    }
  }
  return pending.filter((p) => used.has(p.id));
}

/** Thay `media:pending-x` bằng `media:<id thật>` theo bảng thay thế. */
export function replacePendingIds(text: string, replacements: Readonly<Record<string, string>>): string {
  return text.replace(PENDING_REF, (whole, id: string) => {
    const real = replacements[id.toLowerCase()];
    return real ? `media:${real}` : whole;
  });
}

/** Xóa mọi ảnh `![...](media:<id>)` có mã cho trước khỏi văn bản (khi bỏ một ảnh chờ lưu). */
export function removeImageRefs(text: string, id: string): string {
  const escaped = id.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return text
    .replace(new RegExp(`\\s?!\\[[^\\]]*\\]\\(media:${escaped}\\)`, "gi"), "")
    .trimEnd();
}
