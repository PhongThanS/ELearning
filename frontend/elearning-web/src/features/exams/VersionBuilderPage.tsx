import { useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, InputGroup, ListGroup, Modal, Row, Tab, Tabs } from "react-bootstrap";
import { Link, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { examsApi, questionsApi } from "../../services/api";
import { ConfirmDialog, ErrorAlert, Loading } from "../../components/common/Feedback";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import { Pager, PageHeader } from "../../components/common/DataTable";
import { MarkdownView } from "../../components/common/MarkdownView";
import { MediaUrls } from "../../components/common/MediaUrls";
import { useListQuery } from "../../hooks/useListQuery";
import { formatNumber, markdownExcerpt } from "../../utils/format";
import { Permissions } from "../../constants/permissions";
import { useAuth } from "../auth/useAuth";
import { QuestionCard } from "../attempts/ExamPlayerPage";
import { QuestionFilters } from "../questions/QuestionPages";
import { useActiveCategories } from "../../hooks/useCategories";
import { VersionSettingsFields } from "./ExamPages";
import { PoolRulesCard } from "./PoolRulesCard";
import type { PublishValidation, VersionDetail, VersionQuestion } from "../../types/api";

/** Dựng phiên bản đề (docs/06-frontend.md mục 3.3). Version đã publish chỉ đọc, trừ sửa đáp án / hủy câu (D-11). */
export function VersionBuilderPage() {
  const { t } = useTranslation();
  const { id = "", versionId = "" } = useParams();
  const queryClient = useQueryClient();
  const toast = useToast();
  const { hasPermission } = useAuth();
  const exam = useQuery({ queryKey: ["exam", id], queryFn: () => examsApi.get(id) });
  const version = useQuery({ queryKey: ["version", versionId], queryFn: () => examsApi.version(id, versionId) });

  const setVersion = (v: VersionDetail) => {
    queryClient.setQueryData(["version", versionId], v);
    void queryClient.invalidateQueries({ queryKey: ["exam", id] });
  };
  const change = useMutation({
    mutationFn: async (run: () => Promise<VersionDetail>) => run(),
    onSuccess: setVersion,
    onError: (e) => toast.error(e),
  });

  if (exam.error || version.error) {
    return <ErrorAlert error={exam.error ?? version.error} />;
  }
  if (!exam.data || !version.data) {
    return <Loading />;
  }
  const v = version.data;
  const draft = v.status === "DRAFT";
  const canEdit = draft && hasPermission(Permissions.ExamUpdate);

  return (
    <>
      <Link to={`/admin/exams/${id}`} className="small">← {exam.data.name}</Link>
      <PageHeader title={`Phiên bản ${v.versionNumber} — ${exam.data.code}`}>
        <Badge bg={draft ? "secondary" : v.status === "PUBLISHED" ? "success" : "dark"}>{t(`enums.versionStatus.${v.status}`)}</Badge>{" "}
        <span className="small text-secondary">
          {v.questionCount} câu · {formatNumber(v.maxScore)} điểm · {v.durationMinutes} phút
        </span>
      </PageHeader>
      {!draft && (
        <Alert variant="info" className="small">
          Phiên bản đã publish không thể chỉnh sửa. Muốn thay đổi, hãy tạo phiên bản mới từ trang đề.
          {hasPermission(Permissions.ExamRegrade) && " Nếu đáp án sai, dùng “Sửa đáp án” hoặc “Hủy câu” để chấm lại mọi bài đã nộp."}
        </Alert>
      )}
      <Tabs defaultActiveKey="questions" className="mb-3" mountOnEnter>
        <Tab eventKey="questions" title={`Câu hỏi (${v.questionCount})`}>
          <Row className="g-3">
            {canEdit && (
              <Col xl={5}>
                <QuestionPicker existing={v.questions} onAdd={(ids, score) => change.mutate(() => examsApi.addQuestions(id, versionId, ids, score))} busy={change.isPending} />
              </Col>
            )}
            <Col xl={canEdit ? 7 : 12}>
              <PoolRulesCard examId={id} version={v} canEdit={canEdit} change={change} />
              <SelectedQuestions examId={id} version={v} canEdit={canEdit} change={change} />
            </Col>
          </Row>
        </Tab>
        <Tab eventKey="settings" title="Cấu hình">
          <SettingsTab examId={id} version={v} canEdit={canEdit} onSaved={setVersion} />
        </Tab>
        <Tab eventKey="preview" title={t("common.preview")}>
          <PreviewTab examId={id} versionId={versionId} />
        </Tab>
        {draft && hasPermission(Permissions.ExamPublish) && (
          <Tab eventKey="publish" title="Publish">
            <PublishTab examId={id} versionId={versionId} onPublished={setVersion} />
          </Tab>
        )}
      </Tabs>
    </>
  );
}

function QuestionPicker({ existing, onAdd, busy }: { existing: VersionQuestion[]; onAdd: (ids: string[], score: number | null) => void; busy: boolean }) {
  const list = useListQuery({ isActive: "true", pageSize: 10 });
  const categories = useActiveCategories();
  const query = useQuery({ queryKey: ["questions", "picker", list.params], queryFn: () => questionsApi.list(list.params) });
  const [selected, setSelected] = useState<string[]>([]);
  const [score, setScore] = useState<string>("");
  const inVersion = new Set(existing.map((q) => q.sourceQuestionId));
  const { t } = useTranslation();

  return (
    <Card>
      <Card.Body>
        <h2 className="h6">Ngân hàng câu hỏi</h2>
        <QuestionFilters list={list} categories={categories.data?.items ?? []} />
        {query.isLoading && <Loading />}
        <ListGroup className="mb-2">
          {query.data?.items.map((q) => {
            const added = inVersion.has(q.id);
            return (
              <ListGroup.Item key={q.id} className="d-flex gap-2 align-items-start">
                <Form.Check
                  aria-label={`Chọn ${q.code}`}
                  disabled={added}
                  checked={added || selected.includes(q.id)}
                  onChange={(e) => setSelected(e.target.checked ? [...selected, q.id] : selected.filter((x) => x !== q.id))}
                />
                <div className="small">
                  <code>{q.code}</code> <Badge bg="light" text="dark">{t(`enums.questionType.${q.questionType}`)}</Badge> {added && <Badge bg="success">Đã có</Badge>}
                  <div>{markdownExcerpt(q.contentPreview)}</div>
                </div>
              </ListGroup.Item>
            );
          })}
        </ListGroup>
        {query.data && <Pager data={query.data} onPage={list.setPage} />}
        <InputGroup size="sm" className="mt-2">
          <Form.Control type="number" min={0.25} max={100} step={0.25} placeholder="Điểm (mặc định của câu)" aria-label="Điểm" value={score} onChange={(e) => setScore(e.target.value)} />
          <Button
            disabled={selected.length === 0 || busy}
            onClick={() => {
              onAdd(selected, score ? Number(score) : null);
              setSelected([]);
            }}
          >
            Thêm {selected.length > 0 ? selected.length : ""} câu
          </Button>
        </InputGroup>
      </Card.Body>
    </Card>
  );
}

function SelectedQuestions({
  examId,
  version,
  canEdit,
  change,
}: {
  examId: string;
  version: VersionDetail;
  canEdit: boolean;
  change: { mutate: (run: () => Promise<VersionDetail>) => void; isPending: boolean };
}) {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  const draft = version.status === "DRAFT";
  const questions = [...version.questions].filter((q) => !draft || q.poolRuleId === null).sort((a, b) => a.order - b.order);
  const ruleOrder = new Map(version.poolRules.map((r) => [r.id, r.order]));
  const [keyEditing, setKeyEditing] = useState<VersionQuestion | null>(null);
  const [voiding, setVoiding] = useState<VersionQuestion | null>(null);
  const changedCount = questions.filter((q) => q.sourceChanged).length;

  const move = (index: number, delta: number) => {
    const ids = questions.map((q) => q.id);
    const target = index + delta;
    [ids[index], ids[target]] = [ids[target]!, ids[index]!];
    change.mutate(() => examsApi.reorder(examId, version.id, ids));
  };

  return (
    <>
      {canEdit && changedCount > 0 && (
        <Alert variant="warning" className="d-flex justify-content-between align-items-center py-2">
          <span className="small">{changedCount} câu đã thay đổi trong ngân hàng sau khi thêm vào đề.</span>
          <Button size="sm" variant="outline-dark" onClick={() => change.mutate(() => examsApi.sync(examId, version.id))}>Đồng bộ tất cả</Button>
        </Alert>
      )}
      {questions.length === 0 && <p className="text-secondary">Chưa có câu hỏi.</p>}
      {questions.map((q, index) => (
        <Card key={q.id} className="mb-2">
          <Card.Body className="py-2">
            <div className="d-flex justify-content-between align-items-start gap-2">
              <div className="small">
                <strong>Câu {index + 1}</strong> <Badge bg="light" text="dark">{t(`enums.questionType.${q.questionType}`)}</Badge>{" "}
                {q.sourceCode && <code>{q.sourceCode}</code>} {q.sourceChanged && <Badge bg="warning" text="dark">Câu gốc đã thay đổi</Badge>}{" "}
                {q.isVoided && <Badge bg="dark">Đã hủy</Badge>}{" "}
                {q.poolRuleId && <Badge bg="info-subtle" text="dark">Pool {ruleOrder.get(q.poolRuleId)}</Badge>}
              </div>
              <div className="d-flex gap-1 align-items-center">
                {canEdit ? (
                  <>
                    <Form.Control
                      size="sm"
                      type="number"
                      min={0.25}
                      max={100}
                      step={0.25}
                      style={{ width: 80 }}
                      aria-label={`Điểm câu ${index + 1}`}
                      defaultValue={q.score}
                      onBlur={(e) => {
                        const value = Number(e.target.value);
                        if (value !== q.score && value > 0) {
                          change.mutate(() => examsApi.setQuestionScore(examId, version.id, q.id, value));
                        }
                      }}
                    />
                    <Button size="sm" variant="outline-secondary" aria-label="Lên" disabled={index === 0 || change.isPending} onClick={() => move(index, -1)}>↑</Button>
                    <Button size="sm" variant="outline-secondary" aria-label="Xuống" disabled={index === questions.length - 1 || change.isPending} onClick={() => move(index, 1)}>↓</Button>
                    {q.sourceChanged && (
                      <Button size="sm" variant="outline-warning" onClick={() => change.mutate(() => examsApi.sync(examId, version.id, [q.id]))}>Đồng bộ</Button>
                    )}
                    <Button size="sm" variant="outline-danger" aria-label={`Xóa câu ${index + 1}`} onClick={() => change.mutate(() => examsApi.removeQuestion(examId, version.id, q.id))}>✕</Button>
                  </>
                ) : (
                  <>
                    <Badge bg="secondary">{formatNumber(q.score)} điểm</Badge>
                    {version.status !== "DRAFT" && hasPermission(Permissions.ExamRegrade) && !q.isVoided && (
                      <>
                        <Button size="sm" variant="outline-primary" onClick={() => setKeyEditing(q)}>Sửa đáp án</Button>
                        <Button size="sm" variant="outline-danger" onClick={() => setVoiding(q)}>Hủy câu</Button>
                      </>
                    )}
                  </>
                )}
              </div>
            </div>
            <div className="mt-1">
              <MarkdownView content={q.content} format={q.contentFormat} media={version.media} />
            </div>
            <div className="small text-secondary">
              Đáp án:{" "}
              {q.questionType === "FILL_IN"
                ? q.answerDataType === "NUMBER"
                  ? `${formatNumber(q.correctAnswerNumber)} (±${formatNumber(q.numericTolerance)})`
                  : q.acceptedAnswers.join(" / ")
                : q.options.filter((o) => o.isCorrect).map((o) => o.optionCode).join(", ")}
            </div>
          </Card.Body>
        </Card>
      ))}
      <AnswerKeyModal examId={examId} version={version} question={keyEditing} onHide={() => setKeyEditing(null)} />
      <VoidDialog examId={examId} version={version} question={voiding} onHide={() => setVoiding(null)} />
    </>
  );
}

function useRegrade(examId: string, version: VersionDetail, onDone: () => void) {
  const toast = useToast();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (run: () => Promise<{ affectedAttempts: number; changedResults: number }>) => run(),
    onSuccess: (summary) => {
      toast.success(`Đã chấm lại ${summary.affectedAttempts} lượt thi, ${summary.changedResults} kết quả thay đổi.`);
      void queryClient.invalidateQueries({ queryKey: ["version", version.id] });
      void queryClient.invalidateQueries({ queryKey: ["exam", examId] });
      onDone();
    },
  });
}

