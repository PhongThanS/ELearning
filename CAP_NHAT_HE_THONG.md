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
