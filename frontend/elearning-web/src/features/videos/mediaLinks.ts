/**
 * Nhận dạng link trong kho bài giảng: YouTube, Google Drive (mọi loại tệp: PDF, Word, ảnh, video…),
 * Google Docs / Sheets / Slides, hoặc file video trực tiếp. Tệp Drive phải chia sẻ
 * "Bất kỳ ai có đường liên kết" thì mới xem được khi nhúng.
 */

export type LessonKind = "youtube" | "drive" | "video" | "link";

export function extractYoutubeId(url: string): string | null {
  if (!url) return null;
  const trimmed = url.trim();
  const match = trimmed.match(/(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.*[&?]v=|shorts\/|live\/))([\w-]{11})/i);
  if (match && match[1]) return match[1];
  if (trimmed.length === 11 && /^[\w-]{11}$/.test(trimmed)) return trimmed;
  return null;
}

interface DriveFile {
  id: string;
  /** Trang xem trước nhúng được bằng iframe. */
  previewUrl: string;
  /** Trang mở tệp trên Google (tab mới). */
  openUrl: string;
}

const GOOGLE_EDITORS = ["document", "spreadsheets", "presentation"] as const;

/** Link Drive / Docs / Sheets / Slides → mã tệp và các URL xem. Null nếu không phải link Google. */
export function parseDriveLink(url: string): DriveFile | null {
  if (!url) return null;
  const trimmed = url.trim();

  const editor = trimmed.match(/docs\.google\.com\/(document|spreadsheets|presentation)\/d\/([\w-]{10,})/i);
  if (editor && editor[1] && editor[2]) {
    const kind = editor[1].toLowerCase() as (typeof GOOGLE_EDITORS)[number];
    return {
      id: editor[2],
      previewUrl: `https://docs.google.com/${kind}/d/${editor[2]}/preview`,
      openUrl: `https://docs.google.com/${kind}/d/${editor[2]}/view`,
    };
  }

  const file =
    trimmed.match(/drive\.google\.com\/file\/d\/([\w-]{10,})/i) ??
    trimmed.match(/drive\.google\.com\/(?:open|uc)\?(?:.*&)?id=([\w-]{10,})/i);
  if (file && file[1]) {
    return {
      id: file[1],
      previewUrl: `https://drive.google.com/file/d/${file[1]}/preview`,
      openUrl: `https://drive.google.com/file/d/${file[1]}/view`,
    };
  }
  return null;
}

/** Ảnh bìa trang đầu của tệp Drive (Google tự sinh; tệp riêng tư thì không có). */
export function driveThumbnailUrl(id: string): string {
  return `https://drive.google.com/thumbnail?id=${encodeURIComponent(id)}&sz=w640`;
}

export function isDirectVideo(url: string): boolean {
  if (!url) return false;
  return /\.(mp4|webm|ogg|mov)(\?.*)?$/i.test(url.trim());
}

export function lessonKind(url: string, youtubeVideoId?: string | null): LessonKind {
  if (youtubeVideoId || extractYoutubeId(url)) return "youtube";
  if (parseDriveLink(url)) return "drive";
  if (isDirectVideo(url)) return "video";
  return "link";
}

/** Ảnh bìa cho thẻ bài giảng: YouTube hoặc Drive; null nếu không có. */
export function lessonThumbnail(url: string, youtubeVideoId?: string | null, thumbnailUrl?: string | null): string | null {
  if (thumbnailUrl) return thumbnailUrl;
  const yt = youtubeVideoId || extractYoutubeId(url);
  if (yt) return `https://img.youtube.com/vi/${yt}/mqdefault.jpg`;
  const drive = parseDriveLink(url);
  return drive ? driveThumbnailUrl(drive.id) : null;
}
