import { useMemo, useRef, useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, InputGroup, Modal, Row, Tab, Tabs } from "react-bootstrap";
import { Link, useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { categoriesApi, questionsApi } from "../../services/api";
import { toApiError } from "../../services/apiClient";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import { useActiveCategories } from "../../hooks/useCategories";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import { ActiveBadge, DataTable, PageHeader, SearchBox, type Column } from "../../components/common/DataTable";
import { MarkdownView } from "../../components/common/MarkdownView";
import { MediaUrls } from "../../components/common/MediaUrls";
import { ImageInsertButton, insertAt } from "./ImageInsertButton";
import { QuestionImportDialog } from "./QuestionImportDialog";
import { useListQuery } from "../../hooks/useListQuery";
import { formatDateTime, formatNumber, markdownExcerpt } from "../../utils/format";
import { matchesAny } from "../../utils/answerNormalizer";
import { isValidNumberAnswer } from "../attempts/playerState";
import { Permissions } from "../../constants/permissions";
import { useAuth } from "../auth/useAuth";
import type { AnswerDataType, Category, ContentFormat, MediaUpload, MediaUrlMap, QuestionDetail, QuestionDifficulty, QuestionInput, QuestionListItem, QuestionType } from "../../types/api";

const QUESTION_TYPES: QuestionType[] = ["SINGLE_CHOICE", "MULTIPLE_CHOICE", "TRUE_FALSE", "FILL_IN", "ESSAY"];
const CHOICE_CODES = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J"];

// ======================= Danh mục =======================

export function CategoriesPage() {
  const { t } = useTranslation();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(Permissions.CategoryManage);
  const list = useListQuery({ sortBy: "name", sortDir: "asc" });
  const query = useQuery({ queryKey: ["categories", list.params], queryFn: () => categoriesApi.list(list.params) });
  const [editing, setEditing] = useState<{ id?: string; code: string; name: string; rowVersion?: string; isActive?: boolean } | null>(null);
  const [deleting, setDeleting] = useState<Category | null>(null);

  const save = useMutation({
    mutationFn: () =>
      editing!.id
        ? categoriesApi.update(editing!.id, { name: editing!.name, isActive: editing!.isActive ?? true, rowVersion: editing!.rowVersion! })
        : categoriesApi.create({ code: editing!.code, name: editing!.name }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["categories"] });
      void queryClient.invalidateQueries({ queryKey: ["sidebar-categories"] });
      setEditing(null);
      toast.success(t("common.saved"));
    },
  });

  const toggle = useMutation({
    mutationFn: (c: Category) => categoriesApi.setStatus(c.id, !c.isActive),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["categories"] });
      void queryClient.invalidateQueries({ queryKey: ["sidebar-categories"] });
    },
    onError: (e) => toast.error(e),
  });

  const remove = useMutation({
    mutationFn: (c: Category) => categoriesApi.remove(c.id, true),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["categories"] });
      void queryClient.invalidateQueries({ queryKey: ["sidebar-categories"] });
      setDeleting(null);
      toast.success("Đã xóa chuyên đề thành công.");
    },
    onError: (e) => toast.error(e),
  });

  const columns: Column<Category>[] = [
    { key: "code", header: t("common.code"), sortKey: "code", render: (c) => <code>{c.code}</code> },
    { key: "name", header: t("common.name"), sortKey: "name", render: (c) => c.name },
    { key: "count", header: "Số câu hỏi", render: (c) => <Link to={`/admin/questions?categoryId=${c.id}`}>{c.questionCount}</Link> },
    { key: "status", header: t("common.status"), render: (c) => <ActiveBadge active={c.isActive} /> },
    {
      key: "actions",
      header: "",
      className: "text-end",
      render: (c) =>
        canManage && (
          <div className="d-inline-flex gap-2">
            <Button size="sm" variant="link" className="p-0" onClick={() => setEditing({ id: c.id, code: c.code, name: c.name, rowVersion: c.rowVersion, isActive: c.isActive })}>
              {t("common.edit")}
            </Button>
            <Button size="sm" variant="link" className="p-0" onClick={() => toggle.mutate(c)}>
              {c.isActive ? t("common.deactivate") : t("common.activate")}
            </Button>
            <Button size="sm" variant="link" className="p-0 text-danger" onClick={() => setDeleting(c)}>
              {t("common.delete")}
            </Button>
          </div>
        ),
    },
  ];

  return (
    <>
      <PageHeader title={t("nav.categories")} actions={canManage && <Button onClick={() => setEditing({ code: "", name: "" })}>{t("common.create")}</Button>} />
      <div className="mb-3" style={{ maxWidth: 400 }}>
        <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã hoặc tên danh mục..." />
      </div>
      <DataTable data={query.data} columns={columns} rowKey={(c) => c.id} isLoading={query.isLoading} error={query.error} sort={list.state} onSort={list.toggleSort} onPage={list.setPage} />
      
      {/* Modal Sửa / Thêm */}
      <Modal show={!!editing} onHide={() => setEditing(null)} centered>
        <Form
          onSubmit={(e) => {
            e.preventDefault();
            save.mutate();
          }}
        >
          <Modal.Header closeButton>
            <Modal.Title as="h2" className="h5">{editing?.id ? t("common.edit") : t("common.create")} chuyên đề</Modal.Title>
          </Modal.Header>
          <Modal.Body>
            {save.error && <Alert variant="danger">{describeError(save.error)}</Alert>}
            {editing && (
              <>
                <Form.Group className="mb-3" controlId="cat-code">
                  <Form.Label>{t("common.code")} *</Form.Label>
                  <Form.Control
                    value={editing.code}
                    disabled={!!editing.id}
                    required
                    placeholder="VD: TOAN-12, HOA-HOC-10"
                    onChange={(e) => setEditing({ ...editing, code: e.target.value })}
                  />
                </Form.Group>
                <Form.Group className="mb-3" controlId="cat-name">
                  <Form.Label>{t("common.name")} *</Form.Label>
                  <Form.Control
                    value={editing.name}
                    required
                    placeholder="VD: Đại số & Giải tích 12"
                    onChange={(e) => setEditing({ ...editing, name: e.target.value })}
                  />
                </Form.Group>
              </>
            )}
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onClick={() => setEditing(null)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={save.isPending}>{t("common.save")}</Button>
          </Modal.Footer>
        </Form>
      </Modal>

      {/* Modal Xác nhận xóa */}
      <Modal show={!!deleting} onHide={() => setDeleting(null)} centered>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">Xác nhận xóa chuyên đề</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {deleting && (
            <div>
              <p>
                Bạn có chắc chắn muốn xóa chuyên đề <strong>{deleting.name}</strong> (<code>{deleting.code}</code>)?
              </p>
              {deleting.questionCount > 0 ? (
                <Alert variant="warning" className="mb-0">
                  ⚠️ Chuyên đề này đang chứa <strong>{deleting.questionCount}</strong> câu hỏi. Thao tác này sẽ xóa chuyên đề và giải phóng các câu hỏi liên quan.
                </Alert>
              ) : (
                <p className="text-secondary small mb-0">Hành động này không thể hoàn tác.</p>
              )}
            </div>
          )}
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => setDeleting(null)} disabled={remove.isPending}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="danger"
            onClick={() => deleting && remove.mutate(deleting)}
            disabled={remove.isPending}
          >
            {remove.isPending ? "Đang xóa..." : t("common.delete")}
          </Button>
        </Modal.Footer>
      </Modal>
    </>
  );
}

