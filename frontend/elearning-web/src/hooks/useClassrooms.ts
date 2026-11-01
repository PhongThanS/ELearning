import { useQuery } from "@tanstack/react-query";
import { classesApi } from "../services/api";
import { normalizeVietnamese } from "../components/common/SearchableSelect";

/** Toàn bộ lớp học (mục Lớp học, bảng Classrooms) cho menu và các ô chọn lớp. */
export function useAllClassrooms(enabled = true) {
  return useQuery({
    queryKey: ["classes", "all"],
    queryFn: () => classesApi.listAll(),
    staleTime: 60_000,
    enabled,
  });
}

/** Lọc lớp theo mã hoặc tên (không phân biệt hoa thường, bỏ dấu tiếng Việt). */
export function matchClassroom(c: { code: string; name: string }, keyword: string): boolean {
  const k = normalizeVietnamese(keyword.trim());
  return !k || normalizeVietnamese(c.code).includes(k) || normalizeVietnamese(c.name).includes(k);
}
