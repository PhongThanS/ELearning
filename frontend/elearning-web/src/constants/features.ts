/**
 * Bật / tắt tính năng trên giao diện. Tắt chỉ ẩn phần giao diện; API và dữ liệu cũ giữ nguyên.
 * Muốn hiện lại thì đổi giá trị thành true.
 */
export const Features = {
  /** Ô lọc / tìm câu hỏi theo tag (danh sách câu hỏi, quy tắc bốc ngẫu nhiên, ma trận đề). */
  tagFilter: false,
  /** Nhóm người dùng: mục menu, trang Nhóm, cột nhóm ở chi tiết người dùng, ô thêm nhóm khi gán đề. */
  userGroups: false,
} as const;
