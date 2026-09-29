import { useState } from "react";
import { Alert, Badge, Button, Card, Col, ListGroup, Row, Table } from "react-bootstrap";
import { Link, useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery } from "@tanstack/react-query";
import { studentApi } from "../../services/api";
import { ConfirmDialog, Empty, ErrorAlert, Loading } from "../../components/common/Feedback";
import { describeError } from "../../utils/errors";
import { availabilityVariant } from "../../constants/ui";
import { MarkdownView } from "../../components/common/MarkdownView";
import { Pager } from "../../components/common/DataTable";
import { formatDateTime, formatDuration, formatNumber, formatScore, optionLabel } from "../../utils/format";
import type { StudentExamDetail, StudentExamItem } from "../../types/api";

export function StudentExamsPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ["student-exams", page], queryFn: () => studentApi.exams({ page, pageSize: 20 }) });

  return (
    <>
      <h1 className="h4 mb-3">{t("student.exams")}</h1>
      {query.error && <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />}
      {query.isLoading && <Loading />}
      {query.data && query.data.items.length === 0 && <Empty text={t("student.noExams")} />}
      <Row xs={1} md={2} lg={3} className="g-3">
        {query.data?.items.map((exam) => (
          <Col key={exam.examId}>
            <ExamCard exam={exam} />
          </Col>
        ))}
      </Row>
      {query.data && <div className="mt-3"><Pager data={query.data} onPage={setPage} /></div>}
    </>
  );
}

function ExamCard({ exam }: { exam: StudentExamItem }) {
  const { t } = useTranslation();
  return (
    <Card className="h-100">
      <Card.Body className="d-flex flex-column">
        <div className="d-flex justify-content-between align-items-start gap-2 mb-2">
          <Card.Title as="h2" className="h6 mb-0">
            {exam.name}
          </Card.Title>
          <Badge bg={availabilityVariant[exam.availability]}>{t(`enums.availability.${exam.availability}`)}</Badge>
        </div>
        <ul className="list-unstyled small text-secondary mb-3">
          <li>{t("student.duration", { minutes: exam.durationMinutes })} · {t("student.questions", { count: exam.questionCount })}</li>
          <li>{t("student.attempts", { used: exam.usedAttempts, max: exam.maxAttempts })}</li>
          {(exam.startAt || exam.endAt) && (
            <li>
              {t("student.window")}: {formatDateTime(exam.startAt)} – {formatDateTime(exam.endAt)}
            </li>
          )}
          {exam.officialScore != null && <li>{t("student.officialScore", { score: formatNumber(exam.officialScore) })}</li>}
        </ul>
        <Link to={`/student/exams/${exam.examId}`} className="btn btn-outline-primary btn-sm mt-auto">
          {exam.availability === "IN_PROGRESS" ? t("student.continue") : t("common.detail")}
        </Link>
      </Card.Body>
    </Card>
  );
}

