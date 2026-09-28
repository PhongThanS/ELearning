# 01 — Tổng quan sản phẩm

## 1. Mục đích

ELearning là nền tảng thi trực tuyến, có định hướng mở rộng thành LMS. Tài liệu này là đặc tả chuẩn để một kỹ sư hoặc AI agent có thể:

1. Tạo repository và solution.
2. Tạo database SQL Server bằng EF Core migration.
3. Triển khai backend .NET 10 và frontend React.
4. Triển khai xác thực, phân quyền, ngân hàng câu hỏi, đề thi / version, lượt làm bài và autosave, tính giờ phía server, chấm điểm tự động, kết quả và lịch sử.
5. Viết kiểm thử tự động, chạy local và triển khai.

Khi có chi tiết chưa được đặc tả, ưu tiên theo thứ tự:
1. Hành vi an toàn.
2. Kiểm tra (validation) ở phía server.
3. Giữ đúng dữ liệu lịch sử (snapshot bất biến).
4. Tương thích ngược.
5. Kiến trúc đơn giản thay vì microservices sớm.

## 2. Thông tin chung

| Hạng mục | Giá trị |
|---|---|
| Phiên bản tài liệu | 2.0 |
| Mục tiêu | MVP sẵn sàng production, mở rộng được |
| Backend | .NET 10, ASP.NET Core Web API |
| Frontend | React + TypeScript + Vite |
| Database | SQL Server |
| ORM | EF Core 10; Dapper cho báo cáo |
| UI | Bootstrap 5 (qua `react-bootstrap`) |
| Xác thực | JWT access token + refresh token (cookie HttpOnly) |
| Ngôn ngữ | Giao diện tiếng Việt; code / database / API tiếng Anh |
| Tên repo | `ELearning`; frontend `elearning-web`; solution `ELearning.sln` |

## 3. Nhóm người dùng

### ADMIN
- Đăng nhập; quản lý người dùng, nhóm người dùng, vai trò và quyền.
- Quản lý danh mục và câu hỏi.
- Tạo đề nháp, tạo version, thêm / xóa / sắp xếp câu hỏi, cấu hình luật thi.
- Publish / đóng đề; gán đề cho nhóm hoặc cá nhân.
- Xem lượt thi, kết quả, thống kê; thao tác trên lượt thi (gia hạn, hủy, cấp thêm lượt, buộc nộp).
- Sửa đáp án và chấm lại; xem audit log.

### STUDENT (học viên)
- Đăng ký (nếu cấu hình cho phép), đăng nhập, đổi mật khẩu.
- Xem các đề mình được phép thi và chi tiết đề.
- Bắt đầu hoặc tiếp tục lượt thi; trả lời, chuyển câu, đánh dấu xem lại; đáp án được tự động lưu.
- Nộp bài; hệ thống tự nộp khi hết giờ.
- Xem kết quả theo chính sách của đề; xem lịch sử.

### Vai trò tương lai
`TEACHER`, `EXAM_MANAGER`, `GRADER`, `CONTENT_EDITOR`. Hệ thống phân quyền theo **permission**, không hard-code theo hai vai trò (xem `07-bao-mat.md`).

## 4. Nguyên tắc cốt lõi

1. **Server là nguồn dữ liệu chuẩn.** Không tin client về: điểm, đáp án đúng, thời lượng, thời điểm hết hạn, số lượt, tình trạng mở đề, quyền, trạng thái nộp bài.
2. **Version đã publish là bất biến.** Muốn thay đổi thì: `Version 1 (PUBLISHED) → tạo Version 2 (DRAFT) → sửa → publish`. Ngoại lệ duy nhất là quy trình chấm lại có kiểm soát (D-11).
3. **Snapshot tại thời điểm publish.** Nội dung và đáp án được copy vào `ExamQuestions`. Lượt thi tham chiếu snapshot này, không bao giờ tham chiếu ngân hàng câu hỏi (D-01).
4. **Backend chấm điểm.** `Câu trả lời → API → Grading Engine → Result`. Frontend không bao giờ tính điểm chính thức.
5. **Modular monolith.** `React → ASP.NET Core → SQL Server`. Ranh giới module có thể tách sau này: Identity, Question/Exam, Attempt/Grading, Notification, Reporting.

## 5. Phạm vi MVP

