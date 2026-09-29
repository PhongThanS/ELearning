import { createContext, useContext, type ReactNode } from "react";
import type { MediaUrlMap } from "../../types/api";

const MediaUrlsContext = createContext<MediaUrlMap>({});

/**
 * Bảng ảnh (id → URL đã ký) của DTO đang hiển thị (D-27). Mọi MarkdownView bên trong dùng bảng này để
 * hiển thị `![](media:<id>)`; ảnh không có trong bảng (ví dụ ảnh của giải thích chưa được xem) không được tải.
 */
export function MediaUrls({ value, children }: { value: MediaUrlMap | undefined; children: ReactNode }) {
  return <MediaUrlsContext.Provider value={value ?? {}}>{children}</MediaUrlsContext.Provider>;
}

export function useMediaUrls(): MediaUrlMap {
  return useContext(MediaUrlsContext);
}

export const MEDIA_SCHEME = "media:";

/** URL đã ký cho `media:<id>`; chuỗi rỗng nếu không có (react-markdown bỏ ảnh có src rỗng). */
export function resolveMediaUrl(url: string, media: MediaUrlMap): string {
  return media[url.slice(MEDIA_SCHEME.length).toLowerCase()] ?? "";
}
