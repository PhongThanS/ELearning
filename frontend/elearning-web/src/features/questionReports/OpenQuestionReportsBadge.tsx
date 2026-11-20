import { Badge } from "react-bootstrap";
import { useQuery } from "@tanstack/react-query";
import { questionReportsApi } from "../../services/api";

/** Số câu hỏi đang có báo lỗi chờ xử lý (D-46), hiện cạnh mục menu. */
export function OpenQuestionReportsBadge() {
  const query = useQuery({
    queryKey: ["question-reports-open-count"],
    queryFn: questionReportsApi.openCount,
    refetchInterval: 5 * 60 * 1000,
  });
  if (!query.data) {
    return null;
  }
  return (
    <Badge bg="danger" pill className="ms-auto" title={`${query.data} câu hỏi đang chờ xử lý`}>
      {query.data}
    </Badge>
  );
}
