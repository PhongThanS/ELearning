import { useEffect, useState } from "react";
import { Alert, Badge, Button, Form, Modal, Nav, Spinner, Table } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { zaloApi } from "../../services/api";
import { useToast } from "../../components/common/toast";
import { ConfirmDialog, Empty, ErrorAlert, Loading } from "../../components/common/Feedback";
import { formatDateOnly, formatDateTime } from "../../utils/format";
import type { ZaloBroadcast, ZaloBroadcastInput, ZaloMessageKind, ZaloMessageStatus } from "../../types/api";

type Tab = ZaloMessageKind | "HISTORY";

const STATUS_VARIANT: Record<ZaloMessageStatus, string> = {
  PENDING: "secondary",
  SENDING: "info",
  SENT: "success",
  FAILED: "danger",
  CANCELLED: "dark",
};

/** Giá trị đã trễ vài trăm mili-giây để không gọi xem trước ở mỗi phím gõ. */
function useDebounced<T>(value: T, delayMs = 500): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(id);
  }, [value, delayMs]);
  return debounced;
}

/**
 * Gửi tin Zalo tự động cho phụ huynh cả lớp (D-45): báo cáo từng học sinh theo buổi học hoặc thông báo chung.
 * Tin vào hàng đợi, máy chủ gửi dần bằng Zalo OA (ZNS); tab "Đã gửi" theo dõi kết quả, gửi lại tin lỗi, hủy tin đang chờ.
 */
