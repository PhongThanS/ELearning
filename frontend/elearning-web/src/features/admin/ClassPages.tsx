import { useState } from "react";
import { Alert, Button, Card, Col, Form, Modal, Row, Table } from "react-bootstrap";
import { Link, useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { classesApi, usersApi, type ClassroomInput } from "../../services/api";
import { ConfirmDialog, ErrorAlert, Loading } from "../../components/common/Feedback";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import { ActiveBadge, DataTable, PageHeader, SearchBox, type Column } from "../../components/common/DataTable";
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
}

const emptyForm: ClassForm = { code: "", name: "", schoolYear: "", startDate: "", endDate: "", description: "" };

function toInput(form: ClassForm): ClassroomInput {
  const orNull = (v: string) => (v.trim() === "" ? null : v.trim());
  return {
    name: form.name,
    schoolYear: orNull(form.schoolYear),
    startDate: orNull(form.startDate),
    endDate: orNull(form.endDate),
    description: orNull(form.description),
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
    { key: "code", header: t("common.code"), sortKey: "code", render: (c) => <Link to={`/admin/classes/${c.id}`}>{c.code}</Link> },
    { key: "name", header: t("common.name"), sortKey: "name", render: (c) => c.name },
    { key: "schoolYear", header: t("classes.schoolYear"), sortKey: "schoolYear", render: (c) => c.schoolYear ?? "—" },
    { key: "period", header: t("classes.period"), render: (c) => formatDateRange(c.startDate, c.endDate) },
    { key: "students", header: t("classes.students"), render: (c) => c.studentCount },
    { key: "exams", header: t("classes.exams"), render: (c) => c.examCount },
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
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} />
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

  const columns: Column<ClassroomStudent>[] = [
    { key: "userName", header: t("auth.userNameOnly"), render: (s) => <Link to={`/admin/users/${s.userId}`}>{s.userName}</Link> },
    { key: "fullName", header: t("auth.fullName"), render: (s) => s.fullName },
    { key: "email", header: t("auth.email"), render: (s) => s.email },
    { key: "status", header: t("common.status"), render: (s) => <ActiveBadge active={s.isActive} /> },
    { key: "joinedAt", header: t("classes.joinedAt"), render: (s) => formatDateTime(s.joinedAt) },
    {
      key: "actions",
      header: "",
      render: (s) =>
        canManage && (
          <Button size="sm" variant="outline-danger" onClick={() => change.mutate(() => classesApi.removeStudent(id, s.userId))}>
            <i className="bi bi-person-dash me-1" aria-hidden="true" />
            {t("classes.removeStudent")}
          </Button>
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
              <Table size="sm" className="mt-2 mb-0">
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
      <h2 className="h6 mb-2">{t("classes.studentList")}</h2>
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
          }}
          editing
          error={update.error}
          pending={update.isPending}
          onHide={() => setEditing(false)}
          onSubmit={(f) => update.mutate(f)}
        />
      )}
    </>
  );
}
