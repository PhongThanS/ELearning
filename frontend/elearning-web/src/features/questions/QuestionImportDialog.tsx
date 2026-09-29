import { useState } from "react";
import { Alert, Badge, Button, Form, Modal, Table } from "react-bootstrap";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { questionsApi } from "../../services/api";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import type { QuestionImportResult } from "../../types/api";

const MAX_FILE_BYTES = 5 * 1024 * 1024;

/**
 * Import câu hỏi từ Excel (docs/02-nghiep-vu.md mục 1.4): chọn file → kiểm tra (dry run) → import.
 * Tất cả hoặc không: chỉ khi mọi dòng hợp lệ mới bật nút import.
 */
export function QuestionImportDialog({ show, onHide }: { show: boolean; onHide: () => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [checked, setChecked] = useState<QuestionImportResult | null>(null);

  const template = useMutation({ mutationFn: questionsApi.importTemplate, onError: (e) => toast.error(e) });
  const check = useMutation({
    mutationFn: (f: File) => questionsApi.import(f, true),
    onSuccess: setChecked,
  });
  const run = useMutation({
    mutationFn: (f: File) => questionsApi.import(f, false),
    onSuccess: (result) => {
      if (!result.imported) {
        // Dữ liệu thay đổi giữa lúc kiểm tra và lúc import (ví dụ mã vừa bị dùng): hiển thị lỗi mới
        setChecked(result);
        return;
      }
      void queryClient.invalidateQueries({ queryKey: ["questions"] });
      toast.success(`Đã import ${result.importedCount} câu hỏi.`);
      close();
    },
  });

  const reset = () => {
    setChecked(null);
    check.reset();
    run.reset();
  };
  const close = () => {
    setFile(null);
    setFileError(null);
    reset();
    onHide();
  };

  const chooseFile = (f: File | null) => {
    reset();
    setFileError(null);
    if (f && (!f.name.toLowerCase().endsWith(".xlsx") || f.size > MAX_FILE_BYTES)) {
      setFileError("Chỉ nhận file .xlsx, tối đa 5 MB.");
      setFile(null);
      return;
    }
    setFile(f);
  };

  const invalidRows = checked?.rows.filter((r) => r.issues.length > 0) ?? [];
  const allValid = !!checked && checked.validRows === checked.totalRows;
  const error = check.error ?? run.error;

  return (
    <Modal show={show} onHide={close} size="lg" centered>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">Import câu hỏi từ Excel</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <ol className="small ps-3">
          <li>
            Tải{" "}
            <Button variant="link" size="sm" className="p-0 align-baseline" disabled={template.isPending} onClick={() => template.mutate()}>
              file mẫu
            </Button>{" "}
            (có sheet hướng dẫn và danh sách mã danh mục), điền câu hỏi vào sheet <code>CauHoi</code>.
          </li>
          <li>Chọn file và bấm <strong>Kiểm tra</strong>. Hệ thống báo lỗi theo từng dòng, chưa tạo câu nào.</li>
          <li>Khi mọi dòng hợp lệ, bấm <strong>Import</strong>. Có dòng lỗi thì không câu nào được tạo.</li>
        </ol>

        <Form.Group controlId="import-file" className="mb-3">
          <Form.Label>File Excel (.xlsx, tối đa 5 MB, 1.000 câu)</Form.Label>
          <Form.Control
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            isInvalid={!!fileError}
            onChange={(e) => chooseFile((e.target as HTMLInputElement).files?.[0] ?? null)}
          />
          <Form.Control.Feedback type="invalid">{fileError}</Form.Control.Feedback>
        </Form.Group>

        {error && <Alert variant="danger">{describeError(error)}</Alert>}

        {checked && (
          <>
            <Alert variant={allValid ? "success" : "warning"} className="py-2">
              {allValid
                ? `Cả ${checked.totalRows} dòng hợp lệ, sẵn sàng import.`
                : `${checked.validRows}/${checked.totalRows} dòng hợp lệ. Sửa ${invalidRows.length} dòng lỗi dưới đây rồi kiểm tra lại.`}
            </Alert>
            {invalidRows.length > 0 && (
              <div className="table-responsive" style={{ maxHeight: 320 }}>
                <Table size="sm" bordered className="small align-middle mb-0">
                  <thead className="sticky-top bg-white">
                    <tr>
                      <th>Dòng</th>
                      <th>Câu hỏi</th>
                      <th>Lỗi</th>
                    </tr>
                  </thead>
                  <tbody>
                    {invalidRows.map((r) => (
                      <tr key={r.rowNumber}>
                        <td>{r.rowNumber}</td>
                        <td>
                          {r.questionType && <Badge bg="light" text="dark" className="me-1">{t(`enums.questionType.${r.questionType}`)}</Badge>}
                          <span className="text-secondary">{r.contentPreview || "—"}</span>
                        </td>
                        <td>
                          <ul className="mb-0 ps-3">
                            {r.issues.map((issue, i) => (
                              <li key={i}>
                                {issue.field && <strong>{issue.field}: </strong>}
                                {issue.message}
                              </li>
                            ))}
                          </ul>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </div>
            )}
          </>
        )}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={close}>{t("common.cancel")}</Button>
        <Button variant="outline-primary" disabled={!file || check.isPending || run.isPending} onClick={() => check.mutate(file!)}>
          Kiểm tra
        </Button>
        <Button disabled={!file || !allValid || run.isPending} onClick={() => run.mutate(file!)}>
          Import {allValid ? `${checked.totalRows} câu` : ""}
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
