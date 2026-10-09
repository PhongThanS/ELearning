# Tài Liệu Tính Năng: Kho Video Bài Giảng (Gắn Link YouTube)

Tài liệu ghi lại toàn bộ thay đổi, thiết kế kỹ thuật và hướng dẫn sử dụng tính năng **Kho Video Bài Giảng** vừa được bổ sung vào hệ thống ELearning.

---

## 1. Mục Tiêu & Nghiệp Vụ

### Vấn đề giải quyết:
- Giảng viên và Quản trị viên cần chia sẻ các video bài giảng, video hướng dẫn giải đề, bài học minh họa cho học sinh.
- Nếu lưu trữ trực tiếp file video dung lượng lớn trên máy chủ sẽ gây quá tải ổ đĩa và tốn kém băng thông mạng.
- **Giải pháp:** Chỉ lưu trữ **đường dẫn (link) YouTube** (hoặc URL video ngoài), tự động nhận diện và bóc tách `Video ID`, tự động tạo ảnh đại diện (Thumbnail) và nhúng trình phát video YouTube bảo mật (`youtube-nocookie.com`).

### Phân quyền & Đối tượng sử dụng:
1. **Quản trị viên / Giáo viên (`Video.Manage`, `Video.View`):**
   - Quản lý toàn bộ danh sách video: Thêm mới, chỉnh sửa thông tin, xóa, bật/tắt hiển thị (`isActive`).
   - Phân loại video theo **Chuyên đề (`Category`)** và/hoặc **Lớp học (`Classroom`)**.
   - Xem trước trực tiếp video YouTube ngay trong form khi dán đường dẫn URL.
   - Sắp xếp thứ tự ưu tiên hiển thị (`displayOrder`).
2. **Học sinh (`Video.View`):**
   - Vào mục **Video bài giảng** trên thanh điều hướng.
   - Tìm kiếm video theo tiêu đề, lọc theo chuyên đề hoặc lớp học mà mình đang tham gia.
   - Bấm xem video trực tiếp trong popup phát video mượt mà, không bị quảng cáo điều hướng ra ngoài.

---

## 2. Thiết Kế Cơ Sở Dữ Liệu (PostgreSQL)

### Bảng `VideoLessons`
Bảng được tạo qua Entity Framework Core Migration `20261009094351_AddVideoLessons`:

| Cột | Kiểu dữ liệu | Ràng buộc | Mô tả |
|---|---|---|---|
| `Id` | `uuid` | PK | Mã định danh duy nhất của video |
| `Title` | `varchar(200)` | NOT NULL | Tiêu đề video bài giảng |
| `VideoUrl` | `varchar(1000)` | NOT NULL | Đường dẫn URL đầy đủ (hỗ trợ `youtube.com/watch`, `youtu.be/`, `shorts/`...) |
| `YoutubeVideoId`| `varchar(50)` | NULL | Mã ID 11 ký tự của YouTube (được bóc tách tự động qua Regex) |
| `ThumbnailUrl` | `varchar(1000)` | NULL | Đường dẫn ảnh thumbnail (tự động lấy từ YouTube nếu không nhập) |
| `Description` | `text` | NULL | Nội dung mô tả tóm tắt bài giảng |
| `CategoryId` | `uuid` | FK -> `QuestionCategories` | Khóa ngoại danh mục chuyên đề (ON DELETE NO ACTION) |
| `ClassroomId` | `uuid` | FK -> `Classrooms` | Khóa ngoại lớp học áp dụng (ON DELETE NO ACTION) |
| `DurationMinutes`| `int` | NULL | Thời lượng video (phút) |
| `DisplayOrder` | `int` | NOT NULL, DEFAULT 0 | Thứ tự hiển thị |
| `IsActive` | `boolean` | NOT NULL, DEFAULT true | Trạng thái hiển thị (Bật / Tắt) |
| `CreatedAt` | `timestamptz` | NOT NULL | Thời gian tạo |
| `CreatedBy` | `uuid` | NULL | Người tạo |
| `UpdatedAt` | `timestamptz` | NULL | Thời gian cập nhật cuối |
| `UpdatedBy` | `uuid` | NULL | Người cập nhật cuối |
| `RowVersion` | `bytea` | RowVersion / Concurrency | Chống ghi đè đồng thời |

### Chỉ mục (Indexes):
- `IX_VideoLessons_CategoryId`
- `IX_VideoLessons_ClassroomId`
- `IX_VideoLessons_IsActive_DisplayOrder`

---

