import { useEffect, useMemo, useRef, useState } from "react";
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
import { NameCell, RowActions, StatusSwitch } from "../../components/common/TableCells";
import { SearchableSelect } from "../../components/common/SearchableSelect";
import { MarkdownView } from "../../components/common/MarkdownView";
import { MediaUrls } from "../../components/common/MediaUrls";
import { ImageInsertButton, insertAt } from "./ImageInsertButton";
import { QuestionImportDialog } from "./QuestionImportDialog";
import { useListQuery } from "../../hooks/useListQuery";
import { formatDateTime, formatNumber, markdownExcerpt } from "../../utils/format";
import { isValidNumberAnswer } from "../attempts/playerState";
import { Permissions } from "../../constants/permissions";
import { Features } from "../../constants/features";
import { useAuth } from "../auth/useAuth";
import type { Category, ContentFormat, MediaUpload, MediaUrlMap, QuestionDetail, QuestionDifficulty, QuestionInput, QuestionListItem, QuestionType } from "../../types/api";
import "./categoryTree.css";
import { nextChildCode, validateCategory } from "./categoryForm";

const QUESTION_TYPES: QuestionType[] = ["SINGLE_CHOICE", "MULTIPLE_CHOICE", "TRUE_FALSE", "FILL_IN"];
const CHOICE_CODES = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J"];
const PROPOSITION_CODES = ["a", "b", "c", "d", "e", "f"];

// ======================= Danh mục =======================

type CategoryEdit = {
  id?: string;
  code: string;
  name: string;
  rowVersion?: string;
  isActive?: boolean;
  parentId: string | null;
  childCount?: number;
  /** Mã do hệ thống gợi ý; người dùng chưa sửa tay thì đổi cha sẽ gợi ý lại. */
  autoCode?: string;
};

/**
 * Chuyên đề 2 cấp: chuyên đề gốc có chuyên đề con, chuyên đề gốc không có chuyên đề con, và chuyên đề con.
 * Không tìm kiếm: liệt kê chuyên đề gốc (phân trang), chuyên đề con nằm ngay dưới cha. Có từ khóa: danh sách phẳng.
 */
