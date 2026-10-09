# Báo Cáo & Tài Liệu Cập Nhật Hệ Thống ELearning

Tài liệu này tổng hợp toàn bộ các tính năng, cải tiến giao diện, sửa lỗi và đồng bộ hạ tầng CI/CD đã thực hiện.

---

## 1. Quyền xem kết quả bài thi của học sinh (Admin mở trạng thái)

### Nghiệp vụ & Luồng hoạt động:
- **Mặc định khi tạo đề thi:** Chính sách xem lại (`reviewPolicy`) được thiết lập mặc định là `NEVER` (Khóa xem chi tiết).
- **Khi chưa cấp quyền:**
  - Học sinh sau khi nộp bài **vẫn xem được kết quả tổng quan**: Vòng tròn điểm số (%), tổng điểm đạt được / điểm tối đa, số câu đúng / tổng số câu, trạng thái Đạt / Chưa đạt.
  - Phần chi tiết câu hỏi và đáp án hoàn toàn bị ẩn, kèm thông báo hướng dẫn:
    > 🔒 *"Kết quả bài thi đã được ghi nhận. Chi tiết câu hỏi và đáp án sẽ hiển thị sau khi giáo viên / quản trị viên mở quyền xem lại."*
  - Phía API học sinh (`GET /api/student/attempts/{id}/result`) không trả về danh sách câu hỏi và đáp án (`questions: null`, `reviewAvailable: false`).
- **Khi Admin mở quyền:**
  - Tại trang chi tiết đề thi của Quản trị viên (`ExamDetailPage`), bổ sung khối điều khiển nhanh **"Quyền xem lại kết quả bài thi"**:
    - **Nút "Mở cho học sinh xem chi tiết"** (chuyển sang `AFTER_SUBMIT` chỉ với 1 click).
    - **Nút "Khóa xem lại chi tiết"** (chuyển về `NEVER` bất cứ lúc nào).
  - Backend cung cấp endpoint `PATCH /api/exams/{id}/review-policy` cập nhật tức thì chính sách cho tất cả các phiên bản của đề thi.
  - Ngay khi mở quyền, học sinh vào xem lại bài thi sẽ thấy toàn bộ chi tiết từng câu hỏi, các phương án đã chọn và đáp án đúng.

---

## 2. Giao diện chi tiết bài thi: Bỏ chữ "V" và "X", hiển thị Icon trực quan

### Thay đổi giao diện:
- Loại bỏ hoàn toàn các chuỗi ký tự text `✓ V` và `✗ X` gây thô ráp trên giao diện.
- Thay thế bằng bộ icon Bootstrap Icons chuẩn, hiện đại và thân thiện:
  - **Phương án chọn đúng:** Badge icon tích xanh `<i className="bi bi-check-lg" />`, badge trạng thái `<i className="bi bi-check2-circle" /> Bạn chọn đúng`.
  - **Phương án chọn sai:** Badge icon dấu chéo đỏ `<i className="bi bi-x-lg" />`, badge trạng thái `<i className="bi bi-x-circle" /> Bạn chọn sai`.
  - **Đáp án đúng của đề:** Badge tích xanh `<i className="bi bi-check-lg" /> Đáp án đúng`.
  - **Tiêu đề mỗi câu hỏi:** Icon trạng thái `<i className="bi bi-check-circle-fill text-success" />` (khi làm đúng) hoặc `<i className="bi bi-x-circle-fill text-danger" />` (khi làm sai).
- Đã áp dụng đồng bộ trên cả 2 màn hình:
  - Trang kết quả bài thi của học sinh: `frontend/elearning-web/src/features/attempts/StudentPages.tsx`.
  - Trang xem chi tiết bài làm của quản trị viên: `frontend/elearning-web/src/features/exams/ResultPages.tsx`.

---

## 3. Hỗ trợ Đề thi nhiều phiên bản (Multi-version) cho Lớp học

### Cơ chế hoạt động:
- Đề thi được gán cho Lớp học (`Classroom`) hoặc Nhóm người dùng (`UserGroup`).
- Khi học sinh đã hoàn thành phiên bản cũ (v1) và đã sử dụng hết số lượt thi quy định (`maxAttempts`):
  - Giáo viên / Admin tạo phiên bản mới (v2, v3...), bổ sung hoặc điều chỉnh câu hỏi và bấm **Xuất bản (Publish)**.
  - Backend (`AttemptService.cs`) đã được nâng cấp logic kiểm tra số lượt thi: Tính `usedAttempts` dựa trên phiên bản đang xuất bản (`a.ExamVersionId == version.Id`).
  - Học sinh thuộc lớp khi vào xem đề thi sẽ thấy phiên bản mới có số lượt đã dùng là `0`, trạng thái chuyển sang `AVAILABLE`.
  - Nút **"Bắt đầu làm bài"** sẽ hiển thị để học sinh tiếp tục làm bài thi theo phiên bản mới nhất vừa xuất bản.
  - Bảng lịch sử các lượt thi trước đây của học sinh (ở các phiên bản cũ) vẫn được lưu trữ và hiển thị đầy đủ bên dưới để tra cứu điểm số và kết quả.