function AnswerKeyModal(props: { examId: string; version: VersionDetail; question: VersionQuestion | null; onHide: () => void }) {
  // key theo câu hỏi: mở cho câu khác thì form khởi tạo lại từ đáp án hiện tại
  return props.question ? <AnswerKeyForm key={props.question.id} {...props} question={props.question} /> : null;
}

function AnswerKeyForm({ examId, version, question, onHide }: { examId: string; version: VersionDetail; question: VersionQuestion; onHide: () => void }) {
  const [codes, setCodes] = useState<string[]>(() => question.options.filter((o) => o.isCorrect).map((o) => o.optionCode));
  const [accepted, setAccepted] = useState(() => question.acceptedAnswers.join("\n"));
  const [number, setNumber] = useState(() => question.correctAnswerNumber?.toString() ?? "");
  const [tolerance, setTolerance] = useState(() => question.numericTolerance?.toString() ?? "0");
  const [reason, setReason] = useState("");
  const regrade = useRegrade(examId, version, onHide);

  const single = question.questionType !== "MULTIPLE_CHOICE";

  return (
    <Modal show onHide={onHide} centered>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">Sửa đáp án và chấm lại</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {regrade.error && <Alert variant="danger">{describeError(regrade.error)}</Alert>}
        <MarkdownView content={question.content} format={question.contentFormat} media={version.media} />
        {question.questionType === "FILL_IN" ? (
          question.answerDataType === "NUMBER" ? (
            <Row className="g-2">
              <Col>
                <Form.Label htmlFor="ak-number">Đáp án</Form.Label>
                <Form.Control id="ak-number" type="number" step="any" value={number} onChange={(e) => setNumber(e.target.value)} />
              </Col>
              <Col>
                <Form.Label htmlFor="ak-tol">Sai số</Form.Label>
                <Form.Control id="ak-tol" type="number" min={0} step="any" value={tolerance} onChange={(e) => setTolerance(e.target.value)} />
              </Col>
            </Row>
          ) : (
            <Form.Group controlId="ak-accepted">
              <Form.Label>Đáp án chấp nhận (mỗi dòng một đáp án)</Form.Label>
              <Form.Control as="textarea" rows={3} value={accepted} onChange={(e) => setAccepted(e.target.value)} />
            </Form.Group>
          )
        ) : (
          <fieldset>
            <legend className="form-label fs-6">Đáp án đúng</legend>
            {question.options.map((o) => (
              <Form.Check
                key={o.optionCode}
                id={`ak-${o.optionCode}`}
                type={single ? "radio" : "checkbox"}
                name="ak-options"
                label={<>{o.optionCode}. <MarkdownView content={o.content} inline media={version.media} /></>}
                checked={codes.includes(o.optionCode)}
                onChange={(e) => setCodes(single ? [o.optionCode] : e.target.checked ? [...codes, o.optionCode] : codes.filter((c) => c !== o.optionCode))}
              />
            ))}
          </fieldset>
        )}
        <Form.Group className="mt-3" controlId="ak-reason">
          <Form.Label>Lý do *</Form.Label>
          <Form.Control as="textarea" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Form.Group>
        <p className="small text-secondary mt-2 mb-0">Mọi bài đã nộp của phiên bản này sẽ được chấm lại; điểm cũ được lưu vào lịch sử.</p>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>Hủy</Button>
        <Button
          disabled={!reason.trim() || regrade.isPending}
          onClick={() =>
            regrade.mutate(() =>
              examsApi.correctAnswerKey(examId, version.id, question.id, {
                reason: reason.trim(),
                ...(question.questionType === "FILL_IN"
                  ? question.answerDataType === "NUMBER"
                    ? { correctAnswerNumber: Number(number), numericTolerance: Number(tolerance) }
                    : { acceptedAnswers: accepted.split("\n").map((a) => a.trim()).filter(Boolean) }
                  : { correctOptionCodes: codes }),
              }),
            )
          }
        >
          Lưu và chấm lại
        </Button>
      </Modal.Footer>
    </Modal>
  );
}

