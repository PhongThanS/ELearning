import { useQuery } from "@tanstack/react-query";
import { categoriesApi } from "../services/api";

/** Danh mục đang hoạt động (tối đa 100) cho các ô lọc / chọn. */
export function useActiveCategories() {
  return useQuery({
    queryKey: ["categories", "active-all"],
    queryFn: () => categoriesApi.list({ pageSize: 100, isActive: true, sortBy: "name" }),
  });
}
