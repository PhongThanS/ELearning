# Báo Cáo Cập Nhật: Đồng Bộ Dữ Liệu Chuyên Đề & Lớp Học Cho Video Bài Giảng

Tài liệu này ghi lại chi tiết các thay đổi nhằm bảo đảm **tất cả Chuyên đề và Lớp học trong tính năng Video đều lấy trực tiếp và đồng bộ từ mục Chuyên đề (`QuestionCategories`) và mục Lớp học (`Classrooms`)**.

---

## 1. Mục Đích & Yêu Cầu Nghiệp Vụ

- **Chuyên đề liên quan:** Phải lấy từ dữ liệu danh mục chuyên đề được quản lý tại mục **Chuyên đề** (`QuestionCategories` - đường dẫn `/admin/categories`).
- **Lớp học được xem:** Phải lấy từ dữ liệu danh sách lớp học được quản lý tại mục **Lớp học** (`Classrooms` - đường dẫn `/admin/classes`).
- **Đồng bộ thời gian thực:** Khi quản trị viên thêm mới, sửa tên hoặc cập nhật trạng thái của chuyên đề hoặc lớp học, danh sách lựa chọn trong Kho Video bài giảng phải tự động cập nhật mà không cần tải lại trang.
- **Hỗ trợ giao diện Học sinh:** Học sinh được phép tải danh mục các chuyên đề đang hoạt động (`GET /api/student/categories`) để lọc video bài giảng và bài tập theo đúng chuyên đề của hệ thống.

---

## 2. Các Thay Đổi Kỹ Thuật

### Backend (.NET Core):
1. **`StudentController.cs` (`/api/student`)**:
   - Tiêm phụ thuộc `ICategoryService categories`.
   - Bổ sung endpoint `GET /api/student/categories`:
     - Trả về danh sách chuyên đề đang kích hoạt (`IsActive = true`) cho học sinh.
     - Giúp học sinh lọc video theo đúng các chuyên đề thực tế của trường/trung tâm mà không bị lỗi phân quyền 403.

### Frontend (React + TypeScript):
1. **`services/api.ts`**:
   - Bổ sung `studentApi.categories()` gọi tới endpoint `/api/student/categories`.
2. **`AdminVideoPages.tsx`**:
   - Chuẩn hóa `queryKey: ["categories", "for-videos"]` và `queryKey: ["classes", "for-videos"]`:
     - Giúp React Query tự động invalidate và cập nhật tức thì mỗi khi mục Chuyên đề hoặc Lớp học có thay đổi dữ liệu.
   - Nâng cấp hiển thị lựa chọn trong form Thêm/Sửa video và thanh lọc:
     - Chuyên đề: Hiển thị cả Tên và Mã danh mục: `📁 {c.name} ({c.code})`.
     - Lớp học: Hiển thị Tên lớp, Mã lớp và Niên khóa: `🏫 {c.name} [{c.code}] ({c.schoolYear})`.
     - Nhãn form ghi rõ nguồn dữ liệu: *"Chuyên đề liên quan (từ mục Chuyên đề)"* và *"Lớp học được xem (từ mục Lớp học)"*.
3. **`StudentVideoPages.tsx`**:
   - Chuyển sang sử dụng `studentApi.categories()` với queryKey `["student-categories"]`.
   - Dropdown lớp học lấy chính xác các lớp mà học sinh đang theo học (`studentApi.classes()`).

---

## 3. Danh Sách File Đã Chỉnh Sửa

| STT | File | Thay đổi |
|---|---|---|
| 1 | `backend/src/ELearning.Api/Controllers/StudentController.cs` | Thêm endpoint `GET /api/student/categories` cho học sinh |
| 2 | `frontend/elearning-web/src/services/api.ts` | Thêm phương thức `studentApi.categories` |
| 3 | `frontend/elearning-web/src/features/videos/AdminVideoPages.tsx` | Đồng bộ queryKey và hiển thị chi tiết mã/tên chuyên đề, lớp học |
| 4 | `frontend/elearning-web/src/features/videos/StudentVideoPages.tsx` | Kết nối dropdown chuyên đề tới API học sinh |
| 5 | `docs/dong-bo-du-lieu-chuyen-de-va-lop-hoc-cho-video.md` | Tài liệu cập nhật |

---

## 4. Kiểm Thử
- **Backend Tests:** 189/189 Unit Tests Passed.
- **Frontend Build:** `npm run build` thành công, 0 lỗi TypeScript.
