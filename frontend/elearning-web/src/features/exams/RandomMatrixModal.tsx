import { useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, Modal, Row, Table, Spinner } from "react-bootstrap";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { examsApi } from "../../services/api";
import { useActiveCategories } from "../../hooks/useCategories";
import { useToast } from "../../components/common/toast";
import type { MatrixDrawInput, MatrixDrawMode, QuestionType, VersionDetail } from "../../types/api";

const TYPES: QuestionType[] = ["SINGLE_CHOICE", "MULTIPLE_CHOICE", "TRUE_FALSE", "FILL_IN", "ESSAY"];

interface RandomMatrixModalProps {
  show: boolean;
  onHide: () => void;
  examId: string;
  version: VersionDetail;
  onSuccess: (updatedVersion: VersionDetail) => void;
}

export function RandomMatrixModal({ show, onHide, examId, version, onSuccess }: RandomMatrixModalProps) {
  const { t } = useTranslation();
  const toast = useToast();
  const categories = useActiveCategories();

  const [categoryId, setCategoryId] = useState<string>("");
  const [tag, setTag] = useState<string>("");
  const [questionType, setQuestionType] = useState<string>("");

  const [easyCount, setEasyCount] = useState<number>(2);
  const [mediumCount, setMediumCount] = useState<number>(1);
  const [hardCount, setHardCount] = useState<number>(1);
  const [scorePerQuestion, setScorePerQuestion] = useState<string>("1");
  const [mode, setMode] = useState<MatrixDrawMode>("STATIC");
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Lấy thống kê số câu khả dụng trong ngân hàng theo bộ lọc
  const statsQuery = useQuery({
    queryKey: ["matrix-stats", examId, version.id, categoryId, tag, questionType],
    queryFn: () =>
      examsApi.getMatrixStats(examId, version.id, {
        categoryId: categoryId || null,
        tag: tag.trim() || null,
        questionType: (questionType as QuestionType) || null,
      }),
    enabled: show,
  });

  const stats = statsQuery.data;
  const totalWanted = (easyCount || 0) + (mediumCount || 0) + (hardCount || 0);

  const easyAvail = stats?.easyCandidates ?? 0;
  const mediumAvail = stats?.mediumCandidates ?? 0;
  const hardAvail = stats?.hardCandidates ?? 0;

  const isEasyExceeded = easyCount > easyAvail;
  const isMediumExceeded = mediumCount > mediumAvail;
  const isHardExceeded = hardCount > hardAvail;
  const hasExceeded = isEasyExceeded || isMediumExceeded || isHardExceeded;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (totalWanted <= 0) {
      toast.info("Vui lòng chọn ít nhất 1 câu hỏi.");
      return;
    }
    if (hasExceeded) {
      toast.error("Số câu yêu cầu vượt quá số lượng câu có sẵn trong ngân hàng.");
      return;
    }

    const scoreNum = scorePerQuestion.trim() ? Number(scorePerQuestion) : null;
    if (mode === "POOL" && (!scoreNum || scoreNum < 0.25)) {
      toast.error("Chế độ Pool ngẫu nhiên yêu cầu nhập điểm mỗi câu (tối thiểu 0.25).");
      return;
    }

    const input: MatrixDrawInput = {
      categoryId: categoryId || null,
      tag: tag.trim() || null,
      questionType: (questionType as QuestionType) || null,
      easyCount,
      mediumCount,
      hardCount,
      scorePerQuestion: scoreNum,
      mode,
    };

    setSubmitting(true);
    try {
      const updated = await examsApi.matrixDraw(examId, version.id, input);
      toast.success(
        mode === "STATIC"
          ? `Đã bốc ngẫu nhiên thành công ${totalWanted} câu hỏi vào đề thi!`
          : `Đã thiết lập thành công ${[easyCount > 0, mediumCount > 0, hardCount > 0].filter(Boolean).length} quy tắc Pool ngẫu nhiên!`
      );
      onSuccess(updated);
      onHide();
    } catch (err) {
      toast.error(err);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal show={show} onHide={onHide} size="lg" backdrop="static" centered>
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton>
          <Modal.Title className="h5 fw-bold">
            <span className="me-2">🎲</span> Tạo câu hỏi theo ma trận độ khó
          </Modal.Title>
        </Modal.Header>

        <Modal.Body className="pt-2">
          <Alert variant="primary" className="py-2 px-3 small mb-3">
            <strong>Tự động hóa soạn đề:</strong> Chọn chuyên đề và nhập số lượng câu theo từng mức độ (Dễ, Trung bình, Khó). Hệ thống sẽ tự động lọc từ ngân hàng câu hỏi chưa có trong đề và bốc ngẫu nhiên theo đúng tỷ lệ mong muốn.
          </Alert>

          {/* 1. Bộ lọc nguồn câu hỏi */}
          <Card className="mb-3 border bg-light-subtle">
            <Card.Body className="py-2 px-3">
              <div className="fw-semibold small text-uppercase text-secondary mb-2">1. Phạm vi ngân hàng câu hỏi</div>
              <Row className="g-2">
                <Col md={5}>
                  <Form.Label htmlFor="matrix-category" className="small mb-1 fw-medium">
                    Chuyên đề / Danh mục
                  </Form.Label>
                  <Form.Select
                    id="matrix-category"
                    size="sm"
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                  >
                    <option value="">-- Tất cả chuyên đề --</option>
                    {categories.data?.items.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </Form.Select>
                </Col>

                <Col md={4}>
                  <Form.Label htmlFor="matrix-type" className="small mb-1 fw-medium">
                    Loại câu hỏi
                  </Form.Label>
                  <Form.Select
                    id="matrix-type"
                    size="sm"
                    value={questionType}
                    onChange={(e) => setQuestionType(e.target.value)}
                  >
                    <option value="">Tất cả loại câu</option>
                    {TYPES.map((tKey) => (
                      <option key={tKey} value={tKey}>
                        {t(`enums.questionType.${tKey}`)}
                      </option>
                    ))}
                  </Form.Select>
                </Col>

                <Col md={3}>
                  <Form.Label htmlFor="matrix-tag" className="small mb-1 fw-medium">
                    Tag (tùy chọn)
                  </Form.Label>
                  <Form.Control
                    id="matrix-tag"
                    size="sm"
                    placeholder="VD: chuong-1"
                    value={tag}
                    onChange={(e) => setTag(e.target.value)}
                  />
                </Col>
              </Row>
            </Card.Body>
          </Card>

          {/* 2. Ma trận độ khó */}
          <div className="fw-semibold small text-uppercase text-secondary mb-2">
            2. Ma trận phân bổ số lượng câu hỏi
          </div>

          <div className="table-responsive mb-3 border rounded">
            <Table className="table-sm table-hover align-middle mb-0">
              <thead className="table-light">
                <tr>
                  <th style={{ width: "35%" }}>Mức độ khó</th>
                  <th style={{ width: "35%" }} className="text-center">
                    Khả dụng trong ngân hàng
                  </th>
                  <th style={{ width: "30%" }} className="text-end">
                    Số câu cần lấy
                  </th>
                </tr>
              </thead>
              <tbody>
                {/* Dễ */}
                <tr className={isEasyExceeded ? "table-danger" : ""}>
                  <td>
                    <Badge bg="success" className="me-2 px-2 py-1">
                      Dễ (EASY)
                    </Badge>
                  </td>
                  <td className="text-center">
                    {statsQuery.isLoading ? (
                      <Spinner size="sm" animation="border" />
                    ) : (
                      <Badge bg={easyAvail > 0 ? "light" : "secondary"} text={easyAvail > 0 ? "dark" : undefined} className="border px-2 py-1">
                        <strong>{easyAvail}</strong> câu có sẵn
                      </Badge>
                    )}
                  </td>
                  <td className="text-end">
                    <Form.Control
                      type="number"
                      size="sm"
                      min={0}
                      max={500}
                      value={easyCount}
                      onChange={(e) => setEasyCount(Math.max(0, parseInt(e.target.value) || 0))}
                      className="d-inline-block text-end fw-bold"
                      style={{ width: "90px" }}
                    />
                  </td>
                </tr>

                {/* Trung bình */}
                <tr className={isMediumExceeded ? "table-danger" : ""}>
                  <td>
                    <Badge bg="warning" text="dark" className="me-2 px-2 py-1">
                      Trung bình (MEDIUM)
                    </Badge>
                  </td>
                  <td className="text-center">
                    {statsQuery.isLoading ? (
                      <Spinner size="sm" animation="border" />
                    ) : (
                      <Badge bg={mediumAvail > 0 ? "light" : "secondary"} text={mediumAvail > 0 ? "dark" : undefined} className="border px-2 py-1">
                        <strong>{mediumAvail}</strong> câu có sẵn
                      </Badge>
                    )}
                  </td>
                  <td className="text-end">
                    <Form.Control
                      type="number"
                      size="sm"
                      min={0}
                      max={500}
                      value={mediumCount}
                      onChange={(e) => setMediumCount(Math.max(0, parseInt(e.target.value) || 0))}
                      className="d-inline-block text-end fw-bold"
                      style={{ width: "90px" }}
                    />
                  </td>
                </tr>

                {/* Khó */}
                <tr className={isHardExceeded ? "table-danger" : ""}>
                  <td>
                    <Badge bg="danger" className="me-2 px-2 py-1">
                      Khó (HARD)
                    </Badge>
                  </td>
                  <td className="text-center">
                    {statsQuery.isLoading ? (
                      <Spinner size="sm" animation="border" />
                    ) : (
                      <Badge bg={hardAvail > 0 ? "light" : "secondary"} text={hardAvail > 0 ? "dark" : undefined} className="border px-2 py-1">
                        <strong>{hardAvail}</strong> câu có sẵn
                      </Badge>
                    )}
                  </td>
                  <td className="text-end">
                    <Form.Control
                      type="number"
                      size="sm"
                      min={0}
                      max={500}
                      value={hardCount}
                      onChange={(e) => setHardCount(Math.max(0, parseInt(e.target.value) || 0))}
                      className="d-inline-block text-end fw-bold"
                      style={{ width: "90px" }}
                    />
                  </td>
                </tr>
              </tbody>
              <tfoot className="table-secondary fw-bold">
                <tr>
                  <td>Tổng cộng</td>
                  <td className="text-center text-muted small">
                    {stats ? `${stats.totalCandidates} câu phù hợp bộ lọc` : ""}
                  </td>
                  <td className="text-end">
                    <span className="badge bg-primary fs-6 px-3 py-1">{totalWanted} câu</span>
                  </td>
                </tr>
              </tfoot>
            </Table>
          </div>

          {hasExceeded && (
            <Alert variant="danger" className="py-2 small mb-3">
              ⚠️ Số câu yêu cầu ở một số mức độ đang vượt quá số lượng câu khả dụng trong ngân hàng. Vui lòng giảm số câu hoặc mở rộng chuyên đề.
            </Alert>
          )}

          {/* 3. Cấu hình điểm và Chế độ */}
          <div className="fw-semibold small text-uppercase text-secondary mb-2">
            3. Phương thức bốc câu hỏi & Điểm số
          </div>

          <Row className="g-3 mb-2">
            <Col md={6}>
              <Card
                className={`h-100 border p-2 cursor-pointer ${mode === "STATIC" ? "border-primary bg-primary-subtle" : ""}`}
                style={{ cursor: "pointer" }}
                onClick={() => setMode("STATIC")}
              >
                <div className="d-flex align-items-start gap-2">
                  <Form.Check
                    type="radio"
                    name="drawMode"
                    id="mode-static"
                    checked={mode === "STATIC"}
                    onChange={() => setMode("STATIC")}
                    className="mt-1"
                  />
                  <div>
                    <label htmlFor="mode-static" className="fw-bold mb-1 d-block" style={{ cursor: "pointer" }}>
                      📌 Chốt cố định vào đề thi ngay
                    </label>
                    <p className="small text-muted mb-0">
                      Hệ thống tự động bốc ngẫu nhiên đúng {totalWanted} câu và gắn trực tiếp vào đề. Mọi học viên sẽ làm cùng các câu này. Giáo viên có thể duyệt, xem trước và sửa điểm từng câu.
                    </p>
                  </div>
                </div>
              </Card>
            </Col>

            <Col md={6}>
              <Card
                className={`h-100 border p-2 cursor-pointer ${mode === "POOL" ? "border-primary bg-primary-subtle" : ""}`}
                style={{ cursor: "pointer" }}
                onClick={() => setMode("POOL")}
              >
                <div className="d-flex align-items-start gap-2">
                  <Form.Check
                    type="radio"
                    name="drawMode"
                    id="mode-pool"
                    checked={mode === "POOL"}
                    onChange={() => setMode("POOL")}
                    className="mt-1"
                  />
                  <div>
                    <label htmlFor="mode-pool" className="fw-bold mb-1 d-block" style={{ cursor: "pointer" }}>
                      🔄 Bốc ngẫu nhiên cho từng lượt thi (Pool)
                    </label>
                    <p className="small text-muted mb-0">
                      Tự động tạo các quy tắc Pool. Mỗi khi thí sinh bấm &quot;Bắt đầu thi&quot;, hệ thống mới bốc ngẫu nhiên bộ câu khác nhau cho từng thí sinh theo đúng ma trận độ khó này.
                    </p>
                  </div>
                </div>
              </Card>
            </Col>

            <Col md={12}>
              <div className="d-flex align-items-center gap-2 mt-1">
                <Form.Label htmlFor="matrix-score" className="small mb-0 text-nowrap fw-medium">
                  Điểm số mỗi câu hỏi:
                </Form.Label>
                <Form.Control
                  id="matrix-score"
                  size="sm"
                  type="number"
                  min={0.25}
                  max={100}
                  step={0.25}
                  value={scorePerQuestion}
                  placeholder={mode === "STATIC" ? "Để trống = giữ điểm mặc định" : "Bắt buộc với Pool (VD: 1)"}
                  onChange={(e) => setScorePerQuestion(e.target.value)}
                  style={{ width: "200px" }}
                />
                <span className="small text-muted">
                  {mode === "STATIC" ? "(Để trống sẽ lấy điểm mặc định của câu trong ngân hàng)" : "(Bội số của 0.25, tối thiểu 0.25)"}
                </span>
              </div>
            </Col>
          </Row>
        </Modal.Body>

        <Modal.Footer>
          <Button variant="secondary" size="sm" onClick={onHide} disabled={submitting}>
            Hủy
          </Button>
          <Button
            variant="primary"
            size="sm"
            type="submit"
            disabled={submitting || totalWanted <= 0 || hasExceeded || statsQuery.isLoading}
            className="px-3"
          >
            {submitting ? (
              <>
                <Spinner size="sm" animation="border" className="me-2" /> Đang bốc câu hỏi...
              </>
            ) : (
              <>
                <span className="me-1">⚡</span> Sinh {totalWanted} câu hỏi vào đề
              </>
            )}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}
