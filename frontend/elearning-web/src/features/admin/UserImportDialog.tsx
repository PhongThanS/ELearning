import { useState } from "react";
import { Alert, Badge, Button, Form, Modal, Table } from "react-bootstrap";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { usersApi } from "../../services/api";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import type { UserImportResult, UserImportRow } from "../../types/api";

const MAX_FILE_BYTES = 5 * 1024 * 1024;

/** CSV (UTF-8 có BOM để Excel mở đúng tiếng Việt) danh sách tài khoản vừa tạo, kèm mật khẩu tạm. */
export function accountsCsv(rows: UserImportRow[]): string {
  const cell = (value: string) => `"${value.replaceAll('"', '""')}"`;
  const lines = [
    ["Tên đăng nhập", "Họ tên", "Vai trò", "Lớp", "Mật khẩu tạm"],
    ...rows.map((r) => [r.userName ?? "", r.fullName ?? "", r.roles.join(", "), r.classes.join(", "), r.temporaryPassword ?? "(đã đặt trong file)"]),
  ];
  return "﻿" + lines.map((line) => line.map(cell).join(",")).join("\r\n");
}

function downloadCsv(content: string, fileName: string) {
  const url = URL.createObjectURL(new Blob([content], { type: "text/csv;charset=utf-8" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

/**
 * Import tài khoản từ Excel (docs/02-nghiep-vu.md mục 5.2): dòng 1 là tiêu đề, mỗi dòng dưới là một tài khoản.
 * Chọn file → kiểm tra (dry run) → import. Tất cả hoặc không: chỉ khi mọi dòng hợp lệ mới bật nút import.
 * Sau khi import, mật khẩu tạm (dòng để trống mật khẩu) hiện đúng một lần, kèm nút tải CSV.
 */
export function UserImportDialog({ show, onHide }: { show: boolean; onHide: () => void }) {
  const { t } = useTranslation();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [checked, setChecked] = useState<UserImportResult | null>(null);
  const [done, setDone] = useState<UserImportResult | null>(null);

  const template = useMutation({ mutationFn: usersApi.importTemplate, onError: (e) => toast.error(e) });
  const check = useMutation({
    mutationFn: (f: File) => usersApi.import(f, true),
    onSuccess: setChecked,
  });
  const run = useMutation({
    mutationFn: (f: File) => usersApi.import(f, false),
    onSuccess: (result) => {
      if (!result.imported) {
        // Dữ liệu thay đổi giữa lúc kiểm tra và lúc import (ví dụ tên đăng nhập vừa bị dùng): hiển thị lỗi mới
        setChecked(result);
        return;
      }
      void queryClient.invalidateQueries({ queryKey: ["users"] });
      void queryClient.invalidateQueries({ queryKey: ["classes"] });
      toast.success(`Đã tạo ${result.importedCount} tài khoản.`);
      setDone(result);
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
    setDone(null);
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
  const withTemporary = done?.rows.filter((r) => r.temporaryPassword) ?? [];

  if (done) {
    return (
      <Modal show={show} onHide={close} size="lg" centered backdrop="static">
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">Đã tạo {done.importedCount} tài khoản</Modal.Title>
        </Modal.Header>
        <Modal.Body>
          {withTemporary.length > 0 ? (
            <Alert variant="warning" className="py-2">
              <strong>{withTemporary.length} tài khoản dùng mật khẩu tạm</strong> (dòng để trống mật khẩu). Mật khẩu tạm chỉ hiện{" "}
              <strong>lần này</strong>: hãy tải danh sách trước khi đóng. Người dùng đăng nhập bằng mật khẩu này rồi tự đổi khi muốn.
            </Alert>
          ) : (
            <Alert variant="success" className="py-2">Mọi tài khoản dùng mật khẩu đã ghi trong file.</Alert>
          )}
          <div className="table-responsive" style={{ maxHeight: 360 }}>
            <Table size="sm" bordered className="small align-middle mb-0">
              <thead className="sticky-top bg-white">
                <tr>
                  <th>Tên đăng nhập</th>
                  <th>Họ tên</th>
                  <th>Vai trò</th>
                  <th>Lớp</th>
                  <th>Mật khẩu tạm</th>
                </tr>
              </thead>
              <tbody>
                {done.rows.map((r) => (
                  <tr key={r.rowNumber}>
                    <td className="fw-semibold">{r.userName}</td>
                    <td>{r.fullName}</td>
                    <td>{r.roles.map((role) => <Badge key={role} bg="info" className="me-1">{role}</Badge>)}</td>
                    <td>{r.classes.join(", ") || "—"}</td>
                    <td>{r.temporaryPassword ? <code>{r.temporaryPassword}</code> : <span className="text-secondary">—</span>}</td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </div>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="outline-primary" onClick={() => downloadCsv(accountsCsv(done.rows), "tai-khoan-da-tao.csv")}>
            <i className="bi bi-download me-1" aria-hidden="true" />
            Tải danh sách (CSV)
          </Button>
          <Button onClick={close}>Xong</Button>
        </Modal.Footer>
      </Modal>
    );
  }

  return (
    <Modal show={show} onHide={close} size="lg" centered>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">Import tài khoản từ Excel</Modal.Title>
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
              Dòng 1 là tiêu đề, mỗi dòng bên dưới là một tài khoản:{" "}
              <strong>Tên đăng nhập, Mật khẩu, Họ tên, Email, Vai trò, Lớp</strong>. Điền vào sheet <code>TaiKhoan</code> (ví dụ cách
              điền ở sheet <code>HuongDan</code>).
            </li>
            <li>
              <strong>Vai trò</strong> ghi số: <strong>0</strong> = Quản trị viên, <strong>1</strong> = Giáo viên, <strong>2</strong> = Học viên
              (để trống = Học viên). <strong>Lớp</strong> ghi mã lớp (xem sheet <code>Lop</code>), chỉ xếp lớp cho Học viên.
            </li>
            <li>Bắt buộc: Tên đăng nhập, Họ tên. Mật khẩu trống → hệ thống sinh mật khẩu tạm. Email trống → tự tạo.</li>
            <li>Không bắt đổi mật khẩu: người dùng đăng nhập rồi tự đổi mật khẩu khi muốn.</li>
          </ul>
        </div>

        <div className="fw-semibold mb-2">Bước 2. Chọn file đã điền, bấm Kiểm tra rồi Import</div>
        <p className="small text-secondary mb-2">
          Kiểm tra chưa tạo tài khoản nào, chỉ báo lỗi theo từng dòng. Import chỉ bật khi mọi dòng hợp lệ; có dòng lỗi thì không tài khoản nào được tạo.
        </p>
        <Form.Group controlId="user-import-file" className="mb-3">
          <Form.Label>File Excel đã điền (.xlsx, tối đa 5 MB, 500 tài khoản)</Form.Label>
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
                      <th>Tài khoản</th>
                      <th>Lỗi</th>
                    </tr>
                  </thead>
                  <tbody>
                    {invalidRows.map((r) => (
                      <tr key={r.rowNumber}>
                        <td>{r.rowNumber}</td>
                        <td>
                          <div className="fw-semibold">{r.userName || "—"}</div>
                          <div className="text-secondary">{r.fullName}</div>
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
          {run.isPending ? "Đang tạo tài khoản…" : `Import ${allValid ? `${checked.totalRows} tài khoản` : ""}`}
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
