# ELearning — Bộ tài liệu thiết kế (v2.0)

Bộ tài liệu này thay thế `ELearning_Design_Spec_VI.md` (v1.0). Nội dung gốc được giữ lại, sửa các chỗ mâu thuẫn, chốt các điểm còn bỏ ngỏ và bổ sung những phần còn thiếu.

**Quy ước:**
- Văn bản viết bằng tiếng Việt. **Mọi định danh trong code, database, API, enum đều giữ tiếng Anh** (ví dụ `IsCorrect`, không viết `IsĐúng`).
- Mỗi file có mục **"Quyết định / Giả định"**, ghi lại những chỗ spec gốc mâu thuẫn hoặc chưa chốt và phương án đã chọn. Mã quyết định có dạng `D-xx` và dùng chung cho toàn bộ tài liệu.
- Khi hai file mâu thuẫn nhau: file chuyên đề thắng (ví dụ về schema thì `04a`/`04b` thắng). Mâu thuẫn phải được sửa ngay, không để tồn tại.
- Các con số đánh dấu *(cần xác nhận)* là giá trị mặc định đề xuất; chủ sản phẩm có thể đổi.

## Danh sách file

| File | Nội dung chính |
|---|---|
| `00-muc-luc.md` | Mục lục, bảng góp ý → file, sổ quyết định |
| `01-tong-quan.md` | Mục tiêu sản phẩm, vai trò, nguyên tắc cốt lõi, phạm vi MVP / sau MVP, yêu cầu phi chức năng, thuật ngữ Việt–Anh |
| `02-nghiep-vu.md` | Loại câu hỏi, quy tắc chấm, chuẩn hóa đáp án, vòng đời đề thi / version / lượt thi, tính giờ, chính sách xem kết quả, thi lại, quyền dự thi, chấm lại, thao tác admin |
| `03-kien-truc.md` | Công nghệ, cấu trúc repo, các tầng backend, ranh giới service, kiến trúc frontend, thư viện được phép / bị cấm |
| `04a-csdl-danh-muc-de-thi.md` | Quy ước DB; bảng người dùng, phân quyền, nhóm, refresh token, ngân hàng câu hỏi, đề thi, version, snapshot, gán đề, sửa đáp án |
| `04b-csdl-luot-thi-ket-qua.md` | Bảng lượt thi, câu trả lời, sự kiện, kết quả, lịch sử điểm, audit; tổng hợp index; sơ đồ quan hệ; ràng buộc bất biến |
| `05-api.md` | Hợp đồng response, bảng mã lỗi, danh sách endpoint, ma trận phân quyền, phân trang |
| `06-frontend.md` | Route, màn hình admin và học viên, bộ làm bài (exam player), autosave, timer, UX, accessibility, i18n |
| `07-bao-mat.md` | Xác thực, token, mật khẩu, phân quyền, rate limit, chống lộ đáp án, chống gian lận, dữ liệu cá nhân |
| `08-kiem-thu.md` | Unit, integration, API, frontend, E2E, load test, các ca bắt buộc |
| `09-van-hanh.md` | Cấu hình, môi trường, triển khai, migration, backup, giám sát, CI/CD |
| `10-bay-ky-thuat.md` | Các lỗi kỹ thuật cụ thể (.NET / EF Core / PostgreSQL / JS) phải tránh, kèm cách làm đúng |
| `11-quy-trinh-phat-trien.md` | Quy tắc cho AI agent, mẫu giao việc, milestone, Definition of Done, checklist cuối |

## Góp ý nào được áp dụng vào file nào