export function CategoriesPage() {
  const { t } = useTranslation();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(Permissions.CategoryManage);
  const list = useListQuery({ sortBy: "name", sortDir: "asc" });
  const searching = !!list.state.keyword;
  const query = useQuery({
    queryKey: ["categories", list.params, searching],
    queryFn: () => categoriesApi.list(searching ? list.params : { ...list.params, rootOnly: true }),
  });
  // Toàn bộ chuyên đề (cả đang tắt) để gắn chuyên đề con dưới cha và chọn chuyên đề cha
  const all = useQuery({ queryKey: ["categories", "all-tree"], queryFn: () => categoriesApi.list({ pageSize: 100, sortBy: "name" }) });
  const [editing, setEditing] = useState<CategoryEdit | null>(null);
  const [deleting, setDeleting] = useState<Category | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  // Chuyên đề cha mặc định thu gọn; bấm mũi tên để mở / thu các chuyên đề con
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const toggleExpanded = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const roots = useMemo(() => (all.data?.items ?? []).filter((c) => c.parentId === null), [all.data]);
  const rows = useMemo(() => {
    if (!query.data) return undefined;
    if (searching) return query.data;
    const children = (all.data?.items ?? []).filter((c) => c.parentId !== null);
    const items = query.data.items.flatMap((root) => [
      root,
      ...(expanded.has(root.id) ? children.filter((c) => c.parentId === root.id).sort((a, b) => a.name.localeCompare(b.name, "vi")) : []),
    ]);
    return { ...query.data, items };
  }, [query.data, all.data, searching, expanded]);

  useEffect(() => {
    setSelectedIds(new Set());
  }, [query.data]);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["categories"] });
    void queryClient.invalidateQueries({ queryKey: ["sidebar-categories"] });
  };

  // Xóa chuyên đề con trước rồi mới tới chuyên đề cha (cha còn con thì không xóa được)
  const removeMany = useMutation({
    mutationFn: async (ids: string[]) => {
      const items = rows?.items.filter((c) => ids.includes(c.id)) ?? [];
      await Promise.all(items.filter((c) => c.parentId !== null).map((c) => categoriesApi.remove(c.id, true)));
      await Promise.all(items.filter((c) => c.parentId === null).map((c) => categoriesApi.remove(c.id, true)));
    },
    onSuccess: () => {
      invalidate();
      toast.success(`Đã xóa thành công ${selectedIds.size} chuyên đề.`);
      setSelectedIds(new Set());
    },
    onError: (e) => {
      invalidate();
      toast.error(e);
    },
  });

  const handleBulkDelete = () => {
    if (!rows) return;
    const selectedCategories = rows.items.filter((c) => selectedIds.has(c.id));
    const totalQuestions = selectedCategories.reduce((sum, c) => sum + c.questionCount, 0);

    let confirmMsg = `Bạn có chắc chắn muốn xóa ${selectedIds.size} chuyên đề đã chọn?`;
    if (totalQuestions > 0) {
      confirmMsg = `Trong số các chuyên đề đã chọn, có chứa tổng cộng ${totalQuestions} câu hỏi. Việc xóa sẽ làm giải phóng các câu hỏi bên trong.\n\n` + confirmMsg;
    }

    if (window.confirm(confirmMsg)) {
      removeMany.mutate(Array.from(selectedIds));
    }
  };

  const save = useMutation({
    mutationFn: () =>
      editing!.id
        ? categoriesApi.update(editing!.id, {
            name: editing!.name,
            isActive: editing!.isActive ?? true,
            rowVersion: editing!.rowVersion!,
            parentId: editing!.parentId,
          })
        : categoriesApi.create({ code: editing!.code, name: editing!.name, parentId: editing!.parentId }),
    onSuccess: () => {
      invalidate();
      setEditing(null);
      toast.success(t("common.saved"));
    },
  });

  const toggle = useMutation({
    mutationFn: (c: Category) => categoriesApi.setStatus(c.id, !c.isActive),
    onSuccess: invalidate,
    onError: (e) => toast.error(e),
  });

  const remove = useMutation({
    mutationFn: (c: Category) => categoriesApi.remove(c.id, true),
    onSuccess: () => {
      invalidate();
      setDeleting(null);
      toast.success("Đã xóa chuyên đề thành công.");
    },
    onError: (e) => toast.error(e),
  });

  const isAllSelected = rows?.items.length && rows.items.every((c) => selectedIds.has(c.id));
  // Chỉ hiện lỗi sau lần bấm Lưu đầu tiên, để form mới mở không đỏ ngay
  const [submitted, setSubmitted] = useState(false);
  const formErrors = editing ? validateCategory(editing) : {};
  const openEditor = (next: CategoryEdit) => {
    setSubmitted(false);
    save.reset();
    setEditing(next);
  };
  const edit = (c: Category) =>
    openEditor({ id: c.id, code: c.code, name: c.name, rowVersion: c.rowVersion, isActive: c.isActive, parentId: c.parentId, childCount: c.childCount });
  const allCodes = () => (all.data?.items ?? []).map((c) => c.code);
  const codeForParent = (parentId: string | null) => {
    const parent = parentId ? roots.find((r) => r.id === parentId) : undefined;
    return parent ? nextChildCode(parent.code, allCodes()) : "";
  };
  const addChild = (parent: Category) => {
    setExpanded((prev) => new Set(prev).add(parent.id));
    const code = nextChildCode(parent.code, allCodes());
    openEditor({ code, autoCode: code, name: "", parentId: parent.id });
  };
  const canAddChild = (c: Category) => c.parentId === null && c.questionCount === 0;
  const addChildBlockedReason = "Chuyên đề đang có câu hỏi: chuyển câu hỏi sang chuyên đề khác trước khi tạo chuyên đề con";

  const columns: Column<Category>[] = [
    {
      key: "select",
      header: (
        <Form.Check
          type="checkbox"
          checked={!!isAllSelected}
          onChange={(e) => {
            if (e.target.checked && rows) {
              setSelectedIds(new Set(rows.items.map((c) => c.id)));
            } else {
              setSelectedIds(new Set());
            }
          }}
        />
      ),
      render: (c) => (
        <Form.Check
          type="checkbox"
          checked={selectedIds.has(c.id)}
          onChange={(e) => {
            const next = new Set(selectedIds);
            if (e.target.checked) next.add(c.id);
            else next.delete(c.id);
            setSelectedIds(next);
          }}
        />
      ),
    },
    {
      key: "name",
      header: "Chuyên đề",
      sortKey: "name",
      render: (c) => {
        const isChild = c.parentId !== null && !searching;
        const open = expanded.has(c.id);
        const hasChildren = c.parentId === null && c.childCount > 0;
        return (
          <div className={`cat-tree${isChild ? " cat-tree--child" : ""}`}>
            {!isChild && !searching && (
              <button
                type="button"
                className={`cat-tree__toggle${hasChildren ? "" : " is-empty"}`}
                onClick={() => toggleExpanded(c.id)}
                aria-expanded={open}
                aria-label={`${open ? "Thu gọn" : "Mở"} chuyên đề con của ${c.name}`}
                tabIndex={hasChildren ? 0 : -1}
              >
                <i className="bi bi-chevron-right" aria-hidden="true" />
              </button>
            )}
            <div className="cat-tree__text">
              {/* Bấm tên hoặc mã để vào danh sách câu hỏi của chuyên đề (giống bấm số câu hỏi) */}
              <div>
                <Link className="cat-tree__link cat-tree__name" to={`/admin/questions?categoryId=${c.id}`} title="Xem câu hỏi của chuyên đề">
                  {c.name}
                </Link>
              </div>
              <div className="cat-tree__meta">
                {searching && c.parentName && <>{c.parentName} › </>}
                {/* Cùng đích với tên nên bỏ khỏi thứ tự Tab, tránh đọc hai lần */}
                <Link className="cat-tree__link" to={`/admin/questions?categoryId=${c.id}`} tabIndex={-1} title="Xem câu hỏi của chuyên đề">
                  {c.code}
                </Link>
                {hasChildren && <> · {c.childCount} chuyên đề con</>}
              </div>
            </div>
          </div>
        );
      },
    },
    {
      key: "count",
      header: "Số câu hỏi",
      className: "text-end text-nowrap",
      render: (c) => (
        <Link className="cat-count" to={`/admin/questions?categoryId=${c.id}`} title={c.childCount > 0 ? "Gồm cả câu của các chuyên đề con" : undefined}>
          {c.childCount > 0 ? c.totalQuestionCount : c.questionCount}
          {c.childCount > 0 && c.questionCount > 0 && <span className="small text-secondary ms-1">({c.questionCount} câu trực thuộc)</span>}
        </Link>
      ),
    },
    {
      key: "status",
      header: t("common.status"),
      className: "text-nowrap",
      render: (c) => (
        <StatusSwitch
          active={c.isActive}
          label={`chuyên đề ${c.name}`}
          disabled={toggle.isPending && toggle.variables?.id === c.id}
          onToggle={canManage ? () => toggle.mutate(c) : undefined}
        />
      ),
    },
    {
      key: "actions",
      header: "",
      className: "text-end text-nowrap",
      render: (c) => (
        <RowActions
          label={c.name}
          actions={[
            { label: t("common.edit"), icon: "bi-pencil", onClick: () => edit(c), show: canManage },
            {
              label: "Thêm chuyên đề con",
              icon: "bi-plus-lg",
              onClick: () => addChild(c),
              show: canManage && c.parentId === null,
              disabled: !canAddChild(c),
              title: canAddChild(c) ? undefined : addChildBlockedReason,
            },
            { label: t("common.delete"), icon: "bi-trash3", danger: true, onClick: () => setDeleting(c), show: canManage },
          ]}
        />
      ),
    },
  ];

  // Chuyên đề cha chỉ gom nhóm nên không được đang chứa câu hỏi (D-32)
  const parentOptions = roots.filter((r) => r.id !== editing?.id && (r.questionCount === 0 || r.id === editing?.parentId));

  return (
    <>
      <PageHeader
        title={t("nav.categories")}
        actions={
          canManage && (
            <div className="d-flex gap-2">
              {selectedIds.size > 0 && (
                <Button variant="danger" onClick={handleBulkDelete} disabled={removeMany.isPending}>
                  {removeMany.isPending ? "Đang xóa..." : `Xóa đã chọn (${selectedIds.size})`}
                </Button>
              )}
              <Button onClick={() => openEditor({ code: "", name: "", parentId: null })}>{t("common.create")}</Button>
            </div>
          )
        }
      >
        <div className="small text-secondary">
          Chuyên đề có 2 cấp: chuyên đề gốc có thể có hoặc không có chuyên đề con. Chuyên đề có chuyên đề con chỉ để gom nhóm, câu hỏi nằm ở các
          chuyên đề con.
        </div>
      </PageHeader>
      <div className="mb-3" style={{ maxWidth: 400 }}>
        <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã hoặc tên danh mục..." />
      </div>
      <DataTable
        data={rows}
        columns={columns}
        rowKey={(c) => c.id}
        isLoading={query.isLoading}
        error={query.error}
        sort={list.state}
        onSort={list.toggleSort}
        onPage={list.setPage}
      />

      {/* Modal Sửa / Thêm */}
      <Modal show={!!editing} onHide={() => setEditing(null)} centered>
        <Form
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            setSubmitted(true);
            if (Object.keys(formErrors).length > 0) return;
            save.mutate();
          }}
        >
          <Modal.Header closeButton>
            <Modal.Title as="h2" className="h5">
              {editing?.id ? t("common.edit") : t("common.create")} {editing?.parentId ? "chuyên đề con" : "chuyên đề"}
            </Modal.Title>
          </Modal.Header>
          <Modal.Body>
            {save.error && <Alert variant="danger">{describeError(save.error)}</Alert>}
            {editing && (
              <>
                <Form.Group className="mb-3" controlId="cat-parent">
                  <Form.Label>Thuộc chuyên đề</Form.Label>
                  <Form.Select
                    value={editing.parentId ?? ""}
                    disabled={(editing.childCount ?? 0) > 0}
                    onChange={(e) => {
                      const parentId = e.target.value || null;
                      // Mã đang là mã gợi ý (hoặc trống) thì gợi ý lại theo chuyên đề cha mới
                      if (!editing.id && (editing.code === "" || editing.code === editing.autoCode)) {
                        const code = codeForParent(parentId);
                        setEditing({ ...editing, parentId, code, autoCode: code });
                      } else setEditing({ ...editing, parentId });
                    }}
                  >
                    <option value="">— Chuyên đề gốc (không có cha) —</option>
                    {parentOptions.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.name} ({r.code})
                      </option>
                    ))}
                  </Form.Select>
                  <Form.Text className="text-secondary">
                    {(editing.childCount ?? 0) > 0
                      ? "Chuyên đề này đang có chuyên đề con nên luôn là chuyên đề gốc."
                      : "Chọn chuyên đề cha để tạo chuyên đề con. Chỉ có 2 cấp: chuyên đề con không có con."}
                  </Form.Text>
                </Form.Group>
                <Form.Group className="mb-3" controlId="cat-code">
                  <Form.Label>{t("common.code")} *</Form.Label>
                  <Form.Control
                    value={editing.code}
                    disabled={!!editing.id}
                    required
                    placeholder="VD: TOAN-12, HOA-HOC-10"
                    isInvalid={submitted && !!formErrors.code}
                    onChange={(e) => setEditing({ ...editing, code: e.target.value })}
                  />
                  <Form.Control.Feedback type="invalid">{formErrors.code}</Form.Control.Feedback>
                  {!editing.id && editing.parentId && !(submitted && formErrors.code) && (
                    <Form.Text className="text-secondary">Mã được gợi ý theo chuyên đề cha, có thể sửa lại.</Form.Text>
                  )}
                </Form.Group>
                <Form.Group className="mb-3" controlId="cat-name">
                  <Form.Label>{t("common.name")} *</Form.Label>
                  <Form.Control
                    value={editing.name}
                    required
                    autoFocus={!editing.id}
                    placeholder="VD: Đại số & Giải tích 12"
                    isInvalid={submitted && !!formErrors.name}
                    onChange={(e) => setEditing({ ...editing, name: e.target.value })}
                  />
                  <Form.Control.Feedback type="invalid">{formErrors.name}</Form.Control.Feedback>
                </Form.Group>
              </>
            )}
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onClick={() => setEditing(null)}>
              {t("common.cancel")}
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {t("common.save")}
            </Button>
          </Modal.Footer>
        </Form>
      </Modal>

      {/* Modal Xác nhận xóa */}
      <Modal show={!!deleting} onHide={() => setDeleting(null)} centered>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            Xác nhận xóa chuyên đề
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {deleting && (
            <div>
              <p>
                Bạn có chắc chắn muốn xóa chuyên đề <strong>{deleting.name}</strong> (<code>{deleting.code}</code>)?
              </p>
              {deleting.childCount > 0 ? (
                <Alert variant="danger" className="mb-0">
                  Chuyên đề này đang có <strong>{deleting.childCount}</strong> chuyên đề con. Hãy xóa hoặc chuyển các chuyên đề con sang chuyên đề khác
                  trước.
                </Alert>
              ) : deleting.questionCount > 0 ? (
                <Alert variant="warning" className="mb-0">
                  Chuyên đề này đang chứa <strong>{deleting.questionCount}</strong> câu hỏi. Thao tác này sẽ xóa chuyên đề và giải phóng các câu hỏi
                  liên quan.
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
          <Button variant="danger" onClick={() => deleting && remove.mutate(deleting)} disabled={remove.isPending || (deleting?.childCount ?? 0) > 0}>
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
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    setSelectedIds(new Set());
  }, [query.data]);

  const removeMany = useMutation({
    mutationFn: async (ids: string[]) => {
      await Promise.all(ids.map((id) => questionsApi.remove(id, true)));
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["questions"] });
      toast.success(`Đã xóa thành công ${selectedIds.size} câu hỏi.`);
      setSelectedIds(new Set());
    },
    onError: (e) => toast.error(e),
  });

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

  const isAllSelected = query.data?.items.length && query.data?.items.every((q) => selectedIds.has(q.id));

  const columns: Column<QuestionListItem>[] = [
    {
      key: "select",
      header: (
        <Form.Check
          type="checkbox"
          checked={!!isAllSelected}
          onChange={(e) => {
            if (e.target.checked && query.data) {
              setSelectedIds(new Set(query.data.items.map((q) => q.id)));
            } else {
              setSelectedIds(new Set());
            }
          }}
        />
      ),
      render: (q) => (
        <Form.Check
          type="checkbox"
          checked={selectedIds.has(q.id)}
          onChange={(e) => {
            const next = new Set(selectedIds);
            if (e.target.checked) next.add(q.id);
            else next.delete(q.id);
            setSelectedIds(next);
          }}
        />
      ),
    },
    {
      // Nội dung là phần chính, mã (bấm để sửa) nằm dưới màu xám; màn hẹp kèm thêm chuyên đề
      key: "content",
      header: "Câu hỏi",
      sortKey: "code",
      className: "cell-content",
      render: (q) => {
        const excerpt = markdownExcerpt(q.contentPreview);
        return (
          <NameCell
            plain
            name={
              <div className="cell-clamp-2" title={excerpt}>
                <MarkdownView content={excerpt} inline />
              </div>
            }
            code={<Link to={`/admin/questions/${q.id}/edit`}>{q.code}</Link>}
            separator={false}
            meta={q.categoryName && <span className="d-xxl-none"> · {q.categoryName}</span>}
          />
        );
      },
    },
    {
      key: "type",
      header: "Loại",
      className: "text-nowrap",
      render: (q) => (
        <>
          {t(`enums.questionType.${q.questionType}`)}
          {q.answerDataType && <div className="small text-secondary">{t(`enums.answerDataType.${q.answerDataType}`)}</div>}
        </>
      ),
    },
    { key: "category", header: t("nav.categories"), className: "cell-min-8 d-none d-xxl-table-cell", render: (q) => q.categoryName ?? "—" },
    {
      key: "difficulty",
      header: "Độ khó / tag",
      className: "cell-min-8",
      render: (q) => (
        <div className="d-flex flex-wrap gap-1">
          {q.difficulty && <span className="cell-tag">{t(`enums.difficulty.${q.difficulty}`)}</span>}
          {q.tags.map((tag) => (
            <span key={tag} className="cell-tag">{tag}</span>
          ))}
        </div>
      ),
    },
    { key: "score", header: "Điểm", className: "text-center text-nowrap", render: (q) => formatNumber(q.defaultScore) },
    {
      key: "status",
      header: t("common.status"),
      className: "text-nowrap",
      render: (q) => (
        <StatusSwitch
          active={q.isActive}
          label={`câu hỏi ${q.code}`}
          disabled={action.isPending}
          onToggle={hasPermission(Permissions.QuestionUpdate) ? () => action.mutate(() => questionsApi.setStatus(q.id, !q.isActive)) : undefined}
        />
      ),
    },
    {
      key: "updated",
      header: t("common.updatedAt"),
      sortKey: "updatedAt",
      // Màn nhỏ hơn 1400px (còn trừ menu trái): ẩn để nhường chỗ cho nội dung
      className: "text-nowrap small d-none d-xxl-table-cell",
      render: (q) => formatDateTime(q.updatedAt ?? q.createdAt),
    },
    {
      key: "actions",
      header: <span className="visually-hidden">{t("common.actions")}</span>,
      className: "text-end text-nowrap",
      render: (q) => (
        <RowActions
          label={`câu hỏi ${q.code}`}
          actions={[
            { label: t("common.edit"), icon: "bi-pencil", to: `/admin/questions/${q.id}/edit`, show: hasPermission(Permissions.QuestionUpdate) },
            {
              label: t("common.clone"),
              icon: "bi-copy",
              show: hasPermission(Permissions.QuestionCreate),
              onClick: () =>
                action.mutate(async () => {
                  const clone = await questionsApi.clone(q.id);
                  navigate(`/admin/questions/${clone.id}/edit`);
                  return clone;
                }),
            },
            { label: t("common.delete"), icon: "bi-trash3", danger: true, onClick: () => setDeleting(q), show: hasPermission(Permissions.QuestionUpdate) },
          ]}
        />
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
              {selectedIds.size > 0 && (
                <Button variant="danger" onClick={() => {
                  if (window.confirm(`Bạn có chắc chắn muốn xóa ${selectedIds.size} câu hỏi đã chọn?`)) {
                    removeMany.mutate(Array.from(selectedIds));
                  }
                }} disabled={removeMany.isPending}>
                  {removeMany.isPending ? "Đang xóa..." : `Xóa đã chọn (${selectedIds.size})`}
                </Button>
              )}
              <Link to="/admin/questions/import-markdown" className="btn btn-outline-primary">
                <i className="bi bi-filetype-md me-1" aria-hidden="true" />Nhập đề từ file
              </Link>
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
                Thao tác này sẽ xóa vĩnh viễn câu hỏi khỏi ngân hàng câu hỏi.
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
      <Col xs={12} md={6} xl={4} xxl={3}>
        <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã hoặc nội dung..." />
      </Col>
      <Col xs={6} md={3} xl={2}>
        <Form.Select size="sm" aria-label="Độ khó" value={String(list.state.difficulty ?? "")} onChange={(e) => list.setFilter({ difficulty: e.target.value })}>
          <option value="">Mọi độ khó</option>
          {DIFFICULTIES.map((d) => (
            <option key={d} value={d}>{t(`enums.difficulty.${d}`)}</option>
          ))}
        </Form.Select>
      </Col>
      {Features.tagFilter && (
        <Col xs={6} md={3} xl={2}>
          <Form.Control size="sm" aria-label="Tag" placeholder="Tag" defaultValue={String(list.state.tag ?? "")}
            onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); list.setFilter({ tag: e.currentTarget.value.trim() }); } }}
            onBlur={(e) => list.setFilter({ tag: e.currentTarget.value.trim() })} />
        </Col>
      )}
      <Col xs={12} md={6} xl={4} xxl={3}>
        <SearchableSelect
          size="sm"
          value={String(list.state.categoryId ?? "")}
          onChange={(val) => list.setFilter({ categoryId: val })}
          options={categories.map((c) => ({
            value: c.id,
            label: c.name,
            code: c.code,
            sublabel: c.questionCount ? `${c.questionCount} câu` : undefined,
          }))}
          emptyLabel="Mọi danh mục"
          emptyValue=""
          placeholder="Mọi danh mục"
          searchPlaceholder="Tìm mã hoặc tên..."
        />
      </Col>
      <Col xs={6} md={3} xl={2} xxl={1}>
        <Form.Select size="sm" aria-label="Loại câu hỏi" value={String(list.state.questionType ?? "")} onChange={(e) => list.setFilter({ questionType: e.target.value })}>
          <option value="">Mọi loại</option>
          {QUESTION_TYPES.map((type) => (
            <option key={type} value={type}>{t(`enums.questionType.${type}`)}</option>
          ))}
        </Form.Select>
      </Col>
      <Col xs={6} md={3} xl={2} xxl={1}>
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
    const base = current.filter((o) => PROPOSITION_CODES.includes(o.optionCode.toLowerCase()));
    return base.length >= 1
      ? base
      : [
          { optionCode: "a", content: "", isCorrect: true },
          { optionCode: "b", content: "", isCorrect: false },
          { optionCode: "c", content: "", isCorrect: false },
          { optionCode: "d", content: "", isCorrect: true },
        ];
  }
  if (type === "FILL_IN") {
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
  const categoryOptions = useMemo(() => {
    return (
      // Chuyên đề có chuyên đề con chỉ gom nhóm, không chứa câu hỏi: chỉ chọn chuyên đề con hoặc chuyên đề không có con (D-32)
      categories.data?.items
        .filter((c) => !c.childCount)
        .map((c) => ({
          value: c.id,
          label: c.name,
          code: c.code,
          sublabel: c.questionCount ? `${c.questionCount} câu` : undefined,
        })) ?? []
    );
  }, [categories.data]);
  const [form, setForm] = useState<EditorState>(() => (existing ? toEditorState(existing) : emptyQuestion()));
  const [errors, setErrors] = useState<Record<string, string>>({});
  // Câu đã có: mở ở chế độ xem trước (công thức hiển thị như học viên thấy); tab "Sửa nội dung" mới hiện ô soạn mã.
  // Câu mới: soạn ngay.
  const [mode, setMode] = useState<"preview" | "edit">(existing ? "preview" : "edit");
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
    onError: (e) => {
      setErrors(toApiError(e).fieldErrors());
      // Lỗi nhập liệu: mở ô soạn để thấy ô báo lỗi
      setMode("edit");
    },
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
      answerDataType: type === "FILL_IN" ? (form.answerDataType ?? "NUMBER") : null,
    });

  const submit = () => {
    setErrors({});
    const body: QuestionInput = {
      ...form,
      content: fullContentWithImages,
      code: form.code?.trim() || null,
      tags: parseTags(tagText),
      options: form.questionType === "FILL_IN" ? [] : form.options,
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
                      <SearchableSelect
                        id="q-category"
                        value={form.categoryId ?? ""}
                        isInvalid={!!err("categoryId")}
                        onChange={(val) => set({ categoryId: val || null })}
                        options={categoryOptions}
                        emptyLabel="(Không có chuyên đề)"
                        emptyValue=""
                        placeholder="Chọn hoặc tìm chuyên đề..."
                        searchPlaceholder="Nhập mã hoặc tên chuyên đề..."
                      />
                      <Form.Control.Feedback type="invalid">{err("categoryId")}</Form.Control.Feedback>
                    </Form.Group>
                  </Col>
                </Row>
              </Card.Body>
            </Card>

            <Card className="mb-3">
              <Card.Body>
                <Tabs activeKey={mode} onSelect={(key) => setMode(key === "edit" ? "edit" : "preview")} className="mb-2">
                  <Tab eventKey="preview" title={<><i className="bi bi-eye me-1" aria-hidden="true" />{t("common.preview")}</>}>
                    {/* Xem trước gộp cả đề, đáp án và lời giải như học viên thấy; tab "Nội dung" mới hiện các ô soạn */}
                    <MediaUrls value={media}>
                      <div className="border rounded p-3 bg-white">
                        <MarkdownView content={fullContentWithImages || t("questionEditor.noContent")} format={form.contentFormat} />
                        <div className="mt-3">
                          <div className="small fw-semibold text-secondary mb-2">{t("questionEditor.answers")}</div>
                          {form.questionType === "FILL_IN" ? <FillInPreview form={form} /> : <OptionsPreview form={form} />}
                        </div>
                        {form.explanation?.trim() && (
                          <div className="mt-3 p-2 px-3 rounded bg-info-subtle border border-info-subtle small">
                            <div className="fw-semibold mb-1">
                              <i className="bi bi-lightbulb me-1" aria-hidden="true" />
                              {t("questionEditor.explanation")}
                            </div>
                            <MarkdownView content={form.explanation} />
                          </div>
                        )}
                      </div>
                    </MediaUrls>
                    <div className="small text-secondary mt-2">
                      <i className="bi bi-info-circle me-1" aria-hidden="true" />
                      {t("questionEditor.previewHint")}
                    </div>
                  </Tab>
                  <Tab eventKey="edit" title={<><i className="bi bi-pencil-square me-1" aria-hidden="true" />Nội dung</>}>
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
                </Tabs>
              </Card.Body>
            </Card>

            {/* Chế độ xem trước: đáp án và lời giải đã hiện trong tab Xem trước; các khung soạn chỉ hiện ở tab Nội dung */}
            {mode === "edit" && (
            <>
            <Card className="mb-3">
              <Card.Body>
                <h2 className="h6">Đáp án</h2>
                {err("options") && <Alert variant="danger" className="py-2">{err("options")}</Alert>}
                {form.questionType === "FILL_IN" ? (
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
            </>
            )}
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
            Thao tác này sẽ xóa vĩnh viễn câu hỏi và không thể hoàn tác.
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

/**
 * Phương án ở chế độ xem trước: nội dung hiển thị như học viên thấy (công thức, hình), đánh dấu đáp án đúng;
 * câu Đúng/Sai nhiều mệnh đề ghi Đúng / Sai cho từng mệnh đề.
 */
function OptionsPreview({ form }: { form: EditorState }) {
  const { t } = useTranslation();
  const propositions = form.questionType === "TRUE_FALSE";
  return (
    <div className="d-grid gap-2">
      {form.options.map((o) => (
        <div
          key={o.optionCode}
          className={`d-flex align-items-center gap-2 px-3 py-2 rounded border ${
            !propositions && o.isCorrect ? "border-success bg-success-subtle" : "bg-white"
          }`}
        >
          <strong>{propositions ? `${o.optionCode})` : `${o.optionCode}.`}</strong>
          <div className="flex-grow-1 min-w-0">
            <MarkdownView content={o.content || "…"} inline />
          </div>
          {propositions ? (
            <Badge bg={o.isCorrect ? "success" : "danger"}>{o.isCorrect ? t("questionEditor.statementTrue") : t("questionEditor.statementFalse")}</Badge>
          ) : (
            o.isCorrect && (
              <Badge bg="success">
                <i className="bi bi-check-lg me-1" aria-hidden="true" />
                {t("questionEditor.correct")}
              </Badge>
            )
          )}
        </div>
      ))}
    </div>
  );
}

/** Câu điền đáp án ở chế độ xem trước: đáp án số (kèm sai số) hoặc các đáp án chữ được chấp nhận. */
function FillInPreview({ form }: { form: EditorState }) {
  const { t } = useTranslation();
  const answers =
    form.answerDataType === "NUMBER"
      ? form.correctAnswerNumber == null
        ? []
        : [`${formatNumber(form.correctAnswerNumber)}${form.numericTolerance ? ` (± ${formatNumber(form.numericTolerance)})` : ""}`]
      : form.acceptedAnswers.filter((a) => a.trim());
  if (answers.length === 0) {
    return <p className="text-secondary small mb-0">{t("questionEditor.noAnswer")}</p>;
  }
  return (
    <div className="d-flex flex-wrap gap-2">
      {answers.map((a) => (
        <span key={a} className="px-3 py-2 rounded border border-success bg-success-subtle fw-semibold">
          <i className="bi bi-check-lg me-1 text-success" aria-hidden="true" />
          {a}
        </span>
      ))}
    </div>
  );
}

/** Phương án / mệnh đề có công thức ($…$) hoặc định dạng Markdown: ô nhập giữ nguyên văn, dòng này cho thấy kết quả hiển thị. */
function needsRenderedPreview(content: string): boolean {
  return /\$[^$]+\$|\*\*|!\[/.test(content);
}

function OptionRenderedPreview({ content }: { content: string }) {
  if (!needsRenderedPreview(content)) {
    return null;
  }
  return (
    <div className="w-100 small text-secondary pt-1 ps-2">
      <i className="bi bi-eye me-1" aria-hidden="true" />
      <MarkdownView content={content} inline />
    </div>
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
  if (trueFalse) {
    const updateProposition = (index: number, patch: Partial<EditorState["options"][number]>) => {
      set({
        options: form.options.map((o, i) => (i === index ? { ...o, ...patch } : o)),
      });
    };
    const addProposition = () => {
      if (form.options.length >= PROPOSITION_CODES.length) return;
      const nextCode = PROPOSITION_CODES[form.options.length]!;
      set({
        options: [...form.options, { optionCode: nextCode, content: "", isCorrect: true }],
      });
    };
    const removeProposition = (index: number) => {
      const next = form.options.filter((_, i) => i !== index);
      const relabeled = next.map((o, i) => ({ ...o, optionCode: PROPOSITION_CODES[i]! }));
      set({ options: relabeled });
    };

    return (
      <fieldset>
        <legend className="form-label fs-6 fw-bold mb-2">
          {form.options.length === 1
            ? "Nhận định câu hỏi Đúng / Sai (chọn Đúng hoặc Sai):"
            : "Các mệnh đề phát biểu (chọn Đúng hoặc Sai cho từng mệnh đề):"}
        </legend>
        {form.options.map((option, index) => (
          <InputGroup className="mb-2" key={option.optionCode}>
            <InputGroup.Text className="bg-light fw-bold" style={{ minWidth: 48, justifyContent: "center" }}>
              {form.options.length === 1 ? "Ý kiến:" : `${option.optionCode})`}
            </InputGroup.Text>
            <Form.Control
              placeholder={
                form.options.length === 1
                  ? "Nhập nhận định / khẳng định cần xác định Đúng hoặc Sai..."
                  : `Nội dung mệnh đề ${option.optionCode}...`
              }
              value={option.content}
              isInvalid={!!errors[`options[${index}].content`]}
              onChange={(e) => updateProposition(index, { content: e.target.value })}
            />
            <div className="btn-group" role="group" aria-label={`Đáp án cho ${option.optionCode}`}>
              <Button
                size="sm"
                variant={option.isCorrect ? "success" : "outline-secondary"}
                onClick={() => updateProposition(index, { isCorrect: true })}
                title="Đánh dấu mệnh đề này là Đúng"
              >
                <i className="bi bi-check2 me-1" />
                Đúng
              </Button>
              <Button
                size="sm"
                variant={!option.isCorrect ? "danger" : "outline-secondary"}
                onClick={() => updateProposition(index, { isCorrect: false })}
                title="Đánh dấu mệnh đề này là Sai"
              >
                <i className="bi bi-x me-1" />
                Sai
              </Button>
            </div>
            {form.options.length > 1 && (
              <Button
                variant="outline-danger"
                aria-label={`Xóa mệnh đề ${option.optionCode}`}
                onClick={() => removeProposition(index)}
                title="Xóa dòng này"
              >
                ✕
              </Button>
            )}
            <Form.Control.Feedback type="invalid">{errors[`options[${index}].content`]}</Form.Control.Feedback>
            <OptionRenderedPreview content={option.content} />
          </InputGroup>
        ))}
        {form.options.length < 6 && (
          <Button size="sm" variant="outline-primary" className="mt-1" onClick={addProposition}>
            + Thêm mệnh đề ({PROPOSITION_CODES[form.options.length]})
          </Button>
        )}
        <Form.Text className="d-block mt-2 text-muted">
          {form.options.length === 1
            ? "Dạng Đúng/Sai đơn: Học sinh sẽ làm bài bằng cách chọn 1 trong 2 nút [Đúng] hoặc [Sai]."
            : "Dạng Đúng/Sai đa mệnh đề: Học sinh sẽ chọn Đúng hoặc Sai cho từng mệnh đề độc lập (a, b, c, d...). Điểm chia đều theo số ý đúng."}
        </Form.Text>
      </fieldset>
    );
  }

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
            <strong className="ms-2">{option.optionCode}</strong>
          </InputGroup.Text>
          <Form.Control
            aria-label={`Nội dung lựa chọn ${option.optionCode}`}
            value={option.content}
            isInvalid={!!errors[`options[${index}].content`]}
            onChange={(e) => update(index, { content: e.target.value })}
          />
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
          <Form.Control.Feedback type="invalid">{errors[`options[${index}].content`]}</Form.Control.Feedback>
          <OptionRenderedPreview content={option.content} />
        </InputGroup>
      ))}
      {form.options.length < 10 && (
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
  // Luôn đảm bảo kiểu đáp án là NUMBER
  useEffect(() => {
    if (form.answerDataType !== "NUMBER") {
      set({ answerDataType: "NUMBER" });
    }
  }, [form.answerDataType, set]);

  const initialVal =
    form.correctAnswerNumber != null
      ? String(form.correctAnswerNumber).replace(".", ",")
      : form.acceptedAnswers[0] || "";

  const [inputVal, setInputVal] = useState(initialVal);

  useEffect(() => {
    const current =
      form.correctAnswerNumber != null
        ? String(form.correctAnswerNumber).replace(".", ",")
        : form.acceptedAnswers[0] || "";
    if (current && current !== inputVal && !inputVal.endsWith(",") && inputVal !== "-") {
      setInputVal(current);
    }
  }, [form.correctAnswerNumber, form.acceptedAnswers]);

  const handleInputChange = (raw: string) => {
    // Chỉ cho phép các ký tự: 0-9, dấu -, dấu ,, dấu .
    const allowedRegex = /^[0-9\-.,]*$/;
    if (!allowedRegex.test(raw)) {
      return;
    }
    setInputVal(raw);

    const trimmed = raw.trim();
    if (!trimmed) {
      set({ correctAnswerNumber: null, acceptedAnswers: [] });
      return;
    }

    const normalized = trimmed.replace("−", "-").replace(",", ".");
    const num = Number(normalized);
    if (!isNaN(num) && normalized !== "-" && !normalized.endsWith(".")) {
      set({
        correctAnswerNumber: num,
        acceptedAnswers: [trimmed, normalized],
      });
    } else {
      // Đang gõ dở như '-' hoặc '3,' -> lưu text để không mất dữ liệu
      set({
        acceptedAnswers: [trimmed],
      });
    }
  };

  const previewChars = inputVal.split("");

  return (
    <>
      <Row className="g-2">
        <Col md={7}>
          <Form.Group controlId="q-number">
            <Form.Label className="fw-semibold">
              Đáp án số / ký hiệu (chỉ số 0-9, dấu - và dấu ,) *
            </Form.Label>
            <Form.Control
              placeholder="Ví dụ: -3,14 hoặc 25 hoặc -0,5"
              value={inputVal}
              isInvalid={!!errors.correctAnswerNumber}
              onChange={(e) => handleInputChange(e.target.value)}
            />
            <Form.Control.Feedback type="invalid">{errors.correctAnswerNumber}</Form.Control.Feedback>
            <Form.Text className="text-muted">
              Số ô vuông của học sinh sẽ tự động bằng đúng số ký tự của đáp án (ví dụ: <code>-3,14</code> là 5 ô, <code>25</code> là 2 ô).
            </Form.Text>
          </Form.Group>
        </Col>
        <Col md={5}>
          <Form.Group controlId="q-tolerance">
            <Form.Label>Sai số cho phép (±)</Form.Label>
            <Form.Control
              type="number"
              min={0}
              step="any"
              value={form.numericTolerance ?? 0}
              isInvalid={!!errors.numericTolerance}
              onChange={(e) => set({ numericTolerance: Number(e.target.value) })}
            />
            <Form.Control.Feedback type="invalid">{errors.numericTolerance}</Form.Control.Feedback>
            <Form.Text className="text-muted">Mặc định là 0 (chính xác tuyệt đối).</Form.Text>
          </Form.Group>
        </Col>
      </Row>

      {/* Dãy ô vuông xem trước: Số ô bằng đúng số ký tự của câu trả lời */}
      <div className="mt-3 p-3 border rounded bg-light">
        <div className="d-flex justify-content-between align-items-center mb-2">
          <div className="small fw-bold text-secondary d-flex align-items-center gap-1">
            <i className="bi bi-grid-3x3-gap-fill text-primary" />
            <span>Xem trước ô vuông học sinh sẽ điền:</span>
          </div>
          <Badge bg="info" text="dark">
            {previewChars.length} ô vuông
          </Badge>
        </div>

        <div className="d-flex flex-wrap gap-2">
          {previewChars.length === 0 ? (
            <div
              className="d-flex align-items-center justify-content-center fw-bold fs-4 rounded border text-muted opacity-50"
              style={{
                width: "48px",
                height: "52px",
                backgroundColor: "#ffffff",
                borderColor: "#cbd5e1",
                borderWidth: "2px",
              }}
            >
              _
            </div>
          ) : (
            previewChars.map((c, i) => (
              <div
                key={i}
                className="d-flex align-items-center justify-content-center fw-bold fs-4 rounded border shadow-sm"
                style={{
                  width: "48px",
                  height: "52px",
                  backgroundColor: "#ecfdf5",
                  borderColor: "#059669",
                  borderWidth: "2px",
                  color: "#065f46",
                }}
              >
                {c}
              </div>
            ))
          )}
        </div>
      </div>

      <Form.Group className="mt-3" controlId="q-try">
        <Form.Label>Thử đáp án</Form.Label>
        <InputGroup>
          <Form.Control value={tryValue} onChange={(e) => setTryValue(e.target.value)} placeholder="Nhập câu trả lời để thử" />
          {tryValue && (
            <InputGroup.Text>
              {!isValidNumberAnswer(tryValue) ? (
                <Badge bg="warning" text="dark">Sai định dạng</Badge>
              ) : (
                <TryNumber value={tryValue} answer={form.correctAnswerNumber} tolerance={form.numericTolerance ?? 0} />
              )}
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

const DIFFICULTIES: QuestionDifficulty[] = ["LEVEL1", "LEVEL2", "LEVEL3", "LEVEL4"];

function parseTags(text: string): string[] {
  return [...new Set(text.split(/[,;]/).map((t) => t.trim().toLowerCase()).filter(Boolean))];
}
