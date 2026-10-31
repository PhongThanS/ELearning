import { useMemo, useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, Modal, ProgressBar, Row, Table } from "react-bootstrap";
import { Link, useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { classesApi, usersApi, type ClassroomInput } from "../../services/api";
import { ConfirmDialog, ErrorAlert, Loading } from "../../components/common/Feedback";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import { ActiveBadge, DataTable, PageHeader, SearchBox, type Column } from "../../components/common/DataTable";
import { SearchableSelect } from "../../components/common/SearchableSelect";
import { useListQuery } from "../../hooks/useListQuery";
import { formatDateRange, formatDateTime } from "../../utils/format";
import { Permissions, StudentRole } from "../../constants/permissions";
import { useAuth } from "../auth/useAuth";
import type { Classroom, ClassroomStudent } from "../../types/api";

interface ClassForm {
  code: string;
  name: string;
  schoolYear: string;
  startDate: string;
  endDate: string;
  description: string;
  homeroomTeacherId: string;
}

const emptyForm: ClassForm = { code: "", name: "", schoolYear: "", startDate: "", endDate: "", description: "", homeroomTeacherId: "" };

function toInput(form: ClassForm): ClassroomInput {
  const orNull = (v: string) => (v.trim() === "" ? null : v.trim());
  return {
    name: form.name,
    schoolYear: orNull(form.schoolYear),
    startDate: orNull(form.startDate),
    endDate: orNull(form.endDate),
    description: orNull(form.description),
    homeroomTeacherId: orNull(form.homeroomTeacherId),
  };
}

/** Form tạo / sửa lớp. Khi sửa, mã lớp không đổi được. */
function ClassFormModal({
  initial,
  editing,
  error,
  pending,
  onHide,
  onSubmit,
}: {
  initial: ClassForm | null;
  editing: boolean;
  error: unknown;
  pending: boolean;
  onHide: () => void;
  onSubmit: (form: ClassForm) => void;
}) {
  const { t } = useTranslation();
  const [form, setForm] = useState<ClassForm>(initial ?? emptyForm);
  const teachersQuery = useQuery({
    queryKey: ["users", "teachers-list"],
    queryFn: () => usersApi.list({ roleCode: "TEACHER", pageSize: 100, isActive: true }),
    staleTime: 5 * 60 * 1000,
  });

  const teacherOptions = useMemo(() => {
    return (
      teachersQuery.data?.items.map((u) => ({
        value: u.id,
        label: u.fullName,
        code: u.userName,
        sublabel: u.email ?? undefined,
      })) ?? []
    );
  }, [teachersQuery.data]);

  const field = (name: keyof ClassForm) => ({
    value: form[name],
    onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm({ ...form, [name]: e.target.value }),
  });

  return (
    <Modal show={!!initial} onHide={onHide} centered>
      <Form
        onSubmit={(e) => {
          e.preventDefault();
          onSubmit(form);
        }}
      >
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">{editing ? t("classes.edit") : t("classes.create")}</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {error != null && <Alert variant="danger">{describeError(error)}</Alert>}
          <Row className="g-3">
            <Col sm={5}>
              <Form.Group controlId="class-code">
                <Form.Label>{t("common.code")} *</Form.Label>
                <Form.Control {...field("code")} required disabled={editing} maxLength={50} placeholder="10A1" />
              </Form.Group>
            </Col>
            <Col sm={7}>
              <Form.Group controlId="class-name">
                <Form.Label>{t("common.name")} *</Form.Label>
                <Form.Control {...field("name")} required maxLength={200} />
              </Form.Group>
            </Col>
            <Col sm={4}>
              <Form.Group controlId="class-year">
                <Form.Label>{t("classes.schoolYear")}</Form.Label>
                <Form.Control {...field("schoolYear")} maxLength={20} placeholder="2026-2027" />
              </Form.Group>
            </Col>
            <Col sm={4}>
              <Form.Group controlId="class-start">
                <Form.Label>{t("classes.startDate")}</Form.Label>
                <Form.Control type="date" {...field("startDate")} />
              </Form.Group>
            </Col>
            <Col sm={4}>
              <Form.Group controlId="class-end">
                <Form.Label>{t("classes.endDate")}</Form.Label>
                <Form.Control type="date" {...field("endDate")} min={form.startDate || undefined} />
              </Form.Group>
            </Col>
            <Col xs={12}>
              <Form.Group controlId="class-teacher">
                <Form.Label>Giáo viên chủ nhiệm</Form.Label>
                <SearchableSelect
                  id="class-teacher"
                  value={form.homeroomTeacherId}
                  onChange={(val) => setForm({ ...form, homeroomTeacherId: val })}
                  options={teacherOptions}
                  emptyLabel="-- Chưa phân công --"
                  emptyValue=""
                  placeholder="Chọn hoặc tìm theo mã, họ tên giáo viên..."
                  searchPlaceholder="Nhập mã GV hoặc họ tên..."
                />
              </Form.Group>
            </Col>
            <Col xs={12}>
              <Form.Group controlId="class-description">
                <Form.Label>{t("common.description")}</Form.Label>
                <Form.Control as="textarea" rows={3} {...field("description")} maxLength={1000} />
              </Form.Group>
            </Col>
          </Row>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onHide}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={pending}>{editing ? t("common.save") : t("common.create")}</Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}

// ======================= Danh sách lớp =======================

export function ClassesPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  const queryClient = useQueryClient();
  const list = useListQuery({ sortBy: "code", sortDir: "asc" });
  const query = useQuery({ queryKey: ["classes", list.params], queryFn: () => classesApi.list(list.params) });
  const [creating, setCreating] = useState(false);
  const create = useMutation({
    mutationFn: (form: ClassForm) => classesApi.create({ code: form.code.trim(), ...toInput(form) }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["classes"] });
      setCreating(false);
    },
  });

  const columns: Column<Classroom>[] = [
    { key: "code", header: t("common.code"), sortKey: "code", className: "text-nowrap", render: (c) => <Link to={`/admin/classes/${c.id}`}>{c.code}</Link> },
    {
      key: "name",
      header: t("common.name"),
      sortKey: "name",
      className: "cell-min-10",
      render: (c) => (
        <>
          {c.name}
          {/* Màn hẹp hơn 1400px: năm học hiện dưới tên thay cho cột riêng */}
          {c.schoolYear && <div className="small text-secondary text-nowrap d-xxl-none">{c.schoolYear}</div>}
        </>
      ),
    },
    {
      key: "teacher",
      header: <span title="Giáo viên chủ nhiệm">Chủ nhiệm</span>,
      className: "cell-min-8",
      render: (c) =>
        c.homeroomTeacherName ? (
          <div>
            <span className="fw-medium text-primary"><i className="bi bi-person-badge me-1" aria-hidden="true" />{c.homeroomTeacherName}</span>
            {c.homeroomTeacherEmail && <div className="small text-secondary">{c.homeroomTeacherEmail}</div>}
          </div>
        ) : (
          <span className="text-secondary small">Chưa phân công</span>
        ),
    },
    { key: "schoolYear", header: t("classes.schoolYear"), sortKey: "schoolYear", className: "text-nowrap d-none d-xxl-table-cell", render: (c) => c.schoolYear ?? "—" },
    { key: "period", header: t("classes.period"), className: "small", render: (c) => formatDateRange(c.startDate, c.endDate) },
    { key: "students", header: t("classes.students"), className: "text-center", render: (c) => c.studentCount },
    { key: "exams", header: t("classes.exams"), className: "text-center", render: (c) => c.examCount },
    { key: "status", header: t("common.status"), render: (c) => <ActiveBadge active={c.isActive} /> },
  ];

  return (
    <>
      <PageHeader
        title={t("nav.classes")}
        actions={hasPermission(Permissions.ClassManage) && <Button onClick={() => { create.reset(); setCreating(true); }}>{t("common.create")}</Button>}
      />
      <Row className="g-2 mb-3">
        <Col md={5}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã hoặc tên lớp..." />
        </Col>
        <Col md={3}>
          <Form.Select
            aria-label={t("common.status")}
            value={list.state.isActive === undefined ? "" : String(list.state.isActive)}
            onChange={(e) => list.setFilter({ isActive: e.target.value === "" ? undefined : e.target.value === "true" })}
          >
            <option value="">{t("classes.allStatuses")}</option>
            <option value="true">{t("common.active")}</option>
            <option value="false">{t("common.inactive")}</option>
          </Form.Select>
        </Col>
      </Row>
      <DataTable data={query.data} columns={columns} rowKey={(c) => c.id} isLoading={query.isLoading} error={query.error} sort={list.state} onSort={list.toggleSort} onPage={list.setPage} />
      {creating && (
        <ClassFormModal initial={emptyForm} editing={false} error={create.error} pending={create.isPending} onHide={() => setCreating(false)} onSubmit={(f) => create.mutate(f)} />
      )}
    </>
  );
}

