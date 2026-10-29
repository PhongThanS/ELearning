import { useQuery } from "@tanstack/react-query";
import { categoriesApi } from "../services/api";

/** Danh mục đang hoạt động cho các ô lọc / chọn (Lấy tất cả từ DB). */
export function useActiveCategories() {
  return useQuery({
    queryKey: ["categories", "active-all"],
    queryFn: () => categoriesApi.list({ pageSize: 10000, isActive: true, sortBy: "name" }),
    staleTime: 5 * 60 * 1000, // Tối ưu truy vấn: Lưu cache 5 phút, không gọi API trùng lặp khi mở các dropdown
    gcTime: 10 * 60 * 1000,
  });
}