// ======================= Danh sách câu hỏi =======================

export function QuestionsPage() {
  const { t } = useTranslation();
  const toast = useToast();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const initialCategory = new URLSearchParams(window.location.search).get("categoryId") ?? undefined;
  const list = useListQuery({ categoryId: initialCategory });
  const query = useQuery({ queryKey: ["questions", list.params], queryFn: () => questionsApi.list(list.params) });
  const categories = useActiveCategories();
  const [importing, setImporting] = useState(false);
  const [deleting, setDeleting] = useState<QuestionListItem | null>(null);
  const action = useMutation({
    mutationFn: async (run: () => Promise<{ id: string }>) => run(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["questions"] });
      toast.success(t("common.saved"));
    },
    onError: (e) => toast.error(e),
  });

  const remove = useMutation({
    mutationFn: (id: string) => questionsApi.remove(id, true),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["questions"] });
      setDeleting(null);
      toast.success("Đã xóa câu hỏi thành công.");
    },
    onError: (e) => toast.error(e),
  });

  const columns: Column<QuestionListItem>[] = [
    { key: "code", header: t("common.code"), sortKey: "code", render: (q) => <Link to={`/admin/questions/${q.id}/edit`}><code>{q.code}</code></Link> },
    { key: "content", header: "Nội dung", render: (q) => <span className="text-truncate d-inline-block" style={{ maxWidth: 420 }}>{markdownExcerpt(q.contentPreview)}</span> },
    { key: "type", header: "Loại", render: (q) => t(`enums.questionType.${q.questionType}`) + (q.answerDataType ? ` (${t(`enums.answerDataType.${q.answerDataType}`)})` : "") },
    { key: "category", header: t("nav.categories"), render: (q) => q.categoryName ?? "—" },
    {
      key: "difficulty",
      header: "Độ khó / tag",
      render: (q) => (
        <>
          {q.difficulty && <Badge bg="light" text="dark" className="me-1">{t(`enums.difficulty.${q.difficulty}`)}</Badge>}
          {q.tags.map((tag) => (
            <Badge key={tag} bg="info-subtle" text="dark" className="me-1">{tag}</Badge>
          ))}
        </>
      ),
    },
    { key: "score", header: "Điểm", render: (q) => formatNumber(q.defaultScore) },
    { key: "status", header: t("common.status"), render: (q) => <ActiveBadge active={q.isActive} /> },
    { key: "updated", header: t("common.updatedAt"), sortKey: "updatedAt", render: (q) => formatDateTime(q.updatedAt ?? q.createdAt) },
    {
      key: "actions",
      header: "",
      className: "text-end text-nowrap",
      render: (q) => (
        <>
          {hasPermission(Permissions.QuestionCreate) && (
            <Button size="sm" variant="link" onClick={() => action.mutate(async () => {
              const clone = await questionsApi.clone(q.id);
              navigate(`/admin/questions/${clone.id}/edit`);
              return clone;
            })}>
              {t("common.clone")}
            </Button>
          )}
          {hasPermission(Permissions.QuestionUpdate) && (
            <>
              <Button size="sm" variant="link" onClick={() => action.mutate(() => questionsApi.setStatus(q.id, !q.isActive))}>
                {q.isActive ? t("common.deactivate") : t("common.activate")}
              </Button>
              <Button size="sm" variant="link" className="text-danger" onClick={() => setDeleting(q)}>
                {t("common.delete")}
              </Button>
            </>
          )}
        </>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title={t("nav.questions")}
        actions={
          hasPermission(Permissions.QuestionCreate) && (
            <>
              <Button variant="outline-primary" onClick={() => setImporting(true)}>Import Excel</Button>
              <Link to="/admin/questions/create" className="btn btn-primary">{t("common.create")}</Link>
            </>
          )
        }
      />
      <QuestionImportDialog show={importing} onHide={() => setImporting(false)} />
      <QuestionFilters list={list} categories={categories.data?.items ?? []} />
      <DataTable data={query.data} columns={columns} rowKey={(q) => q.id} isLoading={query.isLoading} error={query.error} sort={list.state} onSort={list.toggleSort} onPage={list.setPage} />

      <Modal show={!!deleting} onHide={() => setDeleting(null)} centered>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">Xác nhận xóa câu hỏi</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {deleting && (
            <div>
              <p>
                Bạn có chắc chắn muốn xóa câu hỏi <strong>{deleting.code}</strong> khỏi ngân hàng câu hỏi?
              </p>
              <div className="p-2 border rounded bg-light mb-3 small">
                <em>{markdownExcerpt(deleting.contentPreview)}</em>
              </div>
              <p className="text-danger small mb-0">
                ⚠️ Thao tác này sẽ xóa vĩnh viễn câu hỏi khỏi ngân hàng câu hỏi.
              </p>
            </div>
          )}
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => setDeleting(null)} disabled={remove.isPending}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="danger"
            onClick={() => deleting && remove.mutate(deleting.id)}
            disabled={remove.isPending}
          >
            {remove.isPending ? "Đang xóa..." : t("common.delete")}
          </Button>
        </Modal.Footer>
      </Modal>
    </>
  );
}

