import { useCallback, useMemo, useState, type ReactNode } from "react";
import { Alert, Button, Form, Modal, Spinner, Toast, ToastContainer } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import { toApiError } from "../../services/apiClient";
import { describeError } from "../../utils/errors";
import { ToastContext, type ToastVariant } from "./toast";

export function Loading({ text }: { text?: string }) {
  const { t } = useTranslation();
  return (
    <div className="d-flex align-items-center gap-2 text-secondary py-3" role="status">
      <Spinner animation="border" size="sm" />
      <span>{text ?? t("common.loading")}</span>
    </div>
  );
}

/** Hiển thị lỗi API theo mã (i18n), kèm traceId để tra log. */
export function ErrorAlert({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  if (!error) {
    return null;
  }
  const apiError = toApiError(error);
  return (
    <Alert variant="danger" className="d-flex justify-content-between align-items-start gap-3">
      <div>
        <div>{describeError(apiError)}</div>
        {apiError.errors.length > 1 && (
          <ul className="mb-0 mt-2 small">
            {apiError.errors.slice(1).map((e, i) => (
              <li key={i}>{e.message}</li>
            ))}
          </ul>
        )}
        {apiError.traceId && <div className="small text-secondary mt-1">Mã tra cứu: {apiError.traceId}</div>}
      </div>
      {onRetry && (
        <Button size="sm" variant="outline-danger" onClick={onRetry}>
          {t("common.reload")}
        </Button>
      )}
    </Alert>
  );
}

export function Empty({ text }: { text?: string }) {
  const { t } = useTranslation();
  return <p className="text-secondary py-3 mb-0">{text ?? t("common.noData")}</p>;
}

// ----- Toast -----

interface ToastItem {
  id: number;
  variant: ToastVariant;
  text: string;
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const show = useCallback((text: string, variant: ToastItem["variant"] = "success") => {
    const id = Date.now() + Math.random();
    setItems((current) => [...current, { id, variant, text }]);
  }, []);
  const value = useMemo(() => ({ show }), [show]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <ToastContainer position="bottom-end" className="p-3" style={{ zIndex: 2000 }}>
        {items.map((item) => (
          <Toast
            key={item.id}
            bg={item.variant}
            autohide
            delay={4000}
            onClose={() => setItems((current) => current.filter((i) => i.id !== item.id))}
          >
            <Toast.Body className={item.variant === "warning" ? "" : "text-white"} role="status">
              {item.text}
            </Toast.Body>
          </Toast>
        ))}
      </ToastContainer>
    </ToastContext.Provider>
  );
}

// ----- Hộp thoại xác nhận (có thể bắt buộc lý do) -----

export function ConfirmDialog({
  show,
  title,
  body,
  confirmText,
  variant = "primary",
  requireReason = false,
  busy = false,
  onConfirm,
  onCancel,
  children,
}: {
  show: boolean;
  title: string;
  body?: ReactNode;
  confirmText?: string;
  variant?: string;
  requireReason?: boolean;
  busy?: boolean;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const [reason, setReason] = useState("");
  const canConfirm = !busy && (!requireReason || reason.trim().length > 0);

  return (
    <Modal show={show} onHide={onCancel} onExited={() => setReason("")} centered>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          {title}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {body}
        {children}
        {requireReason && (
          <Form.Group className="mt-3" controlId="confirm-reason">
            <Form.Label>{t("common.reason")} *</Form.Label>
            <Form.Control as="textarea" rows={2} value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
          </Form.Group>
        )}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onCancel} disabled={busy}>
          {t("common.cancel")}
        </Button>
        <Button variant={variant} onClick={() => onConfirm(reason.trim())} disabled={!canConfirm}>
          {busy && <Spinner animation="border" size="sm" className="me-2" />}
          {confirmText ?? t("common.confirm")}
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
