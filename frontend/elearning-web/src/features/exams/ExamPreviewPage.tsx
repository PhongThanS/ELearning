import { useQuery } from "@tanstack/react-query";
import { Alert, Badge, Card, Col, Container, Row, Spinner } from "react-bootstrap";
import { useParams } from "react-router";
import { MarkdownView } from "../../components/common/MarkdownView";
import { examsApi } from "../../services/api";
import { formatScore } from "../../utils/format";

export function ExamPreviewPage() {
  const { id = "" } = useParams();

  const query = useQuery({
    queryKey: ["exam-preview", id],
    queryFn: () => examsApi.previewPublished(id),
    enabled: Boolean(id),
  });

  if (query.isLoading) {
    return (
      <Container className="py-5 text-center">
        <Spinner animation="border" variant="primary" />
        <div className="text-muted mt-2">Đang tải đề thi...</div>
      </Container>
    );
  }

  if (query.error || !query.data) {
    return (
      <Container className="py-4">
        <Alert variant="danger">
          <i className="bi bi-exclamation-triangle-fill me-2" />
          Không thể tải nội dung đề bài này hoặc đề thi chưa được xuất bản.
        </Alert>
      </Container>
    );
  }

  const p = query.data;

  return (
    <Container className="py-4" style={{ maxWidth: 1000 }}>
      {/* Banner chế độ xem */}
      <Alert variant="info" className="d-flex align-items-center justify-content-between flex-wrap gap-2 mb-3">
        <div className="d-flex align-items-center gap-2">
          <i className="bi bi-eye-fill fs-4 text-info" />
          <div>
            <div className="fw-bold">Chế độ xem đề thi (Chỉ đọc)</div>
            <div className="small">Học sinh chưa làm bài này. Bạn đang xem nội dung đề bài được giao cho học sinh.</div>
          </div>
        </div>
        <Badge bg="secondary" className="px-3 py-2">
          VIEW ONLY
        </Badge>
      </Alert>

      {/* Thông tin đề thi */}
      <Card className="mb-4 shadow-sm border-0 bg-light">
        <Card.Body className="p-4">
          <Row className="align-items-center g-3">
            <Col md={8}>
              <h1 className="h3 mb-2 text-dark">{p.examName}</h1>
              {p.instructions && (
                <div className="text-secondary small mt-2">
                  <div className="fw-semibold text-dark mb-1">
                    <i className="bi bi-card-text me-1" /> Hướng dẫn làm bài:
                  </div>
                  <MarkdownView content={p.instructions} />
                </div>
              )}
            </Col>
            <Col md={4} className="text-md-end">
              <div className="d-flex flex-column gap-2 align-items-md-end">
                <span className="badge bg-white text-dark border p-2 px-3 fs-6">
                  <i className="bi bi-question-circle text-primary me-1" />
                  Số câu hỏi: <strong>{p.questionCount}</strong>
                </span>
                <span className="badge bg-white text-dark border p-2 px-3 fs-6">
                  <i className="bi bi-clock-history text-warning me-1" />
                  Thời lượng:{" "}
                  <strong>
                    {p.durationMinutes === 0
                      ? "Không giới hạn (Luyện tập)"
                      : `${p.durationMinutes} phút`}
                  </strong>
                </span>
                <span className="badge bg-white text-dark border p-2 px-3 fs-6">
                  <i className="bi bi-trophy text-success me-1" />
                  Điểm tối đa: <strong>{formatScore(p.maxScore, p.maxScore)}</strong>
                </span>
              </div>
            </Col>
          </Row>
        </Card.Body>
      </Card>

      {/* Danh sách câu hỏi */}
      <div className="d-flex align-items-center justify-content-between mb-3">
        <h2 className="h5 mb-0 d-flex align-items-center gap-2">
          <i className="bi bi-journal-text text-primary" />
          <span>Danh sách câu hỏi trong đề ({p.questions.length} câu)</span>
        </h2>
      </div>

      <div className="d-flex flex-column gap-3 mb-5">
        {p.questions.map((q) => (
          <Card key={q.id} className="shadow-sm border">
            <Card.Header className="bg-light d-flex justify-content-between align-items-center py-2">
              <span className="fw-bold text-primary">Câu {q.order}</span>
              <Badge bg="light" text="dark" className="border px-2 py-1">
                {q.score} điểm
              </Badge>
            </Card.Header>
            <Card.Body className="p-3">
              <div className="mb-3 fs-6 text-dark">
                <MarkdownView content={q.content} media={p.media} />
              </div>

              {q.options && q.options.length > 0 ? (
                <div className="d-flex flex-column gap-2">
                  {q.options.map((opt) => (
                    <div
                      key={opt.code}
                      className="p-2 px-3 border rounded bg-white d-flex align-items-start gap-2 shadow-xs"
                    >
                      <span className="badge bg-light text-dark border font-monospace mt-0">
                        {opt.code}
                      </span>
                      <div className="flex-grow-1 text-secondary">
                        <MarkdownView content={opt.content} inline media={p.media} />
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="p-3 border rounded bg-light text-secondary small fst-italic">
                  <i className="bi bi-pencil-square me-1" />
                  {q.type === "FILL_IN" ? "Câu hỏi điền khuyết / trả lời ngắn." : "Câu hỏi tự luận."}
                </div>
              )}
            </Card.Body>
          </Card>
        ))}
      </div>
    </Container>
  );
}
