import { useState } from "react";
import { Alert, Badge, Button, ButtonGroup, Card, Form, Modal, Table } from "react-bootstrap";
import { Link } from "react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { questionReportsApi } from "../../services/api";
import { PageHeader, Pager, SearchBox } from "../../components/common/DataTable";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import { MarkdownView } from "../../components/common/MarkdownView";
import { MediaUrls } from "../../components/common/MediaUrls";
import { useToast } from "../../components/common/toast";
import { useBackState } from "../../components/common/BackLink";
import { describeError } from "../../utils/errors";
import { formatDateTime } from "../../utils/format";
import { Permissions } from "../../constants/permissions";
import { useAuth } from "../auth/useAuth";
import type { QuestionReportStatus, ReportedQuestion } from "../../types/api";
import { REASON_LABEL, REPORT_NOTE_MAX, REPORT_STATUS } from "./questionReportLabels";

type StatusFilter = QuestionReportStatus | "ALL";

const FILTERS: { value: StatusFilter; label: string }[] = [
  { value: "OPEN", label: "Chờ xử lý" },
  { value: "RESOLVED", label: "Đã sửa" },
  { value: "REJECTED", label: "Không lỗi" },
  { value: "ALL", label: "Tất cả" },
];

/**
 * Câu hỏi bị học sinh báo lỗi (D-46): gom theo câu trong đề, nhiều học sinh báo cùng một câu chỉ thành một dòng.
 * Mở một câu để xem đề + đáp án + các báo lỗi, rồi sửa đáp án / hủy câu (trang phiên bản đề) và đánh dấu đã xử lý.
 */
