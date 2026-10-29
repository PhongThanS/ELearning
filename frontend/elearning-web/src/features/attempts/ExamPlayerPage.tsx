import { useCallback, useEffect, useReducer, useRef, useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, Modal, Offcanvas, Row, Spinner } from "react-bootstrap";
import { useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { studentApi } from "../../services/api";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import { describeError } from "../../utils/errors";
import { MarkdownView } from "../../components/common/MarkdownView";
import { MediaUrls } from "../../components/common/MediaUrls";
import { formatDuration, formatNumber, optionLabel } from "../../utils/format";
import type { Attempt, AttemptQuestion } from "../../types/api";
import {
  clearBackup,
  initPlayerState,
  isAnswered,
  isUnsaved,
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
  return (
    <MediaUrls value={query.data.media}>
      <Player attempt={query.data} />
    </MediaUrls>
  );
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

  const isUntimed =
    (attempt.durationMinutes ?? 0) === 0 ||
    new Date(attempt.expiredAt).getTime() - new Date(attempt.startedAt).getTime() > 30 * 24 * 3600 * 1000;

  const remaining = useCountdown(state, () => {
    if (isUntimed) {
      return;
    }
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
          <Timer remainingMs={remaining} isUntimed={isUntimed} />
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

function Timer({ remainingMs, isUntimed }: { remainingMs: number; isUntimed?: boolean }) {
  const { t } = useTranslation();
  if (isUntimed) {
    return (
      <div className="fw-semibold px-2 py-1 rounded bg-info bg-opacity-10 text-info-emphasis border border-info-subtle d-flex align-items-center gap-1 small">
        <i className="bi bi-infinity fs-6 text-info" />
        <span>Luyện tập tự do (Không giới hạn thời gian)</span>
      </div>
    );
  }
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

/**
 * Câu Đúng/Sai kiểu cũ (hai lựa chọn TRUE / FALSE): chọn một trong hai như câu chọn một.
 * Câu nhiều mệnh đề (A, B, C, D…) thì mỗi mệnh đề chọn Đúng hoặc Sai (docs/02-nghiep-vu.md mục 2).
 */
export function isClassicTrueFalse(options: readonly { code: string }[]): boolean {
  const codes = options.map((o) => o.code.toUpperCase());
  return codes.length === 2 && codes.includes("TRUE") && codes.includes("FALSE");
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
  question: Pick<AttemptQuestion, "id" | "order" | "content" | "contentFormat" | "type" | "answerDataType" | "score" | "options" | "expectedLength">;
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

        {question.type === "TRUE_FALSE" && !isClassicTrueFalse(question.options) ? (
          question.options.length === 1 ? (
            <SingleTrueFalseAnswerBox
              option={question.options[0]!}
              selectedOptions={draft.selectedOptions}
              answerText={draft.answerText}
              readOnly={readOnly}
              onSelect={onSelect}
              onText={onText}
            />
          ) : (
            <TrueFalsePropositionTable
              options={question.options}
              selectedOptions={draft.selectedOptions}
              answerText={draft.answerText}
              readOnly={readOnly}
              onSelect={onSelect}
              onText={onText}
            />
          )
        ) : question.type === "FILL_IN" ? (
          <SquareGridAnswerBox
            value={draft.answerText ?? ""}
            expectedLength={question.expectedLength}
            readOnly={readOnly}
            onChange={onText}
            onPaste={onPaste}
          />
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
                    question.type === "TRUE_FALSE" ? (
                      <strong>{option.code === "TRUE" ? "Đúng" : "Sai"}</strong>
                    ) : (
                      <span className="d-flex gap-2">
                        <strong>{optionLabel(index)}.</strong>
                        <MarkdownView content={option.content} inline />
                      </span>
                    )
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

/**
 * Câu hỏi Đúng / Sai đơn (1 mệnh đề / khẳng định duy nhất)
 */
function SingleTrueFalseAnswerBox({
  option,
  selectedOptions,
  answerText,
  readOnly,
  onSelect,
  onText,
}: {
  option: { code: string; content: string };
  selectedOptions: string[];
  answerText: string | null;
  readOnly?: boolean;
  onSelect: (options: string[]) => void;
  onText: (text: string) => void;
}) {
  let isTrue: boolean | null = null;
  if (answerText && answerText.trim().startsWith("{")) {
    try {
      const parsed = JSON.parse(answerText);
      if (parsed[option.code] === true) isTrue = true;
      else if (parsed[option.code] === false) isTrue = false;
    } catch {
      // ignore
    }
  } else if (selectedOptions.includes(option.code)) {
    isTrue = true;
  } else if (answerText?.trim().toLowerCase() === "false" || answerText?.trim().toLowerCase() === "sai") {
    isTrue = false;
  } else if (answerText?.trim().toLowerCase() === "true" || answerText?.trim().toLowerCase() === "đúng") {
    isTrue = true;
  }

  const handleChoose = (val: boolean) => {
    if (readOnly) return;
    onSelect(val ? [option.code] : []);
    onText(JSON.stringify({ [option.code]: val }));
  };

  const handleClear = () => {
    if (readOnly) return;
    onSelect([]);
    onText("");
  };

  return (
    <div className="mt-3 p-3 border rounded bg-light">
      {option.content && (
        <div className="mb-3 p-3 bg-white border rounded shadow-sm">
          <div className="small text-secondary fw-bold mb-1">Mệnh đề / Nhận định:</div>
          <div className="fs-6">
            <MarkdownView content={option.content} />
          </div>
        </div>
      )}
      <div className="small fw-semibold text-secondary mb-2">Chọn đáp án của bạn:</div>
      <div className="d-flex gap-3">
        <button
          type="button"
          disabled={readOnly}
          onClick={() => handleChoose(true)}
          className={`btn flex-fill py-3 fw-bold d-flex align-items-center justify-content-center gap-2 border-2 ${
            isTrue === true
              ? "btn-success text-white shadow"
              : "btn-outline-success bg-white text-success"
          }`}
          style={{ fontSize: "1.1rem", borderRadius: "10px", transition: "all 0.2s" }}
        >
          <i className="bi bi-check-circle-fill fs-4" />
          <span>ĐÚNG</span>
        </button>

        <button
          type="button"
          disabled={readOnly}
          onClick={() => handleChoose(false)}
          className={`btn flex-fill py-3 fw-bold d-flex align-items-center justify-content-center gap-2 border-2 ${
            isTrue === false
              ? "btn-danger text-white shadow"
              : "btn-outline-danger bg-white text-danger"
          }`}
          style={{ fontSize: "1.1rem", borderRadius: "10px", transition: "all 0.2s" }}
        >
          <i className="bi bi-x-circle-fill fs-4" />
          <span>SAI</span>
        </button>
      </div>

      {!readOnly && isTrue !== null && (
        <div className="text-end mt-2">
          <Button variant="link" size="sm" className="text-secondary px-0 text-decoration-none" onClick={handleClear}>
            <i className="bi bi-eraser me-1" />
            Bỏ chọn
          </Button>
        </div>
      )}
    </div>
  );
}

/**
 * Bảng hiển thị câu hỏi trắc nghiệm Đúng / Sai đa mệnh đề (a, b, c, d...)
 * Thiết kế theo đúng định dạng mẫu của Bộ Giáo dục & Đào tạo.
 */
function TrueFalsePropositionTable({
  options,
  selectedOptions,
  answerText,
  readOnly,
  onSelect,
  onText,
}: {
  options: { code: string; content: string }[];
  selectedOptions: string[];
  answerText: string | null;
  readOnly?: boolean;
  onSelect: (options: string[]) => void;
  onText: (text: string) => void;
}) {
  let parsedChoices: Record<string, boolean> = {};
  if (answerText && answerText.trim().startsWith("{")) {
    try {
      parsedChoices = JSON.parse(answerText);
    } catch {
      // fallback
    }
  } else if (selectedOptions.length > 0) {
    for (const opt of options) {
      if (selectedOptions.includes(opt.code)) {
        parsedChoices[opt.code] = true;
      }
    }
  }

  const handleToggle = (code: string, isTrue: boolean) => {
    if (readOnly) return;
    const nextChoices = { ...parsedChoices, [code]: isTrue };
    const nextSelected = Object.keys(nextChoices).filter((k) => nextChoices[k] === true);
    onSelect(nextSelected);
    onText(JSON.stringify(nextChoices));
  };

  return (
    <div className="table-responsive mt-3 border rounded overflow-hidden shadow-sm">
      <table className="table table-hover mb-0 align-middle">
        <thead>
          <tr style={{ backgroundColor: "#064e3b", color: "#ffffff" }}>
            <th className="py-2 px-3 fw-bold text-uppercase fs-6" style={{ width: "70%", letterSpacing: "0.5px" }}>
              Phát biểu
            </th>
            <th className="py-2 text-center fw-bold text-uppercase fs-6" style={{ width: "15%", letterSpacing: "0.5px" }}>
              Đúng
            </th>
            <th className="py-2 text-center fw-bold text-uppercase fs-6" style={{ width: "15%", letterSpacing: "0.5px" }}>
              Sai
            </th>
          </tr>
        </thead>
        <tbody>
          {options.map((option, idx) => {
            const val = parsedChoices[option.code];
            const isTrueSelected = val === true;
            const isFalseSelected = val === false;
            const isEven = idx % 2 === 1;

            return (
              <tr key={option.code} style={{ backgroundColor: isEven ? "#f8fafc" : "#ffffff" }}>
                <td className="py-3 px-3">
                  <div className="d-flex align-items-baseline gap-2">
                    <span
                      className="badge rounded-1 fw-bold text-white px-2 py-1"
                      style={{ backgroundColor: "#064e3b", minWidth: "26px", fontSize: "0.85rem" }}
                    >
                      {option.code}
                    </span>
                    <div className="flex-grow-1 fs-6">
                      <MarkdownView content={option.content} inline />
                    </div>
                  </div>
                </td>
                <td className="text-center py-3">
                  <button
                    type="button"
                    disabled={readOnly}
                    className="btn p-0 rounded-circle border d-inline-flex align-items-center justify-content-center"
                    style={{
                      width: "36px",
                      height: "36px",
                      backgroundColor: isTrueSelected ? "#059669" : "#ffffff",
                      borderColor: isTrueSelected ? "#059669" : "#cbd5e1",
                      borderWidth: "2px",
                      cursor: readOnly ? "default" : "pointer",
                      transition: "all 0.15s ease-in-out",
                    }}
                    onClick={() => handleToggle(option.code, true)}
                    title={`Chọn Đúng cho mệnh đề ${option.code}`}
                    aria-label={`Chọn Đúng cho mệnh đề ${option.code}`}
                  >
                    {isTrueSelected ? (
                      <i className="bi bi-check-lg text-white fs-5 fw-bold" />
                    ) : (
                      <span className="visually-hidden">Chưa chọn</span>
                    )}
                  </button>
                </td>
                <td className="text-center py-3">
                  <button
                    type="button"
                    disabled={readOnly}
                    className="btn p-0 rounded-circle border d-inline-flex align-items-center justify-content-center"
                    style={{
                      width: "36px",
                      height: "36px",
                      backgroundColor: isFalseSelected ? "#059669" : "#ffffff",
                      borderColor: isFalseSelected ? "#059669" : "#cbd5e1",
                      borderWidth: "2px",
                      cursor: readOnly ? "default" : "pointer",
                      transition: "all 0.15s ease-in-out",
                    }}
                    onClick={() => handleToggle(option.code, false)}
                    title={`Chọn Sai cho mệnh đề ${option.code}`}
                    aria-label={`Chọn Sai cho mệnh đề ${option.code}`}
                  >
                    {isFalseSelected ? (
                      <i className="bi bi-check-lg text-white fs-5 fw-bold" />
                    ) : (
                      <span className="visually-hidden">Chưa chọn</span>
                    )}
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/**
 * Ô vuông điền đáp án ngắn cho câu hỏi điền số / đáp án ngắn
 * Mỗi ô chỉ nhận số 0-9, dấu - hoặc dấu ,
 */
function SquareGridAnswerBox({
  value,
  expectedLength,
  readOnly,
  onChange,
  onPaste,
}: {
  value: string;
  expectedLength?: number | null;
  readOnly?: boolean;
  onChange: (val: string) => void;
  onPaste?: () => void;
}) {
  const targetSize = expectedLength && expectedLength > 0 ? expectedLength : 4;
  const currentLength = (value || "").length;
  const GRID_SIZE = Math.max(targetSize, currentLength, 1);
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  const chars = (value || "").split("").slice(0, GRID_SIZE);
  while (chars.length < GRID_SIZE) {
    chars.push("");
  }

  const ALLOWED_CHARS = new Set([
    "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
    "-", ",", "."
  ]);

  const handleCharInput = (index: number, char: string) => {
    if (readOnly) return;
    const cleanChar = char === "." ? "," : char;
    if (!ALLOWED_CHARS.has(cleanChar)) return;

    const newChars = [...chars];
    newChars[index] = cleanChar;
    const result = newChars.join("").trimEnd();
    onChange(result);

    if (index < GRID_SIZE - 1) {
      inputRefs.current[index + 1]?.focus();
      setActiveIndex(index + 1);
    }
  };

  const handleKeyDown = (index: number, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (readOnly) return;
    if (e.key === "Backspace") {
      e.preventDefault();
      const newChars = [...chars];
      if (newChars[index]) {
        newChars[index] = "";
        onChange(newChars.join("").trimEnd());
      } else if (index > 0) {
        newChars[index - 1] = "";
        onChange(newChars.join("").trimEnd());
        inputRefs.current[index - 1]?.focus();
        setActiveIndex(index - 1);
      }
    } else if (e.key === "ArrowLeft" && index > 0) {
      e.preventDefault();
      inputRefs.current[index - 1]?.focus();
      setActiveIndex(index - 1);
    } else if (e.key === "ArrowRight" && index < GRID_SIZE - 1) {
      e.preventDefault();
      inputRefs.current[index + 1]?.focus();
      setActiveIndex(index + 1);
    } else if (e.key === "Delete") {
      e.preventDefault();
      const newChars = [...chars];
      newChars[index] = "";
      onChange(newChars.join("").trimEnd());
    }
  };

  const handleVirtualKey = (key: string) => {
    if (readOnly) return;
    if (key === "BACKSPACE") {
      const lastFilledIndex = chars.reduce((last, c, idx) => (c ? idx : last), -1);
      if (lastFilledIndex >= 0) {
        const newChars = [...chars];
        newChars[lastFilledIndex] = "";
        onChange(newChars.join("").trimEnd());
        inputRefs.current[lastFilledIndex]?.focus();
        setActiveIndex(lastFilledIndex);
      }
    } else if (key === "CLEAR") {
      onChange("");
      inputRefs.current[0]?.focus();
      setActiveIndex(0);
    } else {
      const targetIndex = chars[activeIndex] === "" ? activeIndex : chars.findIndex((c) => c === "");
      const idx = targetIndex === -1 ? activeIndex : targetIndex;
      handleCharInput(idx, key);
    }
  };

  return (
    <div className="mt-3 p-3 border rounded bg-light">
      <div className="small text-secondary fw-semibold mb-2 d-flex align-items-center gap-1">
        <i className="bi bi-grid-3x3-gap-fill text-primary" />
        <span>Điền đáp án vào các ô vuông bên dưới (chỉ nhận số 0-9, dấu âm - và dấu phẩy ,):</span>
      </div>

      <div className="d-flex justify-content-center gap-2 my-3">
        {chars.map((char, index) => {
          const isActive = index === activeIndex;
          return (
            <input
              key={index}
              ref={(el) => { inputRefs.current[index] = el; }}
              type="text"
              readOnly={readOnly}
              className={`form-control text-center fw-bold fs-3 rounded-2 shadow-sm ${
                isActive ? "border-primary border-2 bg-white" : "border-secondary-subtle bg-white"
              }`}
              style={{
                width: "48px",
                height: "58px",
                caretColor: "transparent",
                color: "#1e293b",
                fontFamily: "monospace",
              }}
              value={char}
              onFocus={() => setActiveIndex(index)}
              onKeyDown={(e) => handleKeyDown(index, e)}
              onChange={(e) => {
                const val = e.target.value;
                const lastChar = val.slice(-1);
                if (lastChar) handleCharInput(index, lastChar);
              }}
              onPaste={onPaste}
            />
          );
        })}
      </div>

      {!readOnly && (
        <div className="mt-3 mx-auto" style={{ maxWidth: "340px" }}>
          <div className="row g-1 mb-1">
            {["1", "2", "3", "-"].map((k) => (
              <div className="col-3" key={k}>
                <button
                  type="button"
                  className={`btn w-100 fw-bold fs-5 py-2 ${k === "-" ? "btn-warning" : "btn-outline-dark bg-white"}`}
                  onClick={() => handleVirtualKey(k)}
                >
                  {k}
                </button>
              </div>
            ))}
          </div>
          <div className="row g-1 mb-1">
            {["4", "5", "6", ","].map((k) => (
              <div className="col-3" key={k}>
                <button
                  type="button"
                  className={`btn w-100 fw-bold fs-5 py-2 ${k === "," ? "btn-warning" : "btn-outline-dark bg-white"}`}
                  onClick={() => handleVirtualKey(k)}
                >
                  {k}
                </button>
              </div>
            ))}
          </div>
          <div className="row g-1 mb-1">
            {["7", "8", "9", "0"].map((k) => (
              <div className="col-3" key={k}>
                <button
                  type="button"
                  className="btn w-100 fw-bold fs-5 py-2 btn-outline-dark bg-white"
                  onClick={() => handleVirtualKey(k)}
                >
                  {k}
                </button>
              </div>
            ))}
          </div>
          <div className="row g-1">
            <div className="col-6">
              <button
                type="button"
                className="btn w-100 btn-outline-danger py-2 fw-semibold"
                onClick={() => handleVirtualKey("BACKSPACE")}
                title="Xóa ký tự cuối"
              >
                <i className="bi bi-backspace me-1" />
                Xóa lùi
              </button>
            </div>
            <div className="col-6">
              <button
                type="button"
                className="btn w-100 btn-outline-secondary py-2 fw-semibold"
                onClick={() => handleVirtualKey("CLEAR")}
                title="Xóa toàn bộ"
              >
                Xóa hết
              </button>
            </div>
          </div>
        </div>
      )}

      {value && (
        <div className="text-center mt-2 small text-secondary">
          Đáp án đang nhập: <strong className="text-dark font-monospace fs-6">{value}</strong>
        </div>
      )}
    </div>
  );
}
