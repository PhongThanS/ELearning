import { useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, ProgressBar, Row, Tab, Table, Tabs } from "react-bootstrap";
import { Link, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { adminApi, examsApi } from "../../services/api";
import { ConfirmDialog, ErrorAlert, Loading } from "../../components/common/Feedback";
import { useToast } from "../../components/common/toast";
import { attemptStatusVariant as attemptVariant } from "../../constants/ui";
import { DataTable, PageHeader, SearchBox, type Column } from "../../components/common/DataTable";
import { MarkdownView } from "../../components/common/MarkdownView";
import { useListQuery } from "../../hooks/useListQuery";
import { formatDateTime, formatDuration, formatNumber, formatScore, markdownExcerpt } from "../../utils/format";
import { Permissions } from "../../constants/permissions";
import { useAuth } from "../auth/useAuth";
import type { AdminAttemptRow, AdminResultRow, AuditLog } from "../../types/api";

/** Kết quả, lượt thi, thống kê câu hỏi của một đề (docs/06-frontend.md mục 3.4). */
export function ExamResultsPage() {
  const { t } = useTranslation();
  const { id = "" } = useParams();
  const { hasPermission } = useAuth();
  const exam = useQuery({ queryKey: ["exam", id], queryFn: () => examsApi.get(id) });

  if (exam.error) {
    return <ErrorAlert error={exam.error} />;
  }
  if (!exam.data) {
    return <Loading />;
  }
  return (
    <>
      <Link to={`/admin/exams/${id}`} className="small">← {exam.data.name}</Link>
      <PageHeader title={`Kết quả — ${exam.data.code}`}>
        <span className="small text-secondary">{t(`enums.retakeScoringPolicy.${exam.data.retakeScoringPolicy}`)}</span>
      </PageHeader>
      <Tabs defaultActiveKey="results" className="mb-3" mountOnEnter>
        {hasPermission(Permissions.ResultView) && (
          <Tab eventKey="results" title="Kết quả">
            <ResultsTab examId={id} />
          </Tab>
        )}
        {hasPermission(Permissions.AttemptView) && (
          <Tab eventKey="attempts" title="Lượt thi">
            <AttemptsTab examId={id} />
          </Tab>
        )}
        {hasPermission(Permissions.ReportView) && (
          <Tab eventKey="stats" title="Thống kê câu hỏi">
            <StatsTab examId={id} />
          </Tab>
        )}
        {hasPermission(Permissions.ExamView) && (
          <Tab eventKey="corrections" title="Sửa đáp án">
            <CorrectionsTab examId={id} />
          </Tab>
        )}
      </Tabs>
    </>
  );
}

function ResultsTab({ examId }: { examId: string }) {
  const { t } = useTranslation();
  const toast = useToast();
  const { hasPermission } = useAuth();
  const list = useListQuery({ official: true, sortBy: "userName", sortDir: "asc" });
  const query = useQuery({ queryKey: ["results", examId, list.params], queryFn: () => adminApi.results(examId, list.params) });
  const exportFile = useMutation({ mutationFn: () => adminApi.exportResults(examId, list.state.official === true), onError: (e) => toast.error(e) });

  const columns: Column<AdminResultRow>[] = [
    { key: "user", header: t("auth.userNameOnly"), sortKey: "userName", render: (r) => <>{r.userName}<div className="small text-secondary">{r.fullName}</div></> },
    { key: "attempt", header: "Lượt", className: "text-nowrap", render: (r) => <>#{r.attemptNumber} {r.isOfficial && <Badge bg="primary">chính thức</Badge>}</> },
    { key: "status", header: t("common.status"), render: (r) => <Badge bg={attemptVariant[r.status]}>{t(`enums.attemptStatus.${r.status}`)}</Badge> },
    { key: "score", header: "Điểm", sortKey: "totalScore", className: "text-nowrap", render: (r) => (
      <>
        {formatScore(r.totalScore, r.maxScore)}
        <div className="small text-secondary">{formatNumber(r.percentage)}%</div>
        {r.pendingManualCount > 0 && <Badge bg="warning" text="dark" className="ms-1" title="Điểm tạm tính">Chờ chấm {r.pendingManualCount}</Badge>}
      </>
    ) },
    { key: "correct", header: "Đúng", render: (r) => `${r.correctCount}/${r.totalQuestion}` },
    { key: "passed", header: "Kết quả", render: (r) => (r.passed == null ? "—" : <Badge bg={r.passed ? "success" : "danger"}>{r.passed ? "Đạt" : "Chưa đạt"}</Badge>) },
    {
      key: "submitted",
      header: "Nộp lúc",
      sortKey: "submittedAt",
      render: (r) => (
        <>
          {formatDateTime(r.submittedAt)}
          <div className="small text-secondary d-xxl-none">Làm trong {formatDuration(r.durationSeconds)}</div>
        </>
      ),
    },
    { key: "duration", header: "Thời gian", className: "d-none d-xxl-table-cell", render: (r) => formatDuration(r.durationSeconds) },
    { key: "detail", header: "", className: "text-nowrap", render: (r) => <Link to={`/admin/attempts/${r.attemptId}`}>{t("common.detail")}</Link> },
  ];

  return (
    <>
      <Row className="g-2 mb-3 align-items-center">
        <Col md={4}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã, tên học viên..." />
        </Col>
        <Col md="auto">
          <Form.Check type="switch" id="official-only" label="Chỉ điểm chính thức" checked={list.state.official === true} onChange={(e) => list.setFilter({ official: e.target.checked })} />
        </Col>
        <Col className="text-end">
          {hasPermission(Permissions.ResultExport) && (
            <Button variant="outline-success" size="sm" disabled={exportFile.isPending} onClick={() => exportFile.mutate()}>Xuất Excel</Button>
          )}
        </Col>
      </Row>
      <DataTable data={query.data} columns={columns} rowKey={(r) => r.attemptId} isLoading={query.isLoading} error={query.error} sort={list.state} onSort={list.toggleSort} onPage={list.setPage} />
    </>
  );
}

function AttemptsTab({ examId }: { examId: string }) {
  const { t } = useTranslation();
  const list = useListQuery();
  const query = useQuery({ queryKey: ["attempts", examId, list.params], queryFn: () => adminApi.attempts(examId, list.params), refetchInterval: 30_000 });

  const columns: Column<AdminAttemptRow>[] = [
    { key: "user", header: t("auth.userNameOnly"), sortKey: "userName", render: (a) => <>{a.userName}<div className="small text-secondary">{a.fullName}</div></> },
    { key: "attempt", header: "Lượt", className: "text-nowrap", render: (a) => `#${a.attemptNumber} (v${a.versionNumber})` },
    { key: "status", header: t("common.status"), render: (a) => <Badge bg={attemptVariant[a.status]}>{t(`enums.attemptStatus.${a.status}`)}</Badge> },
    { key: "started", header: "Bắt đầu", sortKey: "startedAt", render: (a) => formatDateTime(a.startedAt) },
    { key: "expired", header: "Hạn", render: (a) => <>{formatDateTime(a.expiredAt)}{a.timeExtensionMinutes > 0 && <Badge bg="info" className="ms-1">+{a.timeExtensionMinutes}′</Badge>}</> },
    { key: "score", header: "Điểm", className: "text-nowrap", render: (a) => formatScore(a.totalScore, a.maxScore) },
    { key: "events", header: "Sự kiện", render: (a) => (a.eventCount > 0 ? <Badge bg="warning" text="dark">{a.eventCount}</Badge> : 0) },
    { key: "detail", header: "", className: "text-nowrap", render: (a) => <Link to={`/admin/attempts/${a.attemptId}`}>{t("common.detail")}</Link> },
  ];

  return (
    <>
      <Row className="g-2 mb-3">
        <Col md={4}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã, tên học viên..." />
        </Col>
        <Col md={3}>
          <Form.Select size="sm" aria-label={t("common.status")} value={String(list.state.status ?? "")} onChange={(e) => list.setFilter({ status: e.target.value })}>
            <option value="">{t("common.all")}</option>
            {(["IN_PROGRESS", "SUBMITTED", "AUTO_SUBMITTED", "CANCELLED"] as const).map((s) => (
              <option key={s} value={s}>{t(`enums.attemptStatus.${s}`)}</option>
            ))}
          </Form.Select>
        </Col>
      </Row>
      <DataTable data={query.data} columns={columns} rowKey={(a) => a.attemptId} isLoading={query.isLoading} error={query.error} sort={list.state} onSort={list.toggleSort} onPage={list.setPage} />
    </>
  );
}

function StatsTab({ examId }: { examId: string }) {
  const { t } = useTranslation();
  const query = useQuery({ queryKey: ["question-stats", examId], queryFn: () => adminApi.questionStats(examId) });
  if (query.error) {
    return <ErrorAlert error={query.error} />;
  }
  if (!query.data) {
    return <Loading />;
  }
  return (
    <div className="table-responsive">
      <Table size="sm" className="align-middle">
        <thead>
          <tr>
            <th>Câu</th>
            <th>Nội dung</th>
            <th>Lượt làm</th>
            <th>Đúng / Sai / Bỏ trống</th>
            <th style={{ minWidth: 160 }}>Tỉ lệ đúng</th>
            <th>Phân bố lựa chọn</th>
          </tr>
        </thead>
        <tbody>
          {query.data.map((s) => (
            <tr key={s.examQuestionId}>
              <td>{s.order}{s.isVoided && <Badge bg="dark" className="ms-1">hủy</Badge>}</td>
              <td className="small">
                <Badge bg="light" text="dark">{t(`enums.questionType.${s.type}`)}</Badge> {markdownExcerpt(s.contentPreview)}
              </td>
              <td>{s.attemptCount}</td>
              <td>{s.correctCount} / {s.wrongCount} / {s.blankCount}</td>
              <td>
                {s.correctRate == null ? "—" : (
                  <ProgressBar
                    now={s.correctRate}
                    label={`${formatNumber(s.correctRate)}%`}
                    variant={s.correctRate < 30 ? "danger" : s.correctRate < 60 ? "warning" : "success"}
                    aria-label={`Tỉ lệ đúng ${formatNumber(s.correctRate)}%`}
                  />
                )}
              </td>
              <td className="small">{s.optionDistribution.map((o) => `${o.optionCode}: ${o.selectedCount}`).join(" · ") || "—"}</td>
            </tr>
          ))}
        </tbody>
      </Table>
      <p className="small text-secondary">Câu có tỉ lệ đúng rất thấp có thể là câu quá khó hoặc sai đáp án.</p>
    </div>
  );
}

/** Chi tiết một lượt thi cho admin: câu trả lời, đáp án, sự kiện, thao tác (docs/06-frontend.md mục 3.4). */
/** Lịch sử sửa đáp án / hủy câu của mọi phiên bản (D-11); điểm cũ nằm ở lịch sử điểm của từng lượt thi. */
function CorrectionsTab({ examId }: { examId: string }) {
  const query = useQuery({ queryKey: ["answer-key-corrections", examId], queryFn: () => examsApi.answerKeyCorrections(examId) });
  if (query.error) {
    return <ErrorAlert error={query.error} />;
  }
  if (!query.data) {
    return <Loading />;
  }
  if (query.data.length === 0) {
    return <p className="text-secondary">Chưa có lần sửa đáp án hay hủy câu nào.</p>;
  }
  return (
    <div className="table-responsive">
      <Table size="sm" bordered className="align-middle">
        <thead>
          <tr>
            <th>Thời điểm</th>
            <th>Câu</th>
            <th>Thao tác</th>
            <th>Đáp án cũ</th>
            <th>Đáp án mới</th>
            <th>Lý do</th>
            <th className="text-end">Bài chấm lại</th>
            <th>Người thực hiện</th>
          </tr>
        </thead>
        <tbody>
          {query.data.map((c) => (
            <tr key={c.id}>
              <td className="text-nowrap">{formatDateTime(c.correctedAt)}</td>
              <td className="text-nowrap">v{c.versionNumber} · câu {c.questionOrder}</td>
              <td>
                {c.correctionType === "VOID" ? <Badge bg="danger">Hủy câu</Badge> : <Badge bg="primary">Sửa đáp án</Badge>}
              </td>
              <td className="small">{describeAnswerKey(c.oldKeyJson)}</td>
              <td className="small">{c.correctionType === "VOID" ? "—" : describeAnswerKey(c.newKeyJson)}</td>
              <td className="small">{c.reason}</td>
              <td className="text-end">{c.affectedAttemptCount}</td>
              <td>{c.correctedByName}</td>
            </tr>
          ))}
        </tbody>
      </Table>
    </div>
  );
}

const answerKeyLabels: Record<string, string> = {
  correctOptions: "Đáp án đúng",
  acceptedAnswers: "Chấp nhận",
  correctAnswerNumber: "Số",
  numericTolerance: "Sai số",
  isVoided: "Đã hủy",
};

/** Hiển thị gọn đáp án lưu dạng JSON: "Đáp án đúng: A, C · Sai số: 0". */
function describeAnswerKey(json: string): string {
  try {
    const value: unknown = JSON.parse(json);
    if (value === null || typeof value !== "object" || Array.isArray(value)) {
      return String(value);
    }
    return Object.entries(value as Record<string, unknown>)
      .filter(([, v]) => v !== null && v !== undefined && !(Array.isArray(v) && v.length === 0))
      .map(([k, v]) => `${answerKeyLabels[k] ?? k}: ${Array.isArray(v) ? v.join(", ") : typeof v === "boolean" ? (v ? "có" : "không") : String(v)}`)
      .join(" · ");
  } catch {
    return json;
  }
}

function SquareGridDisplay({
  value,
  isCorrect,
  variant,
}: {
  value: string;
  isCorrect?: boolean | null;
  variant: "student" | "correct";
}) {
  const chars = (value || "").split("");
  if (chars.length === 0) {
    chars.push("");
  }

  const isGreen = variant === "correct" || isCorrect === true;
  const borderColor = isGreen ? "#059669" : "#dc2626";
  const bgColor = isGreen ? "#ecfdf5" : "#fef2f2";
  const textColor = isGreen ? "#065f46" : "#991b1b";

  return (
    <div className="d-flex gap-2">
      {chars.map((c, i) => (
        <div
          key={i}
          className="d-flex align-items-center justify-content-center fw-bold fs-4 rounded border shadow-sm"
          style={{
            width: "44px",
            height: "48px",
            backgroundColor: c ? bgColor : "#f8fafc",
            borderColor: c ? borderColor : "#cbd5e1",
            borderWidth: "2px",
            color: textColor,
          }}
        >
          {c || <span className="text-muted small opacity-25">_</span>}
        </div>
      ))}
    </div>
  );
}

export function AttemptAdminPage() {
  const { t } = useTranslation();
  const { attemptId = "" } = useParams();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const query = useQuery({ queryKey: ["admin-attempt", attemptId], queryFn: () => adminApi.attempt(attemptId) });
  const [dialog, setDialog] = useState<"extend" | "force" | "cancel" | null>(null);
  const [minutes, setMinutes] = useState(10);
  const action = useMutation({
    mutationFn: async (run: () => Promise<unknown>) => run(),
    onSuccess: (data) => {
      queryClient.setQueryData(["admin-attempt", attemptId], data);
      void queryClient.invalidateQueries({ queryKey: ["attempts"] });
      void queryClient.invalidateQueries({ queryKey: ["results"] });
      toast.success(t("common.saved"));
      setDialog(null);
    },
    onError: (e) => toast.error(e),
  });

  if (query.error) {
    return <ErrorAlert error={query.error} />;
  }
  if (!query.data) {
    return <Loading />;
  }
  const a = query.data;
  const canManage = hasPermission(Permissions.AttemptManage);

  return (
    <>
      <Link to={`/admin/exams/${a.examId}/results`} className="small">← {a.examName}</Link>
      <PageHeader
        title={`${a.fullName} (${a.userName}) — lượt #${a.attemptNumber}`}
        actions={
          canManage && (
            <>
              {a.status === "IN_PROGRESS" && <Button variant="outline-primary" onClick={() => setDialog("extend")}>Gia hạn</Button>}
              {a.status === "IN_PROGRESS" && <Button variant="outline-warning" onClick={() => setDialog("force")}>Buộc nộp</Button>}
              {a.status !== "CANCELLED" && <Button variant="outline-danger" onClick={() => setDialog("cancel")}>Hủy lượt</Button>}
            </>
          )
        }
      >
        <Badge bg={attemptVariant[a.status]}>{t(`enums.attemptStatus.${a.status}`)}</Badge>{" "}
        {a.submitReason && <span className="small text-secondary">{t(`enums.submitReason.${a.submitReason}`)}</span>}
      </PageHeader>
      <Row className="g-3 mb-3">
        <Col md={6}>
          <Card>
            <Card.Body className="small">
              <dl className="row mb-0">
                <dt className="col-5">Bắt đầu</dt><dd className="col-7">{formatDateTime(a.startedAt)}</dd>
                <dt className="col-5">Hạn nộp</dt><dd className="col-7">{formatDateTime(a.expiredAt)} {a.timeExtensionMinutes > 0 && `(gia hạn ${a.timeExtensionMinutes} phút)`}</dd>
                <dt className="col-5">Nộp lúc</dt><dd className="col-7">{formatDateTime(a.submittedAt)}</dd>
                <dt className="col-5">Điểm</dt><dd className="col-7">{formatScore(a.totalScore, a.maxScore)} {a.percentage != null && `(${formatNumber(a.percentage)}%)`} {a.gradingRevision && a.gradingRevision > 1 && <Badge bg="info">chấm lại lần {a.gradingRevision - 1}</Badge>}</dd>
                <dt className="col-5">Phiên bản đề</dt><dd className="col-7">v{a.versionNumber}</dd>
                <dt className="col-5">IP bắt đầu / nộp</dt><dd className="col-7">{a.startedIp ?? "—"} / {a.submittedIp ?? "—"}</dd>
                <dt className="col-5">Trình duyệt</dt><dd className="col-7 text-break">{a.startedUserAgent ?? "—"}</dd>
                {a.cancelReason && (<><dt className="col-5">Lý do hủy</dt><dd className="col-7">{a.cancelReason}</dd></>)}
              </dl>
            </Card.Body>
          </Card>
        </Col>
        <Col md={6}>
          <Card>
            <Card.Body>
              <h2 className="h6">Sự kiện ({a.events.length})</h2>
              <div style={{ maxHeight: 220, overflowY: "auto" }}>
                <Table size="sm" className="small mb-0">
                  <tbody>
                    {a.events.map((e, i) => (
                      <tr key={i}>
                        <td>{formatDateTime(e.serverTime)}</td>
                        <td><code>{e.type}</code></td>
                        <td>{e.ipAddress}</td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </div>
            </Card.Body>
          </Card>
        </Col>
      </Row>
      {/* Thẻ tổng quan kết quả bài làm theo chuẩn học sinh */}
      <Card className="mb-4 shadow-sm border-0 bg-light">
        <Card.Body className="p-3">
          <Row className="align-items-center text-center g-3">
            <Col xs={12} md={3}>
              <div
                className={`score-ring accent-${a.passed == null ? "blue" : a.passed ? "green" : "pink"} mx-auto`}
                style={{ "--value": Math.min(100, Math.max(0, a.percentage ?? 0)) } as React.CSSProperties}
                role="img"
                aria-label={`Điểm đạt: ${formatNumber(a.percentage)}%`}
              >
                <span>{formatNumber(a.percentage ?? 0)}%</span>
              </div>
            </Col>
            <Col xs={4} md={3}>
              <div className="fs-2 fw-bold text-primary">{formatScore(a.totalScore, a.maxScore)}</div>
              <div className="small text-secondary">Điểm tổng kết</div>
            </Col>
            <Col xs={4} md={3}>
              <div className="fs-2 fw-bold text-info-emphasis">
                {a.correctCount ?? a.answers.filter((q) => q.isCorrect === true).length}/{a.answers.length}
              </div>
              <div className="small text-secondary">Số câu trả lời đúng</div>
            </Col>
            {a.passed != null && (
              <Col xs={4} md={3}>
                <div className={`fs-2 fw-bold ${a.passed ? "text-success" : "text-danger"}`}>
                  <i className={`bi ${a.passed ? "bi-trophy-fill" : "bi-emoji-frown"} me-2`} />
                  {a.passed ? "ĐẠT" : "CHƯA ĐẠT"}
                </div>
                <div className="small text-secondary">Kết quả đánh giá</div>
              </Col>
            )}
          </Row>
        </Card.Body>
      </Card>

      <div className="d-flex align-items-center justify-content-between mb-3 mt-4">
        <h2 className="h5 mb-0 d-flex align-items-center gap-2">
          <i className="bi bi-card-checklist text-primary" />
          <span>Chi tiết câu hỏi và bài làm của học sinh</span>
        </h2>
        <Badge bg="secondary" className="px-2 py-1">
          {a.answers.length} câu hỏi
        </Badge>
      </div>

      {a.answers.map((q) => {
        const isCorrect = q.isCorrect === true;
        const isIncorrect = q.isCorrect === false;

        return (
          <Card
            key={q.attemptQuestionId}
            className={`mb-3 shadow-sm border-2 border-${q.isVoided ? "secondary" : isCorrect ? "success" : "danger"}`}
          >
            <Card.Body>
              <div className="d-flex justify-content-between align-items-center mb-2 pb-2 border-bottom">
                <div className="d-flex align-items-center gap-2">
                  <strong className="fs-6">Câu {q.order}</strong>
                  {q.isMarkedForReview && <Badge bg="warning" text="dark">Đánh dấu xem lại</Badge>}
                  {q.isVoided ? (
                    <Badge bg="secondary">Đã hủy câu</Badge>
                  ) : isCorrect ? (
                    <Badge bg="success" className="px-2 py-1 d-inline-flex align-items-center gap-1">
                      <i className="bi bi-check-circle-fill" /> Đúng (+{formatScore(q.score, q.maxScore)} đ)
                    </Badge>
                  ) : (
                    <Badge bg="danger" className="px-2 py-1 d-inline-flex align-items-center gap-1">
                      <i className="bi bi-x-circle-fill" /> Sai ({formatScore(q.score, q.maxScore)} đ)
                    </Badge>
                  )}
                </div>
                <div className={`fs-4 ${isCorrect ? "text-success" : isIncorrect ? "text-danger" : "text-muted"}`}>
                  <i className={`bi ${isCorrect ? "bi-check-circle-fill" : isIncorrect ? "bi-x-circle-fill" : "bi-dash-circle"}`} />
                </div>
              </div>

              {q.isVoided && <Alert variant="secondary" className="py-1 small my-2">Câu hỏi đã được hủy điểm.</Alert>}

              <div className="mb-3 fs-6">
                <MarkdownView content={q.content} media={a.media} />
              </div>

              {q.type === "FILL_IN" ? (
                <div className="d-flex flex-column gap-3 mb-2 p-3 border rounded bg-light">
                  <div>
                    <div className="d-flex justify-content-between align-items-center mb-1">
                      <span className="small fw-bold text-secondary">
                        Câu trả lời của học sinh:
                      </span>
                      <Badge bg={isCorrect ? "success" : "danger"} className="py-1 px-2 d-inline-flex align-items-center gap-1">
                        <i className={`bi ${isCorrect ? "bi-check-circle-fill" : "bi-x-circle-fill"}`} />
                        <span>{isCorrect ? "Học sinh trả lời đúng" : "Học sinh trả lời sai"}</span>
                      </Badge>
                    </div>
                    <div className="d-flex align-items-center gap-3">
                      <SquareGridDisplay value={q.answerText || ""} isCorrect={isCorrect} variant="student" />
                      {q.answerText && (
                        <span className="text-muted small">({q.answerText})</span>
                      )}
                    </div>
                  </div>

                  {!isCorrect && (
                    <div className="pt-2 border-top">
                      <div className="small fw-bold text-success mb-1">
                        Đáp án đúng chuẩn:
                      </div>
                      {(() => {
                        const correctStr =
                          q.answerDataType === "NUMBER"
                            ? String(formatNumber(q.correctAnswerNumber)).replace(".", ",")
                            : q.acceptedAnswers[0] || "";
                        return (
                          <div className="d-flex align-items-center gap-3">
                            <SquareGridDisplay value={correctStr} variant="correct" />
                            <span className="text-success fw-bold small">({correctStr})</span>
                          </div>
                        );
                      })()}
                    </div>
                  )}
                </div>
              ) : q.options && q.options.length > 0 ? (
                <div className="d-flex flex-column gap-2 mb-2">
                  {q.options.map((o, optIdx) => {
                    const selected = q.selectedOptions.includes(o.optionCode);
                    const correct = o.isCorrect || q.correctOptions.includes(o.optionCode);

                    let containerClass = "border border-light-subtle bg-body-tertiary text-body-secondary";
                    let iconBadge = null;
                    let statusBadge = null;

                    if (selected && correct) {
                      containerClass = "border border-2 border-success bg-success-subtle text-success-emphasis fw-medium";
                      iconBadge = (
                        <span
                          className="badge bg-success text-white d-inline-flex align-items-center justify-content-center me-2"
                          style={{ width: "36px", height: "32px" }}
                          title="Học sinh chọn đúng"
                        >
                          <i className="bi bi-check-lg fs-5" />
                        </span>
                      );
                      statusBadge = (
                        <Badge bg="success" className="ms-auto py-1 px-2 d-inline-flex align-items-center gap-1">
                          <i className="bi bi-check2-circle" /> Học sinh chọn đúng
                        </Badge>
                      );
                    } else if (selected && !correct) {
                      containerClass = "border border-2 border-danger bg-danger-subtle text-danger-emphasis fw-medium";
                      iconBadge = (
                        <span
                          className="badge bg-danger text-white d-inline-flex align-items-center justify-content-center me-2"
                          style={{ width: "36px", height: "32px" }}
                          title="Học sinh chọn sai"
                        >
                          <i className="bi bi-x-lg fs-5" />
                        </span>
                      );
                      statusBadge = (
                        <Badge bg="danger" className="ms-auto py-1 px-2 d-inline-flex align-items-center gap-1">
                          <i className="bi bi-x-circle" /> Học sinh chọn sai
                        </Badge>
                      );
                    } else if (!selected && correct) {
                      containerClass = "border border-2 border-success bg-success-subtle text-success-emphasis fw-medium";
                      iconBadge = (
                        <span
                          className="badge bg-success text-white d-inline-flex align-items-center justify-content-center me-2"
                          style={{ width: "36px", height: "32px" }}
                          title="Đáp án đúng"
                        >
                          <i className="bi bi-check-lg fs-5" />
                        </span>
                      );
                      statusBadge = (
                        <Badge bg="success" className="ms-auto py-1 px-2 d-inline-flex align-items-center gap-1">
                          <i className="bi bi-check-lg" /> Đáp án đúng
                        </Badge>
                      );
                    } else {
                      iconBadge = (
                        <span
                          className="badge bg-white text-muted border me-2 d-inline-flex align-items-center justify-content-center"
                          style={{ width: "36px", height: "32px", fontSize: "0.85rem" }}
                        >
                          {o.optionCode || String.fromCharCode(65 + optIdx)}
                        </span>
                      );
                    }

                    return (
                      <div
                        key={o.optionCode}
                        className={`p-2 px-3 rounded d-flex align-items-center justify-content-between ${containerClass}`}
                      >
                        <div className="d-flex align-items-center flex-grow-1 me-2">
                          {iconBadge}
                          {(selected || correct) && (
                            <strong className="me-2">{o.optionCode || String.fromCharCode(65 + optIdx)}.</strong>
                          )}
                          <div className="flex-grow-1">
                            <MarkdownView content={o.content} inline media={a.media} />
                          </div>
                        </div>
                        {statusBadge}
                      </div>
                    );
                  })}
                </div>
              ) : (
                <div className="p-3 border rounded bg-light mb-2">
                  <div className="d-flex align-items-center flex-wrap gap-2">
                    <span className="text-secondary">Trả lời:</span>{" "}
                    <span className={isCorrect ? "badge bg-success-subtle text-success border border-success fw-bold" : isIncorrect ? "badge bg-danger-subtle text-danger border border-danger fw-bold" : "badge bg-light text-dark border"}>
                      {q.selectedOptions.join(", ") || q.answerText || "(bỏ trống)"}
                    </span>
                    <span className="text-secondary">· Đáp án đúng:</span>{" "}
                    <span className="badge bg-success-subtle text-success border border-success fw-bold">
                      {q.correctOptions.join(", ") || q.acceptedAnswers.join(" / ")}
                    </span>
                  </div>
                </div>
              )}

              {q.explanation && (
                <div className="mt-2 p-2 px-3 rounded bg-info-subtle border border-info text-info-emphasis small">
                  <strong>
                    <i className="bi bi-lightbulb me-1" />
                    Lời giải chi tiết:
                  </strong>{" "}
                  <MarkdownView content={q.explanation} media={a.media} />
                </div>
              )}
            </Card.Body>
          </Card>
        );
      })}

      <ConfirmDialog
        show={dialog === "extend"}
        title="Gia hạn lượt thi"
        requireReason
        busy={action.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={(reason) => action.mutate(() => adminApi.extend(attemptId, minutes, reason))}
      >
        <Form.Group controlId="extend-minutes">
          <Form.Label>Số phút (1–240)</Form.Label>
          <Form.Control type="number" min={1} max={240} value={minutes} onChange={(e) => setMinutes(Number(e.target.value))} />
        </Form.Group>
      </ConfirmDialog>
      <ConfirmDialog
        show={dialog === "force"}
        title="Buộc nộp bài"
        body="Lượt thi sẽ được nộp và chấm ngay với các câu trả lời hiện có."
        variant="warning"
        requireReason
        busy={action.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={(reason) => action.mutate(() => adminApi.forceSubmit(attemptId, reason))}
      />
      <ConfirmDialog
        show={dialog === "cancel"}
        title="Hủy lượt thi"
        body="Lượt bị hủy không tính vào số lượt đã dùng (học viên được thi lại) và bị loại khỏi điểm chính thức."
        variant="danger"
        requireReason
        busy={action.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={(reason) => action.mutate(() => adminApi.cancel(attemptId, reason))}
      />
    </>
  );
}

export function AuditLogsPage() {
  const { t } = useTranslation();
  const list = useListQuery({ pageSize: 50 });
  const query = useQuery({ queryKey: ["audit", list.params], queryFn: () => adminApi.auditLogs(list.params) });
  const [detail, setDetail] = useState<AuditLog | null>(null);

  const columns: Column<AuditLog>[] = [
    { key: "time", header: "Thời điểm", render: (l) => formatDateTime(l.createdAt) },
    { key: "user", header: "Người thực hiện", render: (l) => l.userName ?? "—" },
    { key: "action", header: "Thao tác", render: (l) => <code>{l.action}</code> },
    { key: "entity", header: "Đối tượng", render: (l) => (l.entityName ? `${l.entityName}` : "—") },
    { key: "reason", header: t("common.reason"), render: (l) => l.reason ?? "" },
    { key: "ip", header: "IP", render: (l) => l.ipAddress ?? "" },
    { key: "detail", header: "", className: "text-nowrap", render: (l) => (l.oldValue || l.newValue ? <Button size="sm" variant="link" onClick={() => setDetail(l)}>{t("common.detail")}</Button> : null) },
  ];

  return (
    <>
      <PageHeader title={t("nav.auditLogs")} />
      <Row className="g-2 mb-3">
        <Col md={4}>
          <Form.Control size="sm" aria-label="Thao tác" placeholder="Mã thao tác, ví dụ EXAM_PUBLISHED" defaultValue="" onBlur={(e) => list.setFilter({ action: e.target.value.trim() })} />
        </Col>
        <Col md={3}>
          <Form.Control size="sm" type="date" aria-label="Từ ngày" onChange={(e) => list.setFilter({ from: e.target.value ? new Date(`${e.target.value}T00:00:00+07:00`).toISOString() : "" })} />
        </Col>
        <Col md={3}>
          <Form.Control size="sm" type="date" aria-label="Đến ngày" onChange={(e) => list.setFilter({ to: e.target.value ? new Date(`${e.target.value}T23:59:59+07:00`).toISOString() : "" })} />
        </Col>
      </Row>
      <DataTable data={query.data} columns={columns} rowKey={(l) => String(l.id)} isLoading={query.isLoading} error={query.error} onPage={list.setPage} />
      <ConfirmDialog show={!!detail} title={detail?.action ?? ""} onCancel={() => setDetail(null)} onConfirm={() => setDetail(null)} confirmText={t("common.close")}>
        {detail && (
          <>
            <div className="small text-secondary">Trước</div>
            <MarkdownView content={"```json\n" + pretty(detail.oldValue) + "\n```"} />
            <div className="small text-secondary">Sau</div>
            <MarkdownView content={"```json\n" + pretty(detail.newValue) + "\n```"} />
          </>
        )}
      </ConfirmDialog>
    </>
  );
}

function pretty(json: string | null): string {
  if (!json) {
    return "—";
  }
  try {
    return JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    return json;
  }
}
