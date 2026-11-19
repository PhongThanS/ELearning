import { useEffect, useMemo, useState } from "react";
import { Button, Form, Modal, Spinner } from "react-bootstrap";
import { useToast } from "../../components/common/toast";
import {
  canvasToBlob,
  copyImage,
  downloadImage,
  formatReportDate,
  formatReportScore,
  renderDailyReport,
  sortReportRows,
  splitVietnameseName,
  type DailyReportInfo,
  type DailyReportRow,
  REPORT_ATTENDANCE_COLORS,
} from "./dailyReport";
import { statusLabel } from "./attendanceLogic";
import type { AttendanceStatus } from "../../types/api";

export interface DailyReportStudent {
  studentId: string;
  fullName: string;
  inClass: boolean;
  /** Trạng thái điểm danh buổi này; null = chưa điểm danh (không đưa vào báo cáo). */
  status: AttendanceStatus | null;
  homeworkScore: number | null;
  note: string | null;
}

/** Hạn nộp / chú ý chỉ dùng cho báo cáo: nhớ trên trình duyệt này theo lớp + ngày. */
const extrasKey = (classId: string, date: string) => `sclass.daily-report.${classId}.${date}`;

function readExtras(classId: string, date: string): { deadline: string; attention: string } {
  try {
    const raw = window.localStorage.getItem(extrasKey(classId, date));
    const parsed: unknown = raw ? JSON.parse(raw) : null;
    if (parsed && typeof parsed === "object") {
      const p = parsed as Record<string, unknown>;
      return { deadline: typeof p.deadline === "string" ? p.deadline : "", attention: typeof p.attention === "string" ? p.attention : "" };
    }
  } catch {
    // bỏ qua: localStorage bị chặn hoặc dữ liệu hỏng
  }
  return { deadline: "", attention: "" };
}

/**
 * Báo cáo học tập ngày (mẫu bảng gửi phụ huynh): nội dung, BTVN, hạn nộp, chú ý + bảng điểm BTVN và nhận xét.
 * Nút máy ảnh cuối mỗi dòng chụp ảnh riêng học sinh đó (kèm phần đầu báo cáo); "Chụp toàn bộ" chụp cả lớp.
 * Ảnh được chép vào bộ nhớ tạm để dán thẳng vào Zalo; trình duyệt không cho chép thì tải file PNG.
 */
