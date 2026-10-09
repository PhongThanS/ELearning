import { useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, Modal, Row, Table } from "react-bootstrap";
import { Link, useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { groupsApi, rolesApi, usersApi } from "../../services/api";
import { toApiError } from "../../services/apiClient";
import { ConfirmDialog, ErrorAlert, Loading } from "../../components/common/Feedback";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import { ActiveBadge, DataTable, PageHeader, SearchBox, type Column } from "../../components/common/DataTable";
import { useListQuery } from "../../hooks/useListQuery";
import { formatDateTime } from "../../utils/format";
import { Permissions } from "../../constants/permissions";
import { useAuth } from "../auth/useAuth";
import type { Group, GroupMember, UserListItem, UserWithPassword } from "../../types/api";

// ======================= Người dùng =======================

export function UsersPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  const list = useListQuery({ sortBy: "userName", sortDir: "asc" });
  const query = useQuery({ queryKey: ["users", list.params], queryFn: () => usersApi.list(list.params) });
  const [creating, setCreating] = useState(false);
  const [created, setCreated] = useState<UserWithPassword | null>(null);

  const columns: Column<UserListItem>[] = [
    { key: "userName", header: t("auth.userNameOnly"), sortKey: "userName", render: (u) => <Link to={`/admin/users/${u.id}`}>{u.userName}</Link> },
    { key: "fullName", header: t("auth.fullName"), sortKey: "fullName", render: (u) => u.fullName },
    { key: "email", header: t("auth.email"), sortKey: "email", render: (u) => u.email },
    { key: "roles", header: t("nav.roles"), render: (u) => u.roles.map((r) => <Badge key={r} bg="info" className="me-1">{r}</Badge>) },
    {
      key: "status",
      header: t("common.status"),
      render: (u) => (
        <>
          <ActiveBadge active={u.isActive} /> {u.isLockedOut && <Badge bg="warning" text="dark">Tạm khóa</Badge>}
        </>
      ),
    },
    { key: "lastLogin", header: "Đăng nhập gần nhất", sortKey: "lastLoginAt", render: (u) => formatDateTime(u.lastLoginAt) },
  ];

  return (
    <>
      <PageHeader
        title={t("nav.users")}
        actions={hasPermission(Permissions.UserCreate) && <Button onClick={() => setCreating(true)}>{t("common.create")}</Button>}
      />
      <Row className="g-2 mb-3">
        <Col md={5}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã đăng nhập, họ tên, email..." />
        </Col>
        <Col md={3}>
          <Form.Select size="sm" aria-label="Vai trò" value={String(list.state.roleCode ?? "")} onChange={(e) => list.setFilter({ roleCode: e.target.value })}>
            <option value="">Mọi vai trò</option>
            <option value="ADMIN">ADMIN</option>
            <option value="STUDENT">STUDENT</option>
          </Form.Select>
        </Col>
        <Col md={3}>
          <Form.Select size="sm" aria-label={t("common.status")} value={String(list.state.isActive ?? "")} onChange={(e) => list.setFilter({ isActive: e.target.value })}>
            <option value="">{t("common.all")}</option>
            <option value="true">{t("common.active")}</option>
            <option value="false">{t("common.inactive")}</option>
          </Form.Select>
        </Col>
      </Row>
      <DataTable
        data={query.data}
        columns={columns}
        rowKey={(u) => u.id}
        isLoading={query.isLoading}
        error={query.error}
        sort={list.state}
        onSort={list.toggleSort}
        onPage={list.setPage}
        onRetry={() => void query.refetch()}
      />
      <CreateUserModal show={creating} onHide={() => setCreating(false)} onCreated={setCreated} />
      <TemporaryPasswordModal result={created} onHide={() => setCreated(null)} />
    </>
  );
}

