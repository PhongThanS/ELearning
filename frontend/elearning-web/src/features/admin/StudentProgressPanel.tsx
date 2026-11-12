import { useState } from "react";
import { Badge, ButtonGroup, Button, ProgressBar, Table } from "react-bootstrap";
import { Link } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { classesApi } from "../../services/api";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import { formatDateTime, formatNumber } from "../../utils/format";
import type { StudentClassExamItem } from "../../types/api";
import "../../components/common/tableCells.css";

type Filter = "all" | "done" | "notStarted" | "inProgress";

/** Trang mở khi bấm một bài: đã nộp → bài làm + kết quả; đang làm → bài dở; chưa làm → xem đề (chỉ đọc). */
function targetOf(item: StudentClassExamItem): { to: string; label: string } {
  if (item.isSubmitted && item.latestAttemptId) return { to: `/admin/attempts/${item.latestAttemptId}`, label: "Xem kết quả" };
  if (item.isInProgress && item.inProgressAttemptId) return { to: `/admin/attempts/${item.inProgressAttemptId}`, label: "Xem bài đang làm" };
  return { to: `/admin/exams/${item.examId}/preview`, label: "Xem đề" };
}

function StatusBadge({ item }: { item: StudentClassExamItem }) {
  if (item.isSubmitted) {
    if (item.passed === false) return <Badge bg="danger">Chưa đạt</Badge>;
    return <Badge bg="success">{item.passed === true ? "Đạt" : "Đã nộp"}</Badge>;
  }
  if (item.isInProgress) return <Badge bg="warning" text="dark">Đang làm</Badge>;
  return <Badge bg="secondary">Chưa làm</Badge>;
}

/**
 * Danh sách bài tập (đề được giao) của một học sinh trong lớp: đã làm / chưa làm / đang làm.
 * Bấm tên bài hoặc "Xem kết quả" để mở bài làm và kết quả ở tab mới (giữ nguyên cửa sổ đang xem).
 */
export function StudentProgressPanel({ classroomId, userId }: { classroomId: string; userId: string }) {
  const [filter, setFilter] = useState<Filter>("all");
  const query = useQuery({
    queryKey: ["class-student-progress", classroomId, userId],
    queryFn: () => classesApi.studentProgress(classroomId, userId),
  });

  if (query.isLoading) return <Loading />;
  if (query.error) return <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />;
  const data = query.data;
  if (!data) return null;

  const done = data.exams.filter((e) => e.isSubmitted);
  const inProgress = data.exams.filter((e) => !e.isSubmitted && e.isInProgress);
  const notStarted = data.exams.filter((e) => !e.isSubmitted && !e.isInProgress);
  const shown = filter === "done" ? done : filter === "inProgress" ? inProgress : filter === "notStarted" ? notStarted : data.exams;
  const pct = data.totalAssignedExams > 0 ? Math.round((data.completedExams / data.totalAssignedExams) * 100) : 0;

  const tabs: { key: Filter; label: string; count: number; hide?: boolean }[] = [
    { key: "all", label: "Tất cả", count: data.exams.length },
    { key: "done", label: "Đã làm", count: done.length },
    { key: "notStarted", label: "Chưa làm", count: notStarted.length },
    { key: "inProgress", label: "Đang làm", count: inProgress.length, hide: inProgress.length === 0 },
  ];

  return (
    <>
      <div className="mb-3">
        <div className="d-flex justify-content-between small mb-1">
          <span className="text-secondary">
            Đã làm <strong className="text-body">{data.completedExams}</strong> / {data.totalAssignedExams} bài được giao
          </span>
          <span className="fw-semibold">{pct}%</span>
        </div>
        <ProgressBar now={pct} variant="success" style={{ height: 6 }} aria-label={`Hoàn thành ${pct}%`} />
      </div>

      <ButtonGroup size="sm" className="mb-3" aria-label="Lọc bài tập">
        {tabs
          .filter((x) => !x.hide)
          .map((x) => (
            <Button key={x.key} variant={filter === x.key ? "primary" : "outline-secondary"} onClick={() => setFilter(x.key)} aria-pressed={filter === x.key}>
              {x.label} ({x.count})
            </Button>
          ))}
      </ButtonGroup>

      {shown.length === 0 ? (
        <p className="text-secondary small mb-0">Không có bài tập nào trong mục này.</p>
      ) : (
        <div className="table-card">
          <Table hover size="sm" className="align-middle mb-0">
            <thead>
              <tr>
                <th>Bài tập</th>
                <th>Trạng thái</th>
                <th className="text-end">Điểm</th>
                <th>Nộp lúc</th>
                <th className="text-center">Lượt làm</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {shown.map((item) => {
                const target = targetOf(item);
                return (
                  <tr key={item.examId}>
                    <td>
                      <div className="name-cell">
                        <div className="name-cell__name">
                          <Link to={target.to} target="_blank" rel="noopener noreferrer" title={`${target.label} (mở tab mới)`}>
                            {item.name}
                          </Link>
                        </div>
                        <div className="name-cell__meta">
                          <span className="name-cell__code">{item.code}</span>
                          {" · "}
                          {item.durationMinutes === 0 ? "Luyện tập" : `${item.durationMinutes} phút`} · {item.questionCount} câu
                        </div>
                      </div>
                    </td>
                    <td className="text-nowrap">
                      <StatusBadge item={item} />
                    </td>
                    <td className="text-end text-nowrap">
                      {item.isSubmitted && item.officialScore !== null ? (
                        <>
                          <span className="fw-semibold">{formatNumber(item.officialScore)}</span>
                          {item.percentage !== null && <div className="small text-secondary">{formatNumber(item.percentage)}%</div>}
                        </>
                      ) : (
                        <span className="text-secondary">—</span>
                      )}
                    </td>
                    <td className="text-nowrap small">{item.lastSubmittedAt ? formatDateTime(item.lastSubmittedAt) : <span className="text-secondary">—</span>}</td>
                    <td className="text-center text-nowrap small">
                      {item.usedAttempts} / {item.maxAttempts === 0 ? "∞" : item.maxAttempts}
                    </td>
                    <td className="text-end text-nowrap">
                      <Link to={target.to} target="_blank" rel="noopener noreferrer" className="btn btn-sm btn-outline-secondary">
                        {target.label}
                        <i className="bi bi-box-arrow-up-right ms-1 small" aria-hidden="true" />
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </Table>
        </div>
      )}
    </>
  );
}