---

## 4. Đồng bộ Docker, CI/CD sang PostgreSQL & Sửa lỗi GitHub Actions

### Nguyên nhân lỗi GitHub Actions trước đó:
- Mã nguồn backend gần đây đã chuyển sang dùng **PostgreSQL** (`Npgsql`), nhưng file `docker-compose.yml` và `.env.example` vẫn cấu hình chạy **Microsoft SQL Server** (cổng 1433, `sqlcmd`, image `mssql/server:2022`). Khi chạy CI/CD trên GitHub Actions, migration không thể kết nối tới SQL Server khiến API bị sập và fail ở bước `Smoke test full stack`.
- Unit test `Like_pattern_escapes_wildcards` trong `IdentityDomainTests.cs` trước đó kiểm tra theo cú pháp escape của SQL Server (`[%]`, `[_]`) thay vì PostgreSQL (`\%`, `\_`).

### Các cập nhật đã thực hiện:
1. **`docker-compose.yml`**:
   - Thay thế service `sqlserver` bằng service `postgres` sử dụng image `postgres:17-alpine` (cổng 5432).
   - Cập nhật chuỗi kết nối chuẩn PostgreSQL:  
     `Host=postgres;Port=5432;Database=${DB_NAME:-elearning_db};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD}...`
   - Cấu hình healthcheck PostgreSQL với `pg_isready`.
   - Cập nhật service `migrate` kết nối trực tiếp vào PostgreSQL.
   - Bỏ phụ thuộc vào `sqlcmd` và script T-SQL của SQL Server.
2. **`.env.example`**:
   - Cập nhật các biến môi trường tương ứng: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `DB_NAME=elearning_db`.
3. **`IdentityDomainTests.cs`**:
   - Cập nhật dữ liệu test khớp với cú pháp PostgreSQL LIKE escaping (`\%`, `\_`, `\\`).
   - Chạy kiểm thử thành công toàn bộ: **189/189 Unit Tests Passed**.

---

## 5. Danh Sách Các File Đã Chỉnh Sửa

| STT | Đường dẫn file | Nội dung thay đổi |
|---|---|---|
| 1 | `backend/src/ELearning.Api/Controllers/ExamsController.cs` | Thêm endpoint `PATCH /api/exams/{id}/review-policy` |
| 2 | `backend/src/ELearning.Application/Exams/ExamContracts.cs` | Đổi mặc định `ReviewPolicy.Never`, thêm DTO `SetReviewPolicyRequest`, `PublishedReviewPolicy` |
| 3 | `backend/src/ELearning.Application/Exams/ExamService.cs` | Thêm phương thức `SetReviewPolicyAsync`, cập nhật `ReviewPolicy` cho mọi version của đề thi |
| 4 | `backend/src/ELearning.Domain/Exams/ExamVersion.cs` | Thêm phương thức `SetReviewPolicy(ReviewPolicy policy)` |
| 5 | `backend/src/ELearning.Application/Attempts/AttemptService.cs` | Tính số lượt thi `usedAttempts` theo `ExamVersionId == version.Id` hỗ trợ multi-version |
| 6 | `frontend/elearning-web/src/types/api.ts` | Bổ sung trường `publishedReviewPolicy` vào `ExamDetail` |
| 7 | `frontend/elearning-web/src/services/api.ts` | Thêm hàm gọi API `setReviewPolicy(id, policy)` |
| 8 | `frontend/elearning-web/src/features/exams/ExamPages.tsx` | Thêm khối `ReviewPolicyCard` (bật/tắt 1 click), đổi mặc định tạo đề sang `NEVER` |
| 9 | `frontend/elearning-web/src/features/attempts/StudentPages.tsx` | Bỏ chữ V/X thay bằng icon Bootstrap, cập nhật thông báo khi khóa xem lại |
| 10 | `frontend/elearning-web/src/features/exams/ResultPages.tsx` | Bỏ chữ V/X thay bằng icon Bootstrap trên trang chi tiết lượt thi của Admin |
| 11 | `backend/tests/ELearning.UnitTests/Identity/IdentityDomainTests.cs` | Sửa unit test `Like.Contains` khớp cú pháp PostgreSQL LIKE escape |
| 12 | `docker-compose.yml` | Chuyển đổi từ SQL Server sang `postgres:17-alpine`, cập nhật chuỗi kết nối và migration |
| 13 | `.env.example` | Cập nhật biến môi trường PostgreSQL |
| 14 | `backend/src/ELearning.Infrastructure/Persistence/CodeGenerator.cs` | Chuyển câu lệnh lấy sequence sang PostgreSQL: `SELECT nextval('"{sequence}"')` thay vì SQL Server syntax |
| 15 | `backend/src/ELearning.Infrastructure/Persistence/Seed/DatabaseSeeder.cs` | Khôi phục seed nhóm `DEMO`, học sinh và đề thi demo `CS-BASIC` phục vụ môi trường Development & ApiTests |
| 16 | `backend/tests/ELearning.TestSupport/SqlServerTestDatabase.cs` | Nâng cấp test harness: khởi tạo PostgreSQL container với database `postgres`, tạo và drop database tạm thời cho test suites |

