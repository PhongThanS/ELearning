import { useCallback, useState } from "react";

export interface ListState {
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDir?: "asc" | "desc";
  keyword?: string;
  [filter: string]: string | number | boolean | undefined;
}

/** Trạng thái danh sách (trang, sắp xếp, lọc) cho các bảng phân trang phía server. */
export function useListQuery(initial: Partial<ListState> = {}) {
  const [state, setState] = useState<ListState>({ page: 1, pageSize: 20, ...initial });

  const setPage = useCallback((page: number) => setState((s) => ({ ...s, page })), []);
  const setFilter = useCallback(
    (patch: Partial<ListState>) => setState((s) => ({ ...s, ...patch, page: 1 })),
    [],
  );
  const toggleSort = useCallback(
    (sortBy: string) =>
      setState((s) => ({
        ...s,
        sortBy,
        sortDir: s.sortBy === sortBy && s.sortDir === "asc" ? "desc" : "asc",
        page: 1,
      })),
    [],
  );

  // Bỏ các lọc rỗng trước khi gửi lên API
  const params = Object.fromEntries(Object.entries(state).filter(([, v]) => v !== undefined && v !== "")) as ListState;
  return { state, params, setPage, setFilter, toggleSort };
}
