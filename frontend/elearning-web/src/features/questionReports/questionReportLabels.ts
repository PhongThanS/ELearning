import type { QuestionReportReason, QuestionReportStatus } from "../../types/api";

/** Lý do báo lỗi câu hỏi (D-46), theo thứ tự hiện trong hộp thoại. */
export const REPORT_REASONS: { value: QuestionReportReason; label: string; hint: string }[] = [
  { value: "WRONG_ANSWER", label: "Sai đáp án", hint: "Đáp án đúng của câu bị sai" },
  { value: "WRONG_CONTENT", label: "Sai đề", hint: "Đề sai, thiếu dữ kiện hoặc không có đáp án đúng" },
  { value: "TYPO", label: "Lỗi chính tả / hiển thị", hint: "Sai chính tả, công thức hoặc định dạng bị lỗi" },
  { value: "MISSING_MEDIA", label: "Thiếu hình", hint: "Thiếu hình hoặc hình không hiện" },
  { value: "OTHER", label: "Lỗi khác", hint: "Hãy mô tả lỗi" },
];

export const REASON_LABEL: Record<QuestionReportReason, string> = Object.fromEntries(
  REPORT_REASONS.map((r) => [r.value, r.label]),
) as Record<QuestionReportReason, string>;

/** Trạng thái báo lỗi: chữ cho học sinh, chữ cho người xử lý và màu badge. */
export const REPORT_STATUS: Record<QuestionReportStatus, { student: string; staff: string; variant: string }> = {
  OPEN: { student: "Chờ thầy cô kiểm tra", staff: "Chờ xử lý", variant: "warning" },
  RESOLVED: { student: "Đã sửa", staff: "Đã sửa", variant: "success" },
  REJECTED: { student: "Câu không lỗi", staff: "Không lỗi", variant: "secondary" },
};

export const REPORT_NOTE_MAX = 1000;
