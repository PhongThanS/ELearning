/** Hiển thị theo giờ Việt Nam, định dạng dd/MM/yyyy HH:mm (docs/06-frontend.md mục 5). */
export const BUSINESS_TIME_ZONE = "Asia/Ho_Chi_Minh";

const dateTimeFormatter = new Intl.DateTimeFormat("vi-VN", {
  timeZone: BUSINESS_TIME_ZONE,
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hour12: false,
});

export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return "—";
  }
  const parts = Object.fromEntries(dateTimeFormatter.formatToParts(new Date(value)).map((p) => [p.type, p.value]));
  return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}`;
}

/**
 * Đổi giá trị ô <input type="datetime-local"> (hiểu là giờ Việt Nam, UTC+7, không có giờ mùa hè) sang ISO UTC.
 */
export function vnLocalToUtcIso(local: string | null | undefined): string | null {
  if (!local) {
    return null;
  }
  const date = new Date(`${local}:00+07:00`);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/** ISO UTC → giá trị cho <input type="datetime-local"> theo giờ Việt Nam. */
export function utcIsoToVnLocal(iso: string | null | undefined): string {
  if (!iso) {
    return "";
  }
  const shifted = new Date(new Date(iso).getTime() + 7 * 60 * 60 * 1000);
  return shifted.toISOString().slice(0, 16);
}

export function formatDuration(totalSeconds: number | null | undefined): string {
  if (totalSeconds == null || totalSeconds < 0) {
    return "—";
  }
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = Math.floor(totalSeconds % 60);
  const mm = String(minutes).padStart(2, "0");
  const ss = String(seconds).padStart(2, "0");
  return hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`;
}

const numberFormatter = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });

export function formatNumber(value: number | null | undefined): string {
  return value == null ? "—" : numberFormatter.format(value);
}

export function formatScore(score: number | null | undefined, max: number | null | undefined): string {
  return score == null ? "—" : `${formatNumber(score)}${max == null ? "" : ` / ${formatNumber(max)}`}`;
}

/** Rút gọn Markdown thành một dòng văn bản thuần cho cột "Nội dung" trong bảng. */
export function markdownExcerpt(markdown: string): string {
  return markdown
    .replace(/```[\w-]*/g, " ")
    .replace(/!\[[^\]]*\]\([^)]*\)/g, " ")
    .replace(/\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/^\s{0,3}(#{1,6}|>|[-*+]|\d+\.)\s+/gm, "")
    .replace(/(\*\*|__|~~|`)/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

/**
 * Nhãn lựa chọn theo vị trí hiển thị (A, B, C…). Khi đề xáo đáp án, mã gốc vẫn được gửi lên server
 * nhưng học viên luôn thấy nhãn theo thứ tự trên màn hình.
 */
export function optionLabel(index: number): string {
  return String.fromCharCode(65 + index);
}

/** Ngày không có giờ ("yyyy-MM-dd", ví dụ ngày học của lớp) → "dd/MM/yyyy". Không đổi múi giờ. */
export function formatDateOnly(value: string | null | undefined): string {
  if (!value) {
    return "—";
  }
  const [y, m, d] = value.split("-");
  return `${d}/${m}/${y}`;
}

/** Khoảng ngày học của lớp. */
export function formatDateRange(start: string | null | undefined, end: string | null | undefined): string {
  return start || end ? `${formatDateOnly(start)} – ${formatDateOnly(end)}` : "—";
}
