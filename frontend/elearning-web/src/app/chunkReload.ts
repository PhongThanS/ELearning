/**
 * Sau mỗi lần deploy, tên file JS của từng trang đổi (có mã băm). Tab đã mở từ trước vẫn trỏ tới tên cũ nên khi chuyển
 * trang sẽ lỗi "Failed to fetch dynamically imported module". Gặp lỗi đó thì tải lại trang một lần để lấy bản mới;
 * nếu vừa tải lại mà vẫn lỗi (mất mạng, server hỏng) thì để lỗi hiện ra, không lặp vô hạn.
 */

const KEY = "elearning:chunk-reload-at";
const WINDOW_MS = 30_000;

export function isChunkLoadError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error);
  return /Failed to fetch dynamically imported module|error loading dynamically imported module|Importing a module script failed|ChunkLoadError/i.test(message);
}

/** true nếu đã yêu cầu tải lại trang (người gọi nên dừng, không ném lỗi ra màn hình). */
export function reloadOnceForNewVersion(error: unknown, now = Date.now(), reload: () => void = () => window.location.reload()): boolean {
  if (!isChunkLoadError(error)) {
    return false;
  }
  try {
    const last = Number(window.sessionStorage.getItem(KEY) ?? 0);
    if (now - last < WINDOW_MS) {
      return false;
    }
    window.sessionStorage.setItem(KEY, String(now));
  } catch {
    // Không đọc / ghi được sessionStorage (chế độ riêng tư…): vẫn thử tải lại một lần
  }
  reload();
  return true;
}

/** Bọc hàm import() của một trang: gặp file cũ đã bị thay thì tải lại trang thay vì hiện lỗi. */
export function withChunkReload<M>(loader: () => Promise<M>): () => Promise<M> {
  return () =>
    loader().catch((error: unknown) => {
      if (reloadOnceForNewVersion(error)) {
        // Trang đang tải lại: giữ Suspense ở trạng thái chờ thay vì báo lỗi
        return new Promise<M>(() => {});
      }
      throw error;
    });
}
