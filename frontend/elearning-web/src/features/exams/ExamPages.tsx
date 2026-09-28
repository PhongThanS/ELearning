import { useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, Modal, Row, Table } from "react-bootstrap";
import { Link, useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { examsApi, groupsApi, usersApi, type ExamDetailsBody } from "../../services/api";
import { toApiError } from "../../services/apiClient";
import { ConfirmDialog, ErrorAlert, Loading } from "../../components/common/Feedback";
import { examStatusVariant } from "../../constants/ui";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import { DataTable, PageHeader, SearchBox, type Column } from "../../components/common/DataTable";
import { useListQuery } from "../../hooks/useListQuery";
import { formatDateTime, formatNumber, utcIsoToVnLocal, vnLocalToUtcIso } from "../../utils/format";
import { Permissions } from "../../constants/permissions";
import { useAuth } from "../auth/useAuth";
import type { AccessMode, ExamDetail, ExamListItem, RetakeScoringPolicy, ReviewPolicy, ScoreVisibility } from "../../types/api";

export function ExamsPage() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  const list = useListQuery();
  const query = useQuery({ queryKey: ["exams", list.params], queryFn: () => examsApi.list(list.params) });
  const [creating, setCreating] = useState(false);

  const columns: Column<ExamListItem>[] = [
    { key: "code", header: t("common.code"), sortKey: "code", render: (e) => <Link to={`/admin/exams/${e.id}`}><code>{e.code}</code></Link> },
    { key: "name", header: t("common.name"), sortKey: "name", render: (e) => e.name },
    { key: "status", header: t("common.status"), render: (e) => <Badge bg={examStatusVariant[e.status]}>{t(`enums.examStatus.${e.status}`)}</Badge> },
    { key: "window", header: "Khung giờ", sortKey: "startAt", render: (e) => (e.startAt || e.endAt ? `${formatDateTime(e.startAt)} – ${formatDateTime(e.endAt)}` : "Không giới hạn") },
    { key: "version", header: "Phiên bản", render: (e) => (e.publishedVersionNumber ? `v${e.publishedVersionNumber}` : "—") + (e.hasDraftVersion ? " + nháp" : "") },
    { key: "access", header: "Người thi", render: (e) => t(`enums.accessMode.${e.accessMode}`) },
    { key: "attempts", header: "Lượt thi", render: (e) => e.attemptCount },
  ];

  return (
    <>
      <PageHeader title={t("nav.exams")} actions={hasPermission(Permissions.ExamCreate) && <Button onClick={() => setCreating(true)}>{t("common.create")}</Button>} />
      <Row className="g-2 mb-3">
        <Col md={5}>
          <SearchBox value={list.state.keyword ?? ""} onSearch={(keyword) => list.setFilter({ keyword })} />
        </Col>
        <Col md={3}>
          <Form.Select size="sm" aria-label={t("common.status")} value={String(list.state.status ?? "")} onChange={(e) => list.setFilter({ status: e.target.value })}>
            <option value="">{t("common.all")}</option>
            {(["DRAFT", "PUBLISHED", "CLOSED"] as const).map((s) => (
              <option key={s} value={s}>{t(`enums.examStatus.${s}`)}</option>
            ))}
          </Form.Select>
        </Col>
      </Row>
      <DataTable data={query.data} columns={columns} rowKey={(e) => e.id} isLoading={query.isLoading} error={query.error} sort={list.state} onSort={list.toggleSort} onPage={list.setPage} />
      <CreateExamModal show={creating} onHide={() => setCreating(false)} />
    </>
  );
}

interface ExamForm {
  code: string;
  name: string;
  description: string;
  instructions: string;
  startAt: string;
  endAt: string;
  maxAttempts: number;
  accessMode: AccessMode;
  retakeScoringPolicy: RetakeScoringPolicy;
}

