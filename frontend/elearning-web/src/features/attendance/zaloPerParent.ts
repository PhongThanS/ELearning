import type { AttendanceStudent } from "../../types/api";
import { formatDateOnly } from "../../utils/format";
import { formatScoreValue, homeworkLabel, levelLabel, phoneDigits, statusLabel } from "./attendanceLogic";

/**
 * Nhắn Zalo từng phụ huynh (miễn phí, bán tự động): phần mềm điền sẵn tin cho từng học sinh, sao chép,
 * rồi mở cuộc trò chuyện Zalo với số điện thoại phụ huynh (zalo.me/<số>); thầy cô dán và bấm gửi.
 * Zalo không cho điền sẵn nội dung qua link, và gửi tự động tới số điện thoại chỉ có qua ZNS (tính phí, D-45).
 */

/** Chỗ trống dùng được trong mẫu tin; bấm để chèn. */
export const ZALO_PLACEHOLDERS = [
  { key: "phụ huynh", hint: "Tên phụ huynh (chưa có thì ghi “em <tên học sinh>”)" },
  { key: "học sinh", hint: "Họ tên học sinh" },
  { key: "lớp", hint: "Tên lớp" },
  { key: "ngày", hint: "Ngày buổi học" },
  { key: "điểm danh", hint: "Có mặt / Vắng có phép / …" },
  { key: "BTVN", hint: "Mức làm bài tập về nhà" },
  { key: "điểm", hint: "Điểm BTVN" },
  { key: "phân loại", hint: "Loại 1..5 của học sinh trong buổi" },
  { key: "nhận xét", hint: "Nhận xét của thầy cô" },
  { key: "nội dung", hint: "Nội dung buổi học" },
  { key: "bài tập", hint: "BTVN buổi tới" },
] as const;

export type ZaloPreset = "announcement" | "report";

export const ZALO_PRESETS: Record<ZaloPreset, { label: string; text: string }> = {
  announcement: {
    label: "Thông báo chung",
    text: "Kính gửi phụ huynh {phụ huynh},\nLớp {lớp} xin thông báo: \n\nTrân trọng cảm ơn phụ huynh!",
  },
  report: {
    label: "Báo cáo buổi học",
    text: [
      "Kính gửi phụ huynh {phụ huynh},",
      "Lớp {lớp} gửi kết quả buổi học ngày {ngày} của em {học sinh}:",
      "- Điểm danh: {điểm danh}",
      "- Bài tập về nhà: {BTVN} {điểm}",
      "- Nhận xét: {nhận xét}",
      "- Nội dung buổi học: {nội dung}",
      "- BTVN buổi tới: {bài tập}",
      "Trân trọng cảm ơn phụ huynh đã đồng hành cùng con!",
    ].join("\n"),
  },
};

export interface ZaloContext {
  className: string;
  date: string;
  topic: string;
  nextHomework: string;
}

type Student = Pick<AttendanceStudent, "fullName" | "parentName" | "status" | "homeworkStatus" | "homeworkScore" | "note" | "level">;

/** Điền mẫu cho một học sinh. Chỗ trống không có dữ liệu thành "—"; chỗ trống viết sai giữ nguyên để thầy cô thấy. */
export function fillZaloTemplate(template: string, s: Student, ctx: ZaloContext): string {
  const values: Record<string, string> = {
    "phụ huynh": s.parentName?.trim() || `em ${s.fullName}`,
    "học sinh": s.fullName,
    "lớp": ctx.className,
    "ngày": formatDateOnly(ctx.date),
    "điểm danh": s.status ? statusLabel(s.status) : "Chưa điểm danh",
    "btvn": s.homeworkStatus ? homeworkLabel(s.homeworkStatus) : "—",
    "điểm": s.homeworkScore === null ? "" : `(${formatScoreValue(s.homeworkScore)} điểm)`,
    "phân loại": levelLabel(s.level) || "—",
    "nhận xét": s.note?.trim() || "—",
    "nội dung": ctx.topic.trim() || "—",
    "bài tập": ctx.nextHomework.trim() || "—",
  };
  return template
    .replace(/\{([^{}\n]{1,20})\}/g, (match, raw: string) => values[raw.trim().toLowerCase()] ?? match)
    .replace(/[ \t]+$/gm, "");
}

/** Link mở cuộc trò chuyện Zalo theo số điện thoại (app Zalo trên điện thoại / Zalo PC trên máy tính). */
export function zaloChatUrl(phone: string | null): string | null {
  const digits = phoneDigits(phone);
  return digits ? `https://zalo.me/${digits}` : null;
}

/**
 * Sao chép văn bản ngay trong lúc bấm nút. Trang chạy http (không phải https) thì trình duyệt không có
 * navigator.clipboard, nên dùng cách cũ (ô ẩn + execCommand) — vẫn chạy được vì nằm trong thao tác bấm.
 */
export function copyTextNow(text: string): boolean {
  if (window.isSecureContext && navigator.clipboard) {
    void navigator.clipboard.writeText(text).catch(() => copyWithTextarea(text));
    return true;
  }
  return copyWithTextarea(text);
}

function copyWithTextarea(text: string): boolean {
  const area = document.createElement("textarea");
  area.value = text;
  area.setAttribute("readonly", "");
  area.style.position = "fixed";
  area.style.opacity = "0";
  document.body.appendChild(area);
  area.select();
  let ok = false;
  try {
    ok = document.execCommand("copy");
  } catch {
    ok = false;
  }
  document.body.removeChild(area);
  return ok;
}

/** Các học sinh đã nhắn trong buổi này (chỉ lưu trên trình duyệt của thầy cô, để không nhắn trùng). */
export function loadSent(key: string): Set<string> {
  try {
    const raw = localStorage.getItem(key);
    return new Set(raw ? (JSON.parse(raw) as string[]) : []);
  } catch {
    return new Set();
  }
}

export function saveSent(key: string, sent: Set<string>): void {
  try {
    localStorage.setItem(key, JSON.stringify([...sent]));
  } catch {
    // Trình duyệt chặn lưu: đánh dấu chỉ còn trong phiên hiện tại
  }
}
