import { useState, type CSSProperties } from "react";
import { Alert, Badge, Button, Card, Col, ListGroup, ProgressBar, Row, Table } from "react-bootstrap";
import { Link, useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery } from "@tanstack/react-query";
import { studentApi } from "../../services/api";
import { ConfirmDialog, Empty, ErrorAlert, Loading } from "../../components/common/Feedback";
import { describeError } from "../../utils/errors";
import { availabilityVariant } from "../../constants/ui";
import { MarkdownView } from "../../components/common/MarkdownView";
import { MediaUrls } from "../../components/common/MediaUrls";
import { Pager, SearchBox } from "../../components/common/DataTable";
import { formatDateRange, formatDateTime, formatDuration, formatNumber, formatScore, optionLabel } from "../../utils/format";
import { useAuth } from "../auth/useAuth";
import type { ExamAvailability, StudentExamDetail, StudentExamItem } from "../../types/api";

/** Màu nhấn cố định theo đề (cùng đề luôn cùng màu), xem .accent-* trong theme.scss. */
const EXAM_ACCENTS = ["indigo", "sky", "teal", "violet", "orange", "pink", "green", "amber"];
function examAccent(examId: string): string {
  let hash = 0;
  for (const ch of examId) {
    hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  }
  return EXAM_ACCENTS[hash % EXAM_ACCENTS.length]!;
}

const AVAILABILITY_ICON: Record<ExamAvailability, string> = {
  AVAILABLE: "bi-play-circle-fill",
  IN_PROGRESS: "bi-hourglass-split",
  NOT_STARTED: "bi-calendar-event",
  NO_ATTEMPTS_LEFT: "bi-check2-circle",
  ENDED: "bi-flag-fill",
  CLOSED: "bi-lock-fill",
};