const toBody = (f: ExamForm): ExamDetailsBody => ({
  code: f.code,
  name: f.name,
  description: f.description || null,
  instructions: f.instructions || null,
  startAt: vnLocalToUtcIso(f.startAt),
  endAt: vnLocalToUtcIso(f.endAt),
  maxAttempts: f.maxAttempts,
  accessMode: f.accessMode,
  retakeScoringPolicy: f.retakeScoringPolicy,
});

function ExamFields({ form, set, errors, codeEditable }: { form: ExamForm; set: (p: Partial<ExamForm>) => void; errors: Record<string, string>; codeEditable: boolean }) {
  const { t } = useTranslation();
  return (
    <Row className="g-2">
      <Col md={4}>
        <Form.Group controlId="exam-code">
          <Form.Label>{t("common.code")} *</Form.Label>
          <Form.Control value={form.code} disabled={!codeEditable} isInvalid={!!errors.code} onChange={(e) => set({ code: e.target.value })} />
          <Form.Control.Feedback type="invalid">{errors.code}</Form.Control.Feedback>
        </Form.Group>
      </Col>
      <Col md={8}>
        <Form.Group controlId="exam-name">
          <Form.Label>{t("common.name")} *</Form.Label>
          <Form.Control value={form.name} isInvalid={!!errors.name} onChange={(e) => set({ name: e.target.value })} />
          <Form.Control.Feedback type="invalid">{errors.name}</Form.Control.Feedback>
        </Form.Group>
      </Col>
      <Col md={12}>
        <Form.Group controlId="exam-description">
          <Form.Label>{t("common.description")}</Form.Label>
          <Form.Control as="textarea" rows={2} value={form.description} onChange={(e) => set({ description: e.target.value })} />
        </Form.Group>
      </Col>
      <Col md={12}>
        <Form.Group controlId="exam-instructions">
          <Form.Label>Hướng dẫn làm bài (Markdown)</Form.Label>
          <Form.Control as="textarea" rows={3} value={form.instructions} onChange={(e) => set({ instructions: e.target.value })} />
        </Form.Group>
      </Col>
      <Col md={6}>
        <Form.Group controlId="exam-start">
          <Form.Label>Mở lúc (giờ Việt Nam)</Form.Label>
          <Form.Control type="datetime-local" value={form.startAt} onChange={(e) => set({ startAt: e.target.value })} />
        </Form.Group>
      </Col>
      <Col md={6}>
        <Form.Group controlId="exam-end">
          <Form.Label>Đóng lúc (giờ Việt Nam)</Form.Label>
          <Form.Control type="datetime-local" value={form.endAt} isInvalid={!!errors.endAt} onChange={(e) => set({ endAt: e.target.value })} />
          <Form.Control.Feedback type="invalid">{errors.endAt}</Form.Control.Feedback>
        </Form.Group>
      </Col>
      <Col md={4}>
        <Form.Group controlId="exam-attempts">
          <Form.Label>Số lượt thi</Form.Label>
          <Form.Control type="number" min={1} max={50} value={form.maxAttempts} isInvalid={!!errors.maxAttempts} onChange={(e) => set({ maxAttempts: Number(e.target.value) })} />
          <Form.Control.Feedback type="invalid">{errors.maxAttempts}</Form.Control.Feedback>
        </Form.Group>
      </Col>
      <Col md={4}>
        <Form.Group controlId="exam-access">
          <Form.Label>Người được thi</Form.Label>
          <Form.Select value={form.accessMode} onChange={(e) => set({ accessMode: e.target.value as AccessMode })}>
            {(["ASSIGNED", "PUBLIC"] as const).map((m) => (
              <option key={m} value={m}>{t(`enums.accessMode.${m}`)}</option>
            ))}
          </Form.Select>
        </Form.Group>
      </Col>
      <Col md={4}>
        <Form.Group controlId="exam-retake">
          <Form.Label>Điểm khi thi nhiều lượt</Form.Label>
          <Form.Select value={form.retakeScoringPolicy} onChange={(e) => set({ retakeScoringPolicy: e.target.value as RetakeScoringPolicy })}>
            {(["HIGHEST", "LATEST"] as const).map((m) => (
              <option key={m} value={m}>{t(`enums.retakeScoringPolicy.${m}`)}</option>
            ))}
          </Form.Select>
        </Form.Group>
      </Col>
    </Row>
  );
}

