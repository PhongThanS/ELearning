import { useState } from "react";
import { Alert, Button, Form, Modal, Table } from "react-bootstrap";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { videosApi } from "../../services/api";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import type { VideoImportResult } from "../../types/api";

const MAX_FILE_BYTES = 5 * 1024 * 1024;

/**
 * Thêm video bài giảng từ Excel (D-44): tải file mẫu → điền → kiểm tra (dry run) → import.
 * Tất cả hoặc không: chỉ khi mọi dòng hợp lệ mới bật nút import.
 */
export function VideoImportDialog({ show, onHide }: { show: boolean; onHide: () => void }) {
  const toast = useToast();
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [checked, setChecked] = useState<VideoImportResult | null>(null);

  const template = useMutation({ mutationFn: videosApi.importTemplate, onError: (e) => toast.error(e) });
  const check = useMutation({ mutationFn: (f: File) => videosApi.import(f, true), onSuccess: setChecked });
  const run = useMutation({
    mutationFn: (f: File) => videosApi.import(f, false),
    onSuccess: (result) => {
      if (!result.imported) {
        setChecked(result);
        return;
      }
      void queryClient.invalidateQueries({ queryKey: ["admin-videos"] });
      toast.success(`Đã thêm ${result.importedCount} video.`);
      close();
    },
  });

  const reset = () => {
    setChecked(null);
    check.reset();
    run.reset();
  };
  function close() {
    setFile(null);
    setFileError(null);
    reset();
    onHide();
  }

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
        <Modal.Title as="h2" className="h5">
          <i className="bi bi-file-earmark-excel me-2" aria-hidden="true" />
          Import video từ Excel
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <div className="border rounded p-3 mb-3 bg-light">
          <div className="fw-semibold mb-2">Bước 1. Tải file Excel mẫu và điền thông tin</div>
          <Button variant="success" disabled={template.isPending} onClick={() => template.mutate()}>
            <i className="bi bi-file-earmark-arrow-down me-1" aria-hidden="true" />
            {template.isPending ? "Đang tạo file mẫu…" : "Tải file Excel mẫu"}
          </Button>
          <ul className="small mt-2 mb-0 ps-3">
            <li>
              <strong>Chỉ cần dán link YouTube</strong> vào cột <strong>Link video</strong> của sheet <code>Video</code>, mỗi dòng một
              video: hệ thống tự lấy <strong>tiêu đề</strong> từ YouTube khi import.
            </li>
            <li>
              Các cột khác không bắt buộc: Tiêu đề (ghi nếu muốn đặt tên khác; link Drive / file video thì phải ghi), Link tài liệu PDF,
              Link bài tập về nhà, Mã chuyên đề, Lớp, Mã đề luyện.
            </li>
            <li>
              Tài liệu PDF, bài tập về nhà: link Google Drive / Docs. <strong>Lớp</strong>, <strong>chuyên đề</strong>,{" "}
              <strong>đề luyện</strong> ghi bằng mã (xem sheet <code>Lop</code>, <code>ChuyenDe</code>, <code>DeLuyen</code>); nhiều lớp
              cách nhau dấu phẩy, để trống lớp = bài chung.
            </li>
          </ul>
        </div>

        <div className="fw-semibold mb-2">Bước 2. Chọn file đã điền, bấm Kiểm tra rồi Import</div>
        <p className="small text-secondary mb-2">
          Kiểm tra chưa thêm video nào, chỉ báo lỗi theo từng dòng. Có dòng lỗi thì không video nào được thêm.
        </p>
        <Form.Group controlId="video-import-file" className="mb-3">
          <Form.Label>File Excel đã điền (.xlsx, tối đa 5 MB, 200 video)</Form.Label>
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
                ? `Cả ${checked.totalRows} video hợp lệ, sẵn sàng import.`
                : `${checked.validRows}/${checked.totalRows} dòng hợp lệ. Sửa ${invalidRows.length} dòng lỗi dưới đây rồi kiểm tra lại.`}
            </Alert>
            <div className="table-responsive" style={{ maxHeight: 340 }}>
              <Table size="sm" bordered className="small align-middle mb-0">
                <thead className="sticky-top bg-white">
                  <tr>
                    <th>Dòng</th>
                    <th>Video</th>
                    <th>{allValid ? "Lớp / chuyên đề / đề luyện" : "Lỗi"}</th>
                  </tr>
                </thead>
                <tbody>
                  {(allValid ? checked.rows : invalidRows).map((r) => (
                    <tr key={r.rowNumber}>
                      <td>{r.rowNumber}</td>
                      <td>
                        <div className="fw-semibold">{r.title || "—"}</div>
                        <div className="text-secondary text-break">{r.videoUrl}</div>
                      </td>
                      <td>
                        {r.issues.length > 0 ? (
                          <ul className="mb-0 ps-3">
                            {r.issues.map((issue, i) => (
                              <li key={i}>
                                {issue.field && <strong>{issue.field}: </strong>}
                                {issue.message}
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <>
                            <div>{r.classes.join(", ") || "Bài chung"}</div>
                            {r.categoryName && <div className="text-secondary">Chuyên đề: {r.categoryName}</div>}
                            {r.practiceExamName && <div className="text-secondary">Đề luyện: {r.practiceExamName}</div>}
                          </>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          </>
        )}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={close}>
          Hủy
        </Button>
        <Button variant="outline-primary" disabled={!file || check.isPending || run.isPending} onClick={() => check.mutate(file!)}>
          {check.isPending ? "Đang kiểm tra…" : "Kiểm tra"}
        </Button>
        <Button disabled={!file || !allValid || run.isPending} onClick={() => run.mutate(file!)}>
          {run.isPending ? "Đang thêm video…" : `Import ${allValid ? `${checked.totalRows} video` : ""}`}
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
