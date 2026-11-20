import { useState } from "react";
import { Alert, Badge, Button, Card, Form, Modal } from "react-bootstrap";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { studentApi } from "../../services/api";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import type { MyQuestionReport, QuestionReportReason } from "../../types/api";
import { REASON_LABEL, REPORT_NOTE_MAX, REPORT_REASONS, REPORT_STATUS } from "./questionReportLabels";

const reportsKey = (attemptId: string) => ["my-question-reports", attemptId];

/** Các câu học sinh đã báo lỗi trong lượt thi (D-46). */
export function useMyQuestionReports(attemptId: string, enabled: boolean) {
  return useQuery({
    queryKey: reportsKey(attemptId),
    queryFn: () => studentApi.questionReports(attemptId),
    enabled: enabled && Boolean(attemptId),
  });
}

/**
 * Hộp "Báo câu hỏi lỗi" ở trang kết quả: chỉ hiện sau khi đã nộp bài (lúc đang thi không báo được).
 * Danh sách các câu đã báo và kết quả thầy cô kiểm tra.
 */
export function QuestionReportCard({
  reports,
  onReport,
}: {
  reports: MyQuestionReport[];
  onReport: (order: number | null) => void;
}) {
  return (
    <Card className="mb-3">
      <Card.Body>
        <div className="d-flex flex-wrap justify-content-between align-items-start gap-2">
          <div>
            <h2 className="h6 mb-1">
              <i className="bi bi-flag me-1 text-danger" aria-hidden="true" />
              Báo câu hỏi lỗi
            </h2>
            <p className="small text-secondary mb-0">
              Trong lúc làm bài, nếu thấy câu nào sai đề, sai đáp án hay thiếu hình, hãy báo để thầy cô kiểm tra. Câu bị lỗi thật sẽ
              được sửa và chấm lại điểm.
            </p>
          </div>
          <Button variant="outline-danger" size="sm" onClick={() => onReport(null)}>
            <i className="bi bi-flag me-1" aria-hidden="true" />
            Báo câu hỏi lỗi
          </Button>
        </div>
        {reports.length > 0 && (
          <ul className="list-unstyled small mb-0 mt-3 border-top pt-2">
            {reports.map((r) => (
              <li key={r.id} className="py-1 d-flex flex-wrap align-items-center gap-2">
                <strong>Câu {r.questionOrder}</strong>
                <span>{REASON_LABEL[r.reason]}</span>
                {r.note && <span className="text-secondary text-break">“{r.note}”</span>}
                <Badge bg={REPORT_STATUS[r.status].variant} text={r.status === "OPEN" ? "dark" : undefined}>
                  {REPORT_STATUS[r.status].student}
                </Badge>
                {r.resolutionNote && <span className="text-secondary">Thầy cô: {r.resolutionNote}</span>}
                {r.status === "OPEN" && (
                  <Button variant="link" size="sm" className="p-0" onClick={() => onReport(r.questionOrder)}>
                    Sửa
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}
      </Card.Body>
    </Card>
  );
}

/** Nút nhỏ ở góc từng câu khi xem lại bài: "Báo lỗi", hoặc trạng thái nếu đã báo. */
export function ReportQuestionButton({
  order,
  report,
  onReport,
}: {
  order: number;
  report: MyQuestionReport | undefined;
  onReport: (order: number) => void;
}) {
  if (report && report.status !== "OPEN") {
    return (
      <Badge bg={REPORT_STATUS[report.status].variant} title={report.resolutionNote ?? undefined}>
        Báo lỗi: {REPORT_STATUS[report.status].student}
      </Badge>
    );
  }
  return (
    <Button
      variant={report ? "warning" : "outline-danger"}
      size="sm"
      className="py-0 px-2"
      onClick={() => onReport(order)}
      title={report ? "Đã báo lỗi, đang chờ thầy cô kiểm tra. Bấm để sửa." : "Báo câu hỏi này bị lỗi"}
    >
      <i className="bi bi-flag me-1" aria-hidden="true" />
      {report ? "Đã báo lỗi" : "Báo lỗi"}
    </Button>
  );
}

/** Hộp thoại báo lỗi: chọn câu (khi chưa chọn sẵn), lý do, mô tả. Báo lại câu đã báo thì cập nhật lý do. */
export function QuestionReportDialog({
  attemptId,
  show,
  order,
  totalQuestion,
  reports,
  onHide,
}: {
  attemptId: string;
  show: boolean;
  /** Câu chọn sẵn (bấm từ một câu); null = học sinh tự chọn số câu. */
  order: number | null;
  totalQuestion: number;
  reports: MyQuestionReport[];
  onHide: () => void;
}) {
  return (
    <Modal show={show} onHide={onHide} centered>
      {show && (
        <ReportForm
          key={order ?? "pick"}
          attemptId={attemptId}
          order={order}
          totalQuestion={totalQuestion}
          reports={reports}
          onDone={onHide}
        />
      )}
    </Modal>
  );
}

function ReportForm({
  attemptId,
  order,
  totalQuestion,
  reports,
  onDone,
}: {
  attemptId: string;
  order: number | null;
  totalQuestion: number;
  reports: MyQuestionReport[];
  onDone: () => void;
}) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const initial = order != null ? reports.find((r) => r.questionOrder === order) : undefined;
  const [questionOrder, setQuestionOrder] = useState<number | "">(order ?? "");
  const [reason, setReason] = useState<QuestionReportReason | "">(initial?.reason ?? "");
  const [note, setNote] = useState(initial?.note ?? "");

  const existing = questionOrder === "" ? undefined : reports.find((r) => r.questionOrder === questionOrder);
  const closed = existing != null && existing.status !== "OPEN";
  const needsNote = reason === "OTHER" && note.trim() === "";

  const send = useMutation({
    mutationFn: () =>
      studentApi.reportQuestion(attemptId, {
        questionOrder: Number(questionOrder),
        reason: reason as QuestionReportReason,
        note: note.trim() || undefined,
      }),
    onSuccess: (saved) => {
      void queryClient.invalidateQueries({ queryKey: reportsKey(attemptId) });
      toast.success(existing ? `Đã cập nhật báo lỗi câu ${saved.questionOrder}.` : `Đã báo lỗi câu ${saved.questionOrder}. Thầy cô sẽ kiểm tra.`);
      onDone();
    },
  });

  return (
    <Form
      onSubmit={(e) => {
        e.preventDefault();
        send.mutate();
      }}
    >
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          <i className="bi bi-flag me-2 text-danger" aria-hidden="true" />
          Báo câu hỏi lỗi{order != null ? ` — Câu ${order}` : ""}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {order == null && (
          <Form.Group className="mb-3" controlId="report-question-order">
            <Form.Label>Câu số</Form.Label>
            <Form.Select
              value={questionOrder}
              onChange={(e) => setQuestionOrder(e.target.value === "" ? "" : Number(e.target.value))}
              required
            >
              <option value="">— Chọn câu bị lỗi —</option>
              {Array.from({ length: totalQuestion }, (_, i) => i + 1).map((n) => {
                const r = reports.find((x) => x.questionOrder === n);
                return (
                  <option key={n} value={n}>
                    Câu {n}
                    {r ? ` (đã báo: ${REPORT_STATUS[r.status].student.toLowerCase()})` : ""}
                  </option>
                );
              })}
            </Form.Select>
          </Form.Group>
        )}
        {closed ? (
          <Alert variant="secondary" className="mb-0">
            Báo lỗi câu {existing.questionOrder} đã được thầy cô xử lý: <strong>{REPORT_STATUS[existing.status].student}</strong>
            {existing.resolutionNote && <> — {existing.resolutionNote}</>}.
          </Alert>
        ) : (
          <>
            <Form.Group className="mb-3">
              <Form.Label>Lỗi gì?</Form.Label>
              {REPORT_REASONS.map((r) => (
                <Form.Check
                  key={r.value}
                  type="radio"
                  id={`report-reason-${r.value}`}
                  name="report-reason"
                  checked={reason === r.value}
                  onChange={() => setReason(r.value)}
                  label={
                    <>
                      {r.label} <span className="small text-secondary">— {r.hint}</span>
                    </>
                  }
                />
              ))}
            </Form.Group>
            <Form.Group controlId="report-note">
              <Form.Label>Mô tả {reason === "OTHER" ? "(bắt buộc)" : "(không bắt buộc)"}</Form.Label>
              <Form.Control
                as="textarea"
                rows={3}
                maxLength={REPORT_NOTE_MAX}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Ví dụ: đáp án đúng phải là B vì…"
              />
            </Form.Group>
          </>
        )}
        {send.error && (
          <Alert variant="danger" className="mt-3 mb-0">
            {describeError(send.error)}
          </Alert>
        )}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onDone}>
          {closed ? "Đóng" : "Hủy"}
        </Button>
        {!closed && (
          <Button type="submit" variant="danger" disabled={questionOrder === "" || reason === "" || needsNote || send.isPending}>
            {send.isPending ? "Đang gửi…" : existing ? "Cập nhật báo lỗi" : "Gửi báo lỗi"}
          </Button>
        )}
      </Modal.Footer>
    </Form>
  );
}