| # | Góp ý | File |
|---|---|---|
| 1.1 | Sửa định danh bị dịch hỏng (`IsĐúng`, `ĐúngAnswer*`, role "học viên") | Tất cả; bảng thuật ngữ ở `01` |
| 1.2 | Tên file spec không khớp, audit §31/§119 lệch nhau | `00`, `07`, `11` |
| 1.3 | Biến các chỗ "tùy chính sách" thành quyết định đã chốt | Mục "Quyết định" của từng file; sổ tổng hợp bên dưới |
| 2.1 | Chấm trắc nghiệm không có nguồn đáp án; snapshot hai lần | `02`, `04a`/`04b` (D-01) |
| 2.2 | Draft version lưu câu hỏi ở đâu | `02`, `04a`/`04b`, `05` (D-02) |
| 2.3 | Cấu hình thi nằm ở `Exams` và bị sửa được sau publish | `02`, `04a`/`04b` (D-03) |
| 2.4 | Hai state machine không liên kết; thiếu API version | `02`, `04a`/`04b`, `05` (D-04) |
| 2.5 | Kết quả lưu ở hai nơi | `04a`/`04b` (D-20) |
| 3.1 | Job tự nộp bài, thời gian ân hạn | `02`, `03`, `09` (D-05) |
| 3.2 | `ExpiredAt` so với `EndAt`; đóng đề khi đang có người thi | `02` (D-05, D-06) |
| 3.3 | Chính sách xem điểm / xem đáp án; ẩn `Explanation` | `02`, `05`, `07` (D-09) |
| 3.4 | Thi nhiều lượt tính điểm nào; ý nghĩa `MaxAttempts` | `02` (D-07, D-08) |
| 3.5 | Ai được thi đề nào; tự đăng ký | `02`, `04a`/`04b`, `07` (D-10) |
| 3.6 | Chấm lại khi đáp án sai | `02`, `04a`/`04b`, `05` (D-11) |
| 3.7 | Thao tác admin trên lượt thi | `02`, `05` |
| 4 | Autosave sai thứ tự, `serverTime`, đánh dấu xem lại, batch save, máy dùng chung, khóa khi nộp | `02`, `05`, `06`, `10` (D-18, D-21) |
| 5 | NFC/NFD, số thập phân kiểu Việt, nhiều đáp án chấp nhận | `02`, `04a`/`04b` (D-12) |
| 6 | Index thừa/thiếu, FK, cascade, `RowVersion`, CHECK, cột cho Users/RefreshTokens, AttemptEvents | `04a`/`04b` |
| 7 | Bẫy DateTime, enum, retry + transaction, TimeProvider, license, migration | `03`, `09`, `10` (D-19) |
| 8 | API / route / permission / mã lỗi còn thiếu | `05`, `06`, `07` (D-13, D-14) |
| 9 | Chỉ tiêu tải, load test, rate limit autosave, RPO/RTO, Markdown, Excel, dữ liệu cá nhân, CI/CD | `01`, `07`, `08`, `09` (D-17) |
| 10 | Tách tài liệu, thuật ngữ, rút gọn cho AI agent | Chính bộ tài liệu này; `11` |

## Sổ quyết định (tóm tắt)

Chi tiết và lý do nằm ở file được ghi trong ngoặc.

