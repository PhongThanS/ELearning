import { useState, type ReactNode } from "react";
import { Badge, Button, Form, InputGroup, Pagination, Table } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import type { Paged } from "../../types/api";
import { Empty, ErrorAlert, Loading } from "./Feedback";

export interface Column<T> {
  key: string;
  header: ReactNode;
  render: (row: T) => ReactNode;
  className?: string;
  sortKey?: string;
}

/** Bảng dữ liệu phân trang phía server, sắp xếp theo whitelist của API (docs/06-frontend.md mục 5). */
export function DataTable<T>({
  data,
  columns,
  rowKey,
  isLoading,
  error,
  sort,
  onSort,
  onPage,
  onRetry,
}: {
  data: Paged<T> | undefined;
  columns: Column<T>[];
  rowKey: (row: T) => string;
  isLoading: boolean;
  error: unknown;
  sort?: { sortBy?: string; sortDir?: "asc" | "desc" };
  onSort?: (sortBy: string) => void;
  onPage: (page: number) => void;
  onRetry?: () => void;
}) {
  const { t } = useTranslation();
  if (error) {
    return <ErrorAlert error={error} onRetry={onRetry} />;
  }
  if (isLoading || !data) {
    return <Loading />;
  }
  if (data.items.length === 0) {
    return <Empty />;
  }

  return (
    <>
      <div className="table-card table-responsive">
        <Table hover size="sm" className="align-middle">
          <thead>
            <tr>
              {columns.map((c) => (
                <th key={c.key} className={c.className} scope="col">
                  {c.sortKey && onSort ? (
                    <Button
                      variant="link"
                      size="sm"
                      className={`p-0 text-decoration-none fw-semibold table-sort${sort?.sortBy === c.sortKey ? " is-active" : ""}`}
                      onClick={() => onSort(c.sortKey!)}
                    >
                      {c.header}
                      <i
                        className={`bi ms-1 ${sort?.sortBy === c.sortKey ? (sort.sortDir === "desc" ? "bi-sort-down" : "bi-sort-up") : "bi-arrow-down-up"}`}
                        aria-hidden="true"
                      />
                      {sort?.sortBy === c.sortKey && (
                        <span className="visually-hidden">{sort.sortDir === "desc" ? " (giảm dần)" : " (tăng dần)"}</span>
                      )}
                    </Button>
                  ) : (
                    c.header
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.items.map((row) => (
              <tr key={rowKey(row)}>
                {columns.map((c) => (
                  <td key={c.key} className={c.className}>
                    {c.render(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </Table>
      </div>
      <Pager data={data} onPage={onPage} label={t("common.totalCount", { count: data.totalCount })} />
    </>
  );
}

export function Pager<T>({ data, onPage, label }: { data: Paged<T>; onPage: (page: number) => void; label?: string }) {
  const { t } = useTranslation();
  if (data.totalPages <= 1) {
    return label ? <div className="small text-secondary">{label}</div> : null;
  }
  return (
    <div className="d-flex justify-content-between align-items-center flex-wrap gap-2">
      <span className="small text-secondary">
        {label} · {t("common.page", { page: data.page, total: data.totalPages })}
      </span>
      <Pagination size="sm" className="mb-0 sclass-pagination">
        <Pagination.First disabled={data.page <= 1} onClick={() => onPage(1)} aria-label="Trang đầu" />
        <Pagination.Prev disabled={data.page <= 1} onClick={() => onPage(data.page - 1)} aria-label="Trang trước" />
        {pageWindow(data.page, data.totalPages).map((p) => (
          <Pagination.Item key={p} active={p === data.page} onClick={() => p !== data.page && onPage(p)}>
            {p}
          </Pagination.Item>
        ))}
        <Pagination.Next disabled={data.page >= data.totalPages} onClick={() => onPage(data.page + 1)} aria-label="Trang sau" />
        <Pagination.Last disabled={data.page >= data.totalPages} onClick={() => onPage(data.totalPages)} aria-label="Trang cuối" />
      </Pagination>
    </div>
  );
}

/** Các số trang hiển thị quanh trang hiện tại (tối đa 5 số). */
export function pageWindow(page: number, totalPages: number): number[] {
  const start = Math.max(1, Math.min(page - 2, totalPages - 4));
  const end = Math.min(totalPages, start + 4);
  return Array.from({ length: end - start + 1 }, (_, i) => start + i);
}

export function SearchBox({ value, onSearch, placeholder }: { value: string; onSearch: (value: string) => void; placeholder?: string }) {
  const { t } = useTranslation();
  const [text, setText] = useState(value);
  return (
    <Form
      onSubmit={(e) => {
        e.preventDefault();
        onSearch(text.trim());
      }}
      role="search"
    >
      <InputGroup size="sm" className="sclass-search">
        <InputGroup.Text aria-hidden="true">
          <i className="bi bi-search" />
        </InputGroup.Text>
        <Form.Control
          aria-label={placeholder ?? t("common.searchCodeOrName")}
          placeholder={placeholder ?? t("common.searchCodeOrName")}
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
        <Button type="submit" variant="outline-secondary">
          {t("common.search")}
        </Button>
      </InputGroup>
    </Form>
  );
}

export function ActiveBadge({ active }: { active: boolean }) {
  const { t } = useTranslation();
  return <Badge bg={active ? "success" : "secondary"}>{active ? t("common.active") : t("common.inactive")}</Badge>;
}

export function PageHeader({ title, actions, children }: { title: ReactNode; actions?: ReactNode; children?: ReactNode }) {
  return (
    <div className="d-flex justify-content-between align-items-start flex-wrap gap-2 mb-3">
      <div>
        <h1 className="h4 mb-1 page-title">{title}</h1>
        {children}
      </div>
      {actions && <div className="d-flex gap-2 flex-wrap">{actions}</div>}
    </div>
  );
}