function CreateUserModal({ show, onHide, onCreated }: { show: boolean; onHide: () => void; onCreated: (r: UserWithPassword) => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [form, setForm] = useState({ userName: "", email: "", fullName: "", password: "" });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const roles = useQuery({ queryKey: ["roles"], queryFn: rolesApi.list, enabled: show });
  const [roleIds, setRoleIds] = useState<string[]>([]);
  const mutation = useMutation({
    mutationFn: () => usersApi.create({ ...form, password: form.password || null, roleIds }),
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ["users"] });
      onCreated(result);
      onHide();
      setForm({ userName: "", email: "", fullName: "", password: "" });
      setRoleIds([]);
    },
    onError: (e) => setErrors(toApiError(e).fieldErrors()),
  });

  return (
    <Modal show={show} onHide={onHide} centered>
      <Form
        onSubmit={(e) => {
          e.preventDefault();
          setErrors({});
          mutation.mutate();
        }}
      >
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">Tạo người dùng</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {mutation.error && <Alert variant="danger">{describeError(mutation.error)}</Alert>}
          {(["userName", "email", "fullName"] as const).map((name) => (
            <Form.Group className="mb-3" controlId={`new-user-${name}`} key={name}>
              <Form.Label>{name === "userName" ? t("auth.userNameOnly") : name === "email" ? t("auth.email") : t("auth.fullName")} *</Form.Label>
              <Form.Control
                value={form[name]}
                isInvalid={!!errors[name]}
                placeholder={
                  name === "userName"
                    ? "VD: nguyen_van_a, giaovien01"
                    : name === "email"
                      ? "VD: giaovien@example.com"
                      : "VD: Nguyễn Văn An"
                }
                onChange={(e) => setForm({ ...form, [name]: e.target.value })}
                required
              />
              <Form.Control.Feedback type="invalid">{errors[name]}</Form.Control.Feedback>
            </Form.Group>
          ))}
          <Form.Group className="mb-3" controlId="new-user-password">
            <Form.Label>{t("auth.password")}</Form.Label>
            <Form.Control type="password" autoComplete="new-password" value={form.password} isInvalid={!!errors.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
            <Form.Text>Để trống để hệ thống sinh mật khẩu tạm; người dùng phải đổi ở lần đăng nhập đầu.</Form.Text>
            <Form.Control.Feedback type="invalid">{errors.password}</Form.Control.Feedback>
          </Form.Group>
          <fieldset>
            <legend className="form-label fs-6">{t("nav.roles")} (mặc định STUDENT)</legend>
            {roles.data?.map((r) => (
              <Form.Check
                key={r.id}
                id={`new-user-role-${r.id}`}
                label={`${r.code} — ${r.name}`}
                checked={roleIds.includes(r.id)}
                onChange={(e) => setRoleIds(e.target.checked ? [...roleIds, r.id] : roleIds.filter((id) => id !== r.id))}
              />
            ))}
          </fieldset>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onHide}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={mutation.isPending}>{t("common.create")}</Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}

function TemporaryPasswordModal({ result, onHide }: { result: UserWithPassword | null; onHide: () => void }) {
  return (
    <Modal show={!!result?.temporaryPassword} onHide={onHide} centered>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">Mật khẩu tạm</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <p>
          Mật khẩu tạm của <strong>{result?.user.userName}</strong> (chỉ hiển thị một lần):
        </p>
        <p className="font-monospace fs-4 text-center user-select-all border rounded py-2">{result?.temporaryPassword}</p>
        <p className="small text-secondary mb-0">Người dùng phải đổi mật khẩu ở lần đăng nhập đầu tiên.</p>
      </Modal.Body>
    </Modal>
  );
}

