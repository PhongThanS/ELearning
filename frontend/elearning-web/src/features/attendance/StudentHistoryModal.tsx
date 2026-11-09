import { useState } from "react";
import { Button, Form, Modal } from "react-bootstrap";
import { useQuery } from "@tanstack/react-query";
import { attendanceApi } from "../../services/api";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import { useToast } from "../../components/common/toast";
import { MonthlyHistoryView } from "./MonthlyHistoryView";
import { buildMonthlyReport, vnDate } from "./attendanceLogic";

/** Cửa sổ "điểm BTVN theo tháng" của một học sinh trong lớp — mở từ trang chi tiết lớp học. */
export function StudentHistoryModal({
  classId,
  student,
  onHide,
}: {
  classId: string;
  student: { userId: string; fullName: string } | null;
  onHide: () => void;
}) {
  const toast = useToast();
  const [month, setMonth] = useState(() => vnDate().slice(0, 7));
  const history = useQuery({
    queryKey: ["attendance-history", classId, student?.userId, month],
    queryFn: () => attendanceApi.history(classId, student!.userId, month),
    enabled: student !== null && /^\d{4}-\d{2}$/.test(month),
  });
  const data = history.data;

  const copy = async () => {
    if (!data) return;
    try {
      await navigator.clipboard.writeText(buildMonthlyReport(data));
      toast.success("Đã sao chép tổng kết tháng.");
    } catch {
      toast.info("Trình duyệt chặn sao chép tự động — hãy bôi đen và nhấn Ctrl+C.");
    }
  };

  return (
    <Modal show={student !== null} onHide={onHide} onShow={() => setMonth(vnDate().slice(0, 7))} size="xl" centered scrollable>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          <i className="bi bi-graph-up me-2" />
          {student?.fullName} — điểm BTVN theo tháng
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <div className="d-flex flex-wrap gap-2 align-items-center mb-3">
          <Form.Label htmlFor="class-history-month" className="mb-0 small fw-semibold">
            Tháng
          </Form.Label>
          <Form.Control id="class-history-month" type="month" size="sm" value={month} onChange={(e) => setMonth(e.target.value)} style={{ width: 170 }} />
        </div>
        {history.isLoading && <Loading />}
        {history.error && <ErrorAlert error={history.error} onRetry={() => void history.refetch()} />}
        {data && <MonthlyHistoryView history={data} />}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>
          Đóng
        </Button>
        <Button disabled={!data} onClick={() => void copy()}>
          <i className="bi bi-clipboard me-1" />
          Sao chép tổng kết tháng
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