| Mã | Quyết định |
|---|---|
| D-01 | Chỉ snapshot ở cấp version (`ExamQuestions`). `AttemptQuestions` tham chiếu `ExamQuestionId NOT NULL` và chỉ lưu thứ tự. Chấm điểm đọc đáp án từ `ExamQuestions` (`02`, `04a`/`04b`) |
| D-02 | Câu hỏi của draft version được copy vào `ExamQuestions` ngay khi thêm; có thao tác "đồng bộ từ ngân hàng"; bị khóa khi publish (`02`) |
| D-03 | Thời lượng, ngưỡng đạt, chính sách xem điểm / xem đáp án nằm ở `ExamVersions` (bất biến). Lịch thi, số lượt, quyền dự thi nằm ở `Exams` (sửa được, có audit) (`02`) |
| D-04 | State machine cho Exam, ExamVersion, Attempt. Mỗi đề tối đa 1 version PUBLISHED và 1 version DRAFT (`02`) |
| D-05 | `ExpiredAt = min(StartedAt + Duration, EndAt)`; ân hạn 30 giây; job quét mỗi 60 giây để tự nộp (`02`) |
| D-06 | Đóng đề chỉ chặn lượt mới; lượt đang làm được làm tiếp, trừ khi admin chọn `forceSubmit` (`02`) |
| D-07 | `MaxAttempts` từ 1 đến 50, không có "không giới hạn". Tính mọi lượt trừ `CANCELLED`. Mỗi người chỉ có 1 lượt `IN_PROGRESS` / đề; gọi start lần nữa thì trả lượt cũ (`02`) |
| D-08 | Điểm chính thức khi thi nhiều lượt: `HIGHEST` (mặc định) hoặc `LATEST`, bị khóa khi đề đã có lượt thi (`02`) |
| D-09 | `ScoreVisibility` và `ReviewPolicy` thay cho hai cờ bool (`02`) |
| D-10 | `AccessMode = PUBLIC / ASSIGNED`, có nhóm người dùng; tự đăng ký bật/tắt bằng cấu hình (`02`, `07`) |
| D-11 | Chấm lại có kiểm soát: sửa đáp án hoặc hủy câu → chấm lại, có audit và lịch sử điểm (`02`) |
| D-12 | Câu điền: chuẩn hóa NFC, nhiều đáp án chấp nhận, quy tắc parse số rõ ràng, lưu chuỗi gốc (`02`) |
| D-13 | Mọi response dùng wrapper `ApiResponse`; bảng ánh xạ HTTP status cố định (`05`) |
| D-14 | Phân quyền theo permission, tra ở server có cache, không nhét permission vào JWT (`07`) |
| D-15 | Access token giữ trong bộ nhớ; refresh token ở cookie HttpOnly, `SameSite=Strict`; xoay vòng và phát hiện dùng lại (`07`) |
| D-16 | Không xóa cứng câu hỏi / danh mục / user (dùng bật-tắt); chỉ xóa được đề chưa từng publish; user bị xóa thì ẩn danh hóa (`02`, `07`) |
| D-17 | Nội dung câu hỏi hỗ trợ Markdown giới hạn (có code block), không cho HTML thô (`02`, `06`) |
| D-18 | Autosave dùng `clientSeq`, lưu cờ "đánh dấu xem lại", có batch save, backup trong `sessionStorage` (`06`) |
| D-19 | Không dùng MediatR / AutoMapper; dùng AwesomeAssertions thay FluentAssertions v8 (`03`) |
| D-20 | `ExamResults` là nguồn điểm duy nhất; `ExamAttempts` không lưu điểm (`04a`/`04b`) |
| D-21 | Lưu đáp án và nộp bài đều khóa dòng attempt bằng `SELECT ... FOR UPDATE` (PostgreSQL; trước D-29 là `UPDLOCK` của SQL Server) trong transaction (`04a`/`04b`, `10`) |
| D-22 | Nhóm người dùng và gán đề nằm trong MVP (`01`, `02`) |
| D-23 | Application service dùng trực tiếp `IAppDbContext` (DbSet); repository riêng chỉ cho thao tác đặc biệt như khóa dòng. Application tham chiếu gói `Microsoft.EntityFrameworkCore` (`03`) |
| D-24 | Dùng lại refresh token vừa xoay vòng trong 30 giây được coi là hai tab refresh song song, không thu hồi cả chuỗi (`07`) |
| D-25 | `MustChangePassword` được chặn cả ở backend: mọi endpoint cần policy trả 403 `PASSWORD_CHANGE_REQUIRED` (`07`) |
| D-26 | Giám sát không thêm hạ tầng: meter `ELearning` + log `Monitoring` mỗi phút; cảnh báo qua `/health/alerts` (chặn ở Nginx) và container `monitor` gửi webhook; OpenTelemetry / Grafana để sau (`09`) |
| D-27 | Ảnh trong đề / lựa chọn / giải thích: file trên đĩa theo SHA-256 + bảng `MediaFiles` bất biến; nội dung lưu `media:<id>`; server ký URL ngắn hạn chỉ cho ảnh của các trường đang trả về (`02`, `04a`, `05`, `07`) |
| D-28 | Lớp học `Classrooms`: học viên ↔ lớp nhiều-nhiều (`ClassroomStudents`); gán đề cho lớp qua `ExamAssignments.ClassroomId`; chỉ lớp đang hoạt động và thành viên hiện tại mới thấy đề; xóa được lớp (kèm danh sách học viên) khi lớp không còn được gán cho đề nào, nếu không thì bỏ gán trước hoặc tắt lớp (`02`, `04a`, `05`, `07`) |
| D-29 | Chuyển cơ sở dữ liệu từ SQL Server sang **PostgreSQL 17** (Npgsql): kiểu cột, khóa dòng, LIKE, sequence, migration `InitialPostgreSql`, Docker, test harness (`03`, `04a`/`04b`, `08`, `09`, `10`, `CAP_NHAT_HE_THONG`) |
| D-30 | `RowVersion` là `bytea` do ứng dụng gán (GUID ngẫu nhiên mỗi lần ghi, trong `ELearningDbContext.SaveChanges`) thay cho `rowversion` của SQL Server; vẫn là concurrency token của EF (`04a`, `10`) |
| D-31 | Kho video bài giảng: chỉ lưu link YouTube (bảng `VideoLessons`), không lưu file video; liên kết chuyên đề và lớp học; quyền `Video.View` / `Video.Manage` (`tinh-nang-kho-video-bai-giang.md`) |
