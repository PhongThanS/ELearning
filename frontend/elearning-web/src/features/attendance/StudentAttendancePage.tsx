import { useState } from "react";
import { Card, Form } from "react-bootstrap";
import { useQuery } from "@tanstack/react-query";
import { studentApi } from "../../services/api";
import { Empty, ErrorAlert, Loading } from "../../components/common/Feedback";
import { PageHeader } from "../../components/common/DataTable";
import { MonthlyHistoryView } from "./MonthlyHistoryView";
import { vnDate } from "./attendanceLogic";

/** Trang mặc định của học sinh: điểm danh + điểm BTVN theo tháng của chính mình (giống cửa sổ lịch sử bên giáo viên). */
export function StudentAttendancePage() {
  const classes = useQuery({
    queryKey: ["student-classes"],
    queryFn: studentApi.classes,
  });
  const [picked, setPicked] = useState("");
  const [month, setMonth] = useState(() => vnDate().slice(0, 7));
  const items = classes.data ?? [];
  const classId = picked || items[0]?.id || "";
  const validMonth = /^\d{4}-\d{2}$/.test(month);

  const history = useQuery({
    queryKey: ["student-attendance", classId, month],
    queryFn: () => studentApi.attendance(classId, month),
    enabled: classId !== "" && validMonth,
  });

  return (
    <>
      <PageHeader title="Điểm danh & BTVN" />
      {classes.isLoading && <Loading />}
      {classes.error && <ErrorAlert error={classes.error} onRetry={() => void classes.refetch()} />}
      {classes.data && items.length === 0 && <Empty text="Bạn chưa được xếp vào lớp nào." />}
      {items.length > 0 && (
        <Card>
          <Card.Body>
            <div className="d-flex flex-wrap gap-3 align-items-center mb-3">
              {items.length > 1 && (
                <div className="d-flex gap-2 align-items-center">
                  <Form.Label htmlFor="my-attendance-class" className="mb-0 small fw-semibold">
                    Lớp
                  </Form.Label>
                  <Form.Select id="my-attendance-class" size="sm" value={classId} onChange={(e) => setPicked(e.target.value)} style={{ width: 260 }}>
                    {items.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name} ({c.code})
                      </option>
                    ))}
                  </Form.Select>
                </div>
              )}
              <div className="d-flex gap-2 align-items-center">
                <Form.Label htmlFor="my-attendance-month" className="mb-0 small fw-semibold">
                  Tháng
                </Form.Label>
                <Form.Control id="my-attendance-month" type="month" size="sm" value={month} onChange={(e) => setMonth(e.target.value)} style={{ width: 170 }} />
              </div>
              {items.length === 1 && <span className="small text-secondary">{items[0]!.name}</span>}
            </div>
            {history.isLoading && <Loading />}
            {history.error && <ErrorAlert error={history.error} onRetry={() => void history.refetch()} />}
            {history.data && <MonthlyHistoryView history={history.data} />}
          </Card.Body>
        </Card>
      )}
    </>
  );
}
