import type { AttendanceEntry, AttendanceHistoryItem, AttendanceStatus, AttendanceStudentHistory, HomeworkStatus } from "../../types/api";
import { formatDateOnly } from "../../utils/format";

export const statusOptions: { value: AttendanceStatus; label: string; short: string; variant: string }[] = [
  { value: "PRESENT", label: "Có mặt", short: "Có mặt", variant: "success" },
  { value: "LATE", label: "Đi muộn", short: "Muộn", variant: "warning" },
  { value: "EXCUSED", label: "Vắng có phép", short: "Có phép", variant: "info" },
  { value: "UNEXCUSED", label: "Vắng không phép", short: "Không phép", variant: "danger" },
];

export const homeworkOptions: { value: HomeworkStatus; label: string; icon: string }[] = [
  { value: "EXCELLENT", label: "Xuất sắc", icon: "🌟" },
  { value: "DONE", label: "Đạt", icon: "✅" },
  { value: "PARTIAL", label: "Làm thiếu", icon: "⚠️" },
  { value: "MISSING", label: "Không làm", icon: "❌" },
];

export const statusLabel = (s: AttendanceStatus | null) => statusOptions.find((o) => o.value === s)?.label ?? "Chưa điểm danh";

export const homeworkLabel = (h: HomeworkStatus | null) => {
  const option = homeworkOptions.find((o) => o.value === h);
  return option ? `${option.icon} ${option.label}` : "Chưa chấm";
};

export type AttendanceFilter = "ALL" | "UNMARKED" | AttendanceStatus | "HW_ISSUE";

/** BTVN cần lưu ý: làm thiếu / không làm, hoặc điểm dưới 5. */
export function hasHomeworkIssue(e: AttendanceEntry): boolean {
  return e.homeworkStatus === "PARTIAL" || e.homeworkStatus === "MISSING" || (e.homeworkScore !== null && e.homeworkScore < 5);
}

export function matchesFilter(e: AttendanceEntry, filter: AttendanceFilter): boolean {
  switch (filter) {
    case "ALL":
      return true;
    case "UNMARKED":
      return e.status === null;
    case "HW_ISSUE":
      return hasHomeworkIssue(e);
    default:
      return e.status === filter;
  }
}

export interface AttendanceStats {
  total: number;
  present: number;
  late: number;
  excused: number;
  unexcused: number;
  unmarked: number;
  /** Có mặt + đi muộn. */
  attended: number;
  /** % tham gia trên sĩ số. */
  rate: number;
}

export function computeStats(entries: AttendanceEntry[]): AttendanceStats {
  const count = (s: AttendanceStatus | null) => entries.filter((e) => e.status === s).length;
  const present = count("PRESENT");
  const late = count("LATE");
  const total = entries.length;
  return {
    total,
    present,
    late,
    excused: count("EXCUSED"),
    unexcused: count("UNEXCUSED"),
    unmarked: count(null),
    attended: present + late,
    rate: total > 0 ? Math.round(((present + late) / total) * 100) : 0,
  };
}

export interface HomeworkSummary {
  average: number | null;
  excellent: number;
  done: number;
  partial: number;
  missing: number;
  unrated: number;
}

export function homeworkSummary(entries: AttendanceEntry[]): HomeworkSummary {
  const scores = entries.map((e) => e.homeworkScore).filter((s): s is number => s !== null);
  const count = (h: HomeworkStatus | null) => entries.filter((e) => e.homeworkStatus === h).length;
  return {
    average: scores.length > 0 ? Math.round((scores.reduce((a, b) => a + b, 0) / scores.length) * 100) / 100 : null,
    excellent: count("EXCELLENT"),
    done: count("DONE"),
    partial: count("PARTIAL"),
    missing: count("MISSING"),
    unrated: count(null),
  };
}

/** Điểm BTVN 0–10, bước 0,5. Chuỗi rỗng → null; nhận cả dấu phẩy thập phân. */
export function parseScore(text: string): number | null {
  const trimmed = text.trim().replace(",", ".");
  if (trimmed === "") {
    return null;
  }
  const value = Number(trimmed);
  if (!Number.isFinite(value)) {
    return null;
  }
  return Math.min(10, Math.max(0, Math.round(value * 2) / 2));
}

export function stepScore(current: number | null, delta: number): number {
  return Math.min(10, Math.max(0, (current ?? (delta > 0 ? 0 : 10)) + (current === null ? 0 : delta)));
}

/** Dải màu điểm: ≥ 8 xanh lá, 6,5–7,9 xanh dương, 5–6,4 vàng, < 5 đỏ. */
export function scoreVariant(score: number | null): "success" | "primary" | "warning" | "danger" | "secondary" {
  if (score === null) return "secondary";
  if (score >= 8) return "success";
  if (score >= 6.5) return "primary";
  if (score >= 5) return "warning";
  return "danger";
}