export function QuestionFilters({ list, categories }: { list: ReturnType<typeof useListQuery>; categories: Category[] }) {
  const { t } = useTranslation();
  return (
    <Row className="g-2 mb-3">
      <Col md={3}>
        <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã hoặc nội dung..." />
      </Col>
      <Col md={2}>
        <Form.Select size="sm" aria-label="Độ khó" value={String(list.state.difficulty ?? "")} onChange={(e) => list.setFilter({ difficulty: e.target.value })}>
          <option value="">Mọi độ khó</option>
          {DIFFICULTIES.map((d) => (
            <option key={d} value={d}>{t(`enums.difficulty.${d}`)}</option>
          ))}
        </Form.Select>
      </Col>
      <Col md={2}>
        <Form.Control size="sm" aria-label="Tag" placeholder="Tag" defaultValue={String(list.state.tag ?? "")}
          onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); list.setFilter({ tag: e.currentTarget.value.trim() }); } }}
          onBlur={(e) => list.setFilter({ tag: e.currentTarget.value.trim() })} />
      </Col>
      <Col md={2}>
        <Form.Select size="sm" aria-label={t("nav.categories")} value={String(list.state.categoryId ?? "")} onChange={(e) => list.setFilter({ categoryId: e.target.value })}>
          <option value="">Mọi danh mục</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </Form.Select>
      </Col>
      <Col md={2}>
        <Form.Select size="sm" aria-label="Loại câu hỏi" value={String(list.state.questionType ?? "")} onChange={(e) => list.setFilter({ questionType: e.target.value })}>
          <option value="">Mọi loại</option>
          {QUESTION_TYPES.map((type) => (
            <option key={type} value={type}>{t(`enums.questionType.${type}`)}</option>
          ))}
        </Form.Select>
      </Col>
      <Col md={1}>
        <Form.Select size="sm" aria-label={t("common.status")} value={String(list.state.isActive ?? "")} onChange={(e) => list.setFilter({ isActive: e.target.value })}>
          <option value="">{t("common.all")}</option>
          <option value="true">{t("common.active")}</option>
          <option value="false">{t("common.inactive")}</option>
        </Form.Select>
      </Col>
    </Row>
  );
}

// ======================= Trình soạn câu hỏi =======================

type EditorState = QuestionInput;

const emptyQuestion = (): EditorState => ({
  categoryId: null,
  difficulty: null,
  tags: [],
  partialScoring: false,
  code: "",
  content: "",
  contentFormat: "MARKDOWN",
  questionType: "SINGLE_CHOICE",
  answerDataType: null,
  defaultScore: 1,
  explanation: null,
  options: [
    { optionCode: "A", content: "", isCorrect: true },
    { optionCode: "B", content: "", isCorrect: false },
    { optionCode: "C", content: "", isCorrect: false },
    { optionCode: "D", content: "", isCorrect: false },
  ],
  acceptedAnswers: [""],
  correctAnswerNumber: null,
  numericTolerance: 0,
  caseSensitive: false,
  ignoreAccent: false,
});

function optionsForType(type: QuestionType, current: EditorState["options"]): EditorState["options"] {
  if (type === "TRUE_FALSE") {
    return [
      { optionCode: "TRUE", content: "Đúng", isCorrect: true },
      { optionCode: "FALSE", content: "Sai", isCorrect: false },
    ];
  }
  if (type === "FILL_IN" || type === "ESSAY") {
    return [];
  }
  const base = current.filter((o) => CHOICE_CODES.includes(o.optionCode));
  const options = base.length >= 2 ? base : emptyQuestion().options;
  // Chọn một: giữ đúng 1 đáp án đúng
  if (type === "SINGLE_CHOICE") {
    const firstCorrect = Math.max(0, options.findIndex((o) => o.isCorrect));
    return options.map((o, i) => ({ ...o, isCorrect: i === firstCorrect }));
  }
  return options;
}

