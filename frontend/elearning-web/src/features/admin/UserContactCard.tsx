import { useState } from "react";
import { Button, Card, Col, Form, Row, Spinner } from "react-bootstrap";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { usersApi } from "../../services/api";
import { useToast } from "../../components/common/toast";
import type { UserDetail } from "../../types/api";

/** Số điện thoại → link gọi (chỉ giữ chữ số và dấu +). */
function telHref(phone: string) {
  return `tel:${phone.replace(/[^\d+]/g, "")}`;
}

/**
 * Thông tin liên hệ của học sinh (D-43): SĐT học sinh, tên và SĐT phụ huynh.
 * Dùng chung dữ liệu với liên hệ phụ huynh ở trang Điểm danh.
 */
export function UserContactCard({ user, canEdit }: { user: UserDetail; canEdit: boolean }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const initial = { studentPhone: user.studentPhone ?? "", parentName: user.parentName ?? "", parentPhone: user.parentPhone ?? "" };
  const [form, setForm] = useState<typeof initial | null>(null);
  const values = form ?? initial;

  const save = useMutation({
    mutationFn: () =>
      usersApi.updateContact(user.id, {
        studentPhone: values.studentPhone.trim() || null,
        parentName: values.parentName.trim() || null,
        parentPhone: values.parentPhone.trim() || null,
      }),
    onSuccess: (data) => {
      queryClient.setQueryData(["user", user.id], data);
      void queryClient.invalidateQueries({ queryKey: ["attendance-session"] });
      setForm(null);
      toast.success("Đã lưu thông tin liên hệ.");
    },
    onError: (e) => toast.error(e),
  });

  const editable = canEdit && !user.anonymizedAt;
  const set = (patch: Partial<typeof initial>) => setForm({ ...values, ...patch });
  const phoneLink = (phone: string | null | undefined) =>
    phone ? (
      <a href={telHref(phone)} className="ms-2 small text-decoration-none" title={`Gọi ${phone}`}>
        <i className="bi bi-telephone" aria-hidden="true" /> Gọi
      </a>
    ) : null;

  return (
    <Card className="mt-3">
      <Card.Body>
        <h2 className="h6">
          <i className="bi bi-person-lines-fill me-1" aria-hidden="true" />
          Liên hệ học sinh &amp; phụ huynh
        </h2>
        <Form
          onSubmit={(e) => {
            e.preventDefault();
            save.mutate();
          }}
        >
          <Row className="g-2">
            <Col sm={6}>
              <Form.Group controlId="user-student-phone">
                <Form.Label className="small mb-1">
                  SĐT học sinh {phoneLink(user.studentPhone)}
                </Form.Label>
                <Form.Control
                  size="sm"
                  inputMode="tel"
                  maxLength={20}
                  placeholder="VD: 0912 345 678"
                  value={values.studentPhone}
                  disabled={!editable}
                  onChange={(e) => set({ studentPhone: e.target.value })}
                />
              </Form.Group>
            </Col>
            <Col sm={6}>
              <Form.Group controlId="user-parent-phone">
                <Form.Label className="small mb-1">
                  SĐT phụ huynh {phoneLink(user.parentPhone)}
                </Form.Label>
                <Form.Control
                  size="sm"
                  inputMode="tel"
                  maxLength={20}
                  placeholder="VD: 0987 654 321"
                  value={values.parentPhone}
                  disabled={!editable}
                  onChange={(e) => set({ parentPhone: e.target.value })}
                />
              </Form.Group>
            </Col>
            <Col sm={12}>
              <Form.Group controlId="user-parent-name">
                <Form.Label className="small mb-1">Tên phụ huynh</Form.Label>
                <Form.Control
                  size="sm"
                  maxLength={200}
                  placeholder="VD: Mẹ Nguyễn Thị Lan"
                  value={values.parentName}
                  disabled={!editable}
                  onChange={(e) => set({ parentName: e.target.value })}
                />
              </Form.Group>
            </Col>
          </Row>
          {editable && (
            <div className="d-flex gap-2 mt-2">
              <Button type="submit" size="sm" disabled={!form || save.isPending}>
                {save.isPending && <Spinner size="sm" className="me-1" />}
                Lưu liên hệ
              </Button>
              {form && (
                <Button size="sm" variant="outline-secondary" onClick={() => setForm(null)}>
                  Hủy
                </Button>
              )}
            </div>
          )}
        </Form>
      </Card.Body>
    </Card>
  );
}
