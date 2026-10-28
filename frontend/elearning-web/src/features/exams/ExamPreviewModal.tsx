import { useQuery } from "@tanstack/react-query";
import { Alert, Badge, Button, Card, Col, Modal, Row, Spinner } from "react-bootstrap";
import { MarkdownView } from "../../components/common/MarkdownView";
import { examsApi } from "../../services/api";
import { formatScore } from "../../utils/format";

interface Props {
  examId: string | null;
  versionId?: string | null;
  examName?: string;
  onClose: () => void;
}

export function ExamPreviewModal({ examId, versionId, examName, onClose }: Props) {
  const query = useQuery({
    queryKey: ["exam-preview", examId, versionId],
    queryFn: async () => {
      if (!examId) return null;
      if (versionId) {
        return examsApi.preview(examId, versionId);
      }
      return examsApi.previewPublished(examId);
    },
    enabled: Boolean(examId),
  });

  const p = query.data;

  return (
    <Modal show={Boolean(examId)} onHide={onClose} size="xl" scrollable centered>
      <Modal.Header closeButton className="bg-light">
        <Modal.Title className="h5 mb-0 d-flex align-items-center gap-2 flex-wrap">
          <i className="bi bi-eye-fill text-primary" />
          <span>Xem đề bài: {p?.examName || examName || "Đề thi"}</span>
          <Badge bg="secondary" className="border">
            CHỈ ĐỌC (KHÔNG THỂ LÀM BÀI)
          </Badge>
        </Modal.Title>
      </Modal.Header>

      <Modal.Body className="p-3">
        {/* Banner cảnh báo chế độ xem */}
        <Alert variant="info" className="d-flex align-items-center gap-2 mb-3 py-2">
          <i className="bi bi-info-circle-fill fs-5 text-info flex-shrink-0" />
          <div className="small">
            <strong>Chế độ xem trước nội dung:</strong> Học sinh này chưa làm bài thi. Bạn đang xem nội dung đề bài được giao dưới dạng chỉ đọc. Không thể chọn đáp án hay nộp bài tại đây.
          </div>
        </Alert>

        {query.isLoading && (
          <div className="text-center py-5">
            <Spinner animation="border" variant="primary" />
            <div className="text-muted mt-2">Đang tải đề thi...</div>
          </div>
        )}

        {query.error && (
          <Alert variant="danger">
            <i className="bi bi-exclamation-triangle-fill me-2" />
            Không thể tải nội dung đề bài này hoặc đề thi chưa được xuất bản (Publish).
          </Alert>
        )}

        {p && (
          <div>
            {/* Header thông tin đề thi */}
            <Card className="mb-3 border-light-subtle bg-light shadow-sm">
              <Card.Body className="py-2 px-3">
                <Row className="g-2 align-items-center">
                  <Col md={6}>
                    <div className="fw-bold fs-6 text-dark">{p.examName}</div>
                    {p.instructions && (
                      <div className="text-muted small mt-1">
                        <i className="bi bi-card-text me-1" />
                        <MarkdownView content={p.instructions} inline />
                      </div>
                    )}
                  </Col>
                  <Col md={6} className="text-md-end">
                    <div className="d-flex gap-2 justify-content-md-end flex-wrap">
                      <span className="badge bg-white text-dark border p-2">
                        <i className="bi bi-question-circle text-primary me-1" />
                        Tổng số câu: <strong>{p.questionCount}</strong>
                      </span>
                      <span className="badge bg-white text-dark border p-2">
                        <i className="bi bi-clock-history text-warning me-1" />
                        Thời lượng:{" "}
                        <strong>
                          {p.durationMinutes === 0
                            ? "Không giới hạn (Luyện tập)"
                            : `${p.durationMinutes} phút`}
                        </strong>
                      </span>
                      <span className="badge bg-white text-dark border p-2">
                        <i className="bi bi-trophy text-success me-1" />
                        Điểm tối đa: <strong>{formatScore(p.maxScore, p.maxScore)}</strong>
                      </span>
                    </div>
                  </Col>
                </Row>
              </Card.Body>
            </Card>

            {/* Danh sách câu hỏi */}
            <h6 className="fw-bold mb-3 text-dark">
              <i className="bi bi-journal-text me-1 text-primary" /> Nội dung các câu hỏi trong đề:
            </h6>

            <div className="d-flex flex-column gap-3">
              {p.questions.map((q) => (
                <Card key={q.id} className="border shadow-xs">
                  <Card.Header className="bg-light d-flex justify-content-between align-items-center py-2">
                    <span className="fw-bold text-primary">Câu {q.order}</span>
                    <Badge bg="light" text="dark" className="border">
                      {q.score} điểm
                    </Badge>
                  </Card.Header>
                  <Card.Body className="py-3">
                    <div className="mb-3 fw-medium text-dark">
                      <MarkdownView content={q.content} media={p.media} />
                    </div>

                    {q.options && q.options.length > 0 ? (
                      <div className="d-flex flex-column gap-2">
                        {q.options.map((opt) => (
                          <div
                            key={opt.code}
                            className="p-2 px-3 border rounded bg-white d-flex align-items-start gap-2"
                            style={{ opacity: 0.9 }}
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
          </div>
        )}
      </Modal.Body>

      <Modal.Footer className="bg-light">
        <Button variant="secondary" onClick={onClose}>
          Đóng
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