// ======================= Chi tiết lớp =======================

export function ClassDetailPage() {
  const { t } = useTranslation();
  const { id = "" } = useParams();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(Permissions.ClassManage);
  const classroom = useQuery({ queryKey: ["class", id], queryFn: () => classesApi.get(id) });
  const list = useListQuery();
  const students = useQuery({ queryKey: ["class-students", id, list.params], queryFn: () => classesApi.students(id, list.params) });
  const [search, setSearch] = useState("");
  const candidates = useQuery({
    queryKey: ["users", "class-candidates", search],
    queryFn: () => usersApi.list({ keyword: search, pageSize: 10, isActive: true, roleCode: StudentRole }),
    enabled: canManage && search.length >= 2,
  });
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const navigate = useNavigate();
  const remove = useMutation({
    mutationFn: () => classesApi.remove(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["classes"] });
      toast.success(t("classes.deleted"));
      navigate("/admin/classes", { replace: true });
    },
    onError: (e) => {
      setConfirmDelete(false);
      toast.error(e);
    },
  });
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["class", id] });
    void queryClient.invalidateQueries({ queryKey: ["class-students", id] });
    void queryClient.invalidateQueries({ queryKey: ["classes"] });
  };
  const change = useMutation({
    mutationFn: async (action: () => Promise<unknown>) => action(),
    onSuccess: () => {
      refresh();
      toast.success(t("common.saved"));
    },
    onError: (e) => toast.error(e),
  });
  const update = useMutation({
    mutationFn: (form: ClassForm) => {
      const c = classroom.data!;
      return classesApi.update(id, { ...toInput(form), isActive: c.isActive, rowVersion: c.rowVersion });
    },
    onSuccess: () => {
      refresh();
      setEditing(false);
      toast.success(t("common.saved"));
    },
  });

  const [inspectingStudent, setInspectingStudent] = useState<ClassroomStudent | null>(null);

  const columns: Column<ClassroomStudent>[] = [
    {
      key: "userName",
      header: t("auth.userNameOnly"),
      render: (s) => (
        <div className="d-flex align-items-center gap-1">
          <button
            type="button"
            className="btn btn-link p-0 text-start fw-semibold text-decoration-none text-primary"
            onClick={() => setInspectingStudent(s)}
            title="Bấm để xem bài tập đã làm & chưa làm"
          >
            {s.userName}
          </button>
          <Link
            to={`/admin/users/${s.userId}`}
            className="text-secondary small ms-1"
            title="Xem chi tiết hồ sơ tài khoản"
          >
            <i className="bi bi-box-arrow-up-right" style={{ fontSize: "0.75rem" }} />
          </Link>
          {s.email && <div className="small text-secondary d-xxl-none">{s.email}</div>}
        </div>
      ),
    },
    {
      key: "fullName",
      header: t("auth.fullName"),
      render: (s) => (
        <button
          type="button"
          className="btn btn-link p-0 text-start fw-semibold text-decoration-none text-primary"
          onClick={() => setInspectingStudent(s)}
          title="Bấm để xem danh sách bài tập đã làm & chưa làm"
        >
          <i className="bi bi-person-lines-fill me-1" />
          {s.fullName}
        </button>
      ),
    },
    // Màn hẹp hơn 1400px: email hiện dưới tên đăng nhập thay cho cột riêng
    { key: "email", header: t("auth.email"), className: "d-none d-xxl-table-cell", render: (s) => s.email },
    { key: "status", header: t("common.status"), render: (s) => <ActiveBadge active={s.isActive} /> },
    { key: "joinedAt", header: t("classes.joinedAt"), render: (s) => formatDateTime(s.joinedAt) },
    {
      key: "actions",
      header: "",
      render: (s) => (
        <div className="d-flex justify-content-end gap-1">
          <Button
            size="sm"
            variant="outline-primary"
            onClick={() => setInspectingStudent(s)}
            title="Xem bài tập đã làm và chưa làm"
          >
            <i className="bi bi-journal-check" aria-hidden="true" />
            <span className="d-none d-xxl-inline ms-1">Bài tập</span>
          </Button>
          {canManage && (
            <Button
              size="sm"
              variant="outline-danger"
              onClick={() => change.mutate(() => classesApi.removeStudent(id, s.userId))}
              title={t("classes.removeStudent")}
              aria-label={`${t("classes.removeStudent")}: ${s.fullName}`}
            >
              <i className="bi bi-person-dash" aria-hidden="true" />
              <span className="d-none d-xxl-inline ms-1">{t("classes.removeStudent")}</span>
            </Button>
          )}
        </div>
      ),
    },
  ];

  if (classroom.error) {
    return <ErrorAlert error={classroom.error} />;
  }
  if (!classroom.data) {
    return <Loading />;
  }
  const c = classroom.data;
  const memberIds = new Set(students.data?.items.map((s) => s.userId));

  return (
    <>
      <Link to="/admin/classes" className="small">← {t("nav.classes")}</Link>
      <PageHeader
        title={`${c.name} (${c.code})`}
        actions={
          canManage && (
            <>
              <Button variant="outline-primary" onClick={() => { update.reset(); setEditing(true); }}>
                <i className="bi bi-pencil me-1" aria-hidden="true" />
                {t("common.edit")}
              </Button>
              <Button variant={c.isActive ? "outline-warning" : "outline-success"} onClick={() => change.mutate(() => classesApi.setStatus(id, !c.isActive))}>
                {c.isActive ? t("common.deactivate") : t("common.activate")}
              </Button>
              <Button variant="outline-danger" onClick={() => setConfirmDelete(true)}>
                <i className="bi bi-trash me-1" aria-hidden="true" />
                {t("common.delete")}
              </Button>
            </>
          )
        }
      >
        <ActiveBadge active={c.isActive} />
      </PageHeader>
      <Row xs={2} md={4} className="g-3 mb-3">
        {(
          [
            [t("classes.students"), c.studentCount, "bi-people-fill", "green"],
            [t("classes.exams"), c.examCount, "bi-journal-check", "violet"],
            [t("classes.schoolYear"), c.schoolYear ?? "—", "bi-calendar2-range", "sky"],
            [t("classes.period"), formatDateRange(c.startDate, c.endDate), "bi-calendar-week", "amber"],
          ] as const
        ).map(([label, value, icon, accent]) => (
          <Col key={label}>
            <Card className={`h-100 stat-card accent-${accent}`}>
              <Card.Body className="d-flex align-items-center gap-3">
                <span className="stat-icon" aria-hidden="true">
                  <i className={`bi ${icon}`} />
                </span>
                <div className="min-w-0">
                  <div className="small text-secondary text-truncate">{label}</div>
                  <div className="fw-bold lh-sm">{value}</div>
                </div>
              </Card.Body>
            </Card>
          </Col>
        ))}
      </Row>
      {c.homeroomTeacherName && (
        <Card className="mb-3 border-primary-subtle shadow-sm">
          <Card.Body className="d-flex align-items-center gap-3 py-2">
            <span className="fs-3 text-primary"><i className="bi bi-person-badge" /></span>
            <div>
              <div className="small text-secondary fw-semibold text-uppercase">Giáo viên chủ nhiệm</div>
              <div className="fw-bold">{c.homeroomTeacherName}</div>
              {c.homeroomTeacherEmail && <div className="small text-muted">{c.homeroomTeacherEmail}</div>}
            </div>
          </Card.Body>
        </Card>
      )}
      {c.description && <p className="text-secondary">{c.description}</p>}
      {!c.isActive && <Alert variant="warning" className="py-2 small">{t("classes.inactiveHint")}</Alert>}
      {canManage && (
        <Card className="mb-3">
          <Card.Body>
            <Form.Group controlId="class-student-search">
              <Form.Label>{t("classes.addStudentLabel")}</Form.Label>
              <Form.Control size="sm" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("classes.addStudentPlaceholder")} />
            </Form.Group>
            {candidates.data && (
              <Table size="sm" responsive className="mt-2 mb-0">
                <tbody>
                  {candidates.data.items.length === 0 && (
                    <tr>
                      <td className="text-secondary small">{t("common.noData")}</td>
                    </tr>
                  )}
                  {candidates.data.items.map((u) => (
                    <tr key={u.id}>
                      <td>{u.userName}</td>
                      <td>{u.fullName}</td>
                      <td className="text-end">
                        {memberIds.has(u.id) ? (
                          <span className="small text-success">{t("classes.alreadyMember")}</span>
                        ) : (
                          <Button size="sm" onClick={() => change.mutate(() => classesApi.addStudents(id, [u.id]))}>
                            <i className="bi bi-person-plus me-1" aria-hidden="true" />
                            {t("classes.addStudent")}
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Card.Body>
        </Card>
      )}
      <div className="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-2">
        <h2 className="h6 mb-0">{t("classes.studentList")}</h2>
        <div style={{ maxWidth: 320 }}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm học viên theo mã, họ tên..." />
        </div>
      </div>
      <DataTable data={students.data} columns={columns} rowKey={(s) => s.userId} isLoading={students.isLoading} error={students.error} onPage={list.setPage} />
      <ConfirmDialog
        show={confirmDelete}
        title={t("classes.deleteTitle", { code: c.code })}
        body={t("classes.deleteBody", { count: c.studentCount })}
        confirmText={t("common.delete")}
        variant="danger"
        busy={remove.isPending}
        onConfirm={() => remove.mutate()}
        onCancel={() => setConfirmDelete(false)}
      />
      {editing && (
        <ClassFormModal
          initial={{
            code: c.code,
            name: c.name,
            schoolYear: c.schoolYear ?? "",
            startDate: c.startDate ?? "",
            endDate: c.endDate ?? "",
            description: c.description ?? "",
            homeroomTeacherId: c.homeroomTeacherId ?? "",
          }}
          editing
          error={update.error}
          pending={update.isPending}
          onHide={() => setEditing(false)}
          onSubmit={(f) => update.mutate(f)}
        />
      )}
      <StudentProgressModal
        classroomId={id}
        student={inspectingStudent}
        onHide={() => setInspectingStudent(null)}
      />
    </>
  );
}

// ======================= Modal Xem Tiến độ bài tập của học sinh =======================

function StudentProgressModal({
  classroomId,
  student,
  onHide,
}: {
  classroomId: string;
  student: ClassroomStudent | null;
  onHide: () => void;
}) {
  const [filter, setFilter] = useState<"all" | "completed" | "notStarted" | "inProgress">("all");

  const progressQuery = useQuery({
    queryKey: ["class-student-progress", classroomId, student?.userId],
    queryFn: () => classesApi.studentProgress(classroomId, student!.userId),
    enabled: Boolean(student && classroomId),
  });

  const data = progressQuery.data;
  const filteredExams = data?.exams.filter((item) => {
    if (filter === "completed") return item.isSubmitted;
    if (filter === "notStarted") return !item.isSubmitted && !item.isInProgress;
    if (filter === "inProgress") return item.isInProgress;
    return true;
  }) ?? [];

  const completionPct =
    data && data.totalAssignedExams > 0
      ? Math.round((data.completedExams / data.totalAssignedExams) * 100)
      : 0;

  return (
    <>
      <Modal show={Boolean(student)} onHide={onHide} size="xl" centered>
        <Modal.Header closeButton>
          <Modal.Title className="h5 d-flex align-items-center gap-2">
            <i className="bi bi-mortarboard text-primary" />
            <span>Tiến độ bài tập: {student?.fullName}</span>
            <span className="text-muted fs-6 fw-normal">({student?.userName})</span>
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {progressQuery.isLoading && <Loading />}
          {progressQuery.error && <ErrorAlert error={progressQuery.error} />}
          {data && (
            <>
              <div className="mb-3 p-3 bg-light rounded border">
                <div className="d-flex justify-content-between align-items-center mb-1">
                  <span className="small fw-semibold text-secondary">Tỷ lệ hoàn thành bài tập</span>
                  <span className="fw-bold text-primary">{completionPct}%</span>
                </div>
                <ProgressBar
                  now={completionPct}
                  variant={completionPct === 100 ? "success" : completionPct > 50 ? "primary" : "warning"}
                  style={{ height: "8px" }}
                />
                <Row className="g-2 mt-2 text-center">
                  <Col xs={3}>
                    <div className="p-2 border rounded bg-white">
                      <div className="text-muted small">Tổng số đề</div>
                      <div className="fs-5 fw-bold text-dark">{data.totalAssignedExams}</div>
                    </div>
                  </Col>
                  <Col xs={3}>
                    <div className="p-2 border rounded bg-white">
                      <div className="text-success small fw-semibold">Đã làm</div>
                      <div className="fs-5 fw-bold text-success">{data.completedExams}</div>
                    </div>
                  </Col>
                  <Col xs={3}>
                    <div className="p-2 border rounded bg-white">
                      <div className="text-secondary small fw-semibold">Chưa làm</div>
                      <div className="fs-5 fw-bold text-danger">{data.notStartedExams}</div>
                    </div>
                  </Col>
                  <Col xs={3}>
                    <div className="p-2 border rounded bg-white">
                      <div className="text-warning small fw-semibold">Đang làm</div>
                      <div className="fs-5 fw-bold text-warning">{data.inProgressExams}</div>
                    </div>
                  </Col>
                </Row>
              </div>

              <div className="d-flex justify-content-between align-items-center mb-2 flex-wrap gap-2">
                <div className="btn-group btn-group-sm" role="group">
                  <button
                    type="button"
                    className={`btn ${filter === "all" ? "btn-primary" : "btn-outline-secondary"}`}
                    onClick={() => setFilter("all")}
                  >
                    Tất cả ({data.totalAssignedExams})
                  </button>
                  <button
                    type="button"
                    className={`btn ${filter === "completed" ? "btn-success" : "btn-outline-secondary"}`}
                    onClick={() => setFilter("completed")}
                  >
                    Đã làm ({data.completedExams})
                  </button>
                  <button
                    type="button"
                    className={`btn ${filter === "notStarted" ? "btn-danger" : "btn-outline-secondary"}`}
                    onClick={() => setFilter("notStarted")}
                  >
                    Chưa làm ({data.notStartedExams})
                  </button>
                  {data.inProgressExams > 0 && (
                    <button
                      type="button"
                      className={`btn ${filter === "inProgress" ? "btn-warning" : "btn-outline-secondary"}`}
                      onClick={() => setFilter("inProgress")}
                    >
                      Đang làm ({data.inProgressExams})
                    </button>
                  )}
                </div>
              </div>

              {filteredExams.length === 0 ? (
                <Alert variant="info" className="py-2 small mb-0">
                  Không có bài tập nào trong mục này.
                </Alert>
              ) : (
                <div className="table-responsive border rounded">
                  <Table hover size="sm" className="mb-0 align-middle">
                    <thead className="table-light">
                      <tr>
                        <th>Tên bài tập / Đề thi</th>
                        <th className="text-center">Thời lượng</th>
                        <th className="text-center">Lượt làm</th>
                        <th className="text-center">Trạng thái</th>
                        <th className="text-center">Điểm số</th>
                        <th className="text-end">Thời gian nộp</th>
                        <th className="text-end" style={{ width: 150 }}>Hành động</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredExams.map((item) => {
                        const isCompleted = item.isSubmitted && Boolean(item.latestAttemptId);
                        const isInProgress = item.isInProgress && Boolean(item.inProgressAttemptId);
                        const targetUrl = isCompleted
                          ? `/admin/attempts/${item.latestAttemptId}`
                          : isInProgress
                          ? `/admin/attempts/${item.inProgressAttemptId}`
                          : `/admin/exams/${item.examId}/preview`;

                        return (
                          <tr key={item.examId}>
                            <td>
                              <Link
                                to={targetUrl}
                                target="_blank"
                                rel="noopener noreferrer"
                                className="fw-semibold text-primary text-decoration-none"
                                title={
                                  isCompleted
                                    ? "Mở xem bài làm và đáp án chi tiết ở tab mới"
                                    : isInProgress
                                    ? "Mở xem bài đang làm dở ở tab mới"
                                    : "Mở xem đề bài (chế độ chỉ đọc) ở tab mới"
                                }
                              >
                                {item.name} <i className="bi bi-box-arrow-up-right small text-muted ms-1" />
                              </Link>
                              <div className="text-muted small font-monospace">{item.code}</div>
                            </td>
                            <td className="text-center text-nowrap small">
                              {item.durationMinutes === 0 ? (
                                <Badge bg="info" className="text-dark bg-opacity-25 border border-info">
                                  Luyện tập
                                </Badge>
                              ) : (
                                <div>{item.durationMinutes} phút</div>
                              )}
                              <div className="text-muted">{item.questionCount} câu</div>
                            </td>
                            <td className="text-center small">
                              <span className="badge bg-light text-dark border">
                                {item.usedAttempts} / {item.maxAttempts === 0 ? "∞" : item.maxAttempts}
                              </span>
                            </td>
                            <td className="text-center text-nowrap">
                              {item.isSubmitted ? (
                                <Badge bg={item.passed === false ? "danger" : "success"}>
                                  {item.passed === true ? "Đã nộp (Đạt)" : item.passed === false ? "Đã nộp (Chưa đạt)" : "Đã nộp"}
                                </Badge>
                              ) : item.isInProgress ? (
                                <Badge bg="warning" text="dark">Đang làm dở</Badge>
                              ) : (
                                <Badge bg="secondary">Chưa làm</Badge>
                              )}
                            </td>
                            <td className="text-center text-nowrap">
                              {item.isSubmitted && item.officialScore !== null ? (
                                <div>
                                  <span className="fw-bold text-primary">{item.officialScore}</span>
                                  {item.percentage !== null && (
                                    <span className="small text-muted ms-1">({item.percentage}%)</span>
                                  )}
                                </div>
                              ) : (
                                <span className="text-muted">—</span>
                              )}
                            </td>
                            <td className="text-end text-nowrap small text-muted">
                              {item.lastSubmittedAt ? formatDateTime(item.lastSubmittedAt) : "—"}
                            </td>
                            <td className="text-end">
                              {isCompleted ? (
                                <Link
                                  to={targetUrl}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className="btn btn-sm btn-outline-success d-inline-flex align-items-center gap-1"
                                  title="Mở xem bài làm và đáp án chi tiết ở tab mới"
                                >
                                  <i className="bi bi-file-earmark-check" />
                                  <span>Xem bài làm</span>
                                </Link>
                              ) : isInProgress ? (
                                <Link
                                  to={targetUrl}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className="btn btn-sm btn-outline-warning d-inline-flex align-items-center gap-1"
                                  title="Mở xem bài đang làm dở ở tab mới"
                                >
                                  <i className="bi bi-hourglass-split" />
                                  <span>Xem bài dở</span>
                                </Link>
                              ) : (
                                <Link
                                  to={targetUrl}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className="btn btn-sm btn-outline-secondary d-inline-flex align-items-center gap-1"
                                  title="Mở xem đề bài (chế độ chỉ đọc) ở tab mới"
                                >
                                  <i className="bi bi-eye" />
                                  <span>Xem đề bài</span>
                                </Link>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </Table>
                </div>
              )}
            </>
          )}
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" size="sm" onClick={onHide}>
            Đóng
          </Button>
        </Modal.Footer>
      </Modal>
    </>
  );
}