export function StudentExamsPage() {
  const { t } = useTranslation();
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState("");
  const query = useQuery({
    queryKey: ["student-exams", page, keyword],
    queryFn: () => studentApi.exams({ page, pageSize: 20, keyword: keyword || undefined }),
  });
  const { user } = useAuth();
  const items = query.data?.items ?? [];
  const ready = items.filter((e) => e.availability === "AVAILABLE" || e.availability === "IN_PROGRESS").length;

  return (
    <>
      <section className="page-hero mb-4">
        <div className="position-relative" style={{ zIndex: 1 }}>
          <div className="opacity-75 small">{t("student.greeting")}</div>
          <h1 className="h3 fw-bold mb-1">{user?.fullName}</h1>
          <p className="mb-0 opacity-75">{query.data ? t("student.readyCount", { count: ready }) : t("student.exams")}</p>
        </div>
      </section>
      <div className="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-3">
        <h2 className="h5 mb-0">
          <i className="bi bi-journal-text text-primary me-2" aria-hidden="true" />
          {t("student.exams")}
        </h2>
        <div style={{ maxWidth: 360, width: "100%" }}>
          <SearchBox
            value={keyword}
            onSearch={(kw) => {
              setKeyword(kw);
              setPage(1);
            }}
            placeholder="Tìm theo mã hoặc tên đề thi..."
          />
        </div>
      </div>
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
  const accent = examAccent(exam.examId);
  const actionable = exam.availability === "AVAILABLE" || exam.availability === "IN_PROGRESS";
  return (
    <Card className={`h-100 exam-card accent-${accent}`}>
      <div className="exam-card-band" />
      <Card.Body className="d-flex flex-column">
        <div className="d-flex align-items-start gap-3 mb-3">
          <span className="stat-icon" aria-hidden="true">
            <i className={`bi ${AVAILABILITY_ICON[exam.availability]}`} />
          </span>
          <div className="min-w-0 flex-grow-1">
            <Card.Title as="h3" className="h6 mb-1">{exam.name}</Card.Title>
            <Badge bg={availabilityVariant[exam.availability]}>{t(`enums.availability.${exam.availability}`)}</Badge>
          </div>
        </div>
        <ul className="list-unstyled small text-secondary mb-3 d-grid gap-1">
          <li>
            <i className="bi bi-stopwatch me-2" aria-hidden="true" />
            {t("student.duration", { minutes: exam.durationMinutes })} · {t("student.questions", { count: exam.questionCount })}
          </li>
          {(exam.startAt || exam.endAt) && (
            <li>
              <i className="bi bi-calendar3 me-2" aria-hidden="true" />
              {formatDateTime(exam.startAt)} – {formatDateTime(exam.endAt)}
            </li>
          )}
          {exam.officialScore != null && (
            <li className="text-body fw-semibold">
              <i className="bi bi-star-fill text-warning me-2" aria-hidden="true" />
              {t("student.officialScore", { score: formatNumber(exam.officialScore) })}
            </li>
          )}
        </ul>
        <div className="small text-secondary mb-1">{t("student.attempts", { used: exam.usedAttempts, max: exam.maxAttempts })}</div>
        <ProgressBar
          now={exam.maxAttempts > 0 ? (exam.usedAttempts / exam.maxAttempts) * 100 : 0}
          className="mb-3"
          aria-label={t("student.attempts", { used: exam.usedAttempts, max: exam.maxAttempts })}
        />
        <Link to={`/student/exams/${exam.examId}`} className={`btn btn-sm mt-auto ${actionable ? "btn-primary" : "btn-outline-primary"}`}>
          {exam.availability === "IN_PROGRESS" ? t("student.continue") : actionable ? t("student.start") : t("common.detail")}
          <i className="bi bi-arrow-right ms-1" aria-hidden="true" />
        </Link>
      </Card.Body>
    </Card>
  );
}

// ======================= Lớp của tôi (D-28) =======================

export function MyClassesPage() {
  const { t } = useTranslation();
  const query = useQuery({ queryKey: ["student-classes"], queryFn: studentApi.classes });

  return (
    <>
      <h1 className="h4 mb-3">
        <i className="bi bi-easel2-fill text-primary me-2" aria-hidden="true" />
        {t("nav.myClasses")}
      </h1>
      {query.error && <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />}
      {query.isLoading && <Loading />}
      {query.data && query.data.length === 0 && <Empty text={t("student.noClasses")} />}
      <Row xs={1} md={2} lg={3} className="g-3">
        {query.data?.map((c) => (
          <Col key={c.id}>
            <Card className={`h-100 exam-card accent-${examAccent(c.id)}`}>
              <div className="exam-card-band" />
              <Card.Body className="d-flex flex-column">
                <div className="d-flex align-items-start gap-3 mb-3">
                  <span className="stat-icon" aria-hidden="true">
                    <i className="bi bi-easel2-fill" />
                  </span>
                  <div className="min-w-0 flex-grow-1">
                    <Card.Title as="h2" className="h6 mb-1">{c.name}</Card.Title>
                    <Badge bg="light" text="dark" className="border">{c.code}</Badge>
                  </div>
                </div>
                <ul className="list-unstyled small text-secondary mb-3 d-grid gap-1">
                  {c.schoolYear && (
                    <li>
                      <i className="bi bi-calendar2-range me-2" aria-hidden="true" />
                      {t("student.schoolYear", { year: c.schoolYear })}
                    </li>
                  )}
                  {(c.startDate || c.endDate) && (
                    <li>
                      <i className="bi bi-calendar-week me-2" aria-hidden="true" />
                      {formatDateRange(c.startDate, c.endDate)}
                    </li>
                  )}
                  <li>
                    <i className="bi bi-people me-2" aria-hidden="true" />
                    {t("student.classStats", { students: c.studentCount, exams: c.examCount })}
                  </li>
                </ul>
                {c.description && <p className="small mb-3">{c.description}</p>}
                <Link to={`/student/classes/${c.id}`} className="btn btn-sm btn-outline-primary mt-auto">
                  {t("student.classExams")}
                  <i className="bi bi-arrow-right ms-1" aria-hidden="true" />
                </Link>
              </Card.Body>
            </Card>
          </Col>
        ))}
      </Row>
    </>
  );
}

export function MyClassDetailPage() {
  const { t } = useTranslation();
  const { classId = "" } = useParams();
  const [page, setPage] = useState(1);
  const classes = useQuery({ queryKey: ["student-classes"], queryFn: studentApi.classes });
  const exams = useQuery({
    queryKey: ["student-exams", "class", classId, page],
    queryFn: () => studentApi.exams({ page, pageSize: 20, classroomId: classId }),
  });
  const classroom = classes.data?.find((c) => c.id === classId);

  return (
    <>
      <Link to="/student/classes" className="small">← {t("nav.myClasses")}</Link>
      <h1 className="h4 mt-2 mb-1">{classroom ? `${classroom.name} (${classroom.code})` : t("nav.myClasses")}</h1>
      {classroom?.schoolYear && <p className="text-secondary small">{t("student.schoolYear", { year: classroom.schoolYear })}</p>}
      {classes.data && !classroom && <Alert variant="warning">{t("student.classNotFound")}</Alert>}
      {exams.error && <ErrorAlert error={exams.error} onRetry={() => void exams.refetch()} />}
      {exams.isLoading && <Loading />}
      {exams.data && exams.data.items.length === 0 && <Empty text={t("student.noClassExams")} />}
      <Row xs={1} md={2} lg={3} className="g-3 mt-1">
        {exams.data?.items.map((exam) => (
          <Col key={exam.examId}>
            <ExamCard exam={exam} />
          </Col>
        ))}
      </Row>
      {exams.data && <div className="mt-3"><Pager data={exams.data} onPage={setPage} /></div>}
    </>
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
            <Row className="align-items-center text-center g-3">
              <Col xs={12} md={3}>
                {/* Vòng tròn phần trăm: xanh khi đạt, đỏ khi chưa đạt, tím khi đề không xét đạt */}
                <div
                  className={`score-ring accent-${r.passed == null ? "blue" : r.passed ? "green" : "pink"}`}
                  style={{ "--value": Math.min(100, Math.max(0, r.percentage ?? 0)) } as CSSProperties}
                  role="img"
                  aria-label={`${t("result.percentage")} ${formatNumber(r.percentage)}%`}
                >
                  <span>{formatNumber(r.percentage)}%</span>
                </div>
              </Col>
              <Col xs={4} md={3}>
                <div className="fs-2 fw-bold text-primary">{formatScore(r.totalScore, r.maxScore)}</div>
                <div className="small text-secondary">{t("result.score")}</div>
              </Col>
              <Col xs={4} md={3}>
                <div className="fs-2 fw-bold text-info-emphasis">
                  {r.correctCount}/{r.totalQuestion}
                </div>
                <div className="small text-secondary">{t("result.correct")}</div>
              </Col>
              {r.passed != null && (
                <Col xs={4} md={3}>
                  <div className={`fs-2 fw-bold ${r.passed ? "text-success" : "text-danger"}`}>
                    <i className={`bi ${r.passed ? "bi-trophy-fill" : "bi-emoji-frown"} me-2`} aria-hidden="true" />
                    {r.passed ? t("result.passed") : t("result.failed")}
                  </div>
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
          {r.scoreVisible && !r.reviewAvailable && !r.reviewAvailableAt && (
            <Alert variant="warning" className="mt-3 mb-0">
              <i className="bi bi-info-circle me-2" />
              Đề thi này chưa bật quyền xem lại câu hỏi và đáp án sau khi nộp. Giáo viên có thể bật trong phần cài đặt đề thi.
            </Alert>
          )}
        </Card.Body>
      </Card>

      {r.reviewAvailable && r.questions && (
        <MediaUrls value={r.media}>
          <div className="d-flex align-items-center justify-content-between mb-3 mt-4">
            <h2 className="h5 mb-0 d-flex align-items-center gap-2">
              <i className="bi bi-card-checklist text-primary" />
              <span>{t("result.review")} - Chi tiết câu hỏi và đáp án</span>
            </h2>
            <Badge bg="secondary" className="px-2 py-1">
              {r.questions.length} câu hỏi
            </Badge>
          </div>

          {r.questions.map((q) => (
            <Card
              key={q.id}
              className={`mb-3 shadow-sm border-2 border-${q.isVoided ? "secondary" : q.isCorrect ? "success" : "danger"}`}
            >
              <Card.Body>
                <div className="d-flex justify-content-between align-items-center mb-2 pb-2 border-bottom">
                  <div className="d-flex align-items-center gap-2">
                    <strong className="fs-6">Câu {q.order}</strong>
                    {q.isVoided ? (
                      <Badge bg="secondary">{t("result.voided")}</Badge>
                    ) : q.isCorrect ? (
                      <Badge bg="success" className="px-2 py-1 d-inline-flex align-items-center gap-1">
                        <i className="bi bi-check-circle-fill" /> Đúng (+{formatNumber(q.score)}/{formatNumber(q.maxScore)} đ)
                      </Badge>
                    ) : (
                      <Badge bg="danger" className="px-2 py-1 d-inline-flex align-items-center gap-1">
                        <i className="bi bi-x-circle-fill" /> Sai ({formatNumber(q.score)}/{formatNumber(q.maxScore)} đ)
                      </Badge>
                    )}
                  </div>
                  <div className={`fw-bold fs-5 ${q.isCorrect ? "text-success" : "text-danger"}`}>
                    {q.isCorrect ? "✓ V" : "✗ X"}
                  </div>
                </div>

                {q.isVoided && <Alert variant="secondary" className="py-1 small my-2">{t("result.voided")}</Alert>}

                <div className="mb-3 fs-6">
                  <MarkdownView content={q.content} format={q.contentFormat} />
                </div>

                {q.type === "ESSAY" ? (
                  <div className="mt-2">
                    <div className="small fw-semibold mb-1">{t("result.yourAnswer")}:</div>
                    <div className="border rounded p-2 small bg-light" style={{ whiteSpace: "pre-wrap" }}>
                      {q.answerText || <em>{t("result.noAnswer")}</em>}
                    </div>
                    {q.manualComment && (
                      <div className="small mt-2 p-2 rounded bg-info-subtle border border-info">
                        <strong>{t("result.graderComment")}:</strong> <span style={{ whiteSpace: "pre-wrap" }}>{q.manualComment}</span>
                      </div>
                    )}
                  </div>
                ) : q.type === "FILL_IN" ? (
                  <div className="d-flex flex-column gap-2 mb-2">
                    <div
                      className={`p-2 px-3 rounded d-flex align-items-center justify-content-between ${
                        q.isCorrect
                          ? "border border-2 border-success bg-success-subtle text-success-emphasis fw-medium"
                          : "border border-2 border-danger bg-danger-subtle text-danger-emphasis fw-medium"
                      }`}
                    >
                      <div className="d-flex align-items-center flex-grow-1 me-2">
                        <span
                          className={`badge ${q.isCorrect ? "bg-success" : "bg-danger"} text-white fw-bold d-inline-flex align-items-center justify-content-center me-2`}
                          style={{ width: "36px", height: "32px", fontSize: "0.95rem" }}
                        >
                          {q.isCorrect ? "✓ V" : "✗ X"}
                        </span>
                        <div>
                          <span className="text-secondary small d-block">{t("result.yourAnswer")}:</span>
                          <span>{q.answerText || <em>{t("result.noAnswer")}</em>}</span>
                        </div>
                      </div>
                      <Badge bg={q.isCorrect ? "success" : "danger"} className="py-1 px-2">
                        {q.isCorrect ? "✓ Bạn trả lời đúng" : "✗ Bạn trả lời sai"}
                      </Badge>
                    </div>

                    {!q.isCorrect && (
                      <div className="p-2 px-3 rounded d-flex align-items-center justify-content-between border border-2 border-success bg-success-subtle text-success-emphasis fw-medium">
                        <div className="d-flex align-items-center flex-grow-1 me-2">
                          <span
                            className="badge bg-success text-white fw-bold d-inline-flex align-items-center justify-content-center me-2"
                            style={{ width: "36px", height: "32px", fontSize: "0.95rem" }}
                          >
                            ✓ V
                          </span>
                          <div>
                            <span className="text-secondary small d-block">{t("result.correctAnswer")}:</span>
                            <span className="text-success fw-bold">
                              {q.answerDataType === "NUMBER" ? formatNumber(q.correctAnswerNumber) : q.acceptedAnswers.join(" / ")}
                            </span>
                          </div>
                        </div>
                        <Badge bg="success" className="py-1 px-2">
                          ✓ Đáp án đúng
                        </Badge>
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="d-flex flex-column gap-2 mb-2">
                    {q.options.map((o, index) => {
                      const selected = q.selectedOptions.includes(o.code);
                      const correct = q.correctOptions.includes(o.code);

                      let containerClass = "border border-light-subtle bg-body-tertiary text-body-secondary";
                      let iconBadge = null;
                      let statusBadge = null;

                      if (selected && correct) {
                        // Tích đúng: đánh dấu V màu xanh
                        containerClass = "border border-2 border-success bg-success-subtle text-success-emphasis fw-medium";
                        iconBadge = (
                          <span
                            className="badge bg-success text-white fw-bold d-inline-flex align-items-center justify-content-center me-2"
                            style={{ width: "36px", height: "32px", fontSize: "0.95rem" }}
                            title="Bạn chọn đúng"
                          >
                            ✓ V
                          </span>
                        );
                        statusBadge = (
                          <Badge bg="success" className="ms-auto py-1 px-2 d-inline-flex align-items-center gap-1">
                            <i className="bi bi-check2-circle" /> Bạn chọn đúng
                          </Badge>
                        );
                      } else if (selected && !correct) {
                        // Tích sai: đánh dấu X hiển đỏ
                        containerClass = "border border-2 border-danger bg-danger-subtle text-danger-emphasis fw-medium";
                        iconBadge = (
                          <span
                            className="badge bg-danger text-white fw-bold d-inline-flex align-items-center justify-content-center me-2"
                            style={{ width: "36px", height: "32px", fontSize: "0.95rem" }}
                            title="Bạn chọn sai"
                          >
                            ✗ X
                          </span>
                        );
                        statusBadge = (
                          <Badge bg="danger" className="ms-auto py-1 px-2 d-inline-flex align-items-center gap-1">
                            <i className="bi bi-x-circle" /> Bạn chọn sai
                          </Badge>
                        );
                      } else if (!selected && correct) {
                        // Tích V vào đáp án đúng
                        containerClass = "border border-2 border-success bg-success-subtle text-success-emphasis fw-medium";
                        iconBadge = (
                          <span
                            className="badge bg-success text-white fw-bold d-inline-flex align-items-center justify-content-center me-2"
                            style={{ width: "36px", height: "32px", fontSize: "0.95rem" }}
                            title="Đáp án đúng"
                          >
                            ✓ V
                          </span>
                        );
                        statusBadge = (
                          <Badge bg="success" className="ms-auto py-1 px-2 d-inline-flex align-items-center gap-1">
                            <i className="bi bi-check-lg" /> Đáp án đúng
                          </Badge>
                        );
                      } else {
                        // Phương án không chọn và không phải đáp án đúng
                        iconBadge = (
                          <span
                            className="badge bg-white text-muted border me-2 d-inline-flex align-items-center justify-content-center"
                            style={{ width: "36px", height: "32px", fontSize: "0.85rem" }}
                          >
                            {q.type === "TRUE_FALSE" ? "–" : optionLabel(index)}
                          </span>
                        );
                      }

                      return (
                        <div
                          key={o.code}
                          className={`p-2 px-3 rounded d-flex align-items-center justify-content-between ${containerClass}`}
                        >
                          <div className="d-flex align-items-center flex-grow-1 me-2">
                            {iconBadge}
                            {q.type !== "TRUE_FALSE" && (selected || correct) && (
                              <strong className="me-2">{optionLabel(index)}.</strong>
                            )}
                            <div className="flex-grow-1">
                              <MarkdownView content={o.content} inline />
                            </div>
                          </div>
                          {statusBadge}
                        </div>
                      );
                    })}
                  </div>
                )}

                {q.explanation && (
                  <div className="mt-2 p-2 px-3 rounded bg-info-subtle border border-info text-info-emphasis small">
                    <strong>
                      <i className="bi bi-lightbulb me-1" />
                      {t("result.explanation")}:
                    </strong>{" "}
                    <MarkdownView content={q.explanation} />
                  </div>
                )}
              </Card.Body>
            </Card>
          ))}
        </MediaUrls>
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