## 3. Kiến Trúc Backend (.NET Core)

### Domain Layer:
- **`ELearning.Domain.Videos.VideoLesson`**:
  - Entity cốt lõi với logic đóng gói và hàm tự động phân tích URL YouTube:
    ```csharp
    public static string? ExtractYoutubeVideoId(string? url);
    ```
    Hỗ trợ nhận diện các định dạng: `youtu.be/{id}`, `youtube.com/watch?v={id}`, `youtube.com/shorts/{id}`, `youtube.com/embed/{id}` hoặc chuỗi 11 ký tự ID.
  - Tự động gán `ThumbnailUrl = $"https://img.youtube.com/vi/{YoutubeVideoId}/hqdefault.jpg"` nếu người dùng không cung cấp link ảnh riêng.
- **`ELearning.Domain.Identity.Permissions`**:
  - Thêm 2 quyền mới: `VideoView = "Video.View"` và `VideoManage = "Video.Manage"`.

### Infrastructure Layer:
- **`VideoConfigurations.cs`**: Cấu hình Fluent API cho EF Core, thiết lập ràng buộc ngoại và index.
- **`ELearningDbContext.cs`** & **`IAppDbContext.cs`**: Đăng ký `DbSet<VideoLesson> VideoLessons`.
- **Migration**: `20261009094351_AddVideoLessons` đã được áp dụng thành công vào CSDL.

### Application Layer:
- **`VideoContracts.cs`**:
  - `VideoLessonDto`: Chứa thông tin video, tên chuyên đề (`CategoryName`), tên lớp (`ClassroomName`).
  - `CreateVideoRequest` & `CreateVideoRequestValidator`: Kiểm tra bắt buộc URL hợp lệ, tiêu đề <= 200 ký tự.
  - `UpdateVideoRequest` & `UpdateVideoRequestValidator`: Hỗ trợ cập nhật đầy đủ và kiểm tra hợp lệ.
  - `VideoListQuery`: Phân trang, tìm kiếm từ khóa, lọc theo Category, Classroom, IsActive.
- **`IVideoService` & `VideoService`**:
  - `ListAsync`: Danh sách video cho quản trị viên.
  - `GetAsync`: Lấy chi tiết video theo ID.
  - `CreateAsync`: Tạo mới video, kiểm tra tồn tại Category / Classroom nếu có gán.
  - `UpdateAsync`: Cập nhật thông tin video, tự động bóc tách lại ID YouTube khi thay đổi URL.
  - `DeleteAsync`: Xóa video bài giảng.
  - `SetStatusAsync`: Đổi trạng thái hiển thị Bật/Tắt nhanh.
  - `ListForStudentAsync`: Lấy danh sách video đang kích hoạt (`IsActive = true`) và lọc theo các lớp học mà học sinh đang tham gia.

### Web API Controllers:
- **`VideosController` (`/api/videos`)**:
  - `GET /api/videos`: Danh sách video phân trang (Admin).
  - `GET /api/videos/{id}`: Chi tiết video.
  - `POST /api/videos`: Tạo mới video bài học.
  - `PUT /api/videos/{id}`: Chỉnh sửa video.
  - `DELETE /api/videos/{id}`: Xóa video.
  - `PATCH /api/videos/{id}/status`: Bật/Tắt trạng thái hiển thị.
- **`StudentController` (`/api/student/videos`)**:
  - `GET /api/student/videos`: Danh sách video dành cho học sinh.
  - `GET /api/student/videos/{id}`: Chi tiết video học sinh được phép xem.

---

## 4. Giao Diện Frontend (React + TypeScript)

### Cấu trúc Component:
1. **`AdminVideoPages.tsx`** (`/admin/videos`):
   - Quản lý danh sách video bài giảng dưới dạng lưới thẻ (Grid Cards) trực quan có kèm ảnh thumbnail.
   - Bộ lọc tìm kiếm nhanh: Theo từ khóa, Chuyên đề, Lớp học, Trạng thái (Tất cả / Đang hiển thị / Tạm ẩn).
   - Form Thêm mới / Chỉnh sửa:
     - Tự động nhận diện ID YouTube khi dán link.
     - Khung xem trước video YouTube trực tiếp ngay trong modal.
     - Chọn Chuyên đề, Lớp học, Thời lượng, Thứ tự hiển thị.
   - Thao tác nhanh: Nút đổi trạng thái Bật/Tắt, nút Chỉnh sửa, nút Xóa kèm modal xác nhận an toàn.
   - Modal trình chiếu video nhúng chuẩn 16:9 hỗ trợ fullscreen.
