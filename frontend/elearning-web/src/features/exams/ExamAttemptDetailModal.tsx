import { useQuery } from "@tanstack/react-query";
import { Alert, Badge, Button, Card, Col, Modal, Row, Spinner } from "react-bootstrap";
import { Link } from "react-router";
import { MarkdownView } from "../../components/common/MarkdownView";
import { adminApi } from "../../services/api";
import { formatDateTime, formatNumber, formatScore } from "../../utils/format";

interface Props {
  attemptId: string | null;
  onClose: () => void;
  title?: string;
}

export function ExamAttemptDetailModal({ attemptId, onClose, title }: Props) {
  const query = useQuery({
    queryKey: ["admin-attempt", attemptId],
    queryFn: () => adminApi.attempt(attemptId!),
    enabled: Boolean(attemptId),
  });

  const a = query.data;

  return (
    <Modal show={Boolean(attemptId)} onHide={onClose} size="xl" scrollable centered>
      <Modal.Header closeButton className="bg-light">
        <Modal.Title className="h5 mb-0 d-flex align-items-center gap-2 flex-wrap">
          <i className="bi bi-file-earmark-check text-primary" />
          <span>{title || "Chi tiết bài làm của học sinh"}</span>
          {a && (
            <Badge bg={a.passed === true ? "success" : a.passed === false ? "danger" : "secondary"}>
              {a.passed === true ? "ĐẠT" : a.passed === false ? "CHƯA ĐẠT" : a.status}
            </Badge>
          )}
        </Modal.Title>
      </Modal.Header>

      <Modal.Body className="p-3">
        {query.isLoading && (
          <div className="text-center py-5">
            <Spinner animation="border" variant="primary" />
            <div className="text-muted mt-2">Đang tải bài làm chi tiết...</div>
          </div>
        )}

        {query.error && (
          <Alert variant="danger">
            <i className="bi bi-exclamation-triangle-fill me-2" />
            Không thể tải thông tin bài thi này hoặc bạn không có quyền xem.
          </Alert>
        )}

        {a && (
          <div>
            {/* Header thông tin lượt thi */}
            <Card className="mb-3 border-primary-subtle bg-primary-subtle bg-opacity-10 shadow-sm">
              <Card.Body className="py-2 px-3">
                <Row className="g-2 align-items-center">
                  <Col md={5}>
                    <div className="fw-bold fs-6 text-primary">{a.examName}</div>
                    <div className="text-muted small">
                      Mã đề: <span className="font-monospace">{a.examCode}</span> (v{a.versionNumber}) — Lần thi: <strong>#{a.attemptNumber}</strong>
                    </div>
                    <div className="text-dark small">
                      Học sinh: <strong>{a.fullName}</strong> ({a.userName})
                    </div>
                  </Col>
                  <Col md={4}>
                    <div className="small text-secondary">
                      Bắt đầu: <strong>{formatDateTime(a.startedAt)}</strong>
                    </div>
                    <div className="small text-secondary">
                      Nộp bài: <strong>{a.submittedAt ? formatDateTime(a.submittedAt) : "Chưa nộp"}</strong>
                    </div>
                    <div className="small text-secondary">
                      Số câu đúng: <strong>{a.correctCount ?? 0} / {a.answers.length}</strong>
                    </div>
                  </Col>
                  <Col md={3} className="text-md-end">
                    <div className="text-secondary small">Điểm tổng kết</div>
                    <div className="fs-3 fw-bold text-primary">
                      {formatScore(a.totalScore, a.maxScore)}
                    </div>
                    {a.percentage != null && (
                      <span className="badge bg-primary fs-7">{formatNumber(a.percentage)}%</span>
                    )}
                  </Col>
                </Row>
              </Card.Body>
            </Card>

            {/* Danh sách câu hỏi và đáp án */}
            <div className="d-flex justify-content-between align-items-center mb-2">
              <h6 className="fw-bold mb-0 text-dark">
                <i className="bi bi-list-check me-1 text-primary" /> Danh sách câu hỏi ({a.answers.length} câu)
              </h6>
              <div className="small text-muted">
                Đúng: <span className="text-success fw-bold">{a.answers.filter((q) => q.isCorrect === true).length}</span> | 
                Sai: <span className="text-danger fw-bold">{a.answers.filter((q) => q.isCorrect === false).length}</span>
              </div>
            </div>

            <div className="d-flex flex-column gap-3">
              {a.answers.map((q) => {
                const isCorrect = q.isCorrect === true;
                const isIncorrect = q.isCorrect === false;

                return (
                  <Card
                    key={q.attemptQuestionId}
                    className={`border ${isCorrect ? "border-success-subtle shadow-sm" : isIncorrect ? "border-danger-subtle shadow-sm" : ""}`}
                  >
                    <Card.Header
                      className={`d-flex justify-content-between align-items-center py-2 ${
                        isCorrect ? "bg-success bg-opacity-10" : isIncorrect ? "bg-danger bg-opacity-10" : "bg-light"
                      }`}
                    >
                      <div className="d-flex align-items-center gap-2">
                        <span className="fw-bold">Câu {q.order}</span>
                        {q.isMarkedForReview && <Badge bg="warning" text="dark">Đánh dấu xem lại</Badge>}
                        {q.isVoided && <Badge bg="dark">Đã hủy điểm</Badge>}
                      </div>
                      <div className="d-flex align-items-center gap-2">
                        {isCorrect ? (
                          <Badge bg="success" className="px-2 py-1">
                            <i className="bi bi-check-circle-fill me-1" /> Đúng (+{formatScore(q.score, q.maxScore)} đ)
                          </Badge>
                        ) : isIncorrect ? (
                          <Badge bg="danger" className="px-2 py-1">
                            <i className="bi bi-x-circle-fill me-1" /> Sai ({formatScore(q.score, q.maxScore)} đ)
                          </Badge>
                        ) : (
                          <Badge bg="secondary" className="px-2 py-1">
                            {formatScore(q.score, q.maxScore)} đ
                          </Badge>
                        )}
                      </div>
                    </Card.Header>

                    <Card.Body className="py-3">
                      {/* Đề bài */}
                      <div className="mb-3 fw-medium text-dark">
                        <MarkdownView content={q.content} media={a.media} />
                      </div>

                      {/* Hiển thị các lựa chọn nếu có */}
                      {q.options && q.options.length > 0 ? (
                        <div className="d-flex flex-column gap-2 mb-3">
                          {q.options.map((opt) => {
                            const isChosen = q.selectedOptions.includes(opt.optionCode);
                            const isOptCorrect = opt.isCorrect || q.correctOptions.includes(opt.optionCode);

                            let boxClass = "p-2 px-3 border rounded d-flex align-items-start gap-2 ";
                            if (isChosen && isOptCorrect) {
                              boxClass += "bg-success bg-opacity-15 border-success text-success-emphasis fw-semibold";
                            } else if (isChosen && !isOptCorrect) {
                              boxClass += "bg-danger bg-opacity-15 border-danger text-danger-emphasis";
                            } else if (isOptCorrect) {
                              boxClass += "bg-success-subtle bg-opacity-50 border-success-subtle text-success-emphasis";
                            } else {
                              boxClass += "bg-light text-secondary";
                            }

                            return (
                              <div key={opt.optionCode} className={boxClass}>
                                <div className="fw-bold font-monospace mt-0" style={{ minWidth: 24 }}>
                                  {opt.optionCode}.
                                </div>
                                <div className="flex-grow-1">
                                  <MarkdownView content={opt.content} inline media={a.media} />
                                </div>
                                <div className="ms-auto d-flex align-items-center gap-1 flex-shrink-0">
                                  {isOptCorrect && (
                                    <Badge bg="success" className="d-inline-flex align-items-center">
                                      <i className="bi bi-check-lg me-1" /> Đáp án đúng
                                    </Badge>
                                  )}
                                  {isChosen && (
                                    <Badge bg={isOptCorrect ? "success" : "danger"} className="d-inline-flex align-items-center">
                                      {isOptCorrect ? (
                                        <>
                                          <i className="bi bi-check-all me-1" /> Học sinh chọn
                                        </>
                                      ) : (
                                        <>
                                          <i className="bi bi-x-circle me-1" /> Học sinh đã chọn
                                        </>
                                      )}
                                    </Badge>
                                  )}
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      ) : (
                        /* Trường hợp không có options (FILL_IN, ESSAY...) */
                        <div className="p-3 border rounded bg-light mb-3">
                          <Row className="g-2">
                            <Col md={6}>
                              <div className="text-secondary small mb-1">Câu trả lời của học sinh:</div>
                              <div className={`p-2 border rounded fw-bold ${isCorrect ? "bg-success-subtle text-success" : isIncorrect ? "bg-danger-subtle text-danger" : "bg-white"}`}>
                                {q.type === "FILL_IN"
                                  ? q.answerText || <span className="fst-italic text-muted font-monospace">(bỏ trống)</span>
                                  : q.selectedOptions.join(", ") || <span className="fst-italic text-muted font-monospace">(bỏ trống)</span>}
                              </div>
                            </Col>
                            <Col md={6}>
                              <div className="text-secondary small mb-1">Đáp án đúng chuẩn:</div>
                              <div className="p-2 border rounded bg-success-subtle text-success fw-bold">
                                {q.type === "FILL_IN"
                                  ? q.answerDataType === "NUMBER"
                                    ? formatNumber(q.correctAnswerNumber)
                                    : q.acceptedAnswers.join(" / ")
                                  : q.correctOptions.join(", ")}
                              </div>
                            </Col>
                          </Row>
                        </div>
                      )}

                      {/* Lời giải thích nếu có */}
                      {q.explanation && (
                        <div className="p-2 px-3 border rounded bg-info-subtle bg-opacity-40 text-dark small mt-2">
                          <div className="fw-bold text-info-emphasis mb-1">
                            <i className="bi bi-lightbulb-fill me-1" /> Lời giải chi tiết:
                          </div>
                          <MarkdownView content={q.explanation} media={a.media} />
                        </div>
                      )}
                    </Card.Body>
                  </Card>
                );
              })}
            </div>
          </div>
        )}
      </Modal.Body>

      <Modal.Footer className="bg-light d-flex justify-content-between">
        <div>
          {a && (
            <Link to={`/admin/attempts/${a.attemptId}`} className="btn btn-outline-primary btn-sm" target="_blank">
              <i className="bi bi-box-arrow-up-right me-1" /> Mở trang quản lý lượt thi đầy đủ
            </Link>
          )}
        </div>
        <Button variant="secondary" onClick={onClose}>
          Đóng
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
