import { useQuery } from "@tanstack/react-query";
import { categoriesApi } from "../services/api";

/** Danh mục đang hoạt động cho các ô lọc / chọn (Lấy tất cả từ DB). */
export function useActiveCategories() {
  return useQuery({
    queryKey: ["categories", "active-all"],
    queryFn: () => categoriesApi.list({ pageSize: 10000, isActive: true, sortBy: "name" }),
  });
}