2. **`StudentVideoPages.tsx`** (`/student/videos`):
   - Banner học liệu trực tuyến hiện đại.
   - Bộ lọc chuyên đề theo dạng nút bấm nhanh (Badge Pills) và chọn lớp học.
   - Hiển thị thời lượng video, chuyên đề, lớp học áp dụng.
   - Bấm vào thẻ video để mở trình chiếu YouTube tức thì.
3. **Điều hướng & Định tuyến:**
   - Đăng ký route `/admin/videos` và `/student/videos` trong `routes.tsx`.
   - Thêm menu `🎥 Kho Video bài giảng` vào Sidebar Admin của `Layouts.tsx`.
   - Thêm nút `🎥 Video bài giảng` vào Navbar Học sinh của `Layouts.tsx`.

---

## 5. Danh Sách Các File Tạo Mới & Chỉnh Sửa

| STT | Loại | Đường dẫn file | Mô tả thay đổi |
|---|---|---|---|
| 1 | Tạo mới | `backend/src/ELearning.Domain/Videos/VideoLesson.cs` | Entity Video bài giảng & logic bóc tách YouTube ID |
| 2 | Chỉnh sửa | `backend/src/ELearning.Domain/Identity/Permissions.cs` | Thêm quyền `VideoView` và `VideoManage` |
| 3 | Tạo mới | `backend/src/ELearning.Infrastructure/Persistence/Configurations/VideoConfigurations.cs` | Cấu hình bảng và FK cho EF Core |
| 4 | Chỉnh sửa | `backend/src/ELearning.Application/Common/Abstractions/IAppDbContext.cs` | Bổ sung `DbSet<VideoLesson> VideoLessons` |
| 5 | Chỉnh sửa | `backend/src/ELearning.Infrastructure/Persistence/ELearningDbContext.cs` | Đăng ký DbSet trong DbContext |
| 6 | Tạo mới | `backend/src/ELearning.Infrastructure/Persistence/Migrations/20261009094351_AddVideoLessons.cs` | Migration tạo bảng `VideoLessons` |
| 7 | Tạo mới | `backend/src/ELearning.Application/Videos/VideoContracts.cs` | DTOs và FluentValidation cho Video |
| 8 | Tạo mới | `backend/src/ELearning.Application/Videos/VideoService.cs` | Service nghiệp vụ CRUD và lấy video học sinh |
| 9 | Chỉnh sửa | `backend/src/ELearning.Application/DependencyInjection.cs` | Đăng ký DI cho `IVideoService` |
| 10 | Tạo mới | `backend/src/ELearning.Api/Controllers/VideosController.cs` | Controller API CRUD video cho Admin |
| 11 | Chỉnh sửa | `backend/src/ELearning.Api/Controllers/StudentController.cs` | Thêm endpoint `/api/student/videos` cho Học sinh |
| 12 | Chỉnh sửa | `frontend/elearning-web/src/types/api.ts` | Khai báo types `VideoLesson`, DTOs |
| 13 | Chỉnh sửa | `frontend/elearning-web/src/constants/permissions.ts` | Thêm quyền `VideoView`, `VideoManage` ở Frontend |
| 14 | Chỉnh sửa | `frontend/elearning-web/src/services/api.ts` | Bổ sung `videosApi` gọi Backend |
| 15 | Tạo mới | `frontend/elearning-web/src/features/videos/AdminVideoPages.tsx` | Màn hình quản lý video của Admin |
| 16 | Tạo mới | `frontend/elearning-web/src/features/videos/StudentVideoPages.tsx` | Màn hình xem video của Học sinh |
| 17 | Chỉnh sửa | `frontend/elearning-web/src/app/routes.tsx` | Thêm routes `/admin/videos` và `/student/videos` |
| 18 | Chỉnh sửa | `frontend/elearning-web/src/layouts/Layouts.tsx` | Bổ sung menu Kho video trên giao diện Admin & Học sinh |
| 19 | Tạo mới | `docs/tinh-nang-kho-video-bai-giang.md` | Tài liệu tính năng đẩy lên Git |

---

## 6. Kết Quả Kiểm Thử (Verification)

- **Unit Tests Backend:** 189/189 test cases vượt qua (`dotnet test`).
- **EF Core Model Validation:** 0 pending model changes.
- **Frontend TypeScript & Bundle:** `tsc -b && vite build` thành công 100%, không phát sinh lỗi cú pháp hay thiếu kiểu.