---

## 6. Tính năng Kho Video bài giảng (Gắn link YouTube)

Chi tiết tài liệu kỹ thuật riêng: [`tinh-nang-kho-video-bai-giang.md`](./tinh-nang-kho-video-bai-giang.md)

### Nghiệp vụ & Thiết kế:
- **Cơ chế lưu trữ:** Không lưu file video nặng (MP4, MKV...) trên server. Chỉ lưu địa chỉ URL (YouTube), tự động bóc tách `Video ID` (11 ký tự) bằng Regex và tự động sinh link Thumbnail chuẩn `https://img.youtube.com/vi/{id}/hqdefault.jpg`.
- **Cơ sở dữ liệu:** Bảng `VideoLessons` (EF Migration `20261009094351_AddVideoLessons`), liên kết với Chuyên đề (`CategoryId`) và Lớp học (`ClassroomId`), các ràng buộc ngoại không cascade delete.
- **Backend & Quyền hạn:**
  - Quyền mới: `Video.View`, `Video.Manage`.
  - API Quản trị viên: `GET/POST/PUT/DELETE /api/videos`, `PATCH /api/videos/{id}/status`.
  - API Học sinh: `GET /api/student/videos`, `GET /api/student/videos/{id}`.
- **Giao diện Frontend:**
  - Quản trị viên (`/admin/videos`): Quản lý video dạng lưới thẻ trực quan, xem trước YouTube trực tiếp khi nhập link, bật/tắt hiển thị, sửa, xóa, nhúng trình chiếu YouTube (`youtube-nocookie.com`).
  - Học sinh (`/student/videos`): Xem danh sách video bài giảng, lọc theo chuyên đề hoặc lớp học, popup phát video không quảng cáo.
  - Thêm mục điều hướng `🎥 Kho Video bài giảng` trên Sidebar Admin và `🎥 Video bài giảng` trên Navbar Học sinh.

---

## 7. Đồng bộ dữ liệu Chuyên đề & Lớp học cho Video

Chi tiết tài liệu riêng: [`dong-bo-du-lieu-chuyen-de-va-lop-hoc-cho-video.md`](./dong-bo-du-lieu-chuyen-de-va-lop-hoc-cho-video.md)

- Toàn bộ danh mục Chuyên đề được lấy đồng bộ từ mục **Chuyên đề** (`QuestionCategories` - `/admin/categories`).
- Toàn bộ danh sách Lớp học được lấy đồng bộ từ mục **Lớp học** (`Classrooms` - `/admin/classes`).
- Bổ sung endpoint `GET /api/student/categories` cho học sinh lọc video theo chuyên đề thực tế.
- Tự động làm mới dữ liệu dropdown thông qua tiền tố cache queryKey `["categories"]` và `["classes"]`.

---

## 8. Chuyển cơ sở dữ liệu từ SQL Server sang PostgreSQL (D-29)

Commit chính: `9dbd1e5`; đồng bộ Docker / test: `7931449`, `f8f8422`. Mã quyết định D-29, D-30, D-31 đã được ghi vào `docs/00-muc-luc.md`.

### 8.1 Thay đổi mã nguồn

