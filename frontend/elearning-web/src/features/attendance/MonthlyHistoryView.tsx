import { Badge, Card, Col, Row, Table } from "react-bootstrap";
import type { AttendanceStudentHistory } from "../../types/api";
import { Empty } from "../../components/common/Feedback";
import { formatDateOnly } from "../../utils/format";
import { formatScoreValue, homeworkLabel, monthlyStats, scoreVariant, statusLabel, statusOptions } from "./attendanceLogic";

/**
 * Điểm danh + điểm BTVN một tháng của một học sinh: thẻ thống kê, biểu đồ điểm, bảng từng buổi.
 * Dùng chung cho giáo viên (cửa sổ lịch sử ở trang điểm danh) và học sinh (trang "Điểm danh & BTVN").
 */
export function MonthlyHistoryView({ history: data }: { history: AttendanceStudentHistory }) {
  const stats = monthlyStats(data.items);
  return (
    <>
      <Row xs={2} lg={4} className="g-2 mb-3">
        <Col>
          <StatTile
            label="ĐTB tháng"
            value={stats.average === null ? "—" : formatScoreValue(stats.average)}
            sub={data.classRank ? `Hạng ${data.classRank}/${data.rankedCount} lớp` : "Chưa xếp hạng"}
            variant={scoreVariant(stats.average)}
          />
        </Col>
        <Col>
          <StatTile
            label="Cao nhất / thấp nhất"
            value={stats.max === null ? "—" : `${formatScoreValue(stats.max)} / ${formatScoreValue(stats.min)}`}
            sub={`${stats.scored} buổi có điểm`}
            variant="primary"
          />
        </Col>
        <Col>
          <StatTile label="Hoàn thành BTVN" value={stats.homeworkRate === null ? "—" : `${stats.homeworkRate}%`} sub="Xuất sắc + Đạt" variant="success" />
        </Col>
        <Col>
          <StatTile
            label="Chuyên cần"
            value={stats.attendanceRate === null ? "—" : `${stats.attendanceRate}%`}
            sub={`${stats.attended}/${stats.marked} buổi có mặt`}
            variant="info"
          />
        </Col>
      </Row>
      {data.items.length === 0 ? (
        <Empty text="Lớp chưa có buổi học nào trong tháng này." />
      ) : (
        <>
          <ScoreChart history={data} />
          <Table responsive size="sm" className="mt-3 align-middle">
            <thead className="table-light">
              <tr>
                <th>Ngày</th>
                <th>Nội dung</th>
                <th>Điểm danh</th>
                <th>BTVN</th>
                <th className="text-center">Điểm</th>
                <th>Nhận xét</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((i) => (
                <tr key={i.sessionDate}>
                  <td className="text-nowrap">{formatDateOnly(i.sessionDate)}</td>
                  <td className="cell-clamp-2">{i.topic ?? "—"}</td>
                  <td>
                    <Badge bg={statusOptions.find((o) => o.value === i.status)?.variant ?? "secondary"}>{statusLabel(i.status)}</Badge>
                  </td>
                  <td className="text-nowrap">{homeworkLabel(i.homeworkStatus)}</td>
                  <td className="text-center">
                    {i.homeworkScore === null ? "—" : <Badge bg={scoreVariant(i.homeworkScore)}>{formatScoreValue(i.homeworkScore)}</Badge>}
                  </td>
                  <td className="small">{i.note}</td>
                </tr>
              ))}
            </tbody>
          </Table>
        </>
      )}
    </>
  );
}

function StatTile({ label, value, sub, variant }: { label: string; value: string; sub: string; variant: string }) {
  return (
    <Card className={`h-100 border-${variant}`} style={{ borderLeftWidth: 4 }}>
      <Card.Body className="py-2">
        <div className="small text-secondary">{label}</div>
        <div className="fs-5 fw-bold">{value}</div>
        <div className="small text-secondary">{sub}</div>
      </Card.Body>
    </Card>
  );
}

/** Biểu đồ điểm BTVN từng buổi với dải màu ≥ 8 / 6,5 / 5 / < 5. */
function ScoreChart({ history }: { history: AttendanceStudentHistory }) {
  const width = 720;
  const height = 220;
  const pad = { left: 32, right: 16, top: 12, bottom: 28 };
  const plotW = width - pad.left - pad.right;
  const plotH = height - pad.top - pad.bottom;
  const n = history.items.length;
  const x = (i: number) => pad.left + (n === 1 ? plotW / 2 : (plotW * i) / (n - 1));
  const y = (score: number) => pad.top + plotH * (1 - score / 10);
  const bands = [
    { from: 8, to: 10, color: "var(--bs-success)" },
    { from: 6.5, to: 8, color: "var(--bs-primary)" },
    { from: 5, to: 6.5, color: "var(--bs-warning)" },
    { from: 0, to: 5, color: "var(--bs-danger)" },
  ];
  const points = history.items.map((item, i) => ({ item, i })).filter((p) => p.item.homeworkScore !== null);
  const bandColor = (s: number) => bands.find((b) => s >= b.from)!.color;

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="w-100 border rounded bg-body" role="img" aria-label="Biểu đồ điểm BTVN theo buổi">
      {bands.map((b) => (
        <rect key={b.from} x={pad.left} width={plotW} y={y(b.to)} height={y(b.from) - y(b.to)} fill={b.color} opacity={0.08} />
      ))}
      {[0, 5, 6.5, 8, 10].map((v) => (
        <g key={v}>
          <line x1={pad.left} x2={width - pad.right} y1={y(v)} y2={y(v)} stroke="var(--bs-border-color)" strokeDasharray="3 3" />
          <text x={pad.left - 6} y={y(v) + 4} textAnchor="end" fontSize="11" fill="var(--bs-secondary-color)">
            {String(v).replace(".", ",")}
          </text>
        </g>
      ))}
      {points.length > 1 && (
        <polyline fill="none" stroke="var(--bs-primary)" strokeWidth={2} points={points.map((p) => `${x(p.i)},${y(p.item.homeworkScore!)}`).join(" ")} />
      )}
      {points.map((p) => (
        <g key={p.item.sessionDate}>
          <circle cx={x(p.i)} cy={y(p.item.homeworkScore!)} r={5} fill={bandColor(p.item.homeworkScore!)} stroke="var(--bs-body-bg)" strokeWidth={2} />
          <text x={x(p.i)} y={y(p.item.homeworkScore!) - 9} textAnchor="middle" fontSize="11" fontWeight="600" fill="var(--bs-body-color)">
            {formatScoreValue(p.item.homeworkScore)}
          </text>
        </g>
      ))}
      {history.items.map((item, i) => (
        <text key={item.sessionDate} x={x(i)} y={height - 8} textAnchor="middle" fontSize="11" fill="var(--bs-secondary-color)">
          {item.sessionDate.slice(8, 10)}/{item.sessionDate.slice(5, 7)}
        </text>
      ))}
    </svg>
  );
}
