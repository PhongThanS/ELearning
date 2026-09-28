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
  const tiles: [string, string][] = [
    ["Người dùng", formatNumber(d.totalUsers)],
    ["Học viên", formatNumber(d.totalStudents)],
    ["Đề thi", formatNumber(d.totalExams)],
    ["Đề đang mở", formatNumber(d.openExams)],
    ["Lượt thi hôm nay", formatNumber(d.attemptsToday)],
    ["Đang làm bài", formatNumber(d.inProgressAttempts)],
    ["Điểm TB 30 ngày", d.averagePercentage30Days == null ? "—" : `${formatNumber(d.averagePercentage30Days)}%`],
    ["Tỉ lệ đạt 30 ngày", d.passRate30Days == null ? "—" : `${formatNumber(d.passRate30Days)}%`],
  ];

  return (
    <>
      <PageHeader title={t("nav.dashboard")} />
      <Row xs={2} md={4} className="g-3 mb-4">
        {tiles.map(([label, value]) => (
          <Col key={label}>
            <Card className="h-100">
              <Card.Body>
                <div className="small text-secondary">{label}</div>
                <div className="fs-3 fw-semibold">{value}</div>
              </Card.Body>
            </Card>
          </Col>
        ))}
      </Row>
      <h2 className="h5">Đề thi sắp diễn ra / đang mở</h2>
      <p className="small text-secondary">Không deploy hệ thống trong khung giờ có đề đang mở.</p>
      <div className="table-responsive">
        <Table size="sm" className="align-middle">
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
    </>
  );
}
