import { Badge, Card, Table } from "react-bootstrap";
import { useQuery } from "@tanstack/react-query";
import { studentApi } from "../../services/api";
import { Empty, ErrorAlert, Loading } from "../../components/common/Feedback";
import { formatNumber } from "../../utils/format";
import { scoreVariant } from "../attendance/attendanceLogic";
import type { StudentCategoryScore } from "../../types/api";

/** Phần trăm → thang 10, tối đa 2 chữ số thập phân. */
const toTen = (percentage: number) => Math.round(percentage * 10) / 100;

function Score({ percentage }: { percentage: number }) {
  const value = toTen(percentage);
  return <Badge bg={scoreVariant(value)}>{formatNumber(value)}</Badge>;
}

/** Tên chuyên đề con bỏ phần lặp tên cha: "Toán 12 - Xác suất" dưới "Toán 12" hiện "Xác suất". */
function categoryName(row: StudentCategoryScore): string {
  const name = row.categoryName ?? "Chưa gắn chuyên đề";
  const prefix = `${row.parentName} - `;
  return row.parentName && name.startsWith(prefix) ? name.slice(prefix.length) : name;
}

/** Bảng điểm theo chuyên đề của chính học viên (trang "Thông tin"). Server chỉ trả điểm được phép xem. */
export function CategoryScoreCard() {
  const query = useQuery({ queryKey: ["student-category-scores"], queryFn: () => studentApi.categoryScores() });

  return (
    <Card className="mt-3">
      <Card.Body>
        <h2 className="h5 mb-1">
          <i className="bi bi-bar-chart text-primary me-2" aria-hidden="true" />
          Bảng điểm theo chuyên đề
        </h2>
        <p className="small text-secondary mb-3">Thang điểm 10. Mỗi đề tính một điểm chính thức; chỉ gồm các đề đã được xem điểm.</p>
        {query.isLoading && <Loading />}
        {query.error && <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />}
        {query.data && query.data.length === 0 && <Empty text="Bạn chưa có điểm nào." />}
        {query.data && query.data.length > 0 && (
          <Table responsive size="sm" className="align-middle mb-0">
            <thead className="table-light">
              <tr>
                <th>Chuyên đề</th>
                <th className="text-center">Số đề đã làm</th>
                <th className="text-center">Điểm trung bình</th>
                <th className="text-center">Cao nhất</th>
              </tr>
            </thead>
            <tbody>
              {query.data.map((row) => (
                <tr key={row.categoryId ?? "none"}>
                  <td>
                    {row.parentName && <div className="small text-secondary">{row.parentName}</div>}
                    <div className="fw-semibold">{categoryName(row)}</div>
                  </td>
                  <td className="text-center">{row.examCount}</td>
                  <td className="text-center">
                    <Score percentage={row.averagePercentage} />
                  </td>
                  <td className="text-center">
                    <Score percentage={row.bestPercentage} />
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Card.Body>
    </Card>
  );
}