export function QuestionReportsPage() {
  const [status, setStatus] = useState<StatusFilter>("OPEN");
  const [keyword, setKeyword] = useState("");
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["question-reports", status, keyword, page],
    queryFn: () =>
      questionReportsApi.list({ page, pageSize: 20, status: status === "ALL" ? undefined : status, keyword: keyword || undefined }),
  });

  return (
    <>
      <PageHeader title="Câu hỏi bị báo lỗi">
        <p className="text-secondary small mb-0">
          Học sinh báo lỗi sau khi nộp bài. Kiểm tra từng câu: nếu lỗi thật thì sửa đáp án hoặc hủy câu để chấm lại, rồi bấm “Đã sửa
          xong”; nếu không lỗi thì bấm “Câu không lỗi”.
        </p>
      </PageHeader>

      <div className="d-flex flex-wrap gap-2 align-items-center mb-3">
        <ButtonGroup size="sm">
          {FILTERS.map((f) => (
            <Button
              key={f.value}
              variant={status === f.value ? "primary" : "outline-primary"}
              onClick={() => {
                setStatus(f.value);
                setPage(1);
              }}
            >
              {f.label}
            </Button>
          ))}
        </ButtonGroup>
        <div className="ms-auto" style={{ minWidth: 260 }}>
          <SearchBox
            value={keyword}
            placeholder="Tìm theo mã / tên đề"
            onSearch={(v) => {
              setKeyword(v);
              setPage(1);
            }}
          />
        </div>
      </div>

      {query.error ? (
        <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />
      ) : !query.data ? (
        <Loading />
      ) : query.data.items.length === 0 ? (
        <Card body className="text-center text-secondary">
          {status === "OPEN" ? "Không có câu hỏi nào đang chờ xử lý." : "Không có câu hỏi nào."}
        </Card>
      ) : (
        <>
          <div className="table-responsive border rounded mb-3">
            <Table hover className="mb-0 align-middle">
              <thead className="table-light">
                <tr>
                  <th>Đề thi</th>
                  <th>Câu hỏi</th>
                  <th>Lỗi được báo</th>
                  <th className="text-center">Số lượt báo</th>
                  <th>Báo gần nhất</th>
                  <th className="text-end" />
                </tr>
              </thead>
              <tbody>
                {query.data.items.map((q) => (
                  <tr key={q.examQuestionId} role="button" onClick={() => setOpenId(q.examQuestionId)}>
                    <td>
                      <div className="fw-semibold">{q.examName}</div>
                      <div className="small text-secondary font-monospace">
                        {q.examCode} · v{q.versionNumber}
                      </div>
                    </td>
                    <td style={{ maxWidth: 420 }}>
                      <div className="fw-semibold">
                        Câu {q.questionOrder} {q.isVoided && <Badge bg="dark">đã hủy</Badge>}
                      </div>
                      <div className="small text-secondary text-truncate">{q.contentPreview}</div>
                    </td>
                    <td>
                      <div className="d-flex flex-wrap gap-1">
                        {q.reasons.map((r) => (
                          <Badge key={r} bg="light" text="dark" className="border">
                            {REASON_LABEL[r]}
                          </Badge>
                        ))}
                      </div>
                    </td>
                    <td className="text-center">
                      {q.openCount > 0 ? (
                        <Badge bg="warning" text="dark" title="Chờ xử lý / tổng số lượt báo">
                          {q.openCount} / {q.reportCount}
                        </Badge>
                      ) : (
                        <Badge bg="secondary">{q.reportCount}</Badge>
                      )}
                    </td>
                    <td className="small text-nowrap">{formatDateTime(q.lastReportedAt)}</td>
                    <td className="text-end">
                      <Button size="sm" variant="outline-primary">
                        Kiểm tra
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
          <Pager data={query.data} onPage={setPage} />
        </>
      )}

      <ReportedQuestionModal examQuestionId={openId} onHide={() => setOpenId(null)} />
    </>
  );
}

function ReportedQuestionModal({ examQuestionId, onHide }: { examQuestionId: string | null; onHide: () => void }) {
  return (
    <Modal show={examQuestionId != null} onHide={onHide} size="xl" scrollable>
      {examQuestionId && <ReportedQuestionBody key={examQuestionId} examQuestionId={examQuestionId} onDone={onHide} />}
    </Modal>
  );
}

function ReportedQuestionBody({ examQuestionId, onDone }: { examQuestionId: string; onDone: () => void }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const back = useBackState("Câu hỏi bị báo lỗi");
  const [note, setNote] = useState("");
  const query = useQuery({ queryKey: ["question-report", examQuestionId], queryFn: () => questionReportsApi.get(examQuestionId) });

  const resolve = useMutation({
    mutationFn: (status: QuestionReportStatus) => questionReportsApi.resolve(examQuestionId, { status, note: note.trim() || undefined }),
    onSuccess: (result, status) => {
      void queryClient.invalidateQueries({ queryKey: ["question-reports"] });
      void queryClient.invalidateQueries({ queryKey: ["question-reports-open-count"] });
      void queryClient.invalidateQueries({ queryKey: ["question-report", examQuestionId] });
      toast.success(
        status === "OPEN"
          ? `Đã mở lại ${result.changed} báo lỗi.`
          : `Đã đánh dấu ${result.changed} báo lỗi là “${REPORT_STATUS[status].staff}”.`,
      );
      if (status !== "OPEN") {
        onDone();
      }
    },
  });

  if (query.error) {
    return (
      <Modal.Body>
        <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />
      </Modal.Body>
    );
  }
  if (!query.data) {
    return (
      <Modal.Body>
        <Loading />
      </Modal.Body>
    );
  }

  const d = query.data;
  const s: ReportedQuestion = d.summary;
  return (
    <>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          {s.examName} <span className="text-secondary small font-monospace">({s.examCode} · v{s.versionNumber})</span> — Câu{" "}
          {s.questionOrder}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <div className="row g-3">
          <div className="col-lg-7">
            <Card className="h-100">
              <Card.Body>
                <div className="d-flex justify-content-between mb-2">
                  <strong>Nội dung câu hỏi trong đề</strong>
                  <span className="small text-secondary">{d.score} điểm</span>
                </div>
                {s.isVoided && <Alert variant="dark" className="py-1 small">Câu này đã bị hủy (không tính điểm).</Alert>}
                <MediaUrls value={d.media}>
                  <MarkdownView content={d.content} format={d.contentFormat} />
                  {d.options.length > 0 && (
                    <ul className="list-unstyled mt-3 mb-0">
                      {d.options.map((o) => (
                        <li
                          key={o.code}
                          className={`d-flex gap-2 align-items-start border rounded px-2 py-1 mb-1 ${o.isCorrect ? "border-success bg-success-subtle" : ""}`}
                        >
                          <strong>{o.code}.</strong>
                          <div className="flex-grow-1">
                            <MarkdownView content={o.content} format={d.contentFormat} />
                          </div>
                          {o.isCorrect && <i className="bi bi-check-circle-fill text-success" title="Đáp án đúng" />}
                        </li>
                      ))}
                    </ul>
                  )}
                  {d.acceptedAnswers.length > 0 && (
                    <div className="mt-3 small">
                      <strong>Đáp án chấp nhận:</strong> {d.acceptedAnswers.join(" | ")}
                    </div>
                  )}
                  {d.correctAnswerNumber != null && (
                    <div className="mt-3 small">
                      <strong>Đáp án số:</strong> {d.correctAnswerNumber}
                    </div>
                  )}
                  {d.explanation && (
                    <div className="mt-3 small border-top pt-2">
                      <strong>Giải thích:</strong>
                      <MarkdownView content={d.explanation} format={d.contentFormat} />
                    </div>
                  )}
                </MediaUrls>
              </Card.Body>
            </Card>
          </div>
          <div className="col-lg-5">
            <Card className="mb-3">
              <Card.Body className="small">
                <strong className="d-block mb-2">Sửa lỗi</strong>
                <ul className="mb-0 ps-3">
                  {hasPermission(Permissions.ExamView) && (
                    <li>
                      <Link to={`/admin/exams/${s.examId}/versions/${s.examVersionId}`} state={back}>
                        Mở phiên bản đề
                      </Link>{" "}
                      để <strong>sửa đáp án</strong> hoặc <strong>hủy câu</strong>: hệ thống chấm lại mọi bài đã nộp.
                    </li>
                  )}
                  {s.sourceQuestionId && hasPermission(Permissions.QuestionView) && (
                    <li>
                      <Link to={`/admin/questions/${s.sourceQuestionId}/edit`} state={back}>
                        Sửa câu gốc trong ngân hàng
                      </Link>{" "}
                      (chỉ áp dụng cho đề tạo sau; đề đã phát hành không đổi).
                    </li>
                  )}
                </ul>
              </Card.Body>
            </Card>

            <strong className="d-block mb-2">Học sinh báo lỗi ({d.reports.length})</strong>
            <div className="d-flex flex-column gap-2 mb-3">
              {d.reports.map((r) => (
                <div key={r.id} className="border rounded p-2 small">
                  <div className="d-flex justify-content-between gap-2">
                    <Link to={`/admin/attempts/${r.attemptId}`} state={back} className="fw-semibold">
                      {r.fullName || r.userName}
                    </Link>
                    <Badge bg={REPORT_STATUS[r.status].variant} text={r.status === "OPEN" ? "dark" : undefined}>
                      {REPORT_STATUS[r.status].staff}
                    </Badge>
                  </div>
                  <div className="text-secondary">
                    {formatDateTime(r.createdAt)} · câu {r.questionOrder} trong bài của học sinh
                  </div>
                  <div className="mt-1">
                    <strong>{REASON_LABEL[r.reason]}</strong>
                    {r.note && <span className="text-break">: {r.note}</span>}
                  </div>
                  {r.resolvedAt && (
                    <div className="text-secondary mt-1">
                      {r.resolvedByName ?? "—"} xử lý lúc {formatDateTime(r.resolvedAt)}
                      {r.resolutionNote && <>: {r.resolutionNote}</>}
                    </div>
                  )}
                </div>
              ))}
            </div>

            {s.openCount > 0 ? (
              <Form.Group controlId="resolve-note">
                <Form.Label className="small fw-semibold">Ghi chú gửi học sinh (không bắt buộc)</Form.Label>
                <Form.Control
                  as="textarea"
                  rows={2}
                  maxLength={REPORT_NOTE_MAX}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="Ví dụ: đã sửa đáp án thành B và chấm lại."
                />
              </Form.Group>
            ) : (
              <Alert variant="secondary" className="small mb-0">
                Mọi báo lỗi của câu này đã được xử lý.
              </Alert>
            )}
            {resolve.error && (
              <Alert variant="danger" className="mt-2 mb-0">
                {describeError(resolve.error)}
              </Alert>
            )}
          </div>
        </div>
      </Modal.Body>
      <Modal.Footer>
        {s.openCount > 0 ? (
          <>
            <Button variant="outline-secondary" disabled={resolve.isPending} onClick={() => resolve.mutate("REJECTED")}>
              Câu không lỗi
            </Button>
            <Button variant="success" disabled={resolve.isPending} onClick={() => resolve.mutate("RESOLVED")}>
              Đã sửa xong
            </Button>
          </>
        ) : (
          <Button variant="outline-warning" disabled={resolve.isPending} onClick={() => resolve.mutate("OPEN")}>
            Mở lại để xử lý
          </Button>
        )}
      </Modal.Footer>
    </>
  );
}
