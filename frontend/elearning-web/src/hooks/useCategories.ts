import { useQuery } from "@tanstack/react-query";
import { categoriesApi } from "../services/api";
import { categoryLabel, orderCategories } from "../utils/categoryTree";

/**
 * Danh mục đang hoạt động cho các ô lọc / chọn (Lấy tất cả từ DB).
 * Xếp theo cây (chuyên đề gốc rồi tới chuyên đề con) và tên chuyên đề con ghi kèm cha: "Cha › Con".
 */
export function useActiveCategories() {
  return useQuery({
    queryKey: ["categories", "active-all"],
    queryFn: () => categoriesApi.list({ pageSize: 10000, isActive: true, sortBy: "name" }),
    select: (data) => ({ ...data, items: orderCategories(data.items).map((c) => ({ ...c, name: categoryLabel(c) })) }),
    staleTime: 5 * 60 * 1000, // Tối ưu truy vấn: Lưu cache 5 phút, không gọi API trùng lặp khi mở các dropdown
    gcTime: 10 * 60 * 1000,
  });
}
