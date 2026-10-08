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
        <Table hover striped size="sm" className="align-middle">
          <thead>
            <tr>
              {columns.map((c) => (
                <th key={c.key} className={c.className} scope="col">
                  {c.sortKey && onSort ? (
                    <Button variant="link" size="sm" className="p-0 text-decoration-none fw-semibold" onClick={() => onSort(c.sortKey!)}>
                      {c.header}
                      {sort?.sortBy === c.sortKey ? (sort.sortDir === "desc" ? " ▼" : " ▲") : ""}
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
      <Pagination size="sm" className="mb-0">
        <Pagination.First disabled={data.page <= 1} onClick={() => onPage(1)} aria-label="Trang đầu" />
        <Pagination.Prev disabled={data.page <= 1} onClick={() => onPage(data.page - 1)} aria-label="Trang trước" />
        <Pagination.Item active>{data.page}</Pagination.Item>
        <Pagination.Next disabled={data.page >= data.totalPages} onClick={() => onPage(data.page + 1)} aria-label="Trang sau" />
        <Pagination.Last disabled={data.page >= data.totalPages} onClick={() => onPage(data.totalPages)} aria-label="Trang cuối" />
      </Pagination>
    </div>
  );
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
      <InputGroup size="sm">
        <Form.Control
          aria-label={t("common.keyword")}
          placeholder={placeholder ?? t("common.keyword")}
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
        <h1 className="h4 mb-1">{title}</h1>
        {children}
      </div>
      {actions && <div className="d-flex gap-2 flex-wrap">{actions}</div>}
    </div>
  );
}