export function ZaloBroadcastModal({
  show,
  onHide,
  classId,
  className,
  date,
}: {
  show: boolean;
  onHide: () => void;
  classId: string;
  className: string;
  /** Ngày buổi học yyyy-MM-dd. */
  date: string;
}) {
  const { t } = useTranslation();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<Tab>("DAILY_REPORT");
  const [deadline, setDeadline] = useState("");
  const [attention, setAttention] = useState("");
  const [content, setContent] = useState("");
  const [confirm, setConfirm] = useState(false);

  const status = useQuery({ queryKey: ["zalo-status"], queryFn: zaloApi.status, enabled: show, staleTime: 60_000 });
  const enabled = status.data?.enabled === true;
  const kind: ZaloMessageKind | null = tab === "HISTORY" ? null : tab;

  const debouncedDeadline = useDebounced(deadline);
  const debouncedAttention = useDebounced(attention);
  const debouncedContent = useDebounced(content);

  const buildInput = (k: ZaloMessageKind, preview: boolean, force = false): ZaloBroadcastInput =>
    k === "DAILY_REPORT"
      ? {
          kind: k,
          sessionDate: date,
          deadline: (preview ? debouncedDeadline : deadline).trim() || null,
          attention: (preview ? debouncedAttention : attention).trim() || null,
          force,
        }
      : { kind: k, content: ((preview ? debouncedContent : content).trim() || (preview ? "…" : "")), force };

  const preview = useQuery({
    queryKey: ["zalo-preview", classId, kind, date, debouncedDeadline, debouncedAttention, debouncedContent],
    queryFn: () => zaloApi.preview(classId, buildInput(kind!, true)),
    enabled: show && enabled && kind !== null,
    gcTime: 0,
  });

  const history = useQuery({
    queryKey: ["zalo-broadcasts", classId],
    queryFn: () => zaloApi.list(classId),
    enabled: show && enabled,
    // Còn tin đang chờ thì cập nhật mỗi 5 giây để thấy tiến độ
    refetchInterval: (q) => (q.state.data?.some((b) => b.pending > 0) ? 5000 : false),
  });

  const send = useMutation({
    mutationFn: (force: boolean) => zaloApi.create(classId, buildInput(kind!, false, force)),
    onSuccess: async (created) => {
      toast.success(t("zalo.queued", { count: created.broadcast.total }));
      setConfirm(false);
      setContent("");
      setTab("HISTORY");
      await queryClient.invalidateQueries({ queryKey: ["zalo-broadcasts", classId] });
    },
    onError: (e) => {
      setConfirm(false);
      toast.error(e);
    },
  });

  const data = preview.data;
  const canSend =
    enabled && kind !== null && data !== undefined && data.sendableCount > 0 && !send.isPending && (kind !== "ANNOUNCEMENT" || content.trim().length > 0);
  const noSessionData = kind === "DAILY_REPORT" && data !== undefined && data.recipients.length === 0;
  const firstSample = data?.recipients.find((r) => r.data !== null)?.data ?? null;

  return (
    <Modal show={show} onHide={onHide} size="xl" centered scrollable>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          <i className="bi bi-send-check me-2" aria-hidden="true" />
          {t("zalo.title")} — {className} · {formatDateOnly(date)}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {status.isLoading && <Loading />}
        {status.error !== null && <ErrorAlert error={status.error} onRetry={() => void status.refetch()} />}
        {status.data && !enabled && <Alert variant="warning">{t("zalo.disabled")}</Alert>}
        {status.data?.enabled && status.data.provider === "Log" && <Alert variant="info">{t("zalo.logMode")}</Alert>}
        {status.data?.enabled && status.data.sandboxMode && <Alert variant="info">{t("zalo.sandbox")}</Alert>}
        {status.data && status.data.missingTemplates.length > 0 && (
          <Alert variant="warning">{t("zalo.missingTemplates", { names: status.data.missingTemplates.join(", ") })}</Alert>
        )}

        {enabled && (
          <>
            <Nav variant="tabs" className="mb-3" activeKey={tab} onSelect={(k) => k && setTab(k as Tab)}>
              <Nav.Item>
                <Nav.Link eventKey="DAILY_REPORT">{t("zalo.tabReport")}</Nav.Link>
              </Nav.Item>
              <Nav.Item>
                <Nav.Link eventKey="ANNOUNCEMENT">{t("zalo.tabAnnouncement")}</Nav.Link>
              </Nav.Item>
              <Nav.Item>
                <Nav.Link eventKey="HISTORY">
                  {t("zalo.tabHistory")}
                  {history.data && history.data.length > 0 && (
                    <Badge bg="secondary" className="ms-1">
                      {history.data.length}
                    </Badge>
                  )}
                </Nav.Link>
              </Nav.Item>
            </Nav>

            {kind === "DAILY_REPORT" && (
              <>
                <p className="text-secondary small">{t("zalo.reportHint")}</p>
                <div className="row g-3 mb-3">
                  <Form.Group className="col-md-6" controlId="zalo-deadline">
                    <Form.Label>{t("zalo.deadline")}</Form.Label>
                    <Form.Control value={deadline} maxLength={200} placeholder={t("zalo.deadlinePlaceholder")} onChange={(e) => setDeadline(e.target.value)} />
                  </Form.Group>
                  <Form.Group className="col-md-6" controlId="zalo-attention">
                    <Form.Label>{t("zalo.attention")}</Form.Label>
                    <Form.Control value={attention} maxLength={200} placeholder={t("zalo.attentionPlaceholder")} onChange={(e) => setAttention(e.target.value)} />
                  </Form.Group>
                </div>
              </>
            )}

            {kind === "ANNOUNCEMENT" && (
              <>
                <p className="text-secondary small">{t("zalo.announcementHint")}</p>
                <Form.Group className="mb-3" controlId="zalo-content">
                  <Form.Label>{t("zalo.content")}</Form.Label>
                  <Form.Control
                    as="textarea"
                    rows={3}
                    value={content}
                    maxLength={200}
                    placeholder={t("zalo.contentPlaceholder")}
                    onChange={(e) => setContent(e.target.value)}
                  />
                  <Form.Text className="text-secondary">{content.length}/200</Form.Text>
                </Form.Group>
              </>
            )}

            {kind !== null && (
              <>
                {preview.isLoading && <Loading />}
                {preview.error !== null && <ErrorAlert error={preview.error} onRetry={() => void preview.refetch()} />}
                {noSessionData && <Alert variant="secondary">{t("zalo.sessionNotMarked")}</Alert>}
                {data && data.recipients.length > 0 && (
                  <>
                    <h3 className="h6">
                      {t("zalo.recipients")} — {t("zalo.willSend")} {data.sendableCount}/{data.recipients.length}
                    </h3>
                    <Table size="sm" responsive hover className="align-middle">
                      <thead>
                        <tr>
                          <th>{t("zalo.colStudent")}</th>
                          <th>{t("zalo.colParent")}</th>
                          <th>{t("zalo.colPhone")}</th>
                          <th>{t("zalo.colStatus")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {data.recipients.map((r) => (
                          <tr key={r.studentId} className={r.skipReason ? "text-secondary" : undefined}>
                            <td>{r.studentName}</td>
                            <td>{r.parentName ?? "—"}</td>
                            <td>{r.parentPhone ?? "—"}</td>
                            <td>
                              {r.skipReason ? (
                                <Badge bg="warning" text="dark">
                                  {t(`zalo.skip.${r.skipReason}`)}
                                </Badge>
                              ) : (
                                <Badge bg="success">{t("zalo.willSend")}</Badge>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </Table>
                    {data.sendableCount === 0 && <Alert variant="warning">{t("zalo.nobody")}</Alert>}
                    {firstSample && (
                      <details className="small mb-2">
                        <summary>{t("zalo.sample")}</summary>
                        <dl className="row mb-0 mt-2">
                          {Object.entries(firstSample).map(([k, v]) => (
                            <div className="col-md-6 d-flex gap-2" key={k}>
                              <dt className="text-secondary fw-normal">{k}:</dt>
                              <dd className="mb-1">{v}</dd>
                            </div>
                          ))}
                        </dl>
                      </details>
                    )}
                  </>
                )}
              </>
            )}

            {tab === "HISTORY" && (
              <HistoryList
                classId={classId}
                broadcasts={history.data}
                loading={history.isLoading}
                error={history.error}
                onChanged={() => void queryClient.invalidateQueries({ queryKey: ["zalo-broadcasts", classId] })}
              />
            )}
          </>
        )}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>
          {t("common.close")}
        </Button>
        {kind !== null && (
          <Button disabled={!canSend} onClick={() => setConfirm(true)}>
            {send.isPending ? <Spinner size="sm" className="me-1" /> : <i className="bi bi-send me-1" aria-hidden="true" />}
            {t("zalo.sendButton", { count: data?.sendableCount ?? 0 })}
          </Button>
        )}
      </Modal.Footer>

      <ConfirmDialog
        show={confirm}
        title={t("zalo.confirmTitle", { count: data?.sendableCount ?? 0 })}
        body={
          <>
            <p>{t("zalo.confirmBody")}</p>
            {data?.alreadySent === true && kind === "DAILY_REPORT" && <p className="text-danger mb-0">{t("zalo.confirmResend")}</p>}
          </>
        }
        confirmText={t("zalo.confirmSend")}
        onConfirm={() => send.mutate(data?.alreadySent === true)}
        onCancel={() => setConfirm(false)}
      />
    </Modal>
  );
}

function HistoryList({
  classId,
  broadcasts,
  loading,
  error,
  onChanged,
}: {
  classId: string;
  broadcasts: ZaloBroadcast[] | undefined;
  loading: boolean;
  error: unknown;
  onChanged: () => void;
}) {
  const { t } = useTranslation();
  const toast = useToast();
  const [openId, setOpenId] = useState<string | null>(null);

  const act = useMutation({
    mutationFn: ({ id, action }: { id: string; action: "retry" | "cancel" }) =>
      action === "retry" ? zaloApi.retry(classId, id) : zaloApi.cancel(classId, id),
    onSuccess: (_, vars) => {
      toast.success(t(vars.action === "retry" ? "zalo.retried" : "zalo.cancelled"));
      onChanged();
      void detailRefetch();
    },
    onError: (e) => toast.error(e),
  });

  const detail = useQuery({
    queryKey: ["zalo-broadcast", classId, openId],
    queryFn: () => zaloApi.get(classId, openId!),
    enabled: openId !== null,
    refetchInterval: (q) => (q.state.data && q.state.data.broadcast.pending > 0 ? 5000 : false),
  });
  const detailRefetch = () => detail.refetch();

  if (loading) return <Loading />;
  if (error !== null && error !== undefined) return <ErrorAlert error={error} />;
  if (!broadcasts || broadcasts.length === 0) return <Empty text={t("zalo.historyEmpty")} />;

  return (
    <div className="d-flex flex-column gap-2">
      {broadcasts.map((b) => (
        <div key={b.id} className="border rounded p-2">
          <div className="d-flex flex-wrap align-items-center gap-2">
            <strong>
              {t(`zalo.kind.${b.kind}`)}
              {b.sessionDate ? ` · ${formatDateOnly(b.sessionDate)}` : ""}
            </strong>
            <span className="text-secondary small">
              {formatDateTime(b.createdAt)}
              {b.createdByName ? ` · ${t("zalo.by", { name: b.createdByName })}` : ""}
            </span>
            <span className="ms-auto d-flex flex-wrap gap-1 align-items-center">
              <Badge bg="success">{t("zalo.counts", { sent: b.sent, total: b.total })}</Badge>
              {b.pending > 0 && <Badge bg="secondary">{t("zalo.status.PENDING")} {b.pending}</Badge>}
              {b.failed > 0 && <Badge bg="danger">{t("zalo.status.FAILED")} {b.failed}</Badge>}
              {b.cancelled > 0 && <Badge bg="dark">{t("zalo.status.CANCELLED")} {b.cancelled}</Badge>}
            </span>
          </div>
          {b.content && <div className="small mt-1">{b.content}</div>}
          <div className="d-flex flex-wrap gap-2 mt-2">
            <Button size="sm" variant="outline-secondary" onClick={() => setOpenId(openId === b.id ? null : b.id)}>
              {openId === b.id ? t("zalo.hideDetail") : t("zalo.detail")}
            </Button>
            {b.failed > 0 && (
              <Button size="sm" variant="outline-primary" disabled={act.isPending} onClick={() => act.mutate({ id: b.id, action: "retry" })}>
                <i className="bi bi-arrow-repeat me-1" aria-hidden="true" />
                {t("zalo.retry")}
              </Button>
            )}
            {b.pending > 0 && (
              <Button size="sm" variant="outline-danger" disabled={act.isPending} onClick={() => act.mutate({ id: b.id, action: "cancel" })}>
                {t("zalo.cancel")}
              </Button>
            )}
          </div>
          {openId === b.id && (
            <div className="mt-2">
              {detail.isLoading && <Loading />}
              {detail.data && (
                <Table size="sm" responsive className="mb-0 align-middle">
                  <thead>
                    <tr>
                      <th>{t("zalo.colStudent")}</th>
                      <th>{t("zalo.colParent")}</th>
                      <th>{t("zalo.colPhone")}</th>
                      <th>{t("zalo.colStatus")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.data.messages.map((m) => (
                      <tr key={m.id}>
                        <td>{m.studentName}</td>
                        <td>{m.recipientName ?? "—"}</td>
                        <td>{m.phone}</td>
                        <td>
                          <Badge bg={STATUS_VARIANT[m.status]}>{t(`zalo.status.${m.status}`)}</Badge>
                          {m.errorMessage && <span className="small text-danger ms-2">{m.errorMessage}</span>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              )}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
