import { useState } from "react";
import { Alert, Button, Form, Modal } from "react-bootstrap";
import { SearchableSelect } from "../../components/common/SearchableSelect";
import { useActiveCategories } from "../../hooks/useCategories";

export interface ImportChoice {
  /** Chuyên đề (bắt buộc) của cả bộ câu hỏi; mã câu hỏi sinh theo mã chuyên đề. */
  categoryId: string;
  /** Ô "Tạo đề thi": true thì tạo luôn đề thi nháp (thuộc chuyên đề) từ các câu vừa nhập; false chỉ nhập vào ngân hàng. */
  createExam: boolean;
}

/**
 * Mã câu hỏi sinh khi nhập (khớp backend CodeGenerator): mã chuyên đề-mức độ 2 số-số thứ tự 6 số, ví dụ "12.HS-01-000001".
 * Ký tự ngoài A-Z a-z 0-9 . _ - trong mã chuyên đề đổi thành "_".
 */
export function questionCodeExample(categoryCode: string, level: number, sequence: number): string {
  const prefix = categoryCode.trim().replace(/[^A-Za-z0-9._-]/g, "_").slice(0, 90);
  return `${prefix}-${String(level).padStart(2, "0")}-${String(sequence).padStart(6, "0")}`;
}

/**
 * Bấm "Import" sau khi xem trước: chọn chuyên đề (bắt buộc) cho cả bộ câu hỏi; tích "Tạo đề thi" thì tạo luôn đề thi
 * nháp từ các câu này, không tích thì chỉ nhập vào ngân hàng câu hỏi.
 */
export function MarkdownImportDialog({
  show,
  questionCount,
  examTitle,
  onHide,
  onConfirm,
}: {
  show: boolean;
  questionCount: number;
  examTitle: string | null;
  onHide: () => void;
  onConfirm: (choice: ImportChoice) => void;
}) {
  const categories = useActiveCategories();
  const [categoryId, setCategoryId] = useState("");
  const [createExam, setCreateExam] = useState(false);
  // Câu hỏi nhập vào chuyên đề đã chọn: chỉ chuyên đề chứa được câu hỏi (không có chuyên đề con, D-32)
  const items = (categories.data?.items ?? []).filter((c) => !c.childCount);
  const category = items.find((c) => c.id === categoryId);

  return (
    <Modal show={show} onHide={onHide} centered>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">Import {questionCount} câu hỏi</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <Form.Group controlId="md-import-category">
          <Form.Label className="fw-semibold">
            Chuyên đề cho bộ câu hỏi <span className="text-danger">*</span>
          </Form.Label>
          <SearchableSelect
            id="md-import-category"
            value={categoryId}
            onChange={setCategoryId}
            options={items.map((c) => ({ value: c.id, label: c.name, code: c.code }))}
            placeholder="-- Chọn một chuyên đề --"
            searchPlaceholder="Tìm theo mã hoặc tên chuyên đề..."
            loading={categories.isLoading}
          />
          {!categories.isLoading && items.length === 0 && (
            <Form.Text className="text-danger">Chưa có chuyên đề nào đang hoạt động. Tạo chuyên đề trước khi import.</Form.Text>
          )}
        </Form.Group>

        {category && (
          <Alert variant="light" className="border small mt-3 mb-0">
            Mã câu hỏi sinh theo dạng <strong>mã chuyên đề-mức độ-số thứ tự</strong>, ví dụ{" "}
            <code>{questionCodeExample(category.code, 1, 1)}</code>, <code>{questionCodeExample(category.code, 2, 2)}</code>… Số thứ
            tự 6 số đếm tiếp trong chuyên đề, không trùng; câu chưa đặt mức độ ghi <code>00</code>.
          </Alert>
        )}

        <Form.Check
          id="md-import-create-exam"
          className="mt-3"
          checked={createExam}
          onChange={(e) => setCreateExam(e.target.checked)}
          label={<span className="fw-semibold">Tạo đề thi</span>}
        />
        <div className="small text-secondary ms-4">
          {createExam
            ? `Tạo luôn đề thi nháp${examTitle ? ` "${examTitle}"` : ""} gồm ${questionCount} câu này (thuộc chuyên đề đã chọn); các câu hỏi cũng vào ngân hàng câu hỏi.`
            : "Không tích: chỉ nhập các câu hỏi vào ngân hàng câu hỏi của chuyên đề."}
        </div>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>
          Hủy
        </Button>
        <Button disabled={!categoryId} onClick={() => onConfirm({ categoryId, createExam })}>
          <i className="bi bi-cloud-upload me-1" aria-hidden="true" />
          {createExam ? "Nhập và tạo đề thi" : "Nhập vào ngân hàng câu hỏi"}
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
