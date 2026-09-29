import { useState } from "react";
import { Badge, Button, ButtonGroup, Card, Col, Form, Row } from "react-bootstrap";
import { Link } from "react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { adminApi } from "../../services/api";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import { Pager } from "../../components/common/DataTable";
import { MarkdownView } from "../../components/common/MarkdownView";
import { useToast } from "../../components/common/toast";
import { formatDateTime, formatNumber } from "../../utils/format";
import type { ManualGradingItem } from "../../types/api";

type StatusFilter = "PENDING" | "GRADED" | "ALL";

const FILTERS: [StatusFilter, string][] = [
  ["PENDING", "Chờ chấm"],
  ["GRADED", "Đã chấm"],
  ["ALL", "Tất cả"],
];

/**
 * Chấm tay câu tự luận (docs/02-nghiep-vu.md mục 2.3). Mỗi thẻ là một bài làm của một học viên cho một câu;
 * lưu điểm thì server chấm lại cả lượt thi và cập nhật kết quả.
 */
export function ManualGradingTab({ examId }: { examId: string }) {
  const [status, setStatus] = useState<StatusFilter>("PENDING");
  const [page, setPage] = useState(1);
  const query = useQuery({
    queryKey: ["manual-grading", examId, status, page],
    queryFn: () => adminApi.manualGrading(examId, { status, page, pageSize: 10 }),
  });

  return (
    <>
      <ButtonGroup size="sm" className="mb-3" aria-label="Lọc bài chấm tay">
        {FILTERS.map(([value, label]) => (
          <Button
            key={value}
            variant={status === value ? "primary" : "outline-primary"}
            onClick={() => {
              setStatus(value);
              setPage(1);
            }}
          >
            {label}
          </Button>
        ))}
      </ButtonGroup>
      {query.error && <ErrorAlert error={query.error} />}
      {query.isLoading && <Loading />}
      {query.data && query.data.items.length === 0 && (
        <p className="text-secondary">{status === "PENDING" ? "Không còn bài tự luận nào chờ chấm." : "Không có bài nào."}</p>
      )}
      {query.data?.items.map((item) => (
        <GradeCard key={item.attemptQuestionId} examId={examId} item={item} />
      ))}
      {query.data && query.data.totalPages > 1 && <Pager data={query.data} onPage={setPage} />}
    </>
  );
}

function GradeCard({ examId, item }: { examId: string; item: ManualGradingItem }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [score, setScore] = useState(item.manualScore == null ? "" : String(item.manualScore));
  const [comment, setComment] = useState(item.manualComment ?? "");
  const value = Number(score.replace(",", "."));
  const valid = score.trim() !== "" && !Number.isNaN(value) && value >= 0 && value <= item.maxScore && (value * 4) % 1 === 0;
  const scoreId = `score-${item.attemptQuestionId}`;
  const commentId = `comment-${item.attemptQuestionId}`;

  const save = useMutation({
    mutationFn: () => adminApi.manualGrade(item.attemptId, item.attemptQuestionId, value, comment.trim() || null),
    onSuccess: (r) => {
      toast.success(
        r.pendingManualCount > 0
          ? `Đã lưu. Lượt thi còn ${r.pendingManualCount} câu tự luận chờ chấm.`
          : `Đã lưu. Tổng điểm lượt thi: ${formatNumber(r.totalScore)} / ${formatNumber(r.maxScore)}.`,
      );
      void queryClient.invalidateQueries({ queryKey: ["manual-grading", examId] });
      void queryClient.invalidateQueries({ queryKey: ["results", examId] });
    },
    onError: (e) => toast.error(e),
  });

  return (
    <Card className="mb-3">
      <Card.Body>
        <div className="d-flex justify-content-between flex-wrap gap-2 mb-2">
          <div className="small">
            <strong>{item.userName}</strong> — {item.fullName} · lượt #{item.attemptNumber} · câu {item.questionOrder} · nộp{" "}
            {formatDateTime(item.submittedAt)} <Link to={`/admin/attempts/${item.attemptId}`}>chi tiết lượt thi</Link>
          </div>
          {item.manualScore != null ? (
            <Badge bg="success">
              Đã chấm {formatNumber(item.manualScore)} / {formatNumber(item.maxScore)} · {formatDateTime(item.manualGradedAt)}
            </Badge>
          ) : (
            <Badge bg="warning" text="dark">Chờ chấm</Badge>
          )}
        </div>
        <MarkdownView content={item.content} format={item.contentFormat} media={item.media} />
        {item.explanation && (
          <details className="small mb-2">
            <summary>Đáp án mẫu / hướng dẫn chấm</summary>
            <MarkdownView content={item.explanation} media={item.media} />
          </details>
        )}
        <div className="border rounded p-2 mb-2 bg-body-tertiary" style={{ whiteSpace: "pre-wrap" }}>{item.answerText}</div>
        <Form
          onSubmit={(e) => {
            e.preventDefault();
            save.mutate();
          }}
        >
          <Row className="g-2 align-items-start">
            <Col sm={3}>
              <Form.Label htmlFor={scoreId} className="small mb-0">Điểm (tối đa {formatNumber(item.maxScore)})</Form.Label>
              <Form.Control id={scoreId} size="sm" inputMode="decimal" value={score} isInvalid={score !== "" && !valid}
                onChange={(e) => setScore(e.target.value)} />
              <Form.Control.Feedback type="invalid">Từ 0 đến {formatNumber(item.maxScore)}, bội số của 0,25.</Form.Control.Feedback>
            </Col>
            <Col sm={7}>
              <Form.Label htmlFor={commentId} className="small mb-0">Nhận xét (học viên thấy khi được xem lại bài)</Form.Label>
              <Form.Control id={commentId} as="textarea" rows={2} size="sm" maxLength={2000} value={comment}
                onChange={(e) => setComment(e.target.value)} />
            </Col>
            <Col sm={2} className="d-grid">
              <Form.Label className="small mb-0 invisible">Lưu</Form.Label>
              <Button type="submit" size="sm" disabled={!valid || save.isPending}>Lưu điểm</Button>
            </Col>
          </Row>
        </Form>
      </Card.Body>
    </Card>
  );
}