export function VersionSettingsFields({
  value,
  onChange,
  disabled,
}: {
  value: { durationMinutes: number; passPercentage: number | null; scoreVisibility: ScoreVisibility; reviewPolicy: ReviewPolicy };
  onChange: (v: typeof value) => void;
  disabled?: boolean;
}) {
  const { t } = useTranslation();
  return (
    <Row className="g-2">
      <Col md={3}>
        <Form.Group controlId="v-duration">
          <Form.Label>Thời lượng (phút)</Form.Label>
          <Form.Control type="number" min={1} max={600} disabled={disabled} value={value.durationMinutes} onChange={(e) => onChange({ ...value, durationMinutes: Number(e.target.value) })} />
        </Form.Group>
      </Col>
      <Col md={3}>
        <Form.Group controlId="v-pass">
          <Form.Label>Tỉ lệ đạt (%)</Form.Label>
          <Form.Control
            type="number"
            min={0}
            max={100}
            disabled={disabled}
            value={value.passPercentage ?? ""}
            placeholder="Không xét"
            onChange={(e) => onChange({ ...value, passPercentage: e.target.value === "" ? null : Number(e.target.value) })}
          />
        </Form.Group>
      </Col>
      <Col md={3}>
        <Form.Group controlId="v-score">
          <Form.Label>Hiển thị điểm</Form.Label>
          <Form.Select disabled={disabled} value={value.scoreVisibility} onChange={(e) => onChange({ ...value, scoreVisibility: e.target.value as ScoreVisibility })}>
            {(["IMMEDIATE", "AFTER_EXAM_END", "HIDDEN"] as const).map((m) => (
              <option key={m} value={m}>{t(`enums.scoreVisibility.${m}`)}</option>
            ))}
          </Form.Select>
        </Form.Group>
      </Col>
      <Col md={3}>
        <Form.Group controlId="v-review">
          <Form.Label>Xem lại bài & đáp án</Form.Label>
          <Form.Select disabled={disabled} value={value.reviewPolicy} onChange={(e) => onChange({ ...value, reviewPolicy: e.target.value as ReviewPolicy })}>
            {(["NEVER", "AFTER_SUBMIT", "AFTER_EXAM_END", "AFTER_LAST_ATTEMPT"] as const).map((m) => (
              <option key={m} value={m}>{t(`enums.reviewPolicy.${m}`)}</option>
            ))}
          </Form.Select>
        </Form.Group>
      </Col>
    </Row>
  );
}

function CreateExamModal({ show, onHide }: { show: boolean; onHide: () => void }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [form, setForm] = useState<ExamForm>({
    code: "",
    name: "",
    description: "",
    instructions: "",
    startAt: "",
    endAt: "",
    maxAttempts: 1,
    accessMode: "ASSIGNED",
    retakeScoringPolicy: "HIGHEST",
  });
  const [settings, setSettings] = useState({ durationMinutes: 60, passPercentage: 50 as number | null, scoreVisibility: "IMMEDIATE" as ScoreVisibility, reviewPolicy: "NEVER" as ReviewPolicy });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const create = useMutation({
    mutationFn: () => examsApi.create({ ...toBody(form), ...settings }),
    onSuccess: (exam) => navigate(`/admin/exams/${exam.id}/versions/${exam.draftVersionId}`),
    onError: (e) => setErrors(toApiError(e).fieldErrors()),
  });

  return (
    <Modal show={show} onHide={onHide} size="lg" centered>
      <Form
        onSubmit={(e) => {
          e.preventDefault();
          setErrors({});
          create.mutate();
        }}
      >
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">Tạo đề thi</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {create.error && <Alert variant="danger">{describeError(create.error)}</Alert>}
          <ExamFields form={form} set={(p) => setForm({ ...form, ...p })} errors={errors} codeEditable />
          <hr />
          <VersionSettingsFields value={settings} onChange={setSettings} />
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onHide}>{t("common.cancel")}</Button>
          <Button type="submit" disabled={create.isPending}>{t("common.create")}</Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}

