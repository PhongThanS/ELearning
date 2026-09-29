import { useCallback, useEffect, useReducer, useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, Modal, Offcanvas, Row, Spinner } from "react-bootstrap";
import { useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { studentApi } from "../../services/api";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import { describeError } from "../../utils/errors";
import { MarkdownView } from "../../components/common/MarkdownView";
import { formatDuration, formatNumber, optionLabel } from "../../utils/format";
import type { Attempt, AttemptQuestion } from "../../types/api";
import {
  clearBackup,
  initPlayerState,
  isAnswered,
  isUnsaved,
  isValidNumberAnswer,
  loadBackup,
  playerReducer,
  type AnswerDraft,
  type PlayerState,
} from "./playerState";
import { useAttemptEvents, useAutosave, useCountdown, type SaveStatus } from "./playerHooks";

/** Trang làm bài (docs/06-frontend.md mục 4). */
export function ExamPlayerPage() {
  const { attemptId = "" } = useParams();
  const navigate = useNavigate();
  const query = useQuery({
    queryKey: ["attempt", attemptId],
    queryFn: () => studentApi.attempt(attemptId),
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });

  useEffect(() => {
    if (query.data && query.data.status !== "IN_PROGRESS") {
      navigate(`/student/results/${attemptId}`, { replace: true });
    }
  }, [query.data, attemptId, navigate]);

  if (query.error) {
    return <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />;
  }
  if (!query.data || query.data.status !== "IN_PROGRESS") {
    return <Loading />;
  }
  return <Player attempt={query.data} />;
}

function Player({ attempt }: { attempt: Attempt }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [state, dispatch] = useReducer(playerReducer, attempt, (a) => initPlayerState(a, Date.now(), loadBackup(a.attemptId)));
  const [showConfirm, setShowConfirm] = useState(false);
  const [showNavigator, setShowNavigator] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [multiTab, setMultiTab] = useState(false);
  const [timeUp, setTimeUp] = useState(false);

  const questions = [...attempt.questions].sort((a, b) => a.order - b.order);
  const current = questions[state.currentIndex]!;

  const finish = useCallback(() => {
    clearBackup(attempt.attemptId);
    navigate(`/student/results/${attempt.attemptId}`, { replace: true });
  }, [attempt.attemptId, navigate]);

  const autosave = useAutosave(state, dispatch, finish);
  const events = useAttemptEvents(attempt.attemptId, () => setMultiTab(true));

  const submit = useCallback(async () => {
    setSubmitting(true);
    setSubmitError(null);
    await autosave.flush();
    // Nộp bài là idempotent: lỗi mạng thì thử lại tối đa 3 lần
    for (let i = 0; i < 3; i++) {
      try {
        await studentApi.submit(attempt.attemptId);
        autosave.stop();
        finish();
        return;
      } catch (error) {
        if (i === 2) {
          setSubmitError(describeError(error));
        }
      }
    }
    setSubmitting(false);
  }, [attempt.attemptId, autosave, finish]);

  const remaining = useCountdown(state, () => {
    setTimeUp(true);
    void submit();
  });

  // Cảnh báo khi rời trang còn câu chưa lưu
  const unsavedCount = Object.values(state.answers).filter(isUnsaved).length;
  useEffect(() => {
    if (unsavedCount === 0) {
      return;
    }
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [unsavedCount]);

  const answeredCount = questions.filter((q) => isAnswered(state.answers[q.id])).length;
  const markedCount = questions.filter((q) => state.answers[q.id]?.isMarkedForReview).length;

  return (
    <>
      <header className="player-header sticky-top bg-white border-bottom shadow-sm">
        <div className="container-fluid d-flex align-items-center gap-3 py-2">
          <h1 className="h6 mb-0 flex-grow-1 text-truncate">{attempt.examName}</h1>
          <SaveIndicator status={autosave.status} online={state.online} />
          <Timer remainingMs={remaining} />
          <Button size="sm" variant="outline-secondary" className="d-lg-none" onClick={() => setShowNavigator(true)}>
            {t("player.navigator")}
          </Button>
        </div>
      </header>

      <div className="container-fluid py-3">
        {multiTab && <Alert variant="warning">{t("player.multiTab")}</Alert>}
        {!state.online && <Alert variant="warning">{t("player.offline")}</Alert>}
        <Row className="g-3">
          <Col lg={9}>
            <QuestionCard
              question={current}
              total={questions.length}
              draft={state.answers[current.id]!}
              onSelect={(options) => dispatch({ type: "select", questionId: current.id, options })}
              onText={(text) => dispatch({ type: "text", questionId: current.id, text })}
              onMark={(marked) => dispatch({ type: "mark", questionId: current.id, marked })}
              onPaste={events.recordPaste}
            />
            <div className="d-flex justify-content-between mt-3">
              <Button variant="outline-primary" disabled={state.currentIndex === 0} onClick={() => dispatch({ type: "goto", index: state.currentIndex - 1 })}>
                {t("player.previous")}
              </Button>
              <Button
                variant="outline-primary"
                disabled={state.currentIndex >= questions.length - 1}
                onClick={() => dispatch({ type: "goto", index: state.currentIndex + 1 })}
              >
                {t("player.next")}
              </Button>
            </div>
          </Col>
          <Col lg={3} className="d-none d-lg-block">
            <Card>
              <Card.Body>
                <Navigator questions={questions} state={state} onGoto={(index) => dispatch({ type: "goto", index })} />
              </Card.Body>
            </Card>
          </Col>
        </Row>
        <div className="text-center mt-4">
          <p className="small text-secondary">{t("player.answeredCount", { answered: answeredCount, total: questions.length })}</p>
          <Button size="lg" variant="success" onClick={() => setShowConfirm(true)} disabled={submitting}>
            {t("player.submit")}
          </Button>
        </div>
      </div>

      <Offcanvas show={showNavigator} onHide={() => setShowNavigator(false)} placement="end">
        <Offcanvas.Header closeButton>
          <Offcanvas.Title>{t("player.navigator")}</Offcanvas.Title>
        </Offcanvas.Header>
        <Offcanvas.Body>
          <Navigator
            questions={questions}
            state={state}
            onGoto={(index) => {
              dispatch({ type: "goto", index });
              setShowNavigator(false);
            }}
          />
        </Offcanvas.Body>
      </Offcanvas>

      <Modal show={showConfirm} onHide={() => !submitting && setShowConfirm(false)} centered>
        <Modal.Header closeButton={!submitting}>
          <Modal.Title as="h2" className="h5">
            {t("player.confirmTitle")}
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {submitError && <Alert variant="danger">{submitError}</Alert>}
          <ul>
            {questions.length - answeredCount > 0 && <li>{t("player.confirmUnanswered", { count: questions.length - answeredCount })}</li>}
            {markedCount > 0 && <li>{t("player.confirmMarked", { count: markedCount })}</li>}
            {unsavedCount > 0 && <li>{t("player.confirmUnsaved", { count: unsavedCount })}</li>}
          </ul>
          <p className="mb-0">{t("player.confirmText")}</p>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => setShowConfirm(false)} disabled={submitting}>
            {t("common.cancel")}
          </Button>
          <Button variant="success" onClick={() => void submit()} disabled={submitting}>
            {submitting && <Spinner size="sm" animation="border" className="me-2" />}
            {t("player.submit")}
          </Button>
        </Modal.Footer>
      </Modal>

      <Modal show={timeUp} backdrop="static" keyboard={false} centered>
        <Modal.Body className="text-center py-4" role="alert">
          <Spinner animation="border" className="mb-3" />
          <p className="mb-0">{t("player.timeUp")}</p>
          {submitError && <Alert variant="danger" className="mt-3">{submitError}</Alert>}
        </Modal.Body>
      </Modal>
    </>
  );
}

function Timer({ remainingMs }: { remainingMs: number }) {
  const { t } = useTranslation();
  const seconds = Math.max(0, Math.floor(remainingMs / 1000));
  const warning = seconds <= 60;
  const caution = seconds <= 5 * 60;
  return (
    <div
      className={`fw-bold font-monospace px-2 py-1 rounded ${warning ? "bg-danger text-white" : caution ? "bg-warning" : "bg-light"}`}
      aria-label={t("player.timeLeft")}
    >
      <span className="visually-hidden">{t("player.timeLeft")}: </span>
      {formatDuration(seconds)}
      {warning && <span className="ms-2 small">{t("player.timeWarning")}</span>}
      {(seconds === 300 || seconds === 60) && (
        <span className="visually-hidden" role="status">
          {t("player.timeWarning")}
        </span>
      )}
    </div>
  );
}

function SaveIndicator({ status, online }: { status: SaveStatus; online: boolean }) {
  const { t } = useTranslation();
  const map: Record<SaveStatus, [string, string]> = {
    saved: ["success", t("player.saved")],
    saving: ["secondary", t("player.saving")],
    unsaved: ["warning", t("player.unsaved")],
  };
  // Mất mạng: câu đã lưu vẫn là đã lưu; câu đang chờ hiển thị "chưa lưu" (không phải "đang lưu").
  const [bg, text] = map[!online && status === "saving" ? "unsaved" : status];
  return (
    <Badge bg={bg} text={bg === "warning" ? "dark" : undefined} role="status">
      {text}
    </Badge>
  );
}

function Navigator({ questions, state, onGoto }: { questions: AttemptQuestion[]; state: PlayerState; onGoto: (index: number) => void }) {
  const { t } = useTranslation();
  return (
    <>
      <div className="d-flex flex-wrap gap-2" role="list">
        {questions.map((q, index) => {
          const draft = state.answers[q.id];
          const answered = isAnswered(draft);
          const marked = !!draft?.isMarkedForReview;
          const label = `Câu ${q.order}, ${answered ? t("player.legendAnswered") : t("player.legendUnanswered")}${marked ? `, ${t("player.legendMarked")}` : ""}`;
          return (
            <Button
              key={q.id}
              role="listitem"
              size="sm"
              variant={index === state.currentIndex ? "primary" : answered ? "outline-success" : "outline-secondary"}
              className="nav-question"
              aria-label={label}
              aria-current={index === state.currentIndex ? "step" : undefined}
              onClick={() => onGoto(index)}
            >
              {q.order}
              {answered && <span aria-hidden> ✓</span>}
              {marked && <span aria-hidden> ⚑</span>}
            </Button>
          );
        })}
      </div>
      <ul className="list-unstyled small text-secondary mt-3 mb-0">
        <li>✓ {t("player.legendAnswered")}</li>
        <li>⚑ {t("player.legendMarked")}</li>
        <li>□ {t("player.legendUnanswered")}</li>
      </ul>
    </>
  );
}

export function QuestionCard({
  question,
  total,
  draft,
  onSelect,
  onText,
  onMark,
  onPaste,
  readOnly = false,
}: {
  question: Pick<AttemptQuestion, "id" | "order" | "content" | "contentFormat" | "type" | "answerDataType" | "score" | "options">;
  total: number;
  draft: Pick<AnswerDraft, "selectedOptions" | "answerText" | "isMarkedForReview">;
  onSelect: (options: string[]) => void;
  onText: (text: string) => void;
  onMark: (marked: boolean) => void;
  onPaste?: () => void;
  readOnly?: boolean;
}) {
  const { t } = useTranslation();
  const name = `q-${question.id}`;
  const numberInvalid = question.answerDataType === "NUMBER" && !isValidNumberAnswer(draft.answerText);

  return (
    <Card>
      <Card.Body>
        <div className="d-flex justify-content-between align-items-center mb-2">
          <span className="fw-semibold">
            {t("player.question", { order: question.order, total })}{" "}
            <span className="text-secondary small">({t("player.points", { score: formatNumber(question.score) })})</span>
          </span>
          {!readOnly && (
            <Form.Check
              type="switch"
              id={`${name}-mark`}
              label={draft.isMarkedForReview ? t("player.marked") : t("player.mark")}
              checked={draft.isMarkedForReview}
              onChange={(e) => onMark(e.target.checked)}
            />
          )}
        </div>
        <MarkdownView content={question.content} format={question.contentFormat} />

        {question.type === "FILL_IN" ? (
          <Form.Group controlId={`${name}-text`} className="mt-3">
            <Form.Label className="visually-hidden">{t("player.fillPlaceholder")}</Form.Label>
            <Form.Control
              value={draft.answerText ?? ""}
              placeholder={t("player.fillPlaceholder")}
              inputMode={question.answerDataType === "NUMBER" ? "decimal" : "text"}
              maxLength={1000}
              readOnly={readOnly}
              isInvalid={numberInvalid}
              aria-describedby={question.answerDataType === "NUMBER" ? `${name}-hint` : undefined}
              onChange={(e) => onText(e.target.value)}
              onPaste={onPaste}
            />
            {question.answerDataType === "NUMBER" && (
              <Form.Text id={`${name}-hint`}>{t("player.numberHint")}</Form.Text>
            )}
            <Form.Control.Feedback type="invalid">{t("player.numberInvalid")}</Form.Control.Feedback>
          </Form.Group>
        ) : (
          <fieldset className="mt-3">
            <legend className="visually-hidden">{t("player.question", { order: question.order, total })}</legend>
            {question.options.map((option, index) => {
              const multiple = question.type === "MULTIPLE_CHOICE";
              const checked = draft.selectedOptions.includes(option.code);
              return (
                <Form.Check
                  key={option.code}
                  id={`${name}-${option.code}`}
                  type={multiple ? "checkbox" : "radio"}
                  name={name}
                  className="option-item border rounded px-3 py-2 mb-2"
                  disabled={readOnly}
                  checked={checked}
                  onChange={() =>
                    onSelect(
                      multiple
                        ? checked
                          ? draft.selectedOptions.filter((c) => c !== option.code)
                          : [...draft.selectedOptions, option.code]
                        : [option.code],
                    )
                  }
                  label={
                    <span className="d-flex gap-2">
                      {question.type !== "TRUE_FALSE" && <strong>{optionLabel(index)}.</strong>}
                      <MarkdownView content={option.content} inline />
                    </span>
                  }
                />
              );
            })}
            {!readOnly && draft.selectedOptions.length > 0 && question.type !== "MULTIPLE_CHOICE" && (
              <Button variant="link" size="sm" className="px-0" onClick={() => onSelect([])}>
                Bỏ chọn
              </Button>
            )}
          </fieldset>
        )}
      </Card.Body>
    </Card>
  );
}
