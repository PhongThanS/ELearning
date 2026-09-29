import { useState } from "react";
import { Badge, Button, Card, Col, Form, ProgressBar, Row, Tab, Table, Tabs } from "react-bootstrap";
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
    { key: "attempt", header: "Lượt", render: (r) => <>#{r.attemptNumber} {r.isOfficial && <Badge bg="primary">chính thức</Badge>}</> },
    { key: "status", header: t("common.status"), render: (r) => <Badge bg={attemptVariant[r.status]}>{t(`enums.attemptStatus.${r.status}`)}</Badge> },
    { key: "score", header: "Điểm", sortKey: "totalScore", render: (r) => `${formatScore(r.totalScore, r.maxScore)} (${formatNumber(r.percentage)}%)` },
    { key: "correct", header: "Đúng", render: (r) => `${r.correctCount}/${r.totalQuestion}` },
    { key: "passed", header: "Kết quả", render: (r) => (r.passed == null ? "—" : <Badge bg={r.passed ? "success" : "danger"}>{r.passed ? "Đạt" : "Chưa đạt"}</Badge>) },
    { key: "submitted", header: "Nộp lúc", sortKey: "submittedAt", render: (r) => formatDateTime(r.submittedAt) },
    { key: "duration", header: "Thời gian", render: (r) => formatDuration(r.durationSeconds) },
    { key: "detail", header: "", render: (r) => <Link to={`/admin/attempts/${r.attemptId}`}>{t("common.detail")}</Link> },
  ];

  return (
    <>
      <Row className="g-2 mb-3 align-items-center">
        <Col md={4}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} />
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
    { key: "attempt", header: "Lượt", render: (a) => `#${a.attemptNumber} (v${a.versionNumber})` },
    { key: "status", header: t("common.status"), render: (a) => <Badge bg={attemptVariant[a.status]}>{t(`enums.attemptStatus.${a.status}`)}</Badge> },
    { key: "started", header: "Bắt đầu", sortKey: "startedAt", render: (a) => formatDateTime(a.startedAt) },
    { key: "expired", header: "Hạn", render: (a) => <>{formatDateTime(a.expiredAt)}{a.timeExtensionMinutes > 0 && <Badge bg="info" className="ms-1">+{a.timeExtensionMinutes}′</Badge>}</> },
    { key: "score", header: "Điểm", render: (a) => formatScore(a.totalScore, a.maxScore) },
    { key: "events", header: "Sự kiện", render: (a) => (a.eventCount > 0 ? <Badge bg="warning" text="dark">{a.eventCount}</Badge> : 0) },
    { key: "detail", header: "", render: (a) => <Link to={`/admin/attempts/${a.attemptId}`}>{t("common.detail")}</Link> },
  ];

  return (
    <>
      <Row className="g-2 mb-3">
        <Col md={4}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} />
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
      <h2 className="h5">Bài làm</h2>
      {a.answers.map((q) => (
        <Card key={q.attemptQuestionId} className="mb-2">
          <Card.Body className="py-2">
            <div className="d-flex justify-content-between small">
              <strong>Câu {q.order} {q.isMarkedForReview && "⚑"}</strong>
              <span className={q.isCorrect ? "text-success" : q.isCorrect === false ? "text-danger" : ""}>
                {formatScore(q.score, q.maxScore)} {q.isVoided && <Badge bg="dark">hủy</Badge>}
              </span>
            </div>
            <MarkdownView content={q.content} />
            <div className="small">
              <span className="text-secondary">Trả lời:</span>{" "}
              {q.type === "FILL_IN" ? q.answerText ?? "(bỏ trống)" : q.selectedOptions.join(", ") || "(bỏ trống)"} ·{" "}
              <span className="text-secondary">Đáp án:</span>{" "}
              {q.type === "FILL_IN" ? (q.answerDataType === "NUMBER" ? formatNumber(q.correctAnswerNumber) : q.acceptedAnswers.join(" / ")) : q.correctOptions.join(", ")}
              <span className="text-secondary"> · lưu {q.saveCount} lần</span>
            </div>
          </Card.Body>
        </Card>
      ))}

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
    { key: "detail", header: "", render: (l) => (l.oldValue || l.newValue ? <Button size="sm" variant="link" onClick={() => setDetail(l)}>{t("common.detail")}</Button> : null) },
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
