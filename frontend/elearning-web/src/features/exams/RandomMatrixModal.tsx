import { useMemo, useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, Modal, Row, Table, Spinner } from "react-bootstrap";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { examsApi } from "../../services/api";
import { useActiveCategories } from "../../hooks/useCategories";
import { useToast } from "../../components/common/toast";
import { SearchableSelect, type SearchableOption } from "../../components/common/SearchableSelect";
import type { MatrixDrawInput, MatrixDrawMode, MatrixTopicItem, QuestionType, VersionDetail } from "../../types/api";

const TYPES: QuestionType[] = ["SINGLE_CHOICE", "MULTIPLE_CHOICE", "TRUE_FALSE", "FILL_IN", "ESSAY"];

interface TopicRow {
  key: string;
  categoryId: string | null;
  categoryName: string;
  categoryCode?: string;
  easyCount: number;
  mediumCount: number;
  hardCount: number;
}

interface RandomMatrixModalProps {
  show: boolean;
  onHide: () => void;
  examId: string;
  version: VersionDetail;
  /** Chuyên đề của đề (D-30): ma trận chỉ lấy câu của chuyên đề này. */
  lockedCategoryId?: string | null;
  onSuccess: (updatedVersion: VersionDetail) => void;
}

export function RandomMatrixModal({ show, onHide, examId, version, lockedCategoryId = null, onSuccess }: RandomMatrixModalProps) {
  const { t } = useTranslation();
  const toast = useToast();
  const categories = useActiveCategories();
  const categoryItems = useMemo(
    () => (categories.data?.items ?? []).filter((c) => !lockedCategoryId || c.id === lockedCategoryId),
    [categories.data, lockedCategoryId],
  );

  // Bộ lọc chung ngân hàng
  const [tag, setTag] = useState<string>("");
  const [questionType, setQuestionType] = useState<string>("");
  const [scorePerQuestion, setScorePerQuestion] = useState<string>("0.5");
  const [mode, setMode] = useState<MatrixDrawMode>("STATIC");
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Danh sách các chuyên đề trong ma trận
  const [topicRows, setTopicRows] = useState<TopicRow[]>([]);

  // Lấy thống kê số câu khả dụng trong ngân hàng (tổng quan và theo từng chuyên đề)
  const statsQuery = useQuery({
    queryKey: ["matrix-stats", examId, version.id, tag, questionType],
    queryFn: () =>
      examsApi.getMatrixStats(examId, version.id, {
        categoryId: null,
        tag: tag.trim() || null,
        questionType: (questionType as QuestionType) || null,
      }),
    enabled: show,
  });

  const stats = statsQuery.data;

  // Bản đồ thống kê khả dụng theo CategoryId
  const categoryStatsMap = useMemo(() => {
    const map = new Map<string, { easy: number; medium: number; hard: number; total: number }>();
    if (stats?.byCategory) {
      for (const item of stats.byCategory) {
        const key = item.categoryId ?? "uncategorized";
        map.set(key, {
          easy: item.easyCandidates,
          medium: item.mediumCandidates,
          hard: item.hardCandidates,
          total: item.totalCandidates,
        });
      }
    }
    return map;
  }, [stats]);

  // Thêm một chuyên đề vào ma trận
  const handleAddCategory = (catId: string) => {
    if (!catId) return;

    if (catId === "uncategorized") {
      if (topicRows.some((r) => r.categoryId === null)) return;
      setTopicRows((prev) => [
        ...prev,
        {
          key: "uncategorized",
          categoryId: null,
          categoryName: "Chưa phân loại chuyên đề",
          categoryCode: "UNSET",
          easyCount: 2,
          mediumCount: 1,
          hardCount: 1,
        },
      ]);
      return;
    }

    const found = categoryItems.find((c) => c.id === catId);
    if (!found) return;
    if (topicRows.some((r) => r.categoryId === catId)) return;

    setTopicRows((prev) => [
      ...prev,
      {
        key: catId,
        categoryId: catId,
        categoryName: found.name,
        categoryCode: found.code,
        easyCount: 2,
        mediumCount: 1,
        hardCount: 1,
      },
    ]);
  };

  // Nạp tất cả chuyên đề có câu hỏi khả dụng vào ma trận
  const handleLoadAllAvailableCategories = () => {
    const newRows: TopicRow[] = [];
    const addedIds = new Set<string>();

    for (const cat of categoryItems) {
      const cStat = categoryStatsMap.get(cat.id);
      if (cStat && cStat.total > 0) {
        newRows.push({
          key: cat.id,
          categoryId: cat.id,
          categoryName: cat.name,
          categoryCode: cat.code,
          easyCount: Math.min(2, cStat.easy),
          mediumCount: Math.min(1, cStat.medium),
          hardCount: Math.min(1, cStat.hard),
        });
        addedIds.add(cat.id);
      }
    }

    // Nếu có câu chưa phân loại (đề không gắn chuyên đề)
    const uncatStat = categoryStatsMap.get("uncategorized");
    if (!lockedCategoryId && uncatStat && uncatStat.total > 0) {
      newRows.push({
        key: "uncategorized",
        categoryId: null,
        categoryName: "Chưa phân loại chuyên đề",
        categoryCode: "UNSET",
        easyCount: Math.min(2, uncatStat.easy),
        mediumCount: Math.min(1, uncatStat.medium),
        hardCount: Math.min(1, uncatStat.hard),
      });
    }

    if (newRows.length === 0) {
      toast.info("Không có chuyên đề nào có câu hỏi khả dụng phù hợp với bộ lọc hiện tại.");
      return;
    }

    setTopicRows(newRows);
    toast.success(`Đã nạp ${newRows.length} chuyên đề vào ma trận!`);
  };

  // Cập nhật số câu của một chuyên đề
  const handleUpdateCount = (
    key: string,
    field: "easyCount" | "mediumCount" | "hardCount",
    val: number
  ) => {
    setTopicRows((prev) =>
      prev.map((r) => (r.key === key ? { ...r, [field]: Math.max(0, val) } : r))
    );
  };

  // Xóa chuyên đề khỏi ma trận
  const handleRemoveRow = (key: string) => {
    setTopicRows((prev) => prev.filter((r) => r.key !== key));
  };

  // Điền nhanh cho tất cả chuyên đề trong ma trận
  const handleBulkPreset = (easy: number, medium: number, hard: number) => {
    setTopicRows((prev) =>
      prev.map((r) => ({
        ...r,
        easyCount: easy,
        mediumCount: medium,
        hardCount: hard,
      }))
    );
  };

  // Tính toán tổng số lượng và kiểm tra vượt quá
  const rowEvaluations = useMemo(() => {
    return topicRows.map((row) => {
      const catKey = row.categoryId ?? "uncategorized";
      const catStat = categoryStatsMap.get(catKey) ?? { easy: 0, medium: 0, hard: 0, total: 0 };

      const easyExceeded = row.easyCount > catStat.easy;
      const mediumExceeded = row.mediumCount > catStat.medium;
      const hardExceeded = row.hardCount > catStat.hard;
      const rowTotal = row.easyCount + row.mediumCount + row.hardCount;

      return {
        ...row,
        availEasy: catStat.easy,
        availMedium: catStat.medium,
        availHard: catStat.hard,
        availTotal: catStat.total,
        easyExceeded,
        mediumExceeded,
        hardExceeded,
        hasExceeded: easyExceeded || mediumExceeded || hardExceeded,
        rowTotal,
      };
    });
  }, [topicRows, categoryStatsMap]);

  const totalEasyWanted = useMemo(
    () => rowEvaluations.reduce((sum, r) => sum + r.easyCount, 0),
    [rowEvaluations]
  );
  const totalMediumWanted = useMemo(
    () => rowEvaluations.reduce((sum, r) => sum + r.mediumCount, 0),
    [rowEvaluations]
  );
  const totalHardWanted = useMemo(
    () => rowEvaluations.reduce((sum, r) => sum + r.hardCount, 0),
    [rowEvaluations]
  );
  const totalQuestionsWanted = totalEasyWanted + totalMediumWanted + totalHardWanted;

  const anyExceeded = rowEvaluations.some((r) => r.hasExceeded);
  const exceededRows = rowEvaluations.filter((r) => r.hasExceeded);

  const scoreNum = scorePerQuestion.trim() ? Number(scorePerQuestion) : null;
  const estimatedTotalScore =
    scoreNum && scoreNum > 0 ? (totalQuestionsWanted * scoreNum).toFixed(2) : null;

  // Xử lý gửi yêu cầu tạo ma trận
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (topicRows.length === 0) {
      toast.error("Vui lòng thêm ít nhất một chuyên đề vào ma trận.");
      return;
    }

    if (totalQuestionsWanted <= 0) {
      toast.info("Vui lòng nhập số lượng câu hỏi cần lấy cho các chuyên đề.");
      return;
    }

    if (anyExceeded) {
      toast.error(
        `Số câu yêu cầu ở chuyên đề "${exceededRows[0]?.categoryName}" vượt quá số lượng câu có sẵn trong ngân hàng.`
      );
      return;
    }

    if (mode === "POOL" && (!scoreNum || scoreNum < 0.25)) {
      toast.error("Chế độ Pool ngẫu nhiên yêu cầu nhập điểm mỗi câu (tối thiểu 0.25).");
      return;
    }

    const topicsPayload: MatrixTopicItem[] = topicRows
      .filter((r) => r.easyCount > 0 || r.mediumCount > 0 || r.hardCount > 0)
      .map((r) => ({
        categoryId: r.categoryId,
        easyCount: r.easyCount,
        mediumCount: r.mediumCount,
        hardCount: r.hardCount,
        scorePerQuestion: scoreNum,
      }));

    const input: MatrixDrawInput = {
      tag: tag.trim() || null,
      questionType: (questionType as QuestionType) || null,
      scorePerQuestion: scoreNum,
      mode,
      topics: topicsPayload,
    };

    setSubmitting(true);
    try {
      const updated = await examsApi.matrixDraw(examId, version.id, input);
      toast.success(
        mode === "STATIC"
          ? `Đã bốc ngẫu nhiên thành công ${totalQuestionsWanted} câu hỏi từ ${topicsPayload.length} chuyên đề vào đề thi!`
          : `Đã thiết lập thành công quy tắc Pool ngẫu nhiên cho ${topicsPayload.length} chuyên đề!`
      );
      onSuccess(updated);
      onHide();
    } catch (err) {
      toast.error(err);
    } finally {
      setSubmitting(false);
    }
  };

  // Danh sách các chuyên đề chưa được chọn vào bảng (dùng cho SearchableSelect)
  const unselectedOptions = useMemo(() => {
    const selectedIds = new Set(topicRows.map((r) => r.categoryId));
    const unselected = categoryItems.filter((c) => !selectedIds.has(c.id));

    const opts: SearchableOption[] = unselected.map((c) => {
      const cStat = categoryStatsMap.get(c.id);
      return {
        value: c.id,
        label: c.name,
        code: c.code,
        sublabel: cStat && cStat.total > 0 ? `${cStat.total} câu khả dụng` : undefined,
      };
    });

    if (!lockedCategoryId && !topicRows.some((r) => r.categoryId === null)) {
      const uncatStat = categoryStatsMap.get("uncategorized");
      opts.push({
        value: "uncategorized",
        label: "Câu hỏi chưa phân loại chuyên đề",
        code: "UNSET",
        sublabel: uncatStat && uncatStat.total > 0 ? `${uncatStat.total} câu khả dụng` : undefined,
      });
    }

    return opts;
  }, [categoryItems, lockedCategoryId, topicRows, categoryStatsMap]);

  return (
    <Modal show={show} onHide={onHide} size="xl" backdrop="static" centered>
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton>
          <Modal.Title className="h5 fw-bold d-flex align-items-center gap-2">
            <span className="fs-4">🎲</span>
            <span>Ma trận tạo đề thi theo chuyên đề &amp; độ khó</span>
          </Modal.Title>
        </Modal.Header>

        <Modal.Body className="pt-2">
          <Alert variant="primary" className="py-2 px-3 small mb-3">
            <div className="d-flex align-items-center gap-2">
              <i className="bi bi-info-circle-fill fs-5 text-primary" />
              <div>
                <strong>Ma trận đa chuyên đề:</strong> Cho phép chọn nhiều chuyên đề cùng lúc và định lượng chi tiết số câu theo từng mức độ nhận thức (<strong>Dễ</strong>, <strong>Trung bình</strong>, <strong>Khó</strong>). Hệ thống kiểm tra trực tiếp số câu sẵn có trong ngân hàng để đảm bảo đề thi đủ câu và không bị trùng lặp.
              </div>
            </div>
          </Alert>

          {/* 1. Bộ lọc nguồn câu hỏi chung */}
          <Card className="mb-3 border bg-light-subtle shadow-sm">
            <Card.Body className="py-2 px-3">
              <div className="d-flex justify-content-between align-items-center mb-2">
                <span className="fw-semibold small text-uppercase text-secondary">
                  1. Tiêu chí lọc bổ sung từ ngân hàng
                </span>
                {stats && (
                  <span className="small text-muted">
                    Tổng câu hỏi hợp lệ trong ngân hàng: <strong>{stats.totalCandidates}</strong> câu
                  </span>
                )}
              </div>
              <Row className="g-2">
                <Col md={6}>
                  <Form.Label htmlFor="matrix-type" className="small mb-1 fw-medium">
                    Loại câu hỏi
                  </Form.Label>
                  <Form.Select
                    id="matrix-type"
                    size="sm"
                    value={questionType}
                    onChange={(e) => setQuestionType(e.target.value)}
                  >
                    <option value="">Tất cả loại câu (Trắc nghiệm, Điền khuyết, Tự luận...)</option>
                    {TYPES.map((tKey) => (
                      <option key={tKey} value={tKey}>
                        {t(`enums.questionType.${tKey}`)}
                      </option>
                    ))}
                  </Form.Select>
                </Col>

                <Col md={6}>
                  <Form.Label htmlFor="matrix-tag" className="small mb-1 fw-medium">
                    Tag / Thẻ phân loại (tùy chọn)
                  </Form.Label>
                  <Form.Control
                    id="matrix-tag"
                    size="sm"
                    placeholder="VD: hk1, giua-ky..."
                    value={tag}
                    onChange={(e) => setTag(e.target.value)}
                  />
                </Col>
              </Row>
            </Card.Body>
          </Card>

          {/* 2. Bảng Ma trận Chuyên đề & Độ khó */}
          <Card className="mb-3 border shadow-sm">
            <Card.Header className="bg-white py-2">
              <div className="d-flex justify-content-between align-items-center flex-wrap gap-2">
                <div className="fw-bold text-dark d-flex align-items-center gap-2">
                  <i className="bi bi-grid-3x3-gap-fill text-primary" />
                  <span>2. Bảng ma trận chuyên đề &amp; mức độ khó</span>
                  <Badge bg="secondary" pill>
                    {topicRows.length} chuyên đề
                  </Badge>
                </div>

                <div className="d-flex align-items-center gap-2 flex-wrap">
                  <div style={{ width: "280px" }}>
                    <SearchableSelect
                      size="sm"
                      value=""
                      onChange={(val) => {
                        if (val) handleAddCategory(val);
                      }}
                      options={unselectedOptions}
                      placeholder="+ Thêm chuyên đề vào ma trận..."
                      searchPlaceholder="Tìm theo mã hoặc tên chuyên đề..."
                      clearable={false}
                    />
                  </div>

                  <Button
                    size="sm"
                    variant="outline-primary"
                    onClick={handleLoadAllAvailableCategories}
                    title="Tự động thêm tất cả các chuyên đề đang có câu hỏi vào ma trận"
                  >
                    <i className="bi bi-magic me-1" />
                    Nạp tất cả chuyên đề có câu
                  </Button>
                </div>
              </div>
            </Card.Header>

            <Card.Body className="p-0">
              {topicRows.length === 0 ? (
                <div className="text-center py-5 text-muted">
                  <i className="bi bi-collection fs-1 text-secondary opacity-50 d-block mb-2" />
                  <div className="fw-semibold">Chưa có chuyên đề nào trong ma trận</div>
                  <div className="small mb-3">
                    Hãy chọn một hoặc nhiều chuyên đề từ danh sách phía trên hoặc bấm nút bên dưới để nạp nhanh.
                  </div>
                  <Button
                    size="sm"
                    variant="primary"
                    onClick={handleLoadAllAvailableCategories}
                    className="px-3"
                  >
                    <i className="bi bi-plus-circle me-1" />
                    Nạp tất cả chuyên đề có sẵn trong ngân hàng
                  </Button>
                </div>
              ) : (
                <>
                  <div className="p-2 px-3 bg-light border-bottom d-flex align-items-center justify-content-between flex-wrap gap-2 small">
                    <span className="text-secondary fw-semibold">
                      Điền nhanh số câu cho tất cả chuyên đề:
                    </span>
                    <div className="btn-group btn-group-sm">
                      <Button
                        variant="outline-secondary"
                        size="sm"
                        onClick={() => handleBulkPreset(1, 1, 1)}
                      >
                        1 Dễ · 1 TB · 1 Khó
                      </Button>
                      <Button
                        variant="outline-secondary"
                        size="sm"
                        onClick={() => handleBulkPreset(2, 1, 1)}
                      >
                        2 Dễ · 1 TB · 1 Khó
                      </Button>
                      <Button
                        variant="outline-secondary"
                        size="sm"
                        onClick={() => handleBulkPreset(3, 2, 1)}
                      >
                        3 Dễ · 2 TB · 1 Khó
                      </Button>
                      <Button
                        variant="outline-secondary"
                        size="sm"
                        onClick={() => handleBulkPreset(0, 0, 0)}
                      >
                        Đặt lại về 0
                      </Button>
                    </div>
                  </div>

                  <div className="table-responsive">
                    <Table hover size="sm" className="align-middle mb-0 text-nowrap">
                      <thead className="table-light border-bottom">
                        <tr>
                          <th style={{ width: 40 }} className="text-center">
                            #
                          </th>
                          <th style={{ minWidth: 220 }}>Chuyên đề / Danh mục</th>
                          <th style={{ width: 170 }} className="text-center bg-success-subtle bg-opacity-25">
                            <span className="text-success fw-bold">Dễ</span> (Nhận biết)
                          </th>
                          <th style={{ width: 170 }} className="text-center bg-warning-subtle bg-opacity-25">
                            <span className="text-warning-emphasis fw-bold">Trung bình</span> (Thông hiểu)
                          </th>
                          <th style={{ width: 170 }} className="text-center bg-danger-subtle bg-opacity-25">
                            <span className="text-danger fw-bold">Khó</span> (Vận dụng)
                          </th>
                          <th style={{ width: 100 }} className="text-center">
                            Tổng câu
                          </th>
                          <th style={{ width: 60 }} className="text-center">
                            Xóa
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {rowEvaluations.map((row, idx) => (
                          <tr key={row.key} className={row.hasExceeded ? "table-danger" : ""}>
                            <td className="text-center text-muted small">{idx + 1}</td>
                            <td>
                              <div className="fw-semibold text-dark">{row.categoryName}</div>
                              <div className="text-muted small">
                                {row.categoryCode && (
                                  <span className="font-monospace me-2 text-secondary">
                                    [{row.categoryCode}]
                                  </span>
                                )}
                                <span>
                                  Ngân hàng: <strong>{row.availTotal}</strong> câu khả dụng
                                </span>
                              </div>
                            </td>

                            {/* Cột Dễ */}
                            <td className="text-center">
                              <div className="d-flex flex-column align-items-center gap-1">
                                <Form.Control
                                  type="number"
                                  size="sm"
                                  min={0}
                                  max={200}
                                  value={row.easyCount}
                                  className={`text-center fw-bold ${row.easyExceeded ? "is-invalid border-danger text-danger" : ""}`}
                                  style={{ width: "90px" }}
                                  onChange={(e) =>
                                    handleUpdateCount(
                                      row.key,
                                      "easyCount",
                                      parseInt(e.target.value) || 0
                                    )
                                  }
                                />
                                <span
                                  className={`badge ${row.easyExceeded ? "bg-danger text-white" : "bg-light text-secondary border"}`}
                                  style={{ fontSize: "0.75rem" }}
                                >
                                  {row.easyExceeded
                                    ? `Vượt quá (Có: ${row.availEasy})`
                                    : `Có sẵn: ${row.availEasy}`}
                                </span>
                              </div>
                            </td>

                            {/* Cột Trung bình */}
                            <td className="text-center">
                              <div className="d-flex flex-column align-items-center gap-1">
                                <Form.Control
                                  type="number"
                                  size="sm"
                                  min={0}
                                  max={200}
                                  value={row.mediumCount}
                                  className={`text-center fw-bold ${row.mediumExceeded ? "is-invalid border-danger text-danger" : ""}`}
                                  style={{ width: "90px" }}
                                  onChange={(e) =>
                                    handleUpdateCount(
                                      row.key,
                                      "mediumCount",
                                      parseInt(e.target.value) || 0
                                    )
                                  }
                                />
                                <span
                                  className={`badge ${row.mediumExceeded ? "bg-danger text-white" : "bg-light text-secondary border"}`}
                                  style={{ fontSize: "0.75rem" }}
                                >
                                  {row.mediumExceeded
                                    ? `Vượt quá (Có: ${row.availMedium})`
                                    : `Có sẵn: ${row.availMedium}`}
                                </span>
                              </div>
                            </td>

                            {/* Cột Khó */}
                            <td className="text-center">
                              <div className="d-flex flex-column align-items-center gap-1">
                                <Form.Control
                                  type="number"
                                  size="sm"
                                  min={0}
                                  max={200}
                                  value={row.hardCount}
                                  className={`text-center fw-bold ${row.hardExceeded ? "is-invalid border-danger text-danger" : ""}`}
                                  style={{ width: "90px" }}
                                  onChange={(e) =>
                                    handleUpdateCount(
                                      row.key,
                                      "hardCount",
                                      parseInt(e.target.value) || 0
                                    )
                                  }
                                />
                                <span
                                  className={`badge ${row.hardExceeded ? "bg-danger text-white" : "bg-light text-secondary border"}`}
                                  style={{ fontSize: "0.75rem" }}
                                >
                                  {row.hardExceeded
                                    ? `Vượt quá (Có: ${row.availHard})`
                                    : `Có sẵn: ${row.availHard}`}
                                </span>
                              </div>
                            </td>

                            {/* Tổng câu của chuyên đề */}
                            <td className="text-center">
                              <span
                                className={`badge fs-6 ${row.rowTotal > 0 ? "bg-primary" : "bg-secondary text-white"}`}
                              >
                                {row.rowTotal}
                              </span>
                            </td>

                            {/* Nút xóa hàng */}
                            <td className="text-center">
                              <Button
                                size="sm"
                                variant="outline-danger"
                                className="p-1 px-2"
                                title="Xóa chuyên đề này khỏi ma trận"
                                onClick={() => handleRemoveRow(row.key)}
                              >
                                <i className="bi bi-trash" />
                              </Button>
                            </td>
                          </tr>
                        ))}
                      </tbody>

                      {/* Hàng tổng cộng ma trận */}
                      <tfoot className="table-secondary fw-bold border-top-2">
                        <tr>
                          <td colSpan={2} className="text-end pe-3">
                            TỔNG CỘNG ({topicRows.length} chuyên đề):
                          </td>
                          <td className="text-center text-success fs-6">
                            {totalEasyWanted} câu Dễ
                          </td>
                          <td className="text-center text-warning-emphasis fs-6">
                            {totalMediumWanted} câu TB
                          </td>
                          <td className="text-center text-danger fs-6">
                            {totalHardWanted} câu Khó
                          </td>
                          <td className="text-center text-primary fs-5">
                            {totalQuestionsWanted} câu
                          </td>
                          <td></td>
                        </tr>
                      </tfoot>
                    </Table>
                  </div>
                </>
              )}
            </Card.Body>
          </Card>

          {anyExceeded && (
            <Alert variant="danger" className="py-2 small mb-3">
              <i className="bi bi-exclamation-triangle-fill me-2" />
              <strong>Cảnh báo:</strong> Một số chuyên đề yêu cầu số câu vượt quá số câu khả dụng trong ngân hàng (được tô đỏ). Vui lòng điều chỉnh lại số lượng câu hỏi trước khi bốc.
            </Alert>
          )}

          {/* 3. Cấu hình điểm và Phương thức bốc */}
          <div className="fw-semibold small text-uppercase text-secondary mb-2">
            3. Phương thức bốc câu hỏi &amp; Thang điểm
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
                      📌 Chốt cố định vào đề thi ngay (STATIC)
                    </label>
                    <p className="small text-muted mb-0">
                      Hệ thống tự động bốc ngẫu nhiên đúng {totalQuestionsWanted} câu theo ma trận và gắn trực tiếp vào đề thi. Mọi học sinh sẽ làm cùng các câu hỏi này. Bạn có thể xem trước và tinh chỉnh lại từng câu.
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
                      🔄 Bốc ngẫu nhiên cho từng lượt thi (POOL)
                    </label>
                    <p className="small text-muted mb-0">
                      Tự động tạo các quy tắc Pool theo từng chuyên đề và độ khó. Mỗi khi thí sinh bấm &quot;Bắt đầu thi&quot;, hệ thống mới bốc ngẫu nhiên bộ câu khác nhau cho từng thí sinh theo đúng ma trận này.
                    </p>
                  </div>
                </div>
              </Card>
            </Col>

            <Col md={12}>
              <div className="d-flex align-items-center gap-2 mt-1 flex-wrap">
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
                  placeholder={mode === "STATIC" ? "Để trống = giữ điểm gốc" : "Bắt buộc với Pool (VD: 0.5)"}
                  onChange={(e) => setScorePerQuestion(e.target.value)}
                  style={{ width: "160px" }}
                />
                {estimatedTotalScore && (
                  <Badge bg="info" className="text-dark bg-opacity-25 border border-info p-2">
                    Dự tính điểm tổng: <strong>{estimatedTotalScore}</strong> điểm ({totalQuestionsWanted} câu × {scoreNum}đ)
                  </Badge>
                )}
                <span className="small text-muted">
                  {mode === "STATIC"
                    ? "(Để trống sẽ giữ điểm mặc định của câu trong ngân hàng)"
                    : "(Bắt buộc, tối thiểu 0.25 và là bội số của 0.25)"}
                </span>
              </div>
            </Col>
          </Row>
        </Modal.Body>

        <Modal.Footer className="d-flex justify-content-between">
          <div className="text-muted small">
            {totalQuestionsWanted > 0 && (
              <span>
                Đang cấu hình: <strong>{topicRows.length}</strong> chuyên đề · <strong>{totalQuestionsWanted}</strong> câu hỏi
                ({totalEasyWanted} Dễ, {totalMediumWanted} TB, {totalHardWanted} Khó)
              </span>
            )}
          </div>

          <div className="d-flex align-items-center gap-2">
            <Button variant="secondary" size="sm" onClick={onHide} disabled={submitting}>
              Đóng
            </Button>
            <Button
              variant="primary"
              size="sm"
              type="submit"
              disabled={
                submitting ||
                totalQuestionsWanted <= 0 ||
                anyExceeded ||
                statsQuery.isLoading ||
                topicRows.length === 0
              }
              className="px-4"
            >
              {submitting ? (
                <>
                  <Spinner size="sm" animation="border" className="me-2" /> Đang bốc câu hỏi...
                </>
              ) : (
                <>
                  <span className="me-1">⚡</span> Tiến hành bốc {totalQuestionsWanted} câu theo ma trận
                </>
              )}
            </Button>
          </div>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}
