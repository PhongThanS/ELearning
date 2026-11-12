import { Fragment, type ReactNode } from "react";
import { Dropdown } from "react-bootstrap";
import { Link } from "react-router";
import "./tableCells.css";

/*
 * Ô dùng chung cho mọi bảng danh sách, để các trang trông cùng một kiểu:
 * - NameCell: tên (bấm để mở) + dòng phụ màu xám (mã, thông tin thêm).
 * - StatusSwitch: công tắc Bật/Tắt xanh lá.
 * - RowActions: nút ⋯ mở menu thao tác; mục nguy hiểm (Xóa) màu đỏ, nằm cuối sau vạch ngăn.
 */

/** Tên đậm bấm được + dòng phụ (mã chữ đơn cách, thông tin thêm) màu xám bên dưới. */
export function NameCell({
  to,
  name,
  code,
  meta,
  title,
  plain,
  separator = true,
}: {
  to?: string;
  name: ReactNode;
  code?: ReactNode;
  meta?: ReactNode;
  title?: string;
  /** Dòng chính chữ thường (vd. nội dung câu hỏi), không in đậm. */
  plain?: boolean;
  /** Tự chèn dấu " · " giữa mã và thông tin thêm. */
  separator?: boolean;
}) {
  return (
    <div className={`name-cell${plain ? " name-cell--plain" : ""}`}>
      <div className="name-cell__name" title={title}>
        {to ? <Link to={to}>{name}</Link> : name}
      </div>
      {(code || meta) && (
        <div className="name-cell__meta">
          {code && (to ? <Link to={to} tabIndex={-1} className="name-cell__code">{code}</Link> : <span className="name-cell__code">{code}</span>)}
          {separator && code && meta && <span aria-hidden="true"> · </span>}
          {meta}
        </div>
      )}
    </div>
  );
}

/**
 * Công tắc Bật/Tắt. Là <button role="switch"> chứ không phải checkbox: CSS bảng chung tô nền cả hàng
 * khi trong hàng có checkbox đang tích.
 */
export function StatusSwitch({
  active,
  onToggle,
  disabled,
  label,
}: {
  active: boolean;
  onToggle?: () => void;
  disabled?: boolean;
  /** Tên đối tượng, để trình đọc màn hình đọc "Bật/tắt <tên>". */
  label: string;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={active}
      aria-label={`Bật/tắt ${label}`}
      className={`status-switch${active ? " is-on" : ""}`}
      disabled={disabled || !onToggle}
      onClick={onToggle}
    >
      <span className="status-switch__track" aria-hidden="true">
        <span className="status-switch__thumb" />
      </span>
      <span className="status-switch__label">{active ? "Bật" : "Tắt"}</span>
    </button>
  );
}

export type RowAction = {
  label: string;
  onClick?: () => void;
  to?: string;
  icon?: string;
  /** Thao tác nguy hiểm (Xóa): chữ đỏ, tự xếp cuối sau vạch ngăn. */
  danger?: boolean;
  disabled?: boolean;
  /** Lý do khi bị khóa, hiện khi rê chuột. */
  title?: string;
  /** false = không hiện (thường theo quyền). */
  show?: boolean;
};

/** Nút ⋯ mở menu thao tác của một hàng. Không có mục nào được hiện thì không vẽ gì. */
export function RowActions({ label, actions }: { label: string; actions: RowAction[] }) {
  const visible = actions.filter((a) => a.show !== false);
  if (visible.length === 0) return null;
  const normal = visible.filter((a) => !a.danger);
  const danger = visible.filter((a) => a.danger);
  const item = (a: RowAction) => {
    const content = (
      <>
        {a.icon && <i className={`bi ${a.icon} me-2`} aria-hidden="true" />}
        {a.label}
      </>
    );
    const common = { disabled: a.disabled, title: a.title, className: a.danger ? "text-danger" : undefined };
    return a.to ? (
      <Dropdown.Item key={a.label} as={Link} to={a.to} {...common}>
        {content}
      </Dropdown.Item>
    ) : (
      <Dropdown.Item key={a.label} as="button" onClick={a.onClick} {...common}>
        {content}
      </Dropdown.Item>
    );
  };
  return (
    <Dropdown align="end" className="d-inline-block">
      <Dropdown.Toggle as="button" type="button" className="row-actions-btn" aria-label={`Thao tác với ${label}`}>
        <i className="bi bi-three-dots" aria-hidden="true" />
      </Dropdown.Toggle>
      <Dropdown.Menu popperConfig={{ strategy: "fixed" }}>
        {normal.map(item)}
        {normal.length > 0 && danger.length > 0 && <Dropdown.Divider />}
        {danger.map((a) => (
          <Fragment key={a.label}>{item(a)}</Fragment>
        ))}
      </Dropdown.Menu>
    </Dropdown>
  );
}