export function ExamDetailPage() {
  const { t } = useTranslation();
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const query = useQuery({ queryKey: ["exam", id], queryFn: () => examsApi.get(id) });
  const [dialog, setDialog] = useState<"close" | "delete" | "clone" | null>(null);
  const [forceSubmit, setForceSubmit] = useState(false);

  const onChanged = (exam?: ExamDetail) => {
    if (exam) {
      queryClient.setQueryData(["exam", id], exam);
    }
    void queryClient.invalidateQueries({ queryKey: ["exams"] });
    setDialog(null);
  };
  const action = useMutation({
    mutationFn: async (run: () => Promise<ExamDetail | void>) => run(),
    onSuccess: (exam) => onChanged(exam ?? undefined),
    onError: (e) => toast.error(e),
  });

  if (query.error) {
    return <ErrorAlert error={query.error} />;
  }
  if (!query.data) {
    return <Loading />;
  }
  const exam = query.data;

  return (
    <>
      <Link to="/admin/exams" className="small">← {t("nav.exams")}</Link>
      <PageHeader
        title={`${exam.name} (${exam.code})`}
        actions={
          <>
            {exam.status !== "DRAFT" && hasPermission(Permissions.ResultView) && (
              <Link className="btn btn-outline-primary" to={`/admin/exams/${id}/results`}>Kết quả & lượt thi</Link>
            )}
            {exam.status === "PUBLISHED" && hasPermission(Permissions.ExamClose) && (
              <Button variant="outline-warning" onClick={() => setDialog("close")}>Đóng đề</Button>
            )}
            {exam.status === "CLOSED" && hasPermission(Permissions.ExamClose) && (
              <Button variant="outline-success" onClick={() => action.mutate(() => examsApi.reopen(id))}>Mở lại</Button>
            )}
            {hasPermission(Permissions.ExamCreate) && <Button variant="outline-secondary" onClick={() => setDialog("clone")}>{t("common.clone")}</Button>}
            {exam.status === "DRAFT" && hasPermission(Permissions.ExamDelete) && (
              <Button variant="outline-danger" onClick={() => setDialog("delete")}>{t("common.delete")}</Button>
            )}
          </>
        }
      >
        <Badge bg={examStatusVariant[exam.status]}>{t(`enums.examStatus.${exam.status}`)}</Badge>
      </PageHeader>

      <Row className="g-3">
        <Col xl={7}>
          {/* key theo rowVersion: dữ liệu đổi (lưu / tải lại) thì form khởi tạo lại từ dữ liệu mới */}
          <ExamInfoForm key={exam.rowVersion} exam={exam} onSaved={(saved) => onChanged(saved)} />
        </Col>
        <Col xl={5}>
          <VersionsCard exam={exam} />
          <AssignmentsCard exam={exam} />
        </Col>
      </Row>

      <ConfirmDialog
        show={dialog === "close"}
        title="Đóng đề thi"
        body="Học viên không thể bắt đầu lượt mới."
        variant="warning"
        busy={action.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={() => action.mutate(() => examsApi.close(id, forceSubmit))}
      >
        <Form.Check className="mt-3" id="force-submit" label="Buộc nộp ngay mọi lượt đang làm" checked={forceSubmit} onChange={(e) => setForceSubmit(e.target.checked)} />
      </ConfirmDialog>
      <ConfirmDialog
        show={dialog === "delete"}
        title="Xóa đề thi nháp"
        body="Đề chưa từng publish sẽ bị xóa hẳn."
        variant="danger"
        busy={action.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={() =>
          action.mutate(async () => {
            await examsApi.remove(id);
            navigate("/admin/exams", { replace: true });
          })
        }
      />
      <ConfirmDialog
        show={dialog === "clone"}
        title="Nhân bản đề"
        body="Tạo đề nháp mới với cấu hình và câu hỏi của phiên bản hiện tại (không copy lịch thi và danh sách gán)."
        busy={action.isPending}
        onCancel={() => setDialog(null)}
        onConfirm={() =>
          action.mutate(async () => {
            const clone = await examsApi.clone(id, {});
            navigate(`/admin/exams/${clone.id}`);
          })
        }
      />
    </>
  );
}

function ExamInfoForm({ exam, onSaved }: { exam: ExamDetail; onSaved: (exam: ExamDetail) => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const { hasPermission } = useAuth();
  const [form, setForm] = useState<ExamForm>(() => ({
    code: exam.code,
    name: exam.name,
    description: exam.description ?? "",
    instructions: exam.instructions ?? "",
    startAt: utcIsoToVnLocal(exam.startAt),
    endAt: utcIsoToVnLocal(exam.endAt),
    maxAttempts: exam.maxAttempts,
    accessMode: exam.accessMode,
    retakeScoringPolicy: exam.retakeScoringPolicy,
  }));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const save = useMutation({
    mutationFn: () => examsApi.update(exam.id, { ...toBody(form), rowVersion: exam.rowVersion }),
    onSuccess: (saved) => {
      onSaved(saved);
      toast.success(t("common.saved"));
    },
    onError: (e) => setErrors(toApiError(e).fieldErrors()),
  });

  return (
    <Card>
      <Card.Body>
        <h2 className="h6">Thông tin đề</h2>
        {save.error && <Alert variant="danger">{describeError(save.error)}</Alert>}
        <Form
          onSubmit={(e) => {
            e.preventDefault();
            setErrors({});
            save.mutate();
          }}
        >
          <ExamFields form={form} set={(p) => setForm({ ...form, ...p })} errors={errors} codeEditable={exam.status === "DRAFT"} />
          {hasPermission(Permissions.ExamUpdate) && (
            <Button type="submit" className="mt-3" disabled={save.isPending}>{t("common.save")}</Button>
          )}
        </Form>
      </Card.Body>
    </Card>
  );
}

function VersionsCard({ exam }: { exam: ExamDetail }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const toast = useToast();
  const { hasPermission } = useAuth();
  const createVersion = useMutation({
    mutationFn: () => examsApi.createVersion(exam.id),
    onSuccess: (v) => navigate(`/admin/exams/${exam.id}/versions/${v.id}`),
    onError: (e) => toast.error(e),
  });

  return (
    <Card className="mb-3">
      <Card.Body>
        <div className="d-flex justify-content-between align-items-center mb-2">
          <h2 className="h6 mb-0">Phiên bản</h2>
          {!exam.draftVersionId && hasPermission(Permissions.ExamUpdate) && (
            <Button size="sm" variant="outline-primary" disabled={createVersion.isPending} onClick={() => createVersion.mutate()}>
              Tạo phiên bản mới
            </Button>
          )}
        </div>
        <Table size="sm" className="mb-0">
          <thead>
            <tr>
              <th>#</th>
              <th>{t("common.status")}</th>
              <th>Số câu</th>
              <th>Điểm</th>
              <th>Publish</th>
            </tr>
          </thead>
          <tbody>
            {exam.versions.map((v) => (
              <tr key={v.id}>
                <td>
                  <Link to={`/admin/exams/${exam.id}/versions/${v.id}`}>v{v.versionNumber}</Link>
                </td>
                <td>{t(`enums.versionStatus.${v.status}`)}</td>
                <td>{v.questionCount}</td>
                <td>{formatNumber(v.maxScore)}</td>
                <td>{formatDateTime(v.publishedAt)}</td>
              </tr>
            ))}
          </tbody>
        </Table>
      </Card.Body>
    </Card>
  );
}

function AssignmentsCard({ exam }: { exam: ExamDetail }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canAssign = hasPermission(Permissions.ExamAssign);
  const assignments = useQuery({ queryKey: ["assignments", exam.id], queryFn: () => examsApi.assignments(exam.id) });
  const [groupSearch, setGroupSearch] = useState("");
  const [userSearch, setUserSearch] = useState("");
  const groups = useQuery({
    queryKey: ["groups", "search", groupSearch],
    queryFn: () => groupsApi.list({ keyword: groupSearch, pageSize: 10, isActive: true }),
    enabled: canAssign && groupSearch.length >= 1,
  });
  const users = useQuery({
    queryKey: ["users", "search", userSearch],
    queryFn: () => usersApi.list({ keyword: userSearch, pageSize: 10, roleCode: "STUDENT", isActive: true }),
    enabled: canAssign && userSearch.length >= 2,
  });
  const set = useMutation({
    mutationFn: (next: { groupIds: string[]; userIds: string[] }) => examsApi.setAssignments(exam.id, next.groupIds, next.userIds),
    onSuccess: (data) => {
      queryClient.setQueryData(["assignments", exam.id], data);
      void queryClient.invalidateQueries({ queryKey: ["exam", exam.id] });
    },
    onError: (e) => toast.error(e),
  });

  const current = assignments.data;
  const groupIds = current?.groups.map((g) => g.id) ?? [];
  const userIds = current?.users.map((u) => u.id) ?? [];

  return (
    <Card>
      <Card.Body>
        <h2 className="h6">Người được thi</h2>
        {exam.accessMode === "PUBLIC" ? (
          <Alert variant="info" className="py-2 small mb-0">Đề mở cho mọi học viên; danh sách gán không có hiệu lực.</Alert>
        ) : (
          <>
            {!current && <Loading />}
            {current && (
              <>
                <div className="mb-2">
                  {current.groups.map((g) => (
                    <Badge key={g.id} bg="primary" className="me-1 mb-1">
                      Nhóm {g.code} ({g.memberCount})
                      {canAssign && (
                        <button type="button" className="btn-close btn-close-white ms-1" style={{ fontSize: "0.5rem" }} aria-label={`Bỏ nhóm ${g.code}`}
                          onClick={() => set.mutate({ groupIds: groupIds.filter((x) => x !== g.id), userIds })} />
                      )}
                    </Badge>
                  ))}
                  {current.users.map((u) => (
                    <Badge key={u.id} bg="info" className="me-1 mb-1">
                      {u.userName}
                      {canAssign && (
                        <button type="button" className="btn-close ms-1" style={{ fontSize: "0.5rem" }} aria-label={`Bỏ ${u.userName}`}
                          onClick={() => set.mutate({ groupIds, userIds: userIds.filter((x) => x !== u.id) })} />
                      )}
                    </Badge>
                  ))}
                  {current.groups.length + current.users.length === 0 && <span className="small text-danger">Chưa gán cho ai — không ai thấy đề này.</span>}
                </div>
                {canAssign && (
                  <Row className="g-2">
                    <Col md={6}>
                      <Form.Control size="sm" aria-label="Tìm nhóm" placeholder="Thêm nhóm…" value={groupSearch} onChange={(e) => setGroupSearch(e.target.value)} />
                      {groups.data?.items.filter((g) => !groupIds.includes(g.id)).map((g) => (
                        <Button key={g.id} size="sm" variant="link" className="d-block px-0" onClick={() => set.mutate({ groupIds: [...groupIds, g.id], userIds })}>
                          + {g.code} — {g.name}
                        </Button>
                      ))}
                    </Col>
                    <Col md={6}>
                      <Form.Control size="sm" aria-label="Tìm học viên" placeholder="Thêm học viên…" value={userSearch} onChange={(e) => setUserSearch(e.target.value)} />
                      {users.data?.items.filter((u) => !userIds.includes(u.id)).map((u) => (
                        <Button key={u.id} size="sm" variant="link" className="d-block px-0" onClick={() => set.mutate({ groupIds, userIds: [...userIds, u.id] })}>
                          + {u.userName} — {u.fullName}
                        </Button>
                      ))}
                    </Col>
                  </Row>
                )}
              </>
            )}
          </>
        )}
      </Card.Body>
    </Card>
  );
}