**Bắt buộc có:**
- Đăng nhập, đăng ký (bật/tắt bằng cấu hình), đổi mật khẩu, admin đặt lại mật khẩu.
- Vai trò, permission, nhóm người dùng.
- Danh mục câu hỏi; CRUD câu hỏi; 4 loại câu hỏi; nội dung Markdown giới hạn.
- Đề thi, version, chọn câu hỏi thủ công, publish / đóng / mở lại.
- Gán đề (`PUBLIC` / `ASSIGNED`).
- Lượt thi, tính giờ phía server, autosave, đánh dấu xem lại.
- Nộp bài, tự nộp (khi có request và bằng job nền).
- Chấm tự động, kết quả, lịch sử học viên.
- Danh sách kết quả cho admin, export kết quả ra Excel.
- Thao tác admin trên lượt thi; sửa đáp án và chấm lại.
- Ghi nhận sự kiện lượt thi (mất focus, đổi tab); audit log; health check.

**Sau MVP:**
- Ngẫu nhiên câu hỏi từ pool; xáo thứ tự câu / đáp án (schema đã chuẩn bị sẵn).
- Độ khó, tag, chấm điểm từng phần.
- Import câu hỏi từ Excel *(ưu tiên cao nhất sau MVP)*.
- Khóa học, lớp học, giáo viên; bảng xếp hạng, chứng chỉ.
- Email, thông báo, SignalR, Redis.
- Chống gian lận nâng cao, thống kê nâng cao.
- Câu tự luận và chấm tay; đính kèm hình ảnh / file.

## 6. Yêu cầu phi chức năng

Các giá trị dưới đây là mặc định đề xuất *(cần xác nhận)*.

| Chỉ tiêu | Mục tiêu |
|---|---|
| Số học viên thi đồng thời | 500 |
| Đợt bắt đầu dồn dập | 500 lượt `start` trong 60 giây |
| Autosave p95 | < 300 ms |
| Start attempt p95 | < 1 s |
| Submit p95 | < 2 s |
| Tỉ lệ lỗi 5xx lúc cao điểm | < 0.1% |
| RPO (mất dữ liệu tối đa) | 15 phút; 5 phút trong ngày thi |
| RTO (thời gian khôi phục) | 1 giờ |
| Trình duyệt hỗ trợ | Chrome, Edge, Firefox, Safari (2 phiên bản gần nhất) |
| Màn hình tối thiểu | 360 px (mobile) |

Các chỉ tiêu này phải được kiểm chứng bằng load test (`08-kiem-thu.md`). Không deploy trong khung giờ có đề thi đang mở theo lịch.

## 7. Thuật ngữ Việt – Anh

Dùng thống nhất trong UI (tiếng Việt) và code (tiếng Anh).

| Tiếng Việt (UI) | Tiếng Anh (code) | Ghi chú |
|---|---|---|
| Ngân hàng câu hỏi | Question Bank | |
| Câu hỏi | Question | |
| Danh mục | QuestionCategory | |
| Lựa chọn / phương án | Option | Mã A, B, C… |
| Đáp án đúng | Correct answer | `IsCorrect`, `AcceptedAnswers` |
| Đề thi | Exam | |
| Phiên bản đề | ExamVersion | |
| Câu hỏi trong đề | ExamQuestion | Snapshot |
| Lượt làm bài / lượt thi | Attempt | |
| Bài làm / câu trả lời | Answer | |
| Nộp bài | Submit | |
| Tự động nộp | Auto-submit | |
| Kết quả | Result | |
| Chấm điểm | Grading | |
| Chấm lại | Regrade | |
| Hủy câu | Void question | |
| Đánh dấu xem lại | Mark for review | |
| Nhóm người dùng | UserGroup | |
| Gán đề | ExamAssignment | |
| Học viên | Student | Role code `STUDENT` |
| Quản trị viên | Admin | Role code `ADMIN` |
| Nhật ký hệ thống | Audit log | |
| Ân hạn | Grace period | |

## 8. Quyết định / Giả định

- **D-10, D-22 — Quyền dự thi nằm trong MVP.** Spec gốc cho mọi học viên thấy mọi đề, cộng thêm tự đăng ký, nên bất kỳ ai cũng thi được. Với một hệ thống thi thật, điều này không chấp nhận được. Vì vậy nhóm người dùng và gán đề được đưa vào MVP.
- **Export kết quả Excel đưa vào MVP.** Admin hầu như luôn cần export ngay từ kỳ thi đầu tiên. Import câu hỏi Excel để sau MVP nhưng có ưu tiên cao nhất.
- **Chỉ tiêu phi chức năng** trong spec gốc chưa có. Các con số ở mục 6 là giả định để có mục tiêu cho load test.
- **Tên tài liệu:** spec gốc trỏ tới `docs/ONLINE_EXAM_SPEC.md`. Tài liệu chuẩn giờ là bộ `docs/00…11`.