function extractMediaIds(text: string): string[] {
  const ids: string[] = [];
  const regex = /!\[.*?\]\(media:([0-9a-fA-F-]+)\)/gi;
  let match;
  while ((match = regex.exec(text)) !== null) {
    if (match[1] && !ids.includes(match[1].toLowerCase())) {
      ids.push(match[1].toLowerCase());
    }
  }
  return ids;
}

function stripMediaMarkdown(text: string): string {
  return text.replace(/!\[.*?\]\(media:[0-9a-fA-F-]+\)/gi, "").trim();
}

function toEditorState(q: QuestionDetail): EditorState {
  return {
    categoryId: q.categoryId,
    difficulty: q.difficulty,
    tags: q.tags,
    partialScoring: q.partialScoring,
    code: q.code,
    content: stripMediaMarkdown(q.content),
    contentFormat: q.contentFormat,
    questionType: q.questionType,
    answerDataType: q.answerDataType,
    defaultScore: q.defaultScore,
    explanation: q.explanation,
    options: q.options.map((o) => ({ optionCode: o.optionCode, content: o.content, isCorrect: o.isCorrect })),
    acceptedAnswers: q.acceptedAnswers.length > 0 ? q.acceptedAnswers : [""],
    correctAnswerNumber: q.correctAnswerNumber,
    numericTolerance: q.numericTolerance ?? 0,
    caseSensitive: q.caseSensitive,
    ignoreAccent: q.ignoreAccent,
    rowVersion: q.rowVersion,
  };
}

/** Tạo / sửa câu hỏi; editor thay đổi theo loại câu (docs/06-frontend.md mục 3.2). */
export function QuestionEditorPage() {
  const { id } = useParams();
  const existing = useQuery({ queryKey: ["question", id], queryFn: () => questionsApi.get(id!), enabled: !!id });

  if (existing.error) {
    return <ErrorAlert error={existing.error} />;
  }
  if (id && !existing.data) {
    return <Loading />;
  }
  // key theo Id: đổi câu hỏi thì khởi tạo lại form từ dữ liệu mới (không đồng bộ state bằng effect)
  return <QuestionEditor key={id ?? "new"} id={id} existing={existing.data} />;
}

