import { useState } from "react";
import { Alert, Button, Form, Modal } from "react-bootstrap";
import { SearchableSelect } from "../../components/common/SearchableSelect";
import { useActiveCategories } from "../../hooks/useCategories";

export interface ImportChoice {
  /**
   * Đúng một chuyên đề: câu hỏi nhập vào chuyên đề này, mã câu hỏi sinh theo mã chuyên đề. Bắt buộc khi nhập theo chuyên đề;
   * nhập theo đề thi có thể bỏ trống (null): câu hỏi không gắn chuyên đề, mã tự sinh "Q000123".
   */
  categoryId: string | null;
  /** true: tạo luôn đề thi nháp (thuộc chuyên đề) từ các câu vừa nhập. */
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

/** Bấm "Import" sau khi xem trước: chọn cách nhập (theo chuyên đề / theo đề thi) và đúng một chuyên đề. */
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
  const [mode, setMode] = useState<"CATEGORY" | "EXAM">("CATEGORY");
  const [categoryId, setCategoryId] = useState("");
  // Câu hỏi nhập vào chuyên đề đã chọn: chỉ chuyên đề chứa được câu hỏi (không có chuyên đề con, D-32)
  const items = (categories.data?.items ?? []).filter((c) => !c.childCount);
  const category = items.find((c) => c.id === categoryId);
  const categoryRequired = mode === "CATEGORY";

  const option = (value: "CATEGORY" | "EXAM", title: string, detail: string) => (
    <div className={`border rounded p-2 mb-2 ${mode === value ? "border-primary bg-primary-subtle" : ""}`}>
      <Form.Check
        type="radio"
        id={`md-import-${value}`}
        name="md-import-mode"
        checked={mode === value}
        onChange={() => setMode(value)}
        label={<span className="fw-semibold">{title}</span>}
      />
      <div className="small text-secondary ms-4">{detail}</div>
    </div>
  );

  return (
    <Modal show={show} onHide={onHide} centered>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">Import {questionCount} câu hỏi</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <div className="fw-semibold mb-2">1. Cách nhập</div>
        {option("CATEGORY", "Import theo chuyên đề", "Tạo trực tiếp các câu hỏi vào ngân hàng câu hỏi của chuyên đề.")}
        {option(
          "EXAM",
          "Import theo đề thi",
          `Tạo đề thi nháp${examTitle ? ` "${examTitle}"` : ""} gồm các câu này; các câu hỏi cũng được đưa vào ngân hàng câu hỏi.`,
        )}

        <Form.Group controlId="md-import-category" className="mt-3">
          <Form.Label className="fw-semibold">
            2. Chuyên đề {categoryRequired ? <span className="text-danger">*</span> : <span className="text-secondary fw-normal">(không bắt buộc)</span>}
          </Form.Label>
          <SearchableSelect
            id="md-import-category"
            value={categoryId}
            onChange={setCategoryId}
            options={items.map((c) => ({ value: c.id, label: c.name, code: c.code }))}
            placeholder={categoryRequired ? "-- Chọn một chuyên đề --" : "-- Không gắn chuyên đề --"}
            searchPlaceholder="Tìm theo mã hoặc tên chuyên đề..."
            loading={categories.isLoading}
          />
        </Form.Group>

        {!category && !categoryRequired && (
          <Alert variant="light" className="border small mt-3 mb-0">
            Không chọn chuyên đề: đề thi và câu hỏi không gắn chuyên đề, mã câu hỏi tự sinh dạng <code>Q000123</code>. Chọn chuyên đề
            để mã có dạng <code>mã chuyên đề-mức độ-số thứ tự</code> và đề thi chỉ lấy câu của chuyên đề đó.
          </Alert>
        )}

        {category && (
          <Alert variant="light" className="border small mt-3 mb-0">
            Mã câu hỏi sinh theo dạng <strong>mã chuyên đề-mức độ-số thứ tự</strong>, ví dụ{" "}
            <code>{questionCodeExample(category.code, 1, 1)}</code>, <code>{questionCodeExample(category.code, 2, 2)}</code>… Số thứ
            tự 6 số đếm tiếp trong chuyên đề, không trùng; câu chưa đặt mức độ ghi <code>00</code>.
          </Alert>
        )}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>
          Hủy
        </Button>
        <Button
          disabled={categoryRequired && !categoryId}
          onClick={() => onConfirm({ categoryId: categoryId || null, createExam: mode === "EXAM" })}
        >
          <i className="bi bi-cloud-upload me-1" aria-hidden="true" />
          {mode === "EXAM" ? "Nhập và tạo đề thi" : "Nhập vào ngân hàng câu hỏi"}
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
