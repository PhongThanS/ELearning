import { useMemo, useRef, useState } from "react";
import { Alert, Button, ButtonGroup, Form, Modal, Table } from "react-bootstrap";
import { useToast } from "../../components/common/toast";
import type { AttendanceStudent } from "../../types/api";
import {
  ZALO_PLACEHOLDERS,
  ZALO_PRESETS,
  copyTextNow,
  fillZaloTemplate,
  loadSent,
  saveSent,
  zaloChatUrl,
  type ZaloContext,
  type ZaloPreset,
} from "./zaloPerParent";

/**
 * Nhắn Zalo từng phụ huynh — miễn phí: gõ nội dung một lần, phần mềm điền tên từng học sinh, lấy số điện thoại
 * phụ huynh đã lưu; bấm số điện thoại thì tin được sao chép và Zalo mở đúng cuộc trò chuyện, thầy cô dán (Ctrl+V) rồi gửi,
 * xong tự tích ô "Đã gửi".
 */
export function ZaloPerParentModal({
  show,
  onHide,
  classId,
  context,
  students,
}: {
  show: boolean;
  onHide: () => void;
  classId: string;
  context: ZaloContext;
  students: AttendanceStudent[];
}) {
  return (
    <Modal show={show} onHide={onHide} size="xl" scrollable>
      {show && <Body classId={classId} context={context} students={students} onHide={onHide} />}
    </Modal>
  );
}