export function DailyReportModal({
  show,
  onHide,
  classId,
  className,
  date,
  notes,
  onNotes,
  students,
}: {
  show: boolean;
  onHide: () => void;
  classId: string;
  className: string;
  date: string;
  notes: { topic: string; nextHomework: string };
  onNotes: (patch: Partial<{ topic: string; nextHomework: string }>) => void;
  students: DailyReportStudent[];
}) {
  const toast = useToast();
  const [extras, setExtras] = useState(() => readExtras(classId, date));
  const [sortByScore, setSortByScore] = useState(true);
  const [onlyScored, setOnlyScored] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => setExtras(readExtras(classId, date)), [classId, date]);

  const updateExtras = (patch: Partial<typeof extras>) => {
    const next = { ...extras, ...patch };
    setExtras(next);
    try {
      window.localStorage.setItem(extrasKey(classId, date), JSON.stringify(next));
    } catch {
      // không lưu được thì vẫn dùng trong phiên hiện tại
    }
  };

  const rows = useMemo<DailyReportRow[]>(() => {
    const base = students
      // Chưa điểm danh thì không ghi vào báo cáo
      .filter((s) => s.inClass && s.status !== null && (!onlyScored || s.homeworkScore !== null))
      .map((s) => {
        const [lastName, firstName] = splitVietnameseName(s.fullName);
        return {
          key: s.studentId,
          order: 0,
          lastName,
          firstName,
          score: s.homeworkScore,
          attendance: statusLabel(s.status),
          attendanceColor: s.status ? REPORT_ATTENDANCE_COLORS[s.status] : undefined,
          comment: s.note ?? "",
        };
      });
    return (sortByScore ? sortReportRows(base) : base).map((r, i) => ({ ...r, order: i + 1 }));
  }, [students, sortByScore, onlyScored]);

  const info: DailyReportInfo = {
    date,
    className,
    topic: notes.topic,
    homework: notes.nextHomework,
    deadline: extras.deadline,
    attention: extras.attention,
  };

  const capture = async (target: DailyReportRow | null) => {
    const id = target?.key ?? "all";
    setBusy(id);
    try {
      const canvas = await renderDailyReport(info, target ? [target] : rows);
      const blob = await canvasToBlob(canvas);
      const who = target ? `${target.lastName} ${target.firstName}`.trim() : "cả lớp";
      if (await copyImage(blob)) {
        toast.success(`Đã chụp ảnh ${who} — dán (Ctrl+V) vào Zalo để gửi.`);
      } else {
        downloadImage(blob, fileName(target));
        toast.info(`Trình duyệt không cho chép ảnh, đã tải ảnh ${who} về máy.`);
      }
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(null);
    }
  };

  const download = async () => {
    setBusy("download");
    try {
      downloadImage(await canvasToBlob(await renderDailyReport(info, rows)), fileName(null));
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(null);
    }
  };

  const fileName = (target: DailyReportRow | null) =>
    `bao-cao-${date}${target ? `-${`${target.lastName} ${target.firstName}`.trim().replace(/\s+/g, "-")}` : ""}.png`;

  return (
    <Modal show={show} onHide={onHide} size="xl" centered scrollable>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          <i className="bi bi-file-earmark-image me-2" aria-hidden="true" />
          Báo cáo học tập ngày
        </Modal.Title>
      </Modal.Header>
      <Modal.Body className="bg-body-tertiary">
        <div className="d-flex flex-wrap gap-3 align-items-center mb-3">
          <Form.Check id="daily-report-sort" label="Xếp điểm cao → thấp" checked={sortByScore} onChange={(e) => setSortByScore(e.target.checked)} />
          <Form.Check id="daily-report-scored" label="Chỉ học sinh có điểm" checked={onlyScored} onChange={(e) => setOnlyScored(e.target.checked)} />
          <span className="small text-secondary ms-auto">Sửa trực tiếp các ô nội dung bên dưới; nội dung buổi học và BTVN được lưu vào buổi điểm danh.</span>
        </div>

        <div className="daily-report">
          <h3 className="daily-report-title">BÁO CÁO HỌC TẬP NGÀY {formatReportDate(date)}</h3>
          {className && <div className="daily-report-class">Lớp {className}</div>}
          <div className="daily-report-info">
            <label htmlFor="dr-topic" className="text-danger-emphasis">NỘI DUNG:</label>
            <input id="dr-topic" className="daily-report-input is-topic" value={notes.topic} placeholder="VD: Tích vô hướng của hai vector" onChange={(e) => onNotes({ topic: e.target.value })} />
            <label htmlFor="dr-homework">BTVN:</label>
            <input id="dr-homework" className="daily-report-input" value={notes.nextHomework} placeholder="VD: Hoàn thành phiếu bài tập số 5" onChange={(e) => onNotes({ nextHomework: e.target.value })} />
            <label htmlFor="dr-deadline">HẠN NỘP:</label>
            <input id="dr-deadline" className="daily-report-input" value={extras.deadline} placeholder="VD: 18h30 Thứ 7 tuần sau" onChange={(e) => updateExtras({ deadline: e.target.value })} />
            <label htmlFor="dr-attention" className="is-green">CHÚ Ý:</label>
            <input id="dr-attention" className="daily-report-input" value={extras.attention} placeholder="Ghi chú cho phụ huynh (nếu có)" onChange={(e) => updateExtras({ attention: e.target.value })} />
          </div>

          <div className="daily-report-table-wrap">
            <table className="daily-report-table">
              <thead>
                <tr>
                  <th style={{ width: 56 }}>STT</th>
                  <th style={{ width: 200 }}>HỌ VÀ TÊN ĐỆM</th>
                  <th style={{ width: 110 }}>TÊN</th>
                  <th style={{ width: 130 }}>ĐIỂM DANH</th>
                  <th style={{ width: 110 }}>ĐIỂM BTVN</th>
                  <th>NHẬN XÉT</th>
                  <th style={{ width: 56 }} aria-label="Chụp ảnh dòng" />
                </tr>
              </thead>
              <tbody>
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={7} className="text-center text-secondary py-4">
                      Chưa có học sinh nào được điểm danh để báo cáo.
                    </td>
                  </tr>
                )}
                {rows.map((r) => (
                  <tr key={r.key}>
                    <td className="text-center fw-bold">{r.order}</td>
                    <td className="fw-bold">{r.lastName}</td>
                    <td className="text-center fw-bold">{r.firstName}</td>
                    <td className="text-center fw-bold" style={{ color: r.attendanceColor }}>
                      {r.attendance}
                    </td>
                    <td className="text-center fw-bold">{formatReportScore(r.score)}</td>
                    <td className="daily-report-comment">{r.comment}</td>
                    <td className="text-center">
                      <Button
                        size="sm"
                        variant="outline-secondary"
                        className="daily-report-shot"
                        disabled={busy !== null}
                        onClick={() => void capture(r)}
                        title={`Chụp ảnh dòng của ${r.lastName} ${r.firstName}`}
                        aria-label={`Chụp ảnh dòng của ${r.lastName} ${r.firstName}`}
                      >
                        {busy === r.key ? <Spinner size="sm" /> : <i className="bi bi-camera" aria-hidden="true" />}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </Modal.Body>
      <Modal.Footer>
        <span className="small text-secondary me-auto">Ảnh được chép vào bộ nhớ tạm: mở Zalo và nhấn Ctrl+V để gửi.</span>
        <Button variant="outline-secondary" disabled={busy !== null || rows.length === 0} onClick={() => void download()}>
          {busy === "download" ? <Spinner size="sm" className="me-1" /> : <i className="bi bi-download me-1" aria-hidden="true" />}
          Tải ảnh PNG
        </Button>
        <Button disabled={busy !== null || rows.length === 0} onClick={() => void capture(null)}>
          {busy === "all" ? <Spinner size="sm" className="me-1" /> : <i className="bi bi-camera me-1" aria-hidden="true" />}
          Chụp toàn bộ
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