export const formatScoreValue = (score: number | null) => (score === null ? "" : String(score).replace(".", ","));

/** Thêm câu nhận xét nhanh vào cuối (không lặp lại câu đã có). */
export function appendNote(current: string | null, text: string): string {
  const note = (current ?? "").trim();
  if (!note) return `${text}.`;
  if (note.includes(text)) return note;
  return `${note}${note.endsWith(".") ? " " : ". "}${text}.`;
}

export const academicChips = [
  { label: "Nắm chắc bài", text: "Nắm chắc phương pháp giải bài" },
  { label: "Trình bày tốt", text: "Trình bày tự luận sạch đẹp, rõ ràng" },
  { label: "Có tiến bộ", text: "Có nhiều tiến bộ rõ rệt so với buổi trước" },
  { label: "Cẩn thận tính toán", text: "Cần rà soát kỹ bước tính toán để tránh nhầm dấu" },
];

export function statusChips(status: AttendanceStatus | null): { label: string; text: string }[] {
  switch (status) {
    case "LATE":
      return [{ label: "Đi muộn 10p", text: "Đi muộn 10 phút" }];
    case "EXCUSED":
      return [{ label: "Nghỉ ốm có phép", text: "PH đã nhắn xin phép nghỉ ốm, GV đã gửi bài giảng" }];
    case "UNEXCUSED":
      return [{ label: "Đã gọi PH", text: "Vắng không phép, đã liên hệ báo Phụ huynh" }];
    default:
      return [];
  }
}

/** Số điện thoại → chỉ chữ số (đầu +84 đổi thành 0) cho link gọi / Zalo. */
export function phoneDigits(phone: string | null): string | null {
  if (!phone) return null;
  let digits = phone.replace(/\D/g, "");
  if (phone.trim().startsWith("+84")) digits = `0${digits.slice(2)}`;
  return digits.length >= 8 ? digits : null;
}

export interface ReportStudent extends AttendanceEntry {
  fullName: string;
}

export interface ZaloReportOptions {
  className: string;
  date: string;
  topic: string;
  nextHomework: string;
  style: "detailed" | "compact";
  includeScoreList: boolean;
  sortByScore: boolean;
}

const scoreSuffix = (s: number | null) => (s === null ? "" : ` (${formatScoreValue(s)}đ)`);

/** Tin nhắn báo cáo buổi học gửi nhóm Zalo phụ huynh. */
export function buildZaloReport(students: ReportStudent[], o: ZaloReportOptions): string {
  const stats = computeStats(students);
  const withNote = (s: ReportStudent) => (s.note ? ` — ${s.note}` : "");
  const listOf = (status: AttendanceStatus, detail: boolean) =>
    students
      .filter((s) => s.status === status)
      .map((s) => `${s.fullName}${detail ? ` (BTVN: ${homeworkLabel(s.homeworkStatus)}${scoreSuffix(s.homeworkScore)})` : ""}${withNote(s)}`);
  const praised = students.filter((s) => s.homeworkStatus === "EXCELLENT" || (s.homeworkScore !== null && s.homeworkScore >= 8.5));
  const board = [...students];
  if (o.sortByScore) {
    board.sort((a, b) => (b.homeworkScore ?? -1) - (a.homeworkScore ?? -1));
  }
  const scoreLines = board.map(
    (s, i) =>
      `${i + 1}. ${s.fullName}: [${homeworkLabel(s.homeworkStatus)}]${s.homeworkScore === null ? "" : ` - Điểm: ${formatScoreValue(s.homeworkScore)}đ`}${withNote(s)}`,
  );
  const date = formatDateOnly(o.date);
  const unexcused = listOf("UNEXCUSED", false);
  const lines: string[] = [];

  if (o.style === "detailed") {
    const excused = listOf("EXCUSED", true);
    const late = listOf("LATE", true);
    const joined = (items: string[]) => (items.length ? items.join(", ") : "Không");
    lines.push("📋 BÁO CÁO TÌNH HÌNH BUỔI HỌC & BTVN", `🏫 ${o.className}`, `📅 Ngày học: ${date}`);
    if (o.topic.trim()) lines.push(`📖 Nội dung: ${o.topic.trim()}`);
    lines.push(
      "-----------------------------------------",
      `📊 SĨ SỐ: ${stats.attended}/${stats.total} HS tham gia (${stats.rate}%)`,
      "",
      `🔴 Vắng không phép (${unexcused.length}): ${joined(unexcused)}`,
      `🔵 Vắng có phép (${excused.length}): ${joined(excused)}`,
      `🟡 Đi muộn (${late.length}): ${joined(late)}`,
      "",
      "🌟 TUYÊN DƯƠNG HỌC SINH BTVN XUẤT SẮC (≥ 8,5đ):",
      praised.length ? praised.map((s) => `- ${s.fullName}${scoreSuffix(s.homeworkScore)}`).join("\n") : "- Đang cập nhật",
      "",
    );
    if (o.includeScoreList) lines.push("📊 TỔNG HỢP BTVN & NHẬN XÉT CẢ LỚP:", ...scoreLines, "");
    if (o.nextHomework.trim()) lines.push(`📌 DẶN DÒ BTVN BUỔI TỚI: ${o.nextHomework.trim()}`);
    lines.push("-----------------------------------------", "Trân trọng cảm ơn Quý Phụ huynh!");
  } else {
    lines.push(`[BÁO CÁO ĐIỂM DANH & BTVN - ${o.className} - ${date}]`, `• Sĩ số: ${stats.attended}/${stats.total} | Vắng không phép: ${unexcused.length}`);
    if (unexcused.length) lines.push(`• Vắng: ${unexcused.join(", ")}`);
    if (praised.length) lines.push(`• 🌟 Tuyên dương: ${praised.map((s) => s.fullName).join(", ")}`);
    if (o.includeScoreList) lines.push(`• Tổng hợp BTVN: ${scoreLines.join(" | ")}`);
    if (o.nextHomework.trim()) lines.push(`📌 BTVN: ${o.nextHomework.trim()}`);
  }
  return lines.join("\n");
}