export function StudentExamDetailPage() {
  const { t } = useTranslation();
  const { examId = "" } = useParams();
  const navigate = useNavigate();
  const [confirm, setConfirm] = useState(false);
  const [openedAt] = useState(() => Date.now());
  const query = useQuery({ queryKey: ["student-exam", examId], queryFn: () => studentApi.exam(examId) });
  const start = useMutation({
    mutationFn: () => studentApi.start(examId),
    onSuccess: (attempt) => navigate(`/student/attempts/${attempt.attemptId}`),
  });

  if (query.error) {
    return <ErrorAlert error={query.error} />;
  }
  if (!query.data) {
    return <Loading />;
  }
  const exam: StudentExamDetail = query.data;
  const canStart = exam.availability === "AVAILABLE" || exam.availability === "IN_PROGRESS";
  const minutesUntilEnd = exam.endAt ? Math.floor((new Date(exam.endAt).getTime() - openedAt) / 60000) : null;
  const shortTime = minutesUntilEnd != null && minutesUntilEnd < exam.durationMinutes && exam.availability === "AVAILABLE";

  return (
    <>
      <Link to="/student/exams" className="small">← {t("student.exams")}</Link>
      <h1 className="h4 mt-2">{exam.name}</h1>
      <Row className="g-3">
        <Col md={8}>
          <Card>
            <Card.Body>
              {exam.description && <p>{exam.description}</p>}
              {exam.instructions && <MarkdownView content={exam.instructions} />}
              <ListGroup variant="flush" className="mt-2">
                <ListGroup.Item>{t("student.duration", { minutes: exam.durationMinutes })}</ListGroup.Item>
                <ListGroup.Item>{t("student.questions", { count: exam.questionCount })} · {formatNumber(exam.maxScore)} điểm</ListGroup.Item>
                {exam.passPercentage != null && <ListGroup.Item>Điểm đạt: {formatNumber(exam.passPercentage)}%</ListGroup.Item>}
                <ListGroup.Item>{t("student.attempts", { used: exam.usedAttempts, max: exam.maxAttempts })}</ListGroup.Item>
                <ListGroup.Item>
                  {t("student.window")}: {exam.startAt || exam.endAt ? `${formatDateTime(exam.startAt)} – ${formatDateTime(exam.endAt)}` : t("student.noWindow")}
                </ListGroup.Item>
              </ListGroup>
              <p className="small text-secondary mt-3 mb-0">{t("player.tracking")}</p>
            </Card.Body>
          </Card>
        </Col>
        <Col md={4}>
          <Card>
            <Card.Body>
              <Badge bg={availabilityVariant[exam.availability]} className="mb-3">
                {t(`enums.availability.${exam.availability}`)}
              </Badge>
              {shortTime && exam.endAt && (
                <Alert variant="warning" className="small">
                  {t("student.shortTime", { end: formatDateTime(exam.endAt), minutes: Math.max(0, minutesUntilEnd!) })}
                </Alert>
              )}
              {start.error && <Alert variant="danger">{describeError(start.error)}</Alert>}
              {canStart && (
                <Button
                  className="w-100"
                  size="lg"
                  disabled={start.isPending}
                  onClick={() => (exam.inProgressAttemptId ? navigate(`/student/attempts/${exam.inProgressAttemptId}`) : setConfirm(true))}
                >
                  {exam.inProgressAttemptId ? t("student.continue") : t("student.start")}
                </Button>
              )}
            </Card.Body>
          </Card>
        </Col>
      </Row>

      {exam.attempts.length > 0 && (
        <>
          <h2 className="h5 mt-4">{t("student.myAttempts")}</h2>
          <div className="table-responsive">
            <Table size="sm" className="align-middle">
              <thead>
                <tr>
                  <th>#</th>
                  <th>{t("common.status")}</th>
                  <th>Bắt đầu</th>
                  <th>Nộp bài</th>
                  <th>{t("result.score")}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {exam.attempts.map((a) => (
                  <tr key={a.attemptId}>
                    <td>{a.attemptNumber}</td>
                    <td>{t(`enums.attemptStatus.${a.status}`)}</td>
                    <td>{formatDateTime(a.startedAt)}</td>
                    <td>{formatDateTime(a.submittedAt)}</td>
                    <td>{a.scoreVisible ? formatScore(a.totalScore, a.maxScore) : a.pendingManualGrading ? <Badge bg="warning" text="dark">{t("result.grading")}</Badge> : "—"}</td>
                    <td>
                      {a.status === "SUBMITTED" || a.status === "AUTO_SUBMITTED" ? (
                        <Link to={`/student/results/${a.attemptId}`}>{t("student.viewResult")}</Link>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        </>
      )}

      <ConfirmDialog
        show={confirm}
        title={t("student.start")}
        body={t("student.startConfirm")}
        busy={start.isPending}
        onCancel={() => setConfirm(false)}
        onConfirm={() => start.mutate()}
      />
    </>
  );
}

export function ResultPage() {
  const { t } = useTranslation();
  const { attemptId = "" } = useParams();
  const query = useQuery({ queryKey: ["result", attemptId], queryFn: () => studentApi.result(attemptId) });

  if (query.error) {
    return <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />;
  }
  if (!query.data) {
    return <Loading />;
  }
  const r = query.data;

  return (
    <>
      <Link to={`/student/exams/${r.examId}`} className="small">← {r.examName}</Link>
      <h1 className="h4 mt-2">{t("result.title")}</h1>
      <Card className="mb-3">
        <Card.Body>
          <p className="mb-2">
            {t(`enums.attemptStatus.${r.status}`)}
            {r.submitReason && r.submitReason !== "STUDENT" && ` (${t(`enums.submitReason.${r.submitReason}`)})`} · {t("result.submittedAt")}{" "}
            {formatDateTime(r.submittedAt)} · {t("result.duration")}: {formatDuration(r.durationSeconds)}
          </p>
          {r.scoreVisible ? (
            <Row className="text-center g-3">
              <Col xs={6} md={3}>
                <div className="display-6">{formatScore(r.totalScore, r.maxScore)}</div>
                <div className="small text-secondary">{t("result.score")}</div>
              </Col>
              <Col xs={6} md={3}>
                <div className="display-6">{formatNumber(r.percentage)}%</div>
                <div className="small text-secondary">{t("result.percentage")}</div>
              </Col>
              <Col xs={6} md={3}>
                <div className="display-6">
                  {r.correctCount}/{r.totalQuestion}
                </div>
                <div className="small text-secondary">{t("result.correct")}</div>
              </Col>
              {r.passed != null && (
                <Col xs={6} md={3}>
                  <div className={`display-6 ${r.passed ? "text-success" : "text-danger"}`}>{r.passed ? t("result.passed") : t("result.failed")}</div>
                </Col>
              )}
            </Row>
          ) : (
            <Alert variant="info" className="mb-0">
              {r.pendingManualGrading
                ? t("result.pendingManual")
                : r.reviewAvailableAt ? t("result.hiddenUntil", { time: formatDateTime(r.reviewAvailableAt) }) : t("result.hidden")}
            </Alert>
          )}
          {r.scoreVisible && !r.reviewAvailable && r.reviewAvailableAt && (
            <p className="small text-secondary mt-3 mb-0">{t("result.reviewUntil", { time: formatDateTime(r.reviewAvailableAt) })}</p>
          )}
        </Card.Body>
      </Card>

      {r.reviewAvailable && r.questions && (
        <>
          <h2 className="h5">{t("result.review")}</h2>
          {r.questions.map((q) => (
            <Card key={q.id} className={`mb-3 border-${q.isVoided ? "secondary" : q.isCorrect ? "success" : "danger"}`}>
              <Card.Body>
                <div className="d-flex justify-content-between">
                  <strong>Câu {q.order}</strong>
                  <span className={q.isCorrect ? "text-success" : "text-danger"}>
                    {formatNumber(q.score)}/{formatNumber(q.maxScore)} {q.isCorrect ? "✓" : "✗"}
                  </span>
                </div>
                {q.isVoided && <Alert variant="secondary" className="py-1 small my-2">{t("result.voided")}</Alert>}
                <MarkdownView content={q.content} format={q.contentFormat} />
                {q.type === "ESSAY" ? (
                  <>
                    <div className="small fw-semibold mt-2">{t("result.yourAnswer")}</div>
                    <div className="border rounded p-2 small" style={{ whiteSpace: "pre-wrap" }}>{q.answerText || t("result.noAnswer")}</div>
                    {q.manualComment && (
                      <div className="small mt-2"><strong>{t("result.graderComment")}:</strong> <span style={{ whiteSpace: "pre-wrap" }}>{q.manualComment}</span></div>
                    )}
                  </>
                ) : q.type === "FILL_IN" ? (
                  <dl className="row small mb-0">
                    <dt className="col-sm-3">{t("result.yourAnswer")}</dt>
                    <dd className="col-sm-9">{q.answerText || t("result.noAnswer")}</dd>
                    <dt className="col-sm-3">{t("result.correctAnswer")}</dt>
                    <dd className="col-sm-9">
                      {q.answerDataType === "NUMBER" ? formatNumber(q.correctAnswerNumber) : q.acceptedAnswers.join(" / ")}
                    </dd>
                  </dl>
                ) : (
                  <ul className="list-unstyled mb-0">
                    {q.options.map((o, index) => {
                      const selected = q.selectedOptions.includes(o.code);
                      const correct = q.correctOptions.includes(o.code);
                      return (
                        <li key={o.code} className={`px-2 py-1 rounded mb-1 ${correct ? "bg-success-subtle" : selected ? "bg-danger-subtle" : ""}`}>
                          <strong>{q.type === "TRUE_FALSE" ? "" : `${optionLabel(index)}. `}</strong>
                          {o.content}
                          {selected && <Badge bg="secondary" className="ms-2">{t("result.yourAnswer")}</Badge>}
                          {correct && <Badge bg="success" className="ms-2">{t("result.correctAnswer")}</Badge>}
                        </li>
                      );
                    })}
                  </ul>
                )}
                {q.explanation && (
                  <div className="mt-2 small">
                    <strong>{t("result.explanation")}:</strong> <MarkdownView content={q.explanation} />
                  </div>
                )}
              </Card.Body>
            </Card>
          ))}
        </>
      )}
    </>
  );
}

export function HistoryPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ["history", page], queryFn: () => studentApi.history({ page, pageSize: 20 }) });

  return (
    <>
      <h1 className="h4 mb-3">{t("nav.history")}</h1>
      {query.error && <ErrorAlert error={query.error} />}
      {query.isLoading && <Loading />}
      {query.data && query.data.items.length === 0 && <Empty />}
      {query.data && query.data.items.length > 0 && (
        <>
          <div className="table-responsive">
            <Table size="sm" hover className="align-middle">
              <thead>
                <tr>
                  <th>Đề thi</th>
                  <th>Lượt</th>
                  <th>{t("common.status")}</th>
                  <th>Bắt đầu</th>
                  <th>Nộp bài</th>
                  <th>{t("result.score")}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {query.data.items.map((h) => (
                  <tr key={h.attemptId}>
                    <td>{h.examName}</td>
                    <td>{h.attemptNumber}</td>
                    <td>{t(`enums.attemptStatus.${h.status}`)}</td>
                    <td>{formatDateTime(h.startedAt)}</td>
                    <td>{formatDateTime(h.submittedAt)}</td>
                    <td>
                      {h.scoreVisible ? formatScore(h.totalScore, h.maxScore) : h.pendingManualGrading ? <Badge bg="warning" text="dark">{t("result.grading")}</Badge> : "—"}
                      {h.passed != null && h.scoreVisible && (
                        <Badge bg={h.passed ? "success" : "danger"} className="ms-2">
                          {h.passed ? t("result.passed") : t("result.failed")}
                        </Badge>
                      )}
                    </td>
                    <td>
                      {h.status === "IN_PROGRESS" ? (
                        <Link to={`/student/attempts/${h.attemptId}`}>{t("student.continue")}</Link>
                      ) : h.status !== "CANCELLED" ? (
                        <Link to={`/student/results/${h.attemptId}`}>{t("student.viewResult")}</Link>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
          <Pager data={query.data} onPage={setPage} />
        </>
      )}
    </>
  );
}