function Body({
  classId,
  context,
  students,
  onHide,
}: {
  classId: string;
  context: ZaloContext;
  students: AttendanceStudent[];
  onHide: () => void;
}) {
  const toast = useToast();
  const [preset, setPreset] = useState<ZaloPreset>("announcement");
  const [template, setTemplate] = useState(ZALO_PRESETS.announcement.text);
  const [onlyMarked, setOnlyMarked] = useState(false);
  const sentKey = `zalo-per-parent:${classId}:${context.date}`;
  const [sent, setSent] = useState(() => loadSent(sentKey));
  const [previewId, setPreviewId] = useState<string | null>(null);
  const editor = useRef<HTMLTextAreaElement>(null);

  const rows = useMemo(
    () => students.filter((s) => s.inClass && (!onlyMarked || s.status !== null)),
    [students, onlyMarked],
  );
  const withPhone = rows.filter((s) => zaloChatUrl(s.parentPhone));
  const sentCount = withPhone.filter((s) => sent.has(s.studentId)).length;
  const previewStudent = rows.find((s) => s.studentId === previewId) ?? withPhone[0] ?? rows[0];

  const choosePreset = (p: ZaloPreset) => {
    setPreset(p);
    setTemplate(ZALO_PRESETS[p].text);
    setOnlyMarked(p === "report");
  };

  const insert = (key: string) => {
    const area = editor.current;
    const token = `{${key}}`;
    const start = area?.selectionStart ?? template.length;
    const end = area?.selectionEnd ?? template.length;
    setTemplate(template.slice(0, start) + token + template.slice(end));
    requestAnimationFrame(() => {
      area?.focus();
      area?.setSelectionRange(start + token.length, start + token.length);
    });
  };

  const markSent = (studentId: string, value: boolean) => {
    const next = new Set(sent);
    if (value) {
      next.add(studentId);
    } else {
      next.delete(studentId);
    }
    setSent(next);
    saveSent(sentKey, next);
  };

  const openZalo = (s: AttendanceStudent) => {
    const url = zaloChatUrl(s.parentPhone);
    if (!url) {
      return;
    }
    const text = fillZaloTemplate(template, s, context);
    // Sao chép trước khi mở tab (cùng một lần bấm), để trình duyệt cho phép
    const copied = copyTextNow(text);
    window.open(url, "_blank", "noopener");
    setPreviewId(s.studentId);
    if (copied) {
      toast.success(`Đã sao chép tin cho PH ${s.fullName}. Dán (Ctrl+V) vào Zalo rồi bấm gửi.`);
    } else {
      toast.info("Trình duyệt không cho sao chép tự động — hãy bôi đen phần Xem trước và nhấn Ctrl+C.");
    }
  };

  return (
    <>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          <i className="bi bi-chat-dots me-2 text-primary" aria-hidden="true" />
          Nhắn Zalo từng phụ huynh — {context.className}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <Alert variant="info" className="small py-2">
          <strong>Miễn phí, gửi từ Zalo của thầy cô.</strong> Gõ nội dung một lần, phần mềm tự điền tên từng em. Bấm vào{" "}
          <strong>số điện thoại</strong> phụ huynh: tin được sao chép sẵn và Zalo mở đúng cuộc trò chuyện → nhấn <kbd>Ctrl</kbd>+
          <kbd>V</kbd> rồi <strong>Gửi</strong>. Gửi xong thì <strong>tích ô “Đã gửi”</strong>. Phụ huynh chưa kết bạn thì Zalo có thể
          giới hạn số tin gửi người lạ.
        </Alert>

        <div className="row g-3">
          <div className="col-lg-6">
            <div className="d-flex flex-wrap align-items-center gap-2 mb-2">
              <span className="small fw-semibold">Mẫu:</span>
              <ButtonGroup size="sm">
                {(Object.keys(ZALO_PRESETS) as ZaloPreset[]).map((p) => (
                  <Button key={p} variant={preset === p ? "primary" : "outline-primary"} onClick={() => choosePreset(p)}>
                    {ZALO_PRESETS[p].label}
                  </Button>
                ))}
              </ButtonGroup>
            </div>
            <Form.Group controlId="zalo-per-parent-text">
              <Form.Label className="small fw-semibold mb-1">Nội dung tin nhắn</Form.Label>
              <Form.Control
                ref={editor}
                as="textarea"
                rows={9}
                value={template}
                onChange={(e) => setTemplate(e.target.value)}
              />
            </Form.Group>
            <div className="small text-secondary mt-2 mb-1">Bấm để chèn chỗ trống (tự thay theo từng học sinh):</div>
            <div className="d-flex flex-wrap gap-1">
              {ZALO_PLACEHOLDERS.map((p) => (
                <Button key={p.key} size="sm" variant="outline-secondary" className="py-0" title={p.hint} onClick={() => insert(p.key)}>
                  {`{${p.key}}`}
                </Button>
              ))}
            </div>

            {previewStudent && (
              <div className="mt-3">
                <div className="small fw-semibold mb-1">Xem trước — PH em {previewStudent.fullName}</div>
                <pre className="border rounded bg-light p-2 small mb-0" style={{ whiteSpace: "pre-wrap" }}>
                  {fillZaloTemplate(template, previewStudent, context)}
                </pre>
              </div>
            )}
          </div>

          <div className="col-lg-6">
            <div className="d-flex flex-wrap align-items-center gap-2 mb-2">
              <span className="small">
                Đã gửi <strong>{sentCount}</strong> / {withPhone.length} phụ huynh có số điện thoại
              </span>
              <Form.Check
                id="zalo-only-marked"
                className="small ms-auto"
                label="Chỉ học sinh đã điểm danh"
                checked={onlyMarked}
                onChange={(e) => setOnlyMarked(e.target.checked)}
              />
              {sent.size > 0 && (
                <Button
                  size="sm"
                  variant="link"
                  className="p-0"
                  onClick={() => {
                    setSent(new Set());
                    saveSent(sentKey, new Set());
                  }}
                >
                  Bỏ đánh dấu
                </Button>
              )}
            </div>
            <div className="table-responsive border rounded" style={{ maxHeight: 460 }}>
              <Table size="sm" hover className="mb-0 align-middle small">
                <thead className="table-light sticky-top">
                  <tr>
                    <th>#</th>
                    <th>Học sinh / phụ huynh</th>
                    <th>SĐT phụ huynh</th>
                    <th className="text-center">Đã gửi</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((s, i) => {
                    const url = zaloChatUrl(s.parentPhone);
                    const done = sent.has(s.studentId);
                    return (
                      <tr
                        key={s.studentId}
                        className={previewStudent?.studentId === s.studentId ? "table-active" : undefined}
                        onClick={() => setPreviewId(s.studentId)}
                        role="button"
                      >
                        <td>{i + 1}</td>
                        <td>
                          <div className="fw-semibold">{s.fullName}</div>
                          <div className="text-secondary">{s.parentName || "—"}</div>
                        </td>
                        <td className="text-nowrap">
                          {url ? (
                            <Button
                              variant="link"
                              size="sm"
                              className="p-0"
                              title="Sao chép tin và mở Zalo với số này"
                              onClick={(e) => {
                                e.stopPropagation();
                                openZalo(s);
                              }}
                            >
                              <i className="bi bi-chat-dots me-1" aria-hidden="true" />
                              {s.parentPhone}
                            </Button>
                          ) : (
                            <span className="text-danger">Chưa có SĐT</span>
                          )}
                        </td>
                        <td className="text-center" onClick={(e) => e.stopPropagation()}>
                          {url ? (
                            <Form.Check
                              id={`zalo-sent-${s.studentId}`}
                              className="d-inline-block"
                              aria-label={`Đã gửi cho phụ huynh em ${s.fullName}`}
                              checked={done}
                              onChange={(e) => markSent(s.studentId, e.target.checked)}
                            />
                          ) : (
                            <span className="text-secondary">—</span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                  {rows.length === 0 && (
                    <tr>
                      <td colSpan={4} className="text-center text-secondary py-3">
                        Không có học sinh nào.
                      </td>
                    </tr>
                  )}
                </tbody>
              </Table>
            </div>
          </div>
        </div>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>
          Đóng
        </Button>
      </Modal.Footer>
    </>
  );
}