export interface MonthlyStats {
  average: number | null;
  max: number | null;
  min: number | null;
  scored: number;
  /** % buổi có BTVN Xuất sắc / Đạt trên số buổi đã chấm BTVN. */
  homeworkRate: number | null;
  /** % buổi có mặt / đi muộn trên số buổi đã điểm danh. */
  attendanceRate: number | null;
  attended: number;
  marked: number;
}

export function monthlyStats(items: AttendanceHistoryItem[]): MonthlyStats {
  const scores = items.map((i) => i.homeworkScore).filter((s): s is number => s !== null);
  const rated = items.filter((i) => i.homeworkStatus !== null);
  const marked = items.filter((i) => i.status !== null);
  const attended = marked.filter((i) => i.status === "PRESENT" || i.status === "LATE").length;
  return {
    average: scores.length ? Math.round((scores.reduce((a, b) => a + b, 0) / scores.length) * 100) / 100 : null,
    max: scores.length ? Math.max(...scores) : null,
    min: scores.length ? Math.min(...scores) : null,
    scored: scores.length,
    homeworkRate: rated.length
      ? Math.round((rated.filter((i) => i.homeworkStatus === "EXCELLENT" || i.homeworkStatus === "DONE").length / rated.length) * 100)
      : null,
    attendanceRate: marked.length ? Math.round((attended / marked.length) * 100) : null,
    attended,
    marked: marked.length,
  };
}

/** Tin nhắn tổng kết tháng của một học sinh gửi phụ huynh. */
export function buildMonthlyReport(h: AttendanceStudentHistory): string {
  const s = monthlyStats(h.items);
  const lines = [
    `📊 TỔNG KẾT THÁNG ${String(h.month).padStart(2, "0")}/${h.year} — ${h.fullName}`,
    `🏫 ${h.classroomName}`,
    `• Chuyên cần: ${s.attended}/${s.marked} buổi${s.attendanceRate === null ? "" : ` (${s.attendanceRate}%)`}`,
    `• Điểm BTVN trung bình: ${s.average === null ? "chưa có" : `${formatScoreValue(s.average)}đ`}${h.classRank ? ` — xếp hạng ${h.classRank}/${h.rankedCount} lớp` : ""}`,
  ];
  if (s.max !== null && s.min !== null) lines.push(`• Cao nhất ${formatScoreValue(s.max)}đ, thấp nhất ${formatScoreValue(s.min)}đ`);
  if (s.homeworkRate !== null) lines.push(`• Hoàn thành BTVN: ${s.homeworkRate}%`);
  lines.push("", "Chi tiết từng buổi:");
  for (const i of h.items) {
    const score = i.homeworkScore === null ? "" : ` - ${formatScoreValue(i.homeworkScore)}đ`;
    lines.push(`- ${formatDateOnly(i.sessionDate)}: ${statusLabel(i.status)}${score}${i.note ? ` — ${i.note}` : ""}`);
  }
  lines.push("", "Trân trọng cảm ơn Quý Phụ huynh!");
  return lines.join("\n");
}

/** Ngày yyyy-MM-dd theo giờ Việt Nam (ngày học), có thể dịch số ngày. */
export function vnDate(offsetDays = 0, from?: string): string {
  const base = from ? new Date(`${from}T00:00:00Z`) : new Date(Date.now() + 7 * 3600_000);
  base.setUTCDate(base.getUTCDate() + offsetDays);
  return base.toISOString().slice(0, 10);
}