function VoidDialog({ examId, version, question, onHide }: { examId: string; version: VersionDetail; question: VersionQuestion | null; onHide: () => void }) {
  const regrade = useRegrade(examId, version, onHide);
  return (
    <ConfirmDialog
      show={!!question}
      title={`Hủy câu ${question?.order ?? ""}`}
      body="Mọi thí sinh được trọn điểm câu này và toàn bộ bài đã nộp sẽ được chấm lại."
      variant="danger"
      requireReason
      busy={regrade.isPending}
      onCancel={onHide}
      onConfirm={(reason) => regrade.mutate(() => examsApi.voidQuestion(examId, version.id, question!.id, reason))}
    />
  );
}

function SettingsTab({ examId, version, canEdit, onSaved }: { examId: string; version: VersionDetail; canEdit: boolean; onSaved: (v: VersionDetail) => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const [value, setValue] = useState({
    durationMinutes: version.durationMinutes,
    passPercentage: version.passPercentage,
    scoreVisibility: version.scoreVisibility,
    reviewPolicy: version.reviewPolicy,
    shuffleQuestions: version.shuffleQuestions,
    shuffleOptions: version.shuffleOptions,
  });
  const save = useMutation({
    mutationFn: () => examsApi.updateVersion(examId, version.id, { ...value, rowVersion: version.rowVersion }),
    onSuccess: (v) => {
      onSaved(v);
      toast.success(t("common.saved"));
    },
    onError: (e) => toast.error(e),
  });
  return (
    <Card>
      <Card.Body>
        <VersionSettingsFields value={value} onChange={setValue} disabled={!canEdit} />
        <p className="small text-secondary mt-2 mb-0">
          “Xem lại bài ngay sau khi nộp” chỉ dùng được khi đề có 1 lượt thi; “sau khi đề đóng” cần đặt thời điểm đóng.
        </p>
        {canEdit && (
          <Button className="mt-3" onClick={() => save.mutate()} disabled={save.isPending}>{t("common.save")}</Button>
        )}
      </Card.Body>
    </Card>
  );
}

function PreviewTab({ examId, versionId }: { examId: string; versionId: string }) {
  const query = useQuery({ queryKey: ["preview", versionId], queryFn: () => examsApi.preview(examId, versionId) });
  if (query.error) {
    return <ErrorAlert error={query.error} />;
  }
  if (!query.data) {
    return <Loading />;
  }
  const p = query.data;
  return (
    <MediaUrls value={p.media}>
      <Alert variant="secondary" className="small">Hiển thị đúng như học viên thấy (không có đáp án). Chế độ xem trước không lưu và không tính giờ.</Alert>
      {p.instructions && (
        <Card className="mb-3">
          <Card.Body>
            <MarkdownView content={p.instructions} />
          </Card.Body>
        </Card>
      )}
      {p.questions.map((q) => (
        <div className="mb-3" key={q.id}>
          <QuestionCard
            question={q}
            total={p.questionCount}
            draft={{ selectedOptions: [], answerText: null, isMarkedForReview: false }}
            onSelect={() => undefined}
            onText={() => undefined}
            onMark={() => undefined}
            readOnly
          />
        </div>
      ))}
    </MediaUrls>
  );
}

function PublishTab({ examId, versionId, onPublished }: { examId: string; versionId: string; onPublished: (v: VersionDetail) => void }) {
  const toast = useToast();
  const [validation, setValidation] = useState<PublishValidation | null>(null);
  const [confirm, setConfirm] = useState(false);
  const validate = useMutation({ mutationFn: () => examsApi.validate(examId, versionId), onSuccess: setValidation, onError: (e) => toast.error(e) });
  const publish = useMutation({
    mutationFn: () => examsApi.publish(examId, versionId),
    onSuccess: (v) => {
      setConfirm(false);
      onPublished(v);
      toast.success("Đã publish phiên bản.");
    },
  });

  return (
    <Card>
      <Card.Body>
        <p>Kiểm tra toàn bộ điều kiện trước khi publish. Sau khi publish, phiên bản không thể chỉnh sửa.</p>
        <Button variant="outline-primary" onClick={() => validate.mutate()} disabled={validate.isPending}>Kiểm tra</Button>
        {validation && (
          <div className="mt-3">
            {validation.isValid ? (
              <Alert variant="success">Phiên bản hợp lệ, có thể publish.</Alert>
            ) : (
              <Alert variant="danger">
                <strong>Chưa thể publish:</strong>
                <ul className="mb-0">
                  {validation.issues.map((issue, i) => (
                    <li key={i}>{issue.message}</li>
                  ))}
                </ul>
              </Alert>
            )}
            {validation.isValid && <Button onClick={() => setConfirm(true)}>Publish</Button>}
          </div>
        )}
        {publish.error && <ErrorAlert error={publish.error} />}
      </Card.Body>
      <ConfirmDialog
        show={confirm}
        title="Publish phiên bản"
        body="Phiên bản đang dùng (nếu có) sẽ chuyển sang lưu trữ; lượt thi đang làm trên phiên bản cũ vẫn tiếp tục bình thường."
        busy={publish.isPending}
        onCancel={() => setConfirm(false)}
        onConfirm={() => publish.mutate()}
      />
    </Card>
  );
}