function QuestionEditor({ id, existing }: { id: string | undefined; existing: QuestionDetail | undefined }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();
  const categories = useActiveCategories();
  const [form, setForm] = useState<EditorState>(() => (existing ? toEditorState(existing) : emptyQuestion()));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [pendingType, setPendingType] = useState<QuestionType | null>(null);
  const [tryValue, setTryValue] = useState("");
  // Tag nhập dạng "a, b, c"; tách khi lưu để gõ dấu phẩy không bị mất
  const [tagText, setTagText] = useState(() => form.tags.join(", "));
  // Ảnh (D-27): bảng id → URL đã ký của câu đang sửa, thêm dần khi tải ảnh mới để xem trước được
  const { hasPermission } = useAuth();
  const canUpload = hasPermission(Permissions.QuestionCreate);
  const [media, setMedia] = useState<MediaUrlMap>(() => existing?.media ?? {});
  const contentRef = useRef<HTMLTextAreaElement>(null);
  const explanationRef = useRef<HTMLTextAreaElement>(null);

  const [attachedImages, setAttachedImages] = useState<{ id: string; url: string }[]>(() => {
    if (!existing) return [];
    const ids = extractMediaIds(existing.content);
    return ids.map((imgId) => ({
      id: imgId,
      url: existing.media?.[imgId] ?? existing.media?.[imgId.toLowerCase()] ?? "",
    }));
  });

  const fullContentWithImages = useMemo(() => {
    const text = form.content.trim();
    if (attachedImages.length === 0) return text;
    const imgMd = attachedImages.map((img) => `![](media:${img.id})`).join("\n\n");
    return text ? `${text}\n\n${imgMd}` : imgMd;
  }, [form.content, attachedImages]);

  const save = useMutation({
    mutationFn: (body: QuestionInput) => (id ? questionsApi.update(id, body) : questionsApi.create(body)),
    onSuccess: (saved) => {
      void queryClient.invalidateQueries({ queryKey: ["questions"] });
      queryClient.setQueryData(["question", saved.id], saved);
      setMedia((m) => ({ ...m, ...saved.media }));
      toast.success(t("common.saved"));
      navigate(`/admin/questions/${saved.id}/edit`, { replace: true });
      setForm((f) => ({ ...f, rowVersion: saved.rowVersion, code: saved.code }));
    },
    onError: (e) => setErrors(toApiError(e).fieldErrors()),
  });

  const [confirmDelete, setConfirmDelete] = useState(false);
  const removeQuestion = useMutation({
    mutationFn: () => questionsApi.remove(id!, true),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["questions"] });
      toast.success("Đã xóa câu hỏi thành công.");
      navigate("/admin/questions");
    },
    onError: (e) => toast.error(e),
  });

  const set = (patch: Partial<EditorState>) => setForm({ ...form, ...patch });
  const changeType = (type: QuestionType) =>
    set({
      questionType: type,
      options: optionsForType(type, form.options),
      answerDataType: type === "FILL_IN" ? (form.answerDataType ?? "TEXT") : null,
    });

  const submit = () => {
    setErrors({});
    const body: QuestionInput = {
      ...form,
      content: fullContentWithImages,
      code: form.code?.trim() || null,
      tags: parseTags(tagText),
      explanation: form.explanation?.trim() || null,
      options: form.questionType === "FILL_IN" || form.questionType === "ESSAY" ? [] : form.options,
      partialScoring: form.questionType === "MULTIPLE_CHOICE" && form.partialScoring,
      acceptedAnswers: form.questionType === "FILL_IN" && form.answerDataType === "TEXT" ? form.acceptedAnswers.filter((a) => a.trim()) : [],
      correctAnswerNumber: form.answerDataType === "NUMBER" ? form.correctAnswerNumber : null,
      numericTolerance: form.answerDataType === "NUMBER" ? (form.numericTolerance ?? 0) : null,
    };
    save.mutate(body);
  };

  const err = (field: string) => errors[field];

  /** Chèn ảnh minh họa cho câu hỏi (hiển thị trực quan dưới dạng ảnh, không chèn mã ID tự sinh vào nội dung) */
  const handleInsertQuestionImage = (upload: MediaUpload) => {
    const idLower = upload.id.toLowerCase();
    setMedia((m) => ({ ...m, [idLower]: upload.url }));
    setAttachedImages((prev) => [...prev, { id: idLower, url: upload.url }]);
    if (form.contentFormat === "PLAIN") {
      set({ contentFormat: "MARKDOWN" });
    }
  };

  const handleRemoveQuestionImage = (index: number) => {
    setAttachedImages((prev) => prev.filter((_, i) => i !== index));
  };

  /** Upload xong mới chèn nên dùng state mới nhất (người dùng có thể đã gõ tiếp trong lúc tải). */
  const addImage = (upload: MediaUpload, apply: (f: EditorState, markdown: string) => Partial<EditorState>) => {
    setMedia((m) => ({ ...m, [upload.id.toLowerCase()]: upload.url }));
    setForm((f) => ({ ...f, ...apply(f, upload.markdown) }));
  };
  const insertIntoExplanation = (upload: MediaUpload) => {
    const position = explanationRef.current?.selectionStart;
    addImage(upload, (f, md) => ({ explanation: insertAt(f.explanation ?? "", md, position) }));
  };
  const hasChoices = form.questionType !== "FILL_IN" && form.questionType !== "ESSAY";

  return (
    <>
      <Link to="/admin/questions" className="small">← {t("nav.questions")}</Link>
      <PageHeader title={id ? `Sửa câu hỏi ${existing?.code ?? ""}` : "Tạo câu hỏi"}>
        {existing && <ActiveBadge active={existing.isActive} />}
      </PageHeader>
      {save.error && <Alert variant="danger">{describeError(save.error)}</Alert>}
      <Form
        noValidate
        onSubmit={(e) => {
          e.preventDefault();
          submit();
        }}
      >
        <Row className="g-3">
          <Col lg={8}>
            <Card className="mb-3">
              <Card.Body>
                <Row className="g-2">
                  <Col md={4}>
                    <Form.Group controlId="q-code">
                      <Form.Label>{t("common.code")}</Form.Label>
                      <Form.Control
                        value={form.code ?? ""}
                        disabled={!!id}
                        placeholder="Tự sinh nếu để trống (VD: Q000001)"
                        isInvalid={!!err("code")}
                        onChange={(e) => set({ code: e.target.value })}
                      />
                      <Form.Control.Feedback type="invalid">{err("code")}</Form.Control.Feedback>
                    </Form.Group>
                  </Col>
                  <Col md={4}>
                    <Form.Group controlId="q-type">
                      <Form.Label>Loại câu hỏi</Form.Label>
                      <Form.Select value={form.questionType} onChange={(e) => setPendingType(e.target.value as QuestionType)}>
                        {QUESTION_TYPES.map((type) => (
                          <option key={type} value={type}>{t(`enums.questionType.${type}`)}</option>
                        ))}
                      </Form.Select>
                    </Form.Group>
                  </Col>
                  <Col md={4}>
                    <Form.Group controlId="q-category">
                      <Form.Label>{t("nav.categories")}</Form.Label>
                      <Form.Select value={form.categoryId ?? ""} isInvalid={!!err("categoryId")} onChange={(e) => set({ categoryId: e.target.value || null })}>
                        <option value="">(Không có)</option>
                        {categories.data?.items.map((c) => (
                          <option key={c.id} value={c.id}>{c.name}</option>
                        ))}
                      </Form.Select>
                      <Form.Control.Feedback type="invalid">{err("categoryId")}</Form.Control.Feedback>
                    </Form.Group>
                  </Col>
                </Row>
              </Card.Body>
            </Card>

            <Card className="mb-3">
              <Card.Body>
                <Tabs defaultActiveKey="edit" className="mb-2">
                  <Tab eventKey="edit" title="Nội dung">
                    <Form.Group controlId="q-content">
                      <Form.Label className="visually-hidden">Nội dung</Form.Label>
                      <Form.Control ref={contentRef} as="textarea" rows={6} value={form.content} isInvalid={!!err("content")} onChange={(e) => set({ content: e.target.value })} />
                      <Form.Control.Feedback type="invalid">{err("content")}</Form.Control.Feedback>
                    </Form.Group>
                    <div className="d-flex flex-wrap align-items-center gap-2 mt-2">
                      <Form.Check
                        className="me-auto"
                        type="switch"
                        id="q-markdown"
                        label="Dùng Markdown (code block, danh sách, bảng, ảnh)"
                        checked={form.contentFormat === "MARKDOWN"}
                        onChange={(e) => set({ contentFormat: (e.target.checked ? "MARKDOWN" : "PLAIN") as ContentFormat })}
                      />
                      {canUpload && <ImageInsertButton target="đề bài" onInserted={handleInsertQuestionImage} />}
                    </div>
                    {canUpload && <Form.Text>{t("media.hint")}</Form.Text>}

                    {/* Danh sách ảnh đính kèm hiển thị trực quan, không hiển thị mã ID tự sinh */}
                    {attachedImages.length > 0 && (
                      <div className="mt-3 p-2 bg-light border rounded">
                        <div className="small fw-semibold text-secondary mb-2">
                          Ảnh minh họa đã đính kèm ({attachedImages.length}):
                        </div>
                        <div className="d-flex flex-wrap gap-2">
                          {attachedImages.map((img, idx) => (
                            <div key={img.id} className="position-relative border rounded p-1 bg-white shadow-sm">
                              <img
                                src={img.url || (media[img.id] ?? "")}
                                alt={`Ảnh minh họa ${idx + 1}`}
                                style={{ maxHeight: 140, maxWidth: 240, objectFit: "contain", display: "block" }}
                                className="rounded"
                              />
                              <Button
                                size="sm"
                                variant="danger"
                                className="position-absolute top-0 end-0 m-1 py-0 px-2 fw-bold"
                                style={{ lineHeight: "1.2", fontSize: "14px" }}
                                title="Xóa ảnh này"
                                onClick={() => handleRemoveQuestionImage(idx)}
                              >
                                ×
                              </Button>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </Tab>
                  <Tab eventKey="preview" title={t("common.preview")}>
                    <MediaUrls value={media}>
                      <div className="border rounded p-3">
                        <MarkdownView content={fullContentWithImages || "_(trống)_"} format={form.contentFormat} />
                        {hasChoices && (
                          <ul className="list-unstyled mb-0 mt-2">
                            {form.options.map((o, i) => (
                              <li key={i}>
                                <strong>{form.questionType === "TRUE_FALSE" ? "" : `${o.optionCode}. `}</strong>
                                <MarkdownView content={o.content || "_(trống)_"} inline />
                              </li>
                            ))}
                          </ul>
                        )}
                        {form.explanation && (
                          <div className="small mt-2 pt-2 border-top">
                            <strong>Giải thích:</strong> <MarkdownView content={form.explanation} />
                          </div>
                        )}
                      </div>
                    </MediaUrls>
                  </Tab>
                </Tabs>
              </Card.Body>
            </Card>

            <Card className="mb-3">
              <Card.Body>
                <h2 className="h6">Đáp án</h2>
                {err("options") && <Alert variant="danger" className="py-2">{err("options")}</Alert>}
                {form.questionType === "ESSAY" ? (
                  <Alert variant="info" className="small mb-0">
                    Câu tự luận không chấm tự động: học viên viết bài, người có quyền <em>Chấm tay</em> chấm điểm từng bài
                    (tab “Chấm tự luận” ở trang kết quả đề). Ghi đáp án mẫu / hướng dẫn chấm vào phần Giải thích.
                  </Alert>
                ) : form.questionType === "FILL_IN" ? (
                  <FillInEditor form={form} set={set} errors={errors} tryValue={tryValue} setTryValue={setTryValue} />
                ) : (
                  <>
                    <OptionsEditor form={form} set={set} errors={errors} />
                    {form.questionType === "MULTIPLE_CHOICE" && (
                      <Form.Check className="mt-2" type="switch" id="q-partial" checked={form.partialScoring}
                        onChange={(e) => set({ partialScoring: e.target.checked })}
                        label="Chấm từng phần: điểm = điểm câu × (số lựa chọn đúng đã chọn − số lựa chọn sai đã chọn) / số đáp án đúng, không âm" />
                    )}
                  </>
                )}
              </Card.Body>
            </Card>

            <Card>
              <Card.Body>
                <Form.Group controlId="q-explanation">
                  <div className="d-flex align-items-center mb-2">
                    <Form.Label className="me-auto mb-0">Giải thích (hiển thị khi xem lại bài, Markdown)</Form.Label>
                    {canUpload && <ImageInsertButton target="phần giải thích" onInserted={insertIntoExplanation} />}
                  </div>
                  <Form.Control ref={explanationRef} as="textarea" rows={3} value={form.explanation ?? ""} onChange={(e) => set({ explanation: e.target.value })} />
                </Form.Group>
              </Card.Body>
            </Card>
          </Col>
          <Col lg={4}>
            <Card className="sticky-lg-top" style={{ top: 16 }}>
              <Card.Body>
                <Form.Group controlId="q-difficulty" className="mb-3">
                  <Form.Label>Độ khó</Form.Label>
                  <Form.Select value={form.difficulty ?? ""} onChange={(e) => set({ difficulty: (e.target.value || null) as QuestionDifficulty | null })}>
                    <option value="">(Không đặt)</option>
                    {DIFFICULTIES.map((d) => (
                      <option key={d} value={d}>{t(`enums.difficulty.${d}`)}</option>
                    ))}
                  </Form.Select>
                </Form.Group>
                <Form.Group controlId="q-tags" className="mb-3">
                  <Form.Label>Tag</Form.Label>
                  <Form.Control value={tagText} placeholder="ví dụ: oop, linq" isInvalid={!!err("tags")} onChange={(e) => setTagText(e.target.value)} />
                  <Form.Text>Cách nhau bởi dấu phẩy, tối đa 10 tag. Dùng để lọc và lập pool ngẫu nhiên.</Form.Text>
                  <Form.Control.Feedback type="invalid">{err("tags")}</Form.Control.Feedback>
                </Form.Group>
                <Form.Group controlId="q-score" className="mb-3">
                  <Form.Label>Điểm mặc định</Form.Label>
                  <Form.Control type="number" min={0.25} max={100} step={0.25} value={form.defaultScore} isInvalid={!!err("defaultScore")} onChange={(e) => set({ defaultScore: Number(e.target.value) })} />
                  <Form.Control.Feedback type="invalid">{err("defaultScore")}</Form.Control.Feedback>
                </Form.Group>
                <Button type="submit" className="w-100" disabled={save.isPending}>{t("common.save")}</Button>
                {id && hasPermission(Permissions.QuestionUpdate) && (
                  <Button
                    type="button"
                    variant="outline-danger"
                    className="w-100 mt-2"
                    disabled={removeQuestion.isPending}
                    onClick={() => setConfirmDelete(true)}
                  >
                    {t("common.delete")} câu hỏi
                  </Button>
                )}
              </Card.Body>
            </Card>
          </Col>
        </Row>
      </Form>

      <Modal show={confirmDelete} onHide={() => setConfirmDelete(false)} centered>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">Xác nhận xóa câu hỏi</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <p>
            Bạn có chắc chắn muốn xóa câu hỏi <strong>{existing?.code}</strong> này khỏi ngân hàng câu hỏi?
          </p>
          <p className="text-danger small mb-0">
            ⚠️ Thao tác này sẽ xóa vĩnh viễn câu hỏi và không thể hoàn tác.
          </p>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => setConfirmDelete(false)} disabled={removeQuestion.isPending}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="danger"
            onClick={() => removeQuestion.mutate()}
            disabled={removeQuestion.isPending}
          >
            {removeQuestion.isPending ? "Đang xóa..." : t("common.delete")}
          </Button>
        </Modal.Footer>
      </Modal>

      <Modal show={!!pendingType} onHide={() => setPendingType(null)} centered>
        <Modal.Body>Đổi loại câu hỏi sẽ thay đổi danh sách đáp án. Tiếp tục?</Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => setPendingType(null)}>{t("common.cancel")}</Button>
          <Button
            onClick={() => {
              changeType(pendingType!);
              setPendingType(null);
            }}
          >
            {t("common.confirm")}
          </Button>
        </Modal.Footer>
      </Modal>
    </>
  );
}

function OptionsEditor({
  form,
  set,
  errors,
}: {
  form: EditorState;
  set: (p: Partial<EditorState>) => void;
  errors: Record<string, string>;
}) {
  const single = form.questionType !== "MULTIPLE_CHOICE";
  const trueFalse = form.questionType === "TRUE_FALSE";
  const update = (index: number, patch: Partial<EditorState["options"][number]>) =>
    set({
      options: form.options.map((o, i) =>
        i === index ? { ...o, ...patch } : patch.isCorrect && single ? { ...o, isCorrect: false } : o,
      ),
    });
  const move = (index: number, delta: number) => {
    const target = index + delta;
    if (target < 0 || target >= form.options.length) {
      return;
    }
    const next = [...form.options];
    [next[index], next[target]] = [next[target]!, next[index]!];
    set({ options: relabel(next) });
  };
  const relabel = (options: EditorState["options"]) => options.map((o, i) => ({ ...o, optionCode: CHOICE_CODES[i]! }));

  return (
    <fieldset>
      <legend className="visually-hidden">Các lựa chọn</legend>
      {form.options.map((option, index) => (
        <InputGroup className="mb-2" key={index}>
          <InputGroup.Text>
            <Form.Check
              type={single ? "radio" : "checkbox"}
              name="correct-option"
              aria-label={`Đáp án đúng ${option.optionCode}`}
              checked={option.isCorrect}
              onChange={(e) => update(index, { isCorrect: single ? true : e.target.checked })}
            />
            <strong className="ms-2">{trueFalse ? "" : option.optionCode}</strong>
          </InputGroup.Text>
          <Form.Control
            aria-label={`Nội dung lựa chọn ${option.optionCode}`}
            value={option.content}
            readOnly={trueFalse}
            isInvalid={!!errors[`options[${index}].content`]}
            onChange={(e) => update(index, { content: e.target.value })}
          />
          {!trueFalse && (
            <>
              <Button variant="outline-secondary" aria-label="Lên" onClick={() => move(index, -1)}>↑</Button>
              <Button variant="outline-secondary" aria-label="Xuống" onClick={() => move(index, 1)}>↓</Button>
              <Button
                variant="outline-danger"
                aria-label={`Xóa lựa chọn ${option.optionCode}`}
                disabled={form.options.length <= 2}
                onClick={() => set({ options: relabel(form.options.filter((_, i) => i !== index)) })}
              >
                ✕
              </Button>
            </>
          )}
          <Form.Control.Feedback type="invalid">{errors[`options[${index}].content`]}</Form.Control.Feedback>
        </InputGroup>
      ))}
      {!trueFalse && form.options.length < 10 && (
        <Button size="sm" variant="outline-primary" onClick={() => set({ options: relabel([...form.options, { optionCode: "", content: "", isCorrect: false }]) })}>
          + Thêm lựa chọn
        </Button>
      )}
      <Form.Text className="d-block mt-2">{single ? "Chọn đúng 1 đáp án đúng." : "Đánh dấu mọi đáp án đúng; học viên phải chọn đúng toàn bộ mới được điểm."}</Form.Text>
    </fieldset>
  );
}

function FillInEditor({
  form,
  set,
  errors,
  tryValue,
  setTryValue,
}: {
  form: EditorState;
  set: (p: Partial<EditorState>) => void;
  errors: Record<string, string>;
  tryValue: string;
  setTryValue: (v: string) => void;
}) {
  const { t } = useTranslation();
  const accepted = form.acceptedAnswers.filter((a) => a.trim());
  return (
    <>
      <Form.Group className="mb-3" controlId="q-datatype">
        <Form.Label>Kiểu đáp án</Form.Label>
        <Form.Select value={form.answerDataType ?? "TEXT"} onChange={(e) => set({ answerDataType: e.target.value as AnswerDataType })}>
          <option value="TEXT">{t("enums.answerDataType.TEXT")}</option>
          <option value="NUMBER">{t("enums.answerDataType.NUMBER")}</option>
        </Form.Select>
      </Form.Group>
      {form.answerDataType === "NUMBER" ? (
        <Row className="g-2">
          <Col md={6}>
            <Form.Group controlId="q-number">
              <Form.Label>Đáp án số *</Form.Label>
              <Form.Control type="number" step="any" value={form.correctAnswerNumber ?? ""} isInvalid={!!errors.correctAnswerNumber} onChange={(e) => set({ correctAnswerNumber: e.target.value === "" ? null : Number(e.target.value) })} />
              <Form.Control.Feedback type="invalid">{errors.correctAnswerNumber}</Form.Control.Feedback>
            </Form.Group>
          </Col>
          <Col md={6}>
            <Form.Group controlId="q-tolerance">
              <Form.Label>Sai số cho phép (±)</Form.Label>
              <Form.Control type="number" min={0} step="any" value={form.numericTolerance ?? 0} isInvalid={!!errors.numericTolerance} onChange={(e) => set({ numericTolerance: Number(e.target.value) })} />
              <Form.Control.Feedback type="invalid">{errors.numericTolerance}</Form.Control.Feedback>
            </Form.Group>
          </Col>
        </Row>
      ) : (
        <>
          <fieldset>
            <legend className="form-label fs-6">Đáp án chấp nhận (1–20)</legend>
            {form.acceptedAnswers.map((answer, index) => (
              <InputGroup className="mb-2" key={index}>
                <Form.Control
                  aria-label={`Đáp án chấp nhận ${index + 1}`}
                  value={answer}
                  isInvalid={!!errors[`acceptedAnswers[${index}]`]}
                  onChange={(e) => set({ acceptedAnswers: form.acceptedAnswers.map((a, i) => (i === index ? e.target.value : a)) })}
                />
                <Button variant="outline-danger" aria-label="Xóa" disabled={form.acceptedAnswers.length <= 1} onClick={() => set({ acceptedAnswers: form.acceptedAnswers.filter((_, i) => i !== index) })}>
                  ✕
                </Button>
              </InputGroup>
            ))}
            {errors.acceptedAnswers && <div className="text-danger small">{errors.acceptedAnswers}</div>}
            {form.acceptedAnswers.length < 20 && (
              <Button size="sm" variant="outline-primary" onClick={() => set({ acceptedAnswers: [...form.acceptedAnswers, ""] })}>+ Thêm đáp án</Button>
            )}
          </fieldset>
          <Form.Check className="mt-3" type="switch" id="q-case" label="Phân biệt chữ hoa / chữ thường" checked={form.caseSensitive} onChange={(e) => set({ caseSensitive: e.target.checked })} />
          <Form.Check type="switch" id="q-accent" label="Bỏ qua dấu tiếng Việt (ví dụ “ha noi” = “Hà Nội”)" checked={form.ignoreAccent} onChange={(e) => set({ ignoreAccent: e.target.checked })} />
        </>
      )}
      <Form.Group className="mt-3" controlId="q-try">
        <Form.Label>Thử đáp án</Form.Label>
        <InputGroup>
          <Form.Control value={tryValue} onChange={(e) => setTryValue(e.target.value)} placeholder="Nhập câu trả lời để thử" />
          {tryValue && (
            <InputGroup.Text>
              {form.answerDataType === "NUMBER"
                ? !isValidNumberAnswer(tryValue)
                  ? <Badge bg="warning" text="dark">Sai định dạng</Badge>
                  : <TryNumber value={tryValue} answer={form.correctAnswerNumber} tolerance={form.numericTolerance ?? 0} />
                : matchesAny(tryValue, accepted, form.caseSensitive, form.ignoreAccent)
                  ? <Badge bg="success">Đúng</Badge>
                  : <Badge bg="danger">Sai</Badge>}
            </InputGroup.Text>
          )}
        </InputGroup>
        <Form.Text>Kiểm tra nhanh trên trình duyệt; điểm chính thức luôn do máy chủ chấm.</Form.Text>
      </Form.Group>
    </>
  );
}

function TryNumber({ value, answer, tolerance }: { value: string; answer: number | null; tolerance: number }) {
  const parsed = Number(value.trim().replace(",", "."));
  const ok = answer != null && Math.abs(parsed - answer) <= tolerance + 1e-12;
  return ok ? <Badge bg="success">Đúng</Badge> : <Badge bg="danger">Sai</Badge>;
}

const DIFFICULTIES: QuestionDifficulty[] = ["EASY", "MEDIUM", "HARD"];

function parseTags(text: string): string[] {
  return [...new Set(text.split(/[,;]/).map((t) => t.trim().toLowerCase()).filter(Boolean))];
}