export function UserDetailPage() {
  const { t } = useTranslation();
  const { id = "" } = useParams();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission, user: me } = useAuth();
  const query = useQuery({ queryKey: ["user", id], queryFn: () => usersApi.get(id) });
  const roles = useQuery({ queryKey: ["roles"], queryFn: rolesApi.list, enabled: hasPermission(Permissions.RoleAssign) });
  const [profile, setProfile] = useState<{ email: string; fullName: string } | null>(null);
  const [dialog, setDialog] = useState<"status" | "anonymize" | "reset" | null>(null);
  const [tempPassword, setTempPassword] = useState<UserWithPassword | null>(null);

  const refresh = (data?: unknown) => {
    if (data) {
      queryClient.setQueryData(["user", id], (data as UserWithPassword).user ?? data);
    }
    void queryClient.invalidateQueries({ queryKey: ["users"] });
  };
  const run = useMutation({
    mutationFn: async (action: () => Promise<unknown>) => action(),
    onSuccess: (data) => {
      refresh(data);
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
  const u = query.data;
  const isSelf = me?.id === u.id;

  return (
    <>
      <Link to="/admin/users" className="small">← {t("nav.users")}</Link>
      <PageHeader title={`${u.fullName} (${u.userName})`}>
        <ActiveBadge active={u.isActive} /> {u.anonymizedAt && <Badge bg="dark">Đã ẩn danh hóa</Badge>}
      </PageHeader>
      <Row className="g-3">
        <Col lg={6}>
          <Card>
            <Card.Body>
              <h2 className="h6">Thông tin</h2>
              <Form
                onSubmit={(e) => {
                  e.preventDefault();
                  const values = profile ?? { email: u.email, fullName: u.fullName };
                  run.mutate(() => usersApi.update(id, { ...values, rowVersion: u.rowVersion }));
                  setProfile(null);
                }}
              >
                <Form.Group className="mb-2" controlId="user-email">
                  <Form.Label>{t("auth.email")}</Form.Label>
                  <Form.Control value={profile?.email ?? u.email} disabled={!hasPermission(Permissions.UserUpdate) || !!u.anonymizedAt} onChange={(e) => setProfile({ email: e.target.value, fullName: profile?.fullName ?? u.fullName })} />
                </Form.Group>
                <Form.Group className="mb-2" controlId="user-fullname">
                  <Form.Label>{t("auth.fullName")}</Form.Label>
                  <Form.Control value={profile?.fullName ?? u.fullName} disabled={!hasPermission(Permissions.UserUpdate) || !!u.anonymizedAt} onChange={(e) => setProfile({ fullName: e.target.value, email: profile?.email ?? u.email })} />
                </Form.Group>
                {hasPermission(Permissions.UserUpdate) && !u.anonymizedAt && (
                  <Button type="submit" size="sm" disabled={!profile || run.isPending}>{t("common.save")}</Button>
                )}
              </Form>
              <dl className="row small mt-3 mb-0">
                <dt className="col-5">Đăng nhập gần nhất</dt>
                <dd className="col-7">{formatDateTime(u.lastLoginAt)}</dd>
                <dt className="col-5">Tạm khóa đến</dt>
                <dd className="col-7">{formatDateTime(u.lockoutEnd)}</dd>
                <dt className="col-5">Nhóm</dt>
                <dd className="col-7">{u.groups.map((g) => g.code).join(", ") || "—"}</dd>
                <dt className="col-5">Phải đổi mật khẩu</dt>
                <dd className="col-7">{u.mustChangePassword ? t("common.yes") : t("common.no")}</dd>
              </dl>
            </Card.Body>
          </Card>
        </Col>
        <Col lg={6}>
          {hasPermission(Permissions.RoleAssign) && roles.data && (
            <Card className="mb-3">
              <Card.Body>
                <h2 className="h6">{t("nav.roles")}</h2>
                {roles.data.map((r) => {
                  const checked = u.roles.some((x) => x.id === r.id);
                  return (
                    <Form.Check
                      key={r.id}
                      id={`user-role-${r.id}`}
                      label={`${r.code} — ${r.name}`}
                      checked={checked}
                      disabled={run.isPending || !!u.anonymizedAt}
                      onChange={() =>
                        run.mutate(() => usersApi.setRoles(id, checked ? u.roles.filter((x) => x.id !== r.id).map((x) => x.id) : [...u.roles.map((x) => x.id), r.id]))
                      }
                    />
                  );
                })}
                <Form.Text>Đổi vai trò sẽ đăng xuất người dùng khỏi mọi thiết bị.</Form.Text>
              </Card.Body>
            </Card>
          )}
          <Card>
            <Card.Body className="d-flex flex-wrap gap-2">
              {hasPermission(Permissions.UserUpdate) && !isSelf && !u.anonymizedAt && (
                <Button variant={u.isActive ? "outline-warning" : "outline-success"} onClick={() => setDialog("status")}>
                  {u.isActive ? t("common.deactivate") : t("common.activate")}
                </Button>
              )}
              {hasPermission(Permissions.UserResetPassword) && !u.anonymizedAt && (
                <Button variant="outline-secondary" onClick={() => setDialog("reset")}>Đặt lại mật khẩu</Button>
              )}
              {hasPermission(Permissions.UserAnonymize) && !isSelf && !u.anonymizedAt && (
                <Button variant="outline-danger" onClick={() => setDialog("anonymize")}>Ẩn danh hóa</Button>
              )}
            </Card.Body>
          </Card>
        </Col>
      </Row>

      <ConfirmDialog
        show={dialog === "status"}
        title={u.isActive ? "Vô hiệu hóa tài khoản" : "Kích hoạt tài khoản"}
        body={u.isActive ? "Người dùng sẽ bị đăng xuất khỏi mọi thiết bị." : undefined}
        requireReason={u.isActive}
        busy={run.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={(reason) => run.mutate(() => usersApi.setStatus(id, !u.isActive, reason))}
      />
      <ConfirmDialog
        show={dialog === "reset"}
        title="Đặt lại mật khẩu"
        body="Hệ thống sẽ sinh mật khẩu tạm và thu hồi mọi phiên đăng nhập."
        busy={run.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={() =>
          run.mutate(async () => {
            const result = await usersApi.resetPassword(id);
            setTempPassword(result);
            return result;
          })
        }
      />
      <ConfirmDialog
        show={dialog === "anonymize"}
        title="Ẩn danh hóa người dùng"
        body="Xóa họ tên, email, IP của người dùng. Lượt thi và kết quả được giữ lại. Không thể hoàn tác."
        variant="danger"
        requireReason
        busy={run.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={(reason) => run.mutate(() => usersApi.anonymize(id, reason))}
      />
      <TemporaryPasswordModal result={tempPassword} onHide={() => setTempPassword(null)} />
    </>
  );
}

// ======================= Nhóm =======================

export function GroupsPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  const toast = useToast();
  const queryClient = useQueryClient();
  const list = useListQuery({ sortBy: "code", sortDir: "asc" });
  const query = useQuery({ queryKey: ["groups", list.params], queryFn: () => groupsApi.list(list.params) });
  const [form, setForm] = useState<{ code: string; name: string; description: string } | null>(null);
  const [deleting, setDeleting] = useState<Group | null>(null);

  const create = useMutation({
    mutationFn: () => groupsApi.create(form!),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["groups"] });
      void queryClient.invalidateQueries({ queryKey: ["sidebar-groups"] });
      setForm(null);
    },
  });

  const remove = useMutation({
    mutationFn: (g: Group) => groupsApi.remove(g.id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["groups"] });
      void queryClient.invalidateQueries({ queryKey: ["sidebar-groups"] });
      setDeleting(null);
      toast.success("Đã xóa lớp học thành công.");
    },
    onError: (e) => toast.error(e),
  });

  const columns: Column<Group>[] = [
    { key: "code", header: t("common.code"), sortKey: "code", render: (g) => <Link to={`/admin/groups/${g.id}`}>{g.code}</Link> },
    { key: "name", header: t("common.name"), sortKey: "name", render: (g) => g.name },
    { key: "members", header: "Thành viên", render: (g) => g.memberCount },
    { key: "status", header: t("common.status"), render: (g) => <ActiveBadge active={g.isActive} /> },
    {
      key: "actions",
      header: "",
      className: "text-end",
      render: (g) =>
        hasPermission(Permissions.GroupManage) && (
          <div className="d-inline-flex gap-2">
            <Button size="sm" variant="link" className="p-0 text-danger" onClick={() => setDeleting(g)}>
              {t("common.delete")}
            </Button>
          </div>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title={t("nav.groups")}
        actions={hasPermission(Permissions.GroupManage) && <Button onClick={() => setForm({ code: "", name: "", description: "" })}>{t("common.create")}</Button>}
      />
      <div className="mb-3" style={{ maxWidth: 400 }}>
        <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm theo mã hoặc tên nhóm..." />
      </div>
      <DataTable data={query.data} columns={columns} rowKey={(g) => g.id} isLoading={query.isLoading} error={query.error} sort={list.state} onSort={list.toggleSort} onPage={list.setPage} />
      
      {/* Modal Tạo nhóm */}
      <Modal show={!!form} onHide={() => setForm(null)} centered>
        <Form
          onSubmit={(e) => {
            e.preventDefault();
            create.mutate();
          }}
        >
          <Modal.Header closeButton>
            <Modal.Title as="h2" className="h5">Tạo nhóm lớp học</Modal.Title>
          </Modal.Header>
          <Modal.Body>
            {create.error && <Alert variant="danger">{describeError(create.error)}</Alert>}
            {form && (
              <>
                <Form.Group className="mb-3" controlId="group-code">
                  <Form.Label>{t("common.code")} *</Form.Label>
                  <Form.Control
                    value={form.code}
                    required
                    placeholder="VD: 12A1-2026, LOP-TOAN-01"
                    onChange={(e) => setForm({ ...form, code: e.target.value })}
                  />
                </Form.Group>
                <Form.Group className="mb-3" controlId="group-name">
                  <Form.Label>{t("common.name")} *</Form.Label>
                  <Form.Control
                    value={form.name}
                    required
                    placeholder="VD: Lớp 12A1 (Niên khóa 2025-2026)"
                    onChange={(e) => setForm({ ...form, name: e.target.value })}
                  />
                </Form.Group>
                <Form.Group className="mb-3" controlId="group-description">
                  <Form.Label>{t("common.description")}</Form.Label>
                  <Form.Control
                    as="textarea"
                    rows={2}
                    value={form.description}
                    placeholder="Mô tả nhóm / lớp học (tùy chọn)"
                    onChange={(e) => setForm({ ...form, description: e.target.value })}
                  />
                </Form.Group>
              </>
            )}
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onClick={() => setForm(null)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={create.isPending}>{t("common.create")}</Button>
          </Modal.Footer>
        </Form>
      </Modal>

      {/* Modal Xác nhận xóa */}
      <Modal show={!!deleting} onHide={() => setDeleting(null)} centered>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">Xác nhận xóa lớp học</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {deleting && (
            <div>
              <p>
                Bạn có chắc chắn muốn xóa lớp học <strong>{deleting.name}</strong> (<code>{deleting.code}</code>)?
              </p>
              {deleting.memberCount > 0 ? (
                <Alert variant="warning" className="mb-0">
                  ⚠️ Lớp học này đang có <strong>{deleting.memberCount}</strong> thành viên. Thao tác xóa sẽ giải phóng danh sách học viên và đề thi đã gán cho lớp này.
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

export function GroupDetailPage() {
  const { t } = useTranslation();
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(Permissions.GroupManage);
  const group = useQuery({ queryKey: ["group", id], queryFn: () => groupsApi.get(id) });
  const list = useListQuery();
  const members = useQuery({ queryKey: ["group-members", id, list.params], queryFn: () => groupsApi.members(id, list.params) });
  const [search, setSearch] = useState("");
  const [deletingGroup, setDeletingGroup] = useState(false);

  const candidates = useQuery({
    queryKey: ["users", "candidates", search],
    queryFn: () => usersApi.list({ keyword: search, pageSize: 10, isActive: true }),
    enabled: canManage && search.length >= 2,
  });

  const change = useMutation({
    mutationFn: async (action: () => Promise<unknown>) => action(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["group", id] });
      void queryClient.invalidateQueries({ queryKey: ["group-members", id] });
      toast.success(t("common.saved"));
    },
    onError: (e) => toast.error(e),
  });

  const removeGroup = useMutation({
    mutationFn: () => groupsApi.remove(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["groups"] });
      void queryClient.invalidateQueries({ queryKey: ["sidebar-groups"] });
      toast.success("Đã xóa lớp học thành công.");
      navigate("/admin/groups");
    },
    onError: (e) => toast.error(e),
  });

  const columns: Column<GroupMember>[] = [
    { key: "userName", header: t("auth.userNameOnly"), render: (m) => <Link to={`/admin/users/${m.userId}`}>{m.userName}</Link> },
    { key: "fullName", header: t("auth.fullName"), render: (m) => m.fullName },
    { key: "status", header: t("common.status"), render: (m) => <ActiveBadge active={m.isActive} /> },
    { key: "addedAt", header: "Ngày thêm", render: (m) => formatDateTime(m.addedAt) },
    {
      key: "actions",
      header: "",
      render: (m) =>
        canManage && (
          <Button size="sm" variant="outline-danger" onClick={() => change.mutate(() => groupsApi.removeMember(id, m.userId))}>
            {t("common.delete")}
          </Button>
        ),
    },
  ];

  if (group.error) {
    return <ErrorAlert error={group.error} />;
  }
  if (!group.data) {
    return <Loading />;
  }
  const g = group.data;

  return (
    <>
      <Link to="/admin/groups" className="small">← {t("nav.groups")}</Link>
      <PageHeader
        title={`${g.name} (${g.code})`}
        actions={
          canManage && (
            <div className="d-flex gap-2">
              <Button
                variant={g.isActive ? "outline-warning" : "outline-success"}
                onClick={() => change.mutate(() => groupsApi.update(id, { name: g.name, description: g.description, isActive: !g.isActive, rowVersion: g.rowVersion }))}
              >
                {g.isActive ? t("common.deactivate") : t("common.activate")}
              </Button>
              <Button variant="outline-danger" onClick={() => setDeletingGroup(true)}>
                {t("common.delete")}
              </Button>
            </div>
          )
        }
      >
        <ActiveBadge active={g.isActive} /> <span className="text-secondary small">{g.memberCount} thành viên</span>
      </PageHeader>

      {/* Modal Xác nhận xóa Group */}
      <Modal show={deletingGroup} onHide={() => setDeletingGroup(false)} centered>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">Xác nhận xóa lớp học</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <p>
            Bạn có chắc chắn muốn xóa lớp học <strong>{g.name}</strong> (<code>{g.code}</code>)?
          </p>
          <p className="text-secondary small mb-0">Hành động này sẽ giải phóng danh sách học viên và gán đề thi của lớp học này.</p>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={() => setDeletingGroup(false)} disabled={removeGroup.isPending}>
            {t("common.cancel")}
          </Button>
          <Button variant="danger" onClick={() => removeGroup.mutate()} disabled={removeGroup.isPending}>
            {removeGroup.isPending ? "Đang xóa..." : t("common.delete")}
          </Button>
        </Modal.Footer>
      </Modal>
      {canManage && (
        <Card className="mb-3">
          <Card.Body>
            <Form.Group controlId="member-search">
              <Form.Label>Thêm thành viên (nhập tên đăng nhập hoặc họ tên)</Form.Label>
              <Form.Control size="sm" value={search} onChange={(e) => setSearch(e.target.value)} />
            </Form.Group>
            {candidates.data && (
              <Table size="sm" className="mt-2 mb-0">
                <tbody>
                  {candidates.data.items.map((u) => (
                    <tr key={u.id}>
                      <td>{u.userName}</td>
                      <td>{u.fullName}</td>
                      <td className="text-end">
                        <Button size="sm" onClick={() => change.mutate(() => groupsApi.addMembers(id, [u.id]))}>Thêm</Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Card.Body>
        </Card>
      )}
      <div className="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-3">
        <h2 className="h6 mb-0">Danh sách thành viên</h2>
        <div style={{ maxWidth: 320 }}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} placeholder="Tìm thành viên theo mã, họ tên..." />
        </div>
      </div>
      <DataTable data={members.data} columns={columns} rowKey={(m) => m.userId} isLoading={members.isLoading} error={members.error} onPage={list.setPage} />
    </>
  );
}

// ======================= Vai trò & quyền =======================

export function RolesPage() {
  const { t } = useTranslation();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(Permissions.RoleManage);
  const roles = useQuery({ queryKey: ["roles"], queryFn: rolesApi.list });
  const permissions = useQuery({ queryKey: ["permissions"], queryFn: rolesApi.permissions });
  // id có giá trị: sửa vai trò; không có: tạo mới
  const [editing, setEditing] = useState<{ id?: string; code: string; name: string; isActive: boolean } | null>(null);
  const change = useMutation({
    mutationFn: async (action: () => Promise<unknown>) => action(),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["roles"] });
      toast.success(t("common.saved"));
      setEditing(null);
    },
    onError: (e) => toast.error(e),
  });

  if (roles.error || permissions.error) {
    return <ErrorAlert error={roles.error ?? permissions.error} />;
  }
  if (!roles.data || !permissions.data) {
    return <Loading />;
  }

  return (
    <>
      <PageHeader title={t("nav.roles")} actions={canManage && <Button onClick={() => setEditing({ code: "", name: "", isActive: true })}>{t("common.create")}</Button>} />
      <div className="table-responsive">
        <Table size="sm" bordered className="align-middle">
          <thead>
            <tr>
              <th scope="col">Permission</th>
              {roles.data.map((r) => (
                <th key={r.id} scope="col" className="text-center">
                  <div>
                    {r.code}
                    {r.isSystem && <Badge bg="secondary" className="ms-1">hệ thống</Badge>}
                    {!r.isActive && <Badge bg="warning" text="dark" className="ms-1">{t("common.inactive")}</Badge>}
                  </div>
                  <div className="small fw-normal text-secondary">{r.name}</div>
                  {canManage && !r.isSystem && (
                    <Button size="sm" variant="link" className="p-0" aria-label={`${t("common.edit")} ${r.code}`}
                      onClick={() => setEditing({ id: r.id, code: r.code, name: r.name, isActive: r.isActive })}>
                      {t("common.edit")}
                    </Button>
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {permissions.data.map((p) => (
              <tr key={p.id}>
                <th scope="row" className="fw-normal">
                  <code>{p.code}</code> <span className="small text-secondary">{p.name}</span>
                </th>
                {roles.data.map((r) => {
                  const checked = r.permissions.includes(p.code);
                  return (
                    <td key={r.id} className="text-center">
                      <Form.Check
                        aria-label={`${r.code} — ${p.code}`}
                        checked={checked}
                        disabled={!canManage || r.code === "ADMIN" || change.isPending}
                        onChange={() => {
                          const ids = permissions.data.filter((x) => (x.code === p.code ? !checked : r.permissions.includes(x.code))).map((x) => x.id);
                          change.mutate(() => rolesApi.setPermissions(r.id, ids));
                        }}
                      />
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </Table>
      </div>
      <p className="small text-secondary">Vai trò ADMIN luôn có mọi quyền. Thay đổi có hiệu lực ngay với người dùng đang đăng nhập.</p>
      <Modal show={!!editing} onHide={() => setEditing(null)} centered>
        <Form
          onSubmit={(e) => {
            e.preventDefault();
            const role = editing!;
            change.mutate(() =>
              role.id
                ? rolesApi.update(role.id, { name: role.name, isActive: role.isActive })
                : rolesApi.create({ code: role.code, name: role.name, permissionIds: [] }),
            );
          }}
        >
          <Modal.Header closeButton>
            <Modal.Title as="h2" className="h5">{editing?.id ? `Sửa vai trò ${editing.code}` : "Tạo vai trò"}</Modal.Title>
          </Modal.Header>
          <Modal.Body>
            {editing && (
              <>
                <Form.Group className="mb-3" controlId="role-code">
                  <Form.Label>{t("common.code")} *</Form.Label>
                  <Form.Control
                    value={editing.code}
                    required
                    disabled={!!editing.id}
                    placeholder="VD: TEACHER, EXAM_REVIEWER"
                    onChange={(e) => setEditing({ ...editing, code: e.target.value })}
                  />
                </Form.Group>
                <Form.Group className="mb-3" controlId="role-name">
                  <Form.Label>{t("common.name")} *</Form.Label>
                  <Form.Control
                    value={editing.name}
                    required
                    placeholder="VD: Giáo viên bộ môn"
                    onChange={(e) => setEditing({ ...editing, name: e.target.value })}
                  />
                </Form.Group>
                {editing.id && (
                  <Form.Check type="switch" id="role-active" label={t("common.active")} checked={editing.isActive}
                    onChange={(e) => setEditing({ ...editing, isActive: e.target.checked })} />
                )}
              </>
            )}
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onClick={() => setEditing(null)}>{t("common.cancel")}</Button>
            <Button type="submit" disabled={change.isPending}>{editing?.id ? t("common.save") : t("common.create")}</Button>
          </Modal.Footer>
        </Form>
      </Modal>
    </>
  );
}