| Khu vực | Nội dung |
|---|---|
| Gói | `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 (Infrastructure, Application); test dùng `Testcontainers.PostgreSql` |
| `DependencyInjection.cs` | `UseNpgsql`, `EnableRetryOnFailure(3)`, bảng lịch sử migration `__EFMigrationsHistory` |
| `ELearningDbContext` | Kiểu `timestamp with time zone`; bỏ collation BIN2 của cột enum; `SaveChanges` tự gán `RowVersion` (`bytea`) cho entity `IHasRowVersion` (D-30) |
| `ConfigurationExtensions`, `*Configurations` | `HasFilter` / `HasCheckConstraint` dùng tên cột trong nháy kép; `RowVersion` → `IsConcurrencyToken()`; `SourceRowVersion` → `bytea` |
| `AttemptLock.cs` | `SELECT 1 FROM "ExamAttempts" WHERE "Id" = @id FOR UPDATE` thay `UPDLOCK, ROWLOCK` (D-21); bắt lỗi trùng bằng `PostgresException` `23505` |
| `GlobalExceptionHandler.cs` | Lỗi trùng khóa nhận từ `PostgresException` `UniqueViolation` (thay `SqlException` 2601 / 2627) |
| `CodeGenerator.cs` | `SELECT nextval('"seq"')` thay `NEXT VALUE FOR` |
| `Common/Like.cs` + các service tìm kiếm | Escape `\`, `%`, `_`; dùng `EF.Functions.ILike` (không phân biệt hoa thường) |
| `ReportQuery.cs` | Dapper SQL viết lại: nháy kép, `::int`, `LIMIT`, `TRUE` / `FALSE` |
| Migration | Xóa 8 migration SQL Server; thêm `20261009035512_InitialPostgreSql` (sau đó `20261009094351_AddVideoLessons`) |
| `DatabaseSeeder.cs` | Khôi phục seed nhóm `DEMO`, học viên và đề `CS-BASIC` (Development / ApiTests) |
| Test | `SqlServerTestDatabase.cs` chạy PostgreSQL (biến `ELEARNING_TEST_POSTGRES`, database tạm `elearning_test_<guid>`); `SchemaTests`, `IdentityDomainTests` cập nhật |
| Hạ tầng | `docker-compose.yml` (service `postgres`, bỏ `sqlserver` / `db-init`), `.env.example` (`POSTGRES_USER`, `POSTGRES_PASSWORD`, `DB_NAME`) |
| Khác | `elearning_db_backup.sql` (dump PostgreSQL) được commit vào gốc repo |

### 8.2 Tài liệu đã cập nhật theo thay đổi này

| File | Nội dung bổ sung |
|---|---|
| `CLAUDE.md` | Stack PostgreSQL, quy tắc khóa dòng `FOR UPDATE`, nháy kép / `ILIKE` / `RowVersion`, lệnh chạy test với `ELEARNING_TEST_POSTGRES` |
| `README.md` | Yêu cầu PostgreSQL, chuỗi kết nối dev, cách chạy test |
| `docs/00-muc-luc.md` | D-21 cập nhật; thêm D-29, D-30, D-31 |
| `docs/01`, `02`, `03` | Công nghệ, khóa dòng, health check, Testcontainers; hiện trạng `ReviewPolicy` và đề nhiều version |
| `docs/04a`, `04b` | Quy ước PostgreSQL, bảng ánh xạ kiểu 1.1, bảng `VideoLessons`, RowVersion `bytea`, khóa dòng |
| `docs/05-api.md` | `PATCH /api/exams/{id}/review-policy`, API video và học viên (`/student/videos`, `/student/categories`), ma trận quyền |
| `docs/06`, `07` | Route `/admin/videos`, `/student/videos`; permission `Video.View`, `Video.Manage` |
| `docs/08-kiem-thu.md` | Test chạy trên PostgreSQL, biến môi trường mới |
| `docs/09-van-hanh.md` | Docker Compose PostgreSQL, chuỗi kết nối, backup bằng `pg_dump`, danh sách việc tồn đọng |
| `docs/10-bay-ky-thuat.md` | Sửa các bẫy gắn với SQL Server; thêm mục 20 "Phát hiện khi chuyển sang PostgreSQL" |

### 8.3 Việc còn tồn đọng / cần quyết định

1. **Script vận hành vẫn là SQL Server:** `deploy/scripts/backup.sh`, `restore.sh`, `restore-test.sh`, `monitor.sh` (kiểm tra backup), `windows/register-backup-tasks.ps1`, `deploy/sql/init-app-login.sql`. Cần viết lại bằng `pg_dump` / `pg_restore` (+ WAL archiving).
2. **Tài khoản DB quyền tối thiểu** (trước đây `APP_DB_LOGIN`) chưa có tương đương trong compose.
3. **E2E và load test** (`frontend/elearning-web/e2e/`, `load-tests/lib/seed.js`) còn nhắc SQL Server.
4. **`review-policy` áp cho mọi version, kể cả đã publish** và bỏ qua quy tắc `AFTER_SUBMIT` ⇒ `MaxAttempts = 1` (lệch D-03 / D-09).
5. **Đếm lượt thi theo version** trong danh sách đề (lệch D-07); cần xác nhận quy tắc ở bước `start`.
6. **`DELETE /api/videos/{id}` xóa cứng** (`db.VideoLessons.Remove`), trong khi D-16 cấm xóa cứng dữ liệu; và `GET /api/student/videos/{id}` dùng chung `GetAsync` nên không lọc `IsActive` (học viên có Id vẫn xem được video đã tắt). Cần quyết định.
7. Lớp test còn tên `SqlServerTestDatabase` (đã chạy PostgreSQL) — nên đổi tên.

