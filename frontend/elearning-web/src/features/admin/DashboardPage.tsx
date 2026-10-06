import { Card, Col, Row, Table } from "react-bootstrap";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../../services/api";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import { PageHeader } from "../../components/common/DataTable";
import { formatDateTime, formatNumber } from "../../utils/format";

export function DashboardPage() {
  const { t } = useTranslation();
  const query = useQuery({ queryKey: ["dashboard"], queryFn: adminApi.dashboard, refetchInterval: 60_000 });

  if (query.error) {
    return <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />;
  }
  if (!query.data) {
    return <Loading />;
  }
  const d = query.data;
  // [nhãn, giá trị, icon Bootstrap Icons, màu nhấn (theme.scss)]
  const tiles: [string, string, string, string][] = [
    ["Người dùng", formatNumber(d.totalUsers), "bi-people-fill", "indigo"],
    ["Học viên", formatNumber(d.totalStudents), "bi-mortarboard-fill", "violet"],
    ["Đề thi", formatNumber(d.totalExams), "bi-journal-text", "sky"],
    ["Đề đang mở", formatNumber(d.openExams), "bi-unlock-fill", "teal"],
    ["Lượt thi hôm nay", formatNumber(d.attemptsToday), "bi-calendar-check-fill", "amber"],
    ["Đang làm bài", formatNumber(d.inProgressAttempts), "bi-pencil-square", "orange"],
    ["Điểm TB 30 ngày", d.averagePercentage30Days == null ? "—" : `${formatNumber(d.averagePercentage30Days)}%`, "bi-graph-up-arrow", "green"],
    ["Tỉ lệ đạt 30 ngày", d.passRate30Days == null ? "—" : `${formatNumber(d.passRate30Days)}%`, "bi-trophy-fill", "pink"],
  ];

  return (
    <>
      <PageHeader title={t("nav.dashboard")} />
      <Row xs={2} md={4} className="g-3 mb-4">
        {tiles.map(([label, value, icon, accent]) => (
          <Col key={label}>
            <Card className={`h-100 stat-card accent-${accent}`}>
              <Card.Body className="d-flex align-items-center gap-3">
                <span className="stat-icon" aria-hidden="true">
                  <i className={`bi ${icon}`} />
                </span>
                <div className="min-w-0">
                  <div className="small text-secondary text-truncate">{label}</div>
                  <div className="fs-4 fw-bold lh-sm">{value}</div>
                </div>
              </Card.Body>
            </Card>
          </Col>
        ))}
      </Row>
      <Card>
        <Card.Body>
          <h2 className="h5">
            <i className="bi bi-calendar-event text-primary me-2" aria-hidden="true" />
            Đề thi sắp diễn ra / đang mở
          </h2>
          <p className="small text-secondary">Không deploy hệ thống trong khung giờ có đề đang mở.</p>
          <div className="table-responsive">
            <Table size="sm" hover className="align-middle mb-0">
              <thead>
                <tr>
                  <th>{t("common.code")}</th>
                  <th>{t("common.name")}</th>
                  <th>Bắt đầu</th>
                  <th>Kết thúc</th>
                  <th>Đang làm</th>
                </tr>
              </thead>
              <tbody>
                {d.upcomingExams.map((e) => (
                  <tr key={e.examId}>
                    <td>
                      <Link to={`/admin/exams/${e.examId}`}>{e.code}</Link>
                    </td>
                    <td>{e.name}</td>
                    <td>{formatDateTime(e.startAt)}</td>
                    <td>{formatDateTime(e.endAt)}</td>
                    <td>{e.inProgressAttempts}</td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        </Card.Body>
      </Card>
    </>
  );
}
