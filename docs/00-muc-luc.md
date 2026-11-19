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

Tài liệu khác trong `docs/` (không thuộc bộ đặc tả, chỉ để tham khảo):

| File | Nội dung |
|---|---|
| `HUONG_DAN_SU_DUNG_ELEARNING.md` / `.docx` | Hướng dẫn sử dụng cho người dùng cuối |
| `tinh-nang-kho-video-bai-giang.md`, `dong-bo-du-lieu-chuyen-de-va-lop-hoc-cho-video.md` | Ghi chú thiết kế kho video bài giảng |
| `CAP_NHAT_HE_THONG.md` | Nhật ký một đợt cập nhật (10/2026), gồm việc chuyển sang PostgreSQL |
| `../deploy/server/README.md` | Hướng dẫn cài và cập nhật máy chủ bằng image dựng sẵn |
| `../deploy/linux/README.md` | Máy chủ Linux build từ mã nguồn; cập nhật một lệnh bằng `deploy.cmd`; cấp quyền SSH cho thành viên |
| `../load-tests/README.md` | Cách chạy load test k6 |

**Hệ quản trị CSDL là PostgreSQL 17 (D-35).** Bản thiết kế đầu dùng SQL Server; nơi nào trong tài liệu còn ký hiệu SQL Server (chủ yếu là DDL ở `04a` / `04b`) thì đọc theo bảng quy đổi ở `04a` mục 1.

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
| 6 | Index thừa/thiếu, FK, cascade, rowversion, CHECK, cột cho Users/RefreshTokens, AttemptEvents | `04a`/`04b` |
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
| D-16 | Không bao giờ xóa lượt thi, câu trả lời, kết quả, snapshot, ảnh; user không xóa mà ẩn danh hóa; đề chỉ xóa được khi chưa từng publish. Câu hỏi / chuyên đề / nhóm ban đầu cũng không xóa cứng — phần này đã được nới bằng D-36 (`02`, `07`) Ngoại lệ: tài khoản khóa quá 90 ngày tự động bị xóa cứng cùng dữ liệu của họ (D-41). |
| D-17 | Nội dung câu hỏi hỗ trợ Markdown giới hạn (có code block), không cho HTML thô (`02`, `06`) |
| D-18 | Autosave dùng `clientSeq`, lưu cờ "đánh dấu xem lại", có batch save, backup trong `sessionStorage` (`06`) |
| D-19 | Không dùng MediatR / AutoMapper; dùng AwesomeAssertions thay FluentAssertions v8 (`03`) |
| D-20 | `ExamResults` là nguồn điểm duy nhất; `ExamAttempts` không lưu điểm (`04a`/`04b`) |
| D-21 | Lưu đáp án và nộp bài đều khóa dòng attempt trong transaction (`SELECT … FOR UPDATE` qua `IAttemptLock`) (`04a`/`04b`, `10`) |
| D-22 | Nhóm người dùng và gán đề nằm trong MVP (`01`, `02`) |
| D-23 | Application service dùng trực tiếp `IAppDbContext` (DbSet); repository riêng chỉ cho thao tác đặc biệt như khóa dòng. Application tham chiếu gói `Microsoft.EntityFrameworkCore` (`03`) |
| D-24 | Dùng lại refresh token vừa xoay vòng trong 30 giây được coi là hai tab refresh song song, không thu hồi cả chuỗi (`07`) |
| D-25 | `MustChangePassword` được chặn cả ở backend: mọi endpoint cần policy trả 403 `PASSWORD_CHANGE_REQUIRED` (`07`) |
| D-26 | Giám sát không thêm hạ tầng: meter `ELearning` + log `Monitoring` mỗi phút; cảnh báo qua `/health/alerts` (chặn ở Nginx) và container `monitor` gửi webhook; OpenTelemetry / Grafana để sau (`09`) |
| D-27 | Ảnh trong đề / lựa chọn / giải thích: file trên đĩa theo SHA-256 + bảng `MediaFiles` bất biến; nội dung lưu `media:<id>`; server ký URL ngắn hạn chỉ cho ảnh của các trường đang trả về (`02`, `04a`, `05`, `07`) |
| D-28 | Lớp học `Classrooms`: học viên ↔ lớp nhiều-nhiều (`ClassroomStudents`); gán đề cho lớp qua `ExamAssignments.ClassroomId`; chỉ lớp đang hoạt động và thành viên hiện tại mới thấy đề; xóa được lớp (kèm danh sách học viên) khi lớp không còn được gán cho đề nào, nếu không thì bỏ gán trước hoặc tắt lớp (`02`, `04a`, `05`, `07`) |
| D-29 | Nhập đề từ file Markdown `elearning-exam/1`: định dạng theo dòng `@trường:` đọc bằng quy tắc cố định (không AI trong hệ thống); việc chuyển Word / PDF / ảnh sang định dạng này giao cho AI bên ngoài bằng prompt có sẵn (`public/huong-dan/`). Công thức viết LaTeX `$...$`, hiển thị bằng KaTeX. Hình vẽ lấy từ file `.docx` gốc theo vị trí phần / câu (`hinh-p1-c3.png`), không qua AI; file còn `[?]`, câu nói "như hình vẽ" mà thiếu ảnh, hay lẫn "Ghi chú" của AI thì bị chặn (`02`, `05`, `06`) |
| D-30 | Đề thi có **chuyên đề** (`Exams.CategoryId`, có thể trống, sửa được như các trường của `Exams`). Có chuyên đề thì mọi câu trong đề (câu cố định, quy tắc ngẫu nhiên, ma trận) phải thuộc chuyên đề đó — kiểm tra khi thêm câu và khi đổi chuyên đề; trống thì không giới hạn. Xóa chuyên đề thì đề chỉ mất chuyên đề, không mất đề (`02`, `04a`, `05`) |
| D-31 | **Điểm danh & BTVN theo buổi** (trạng thái: có mặt, đi muộn, online — tính là có tham gia —, vắng có phép, vắng không phép; `AttendanceSessions` một buổi / lớp / ngày, `AttendanceRecords` mỗi học sinh một dòng, `StudentContacts` liên hệ phụ huynh). Quyền `Attendance.Take` = lớp mình là GVCN (`Classrooms.HomeroomTeacherId`), `Attendance.ManageAll` mở rộng ra mọi lớp; lớp ngoài phạm vi → 404. Vai trò `TEACHER` được cấp `Attendance.Take` đúng một lần khi permission được tạo. Lưu tự động từng thay đổi (ghi đè theo học sinh, không audit từng lần lưu; chỉ audit "làm mới buổi" và đổi liên hệ). Báo cáo Zalo soạn ở trình duyệt, không gửi đi đâu. Lớp đã có buổi điểm danh không xóa được (`02`, `04b`, `05`, `06`, `07`) |
| D-32 | **Chuyên đề 2 cấp** (`QuestionCategories.ParentId`): chuyên đề gốc có hoặc không có chuyên đề con; chuyên đề con không có con, cha phải là chuyên đề gốc, chuyên đề đang có con không làm con được. Chuyên đề có con chỉ để gom nhóm: không chứa câu hỏi (thêm / sửa / import câu vào đó → `CATEGORY_HAS_CHILDREN`), chuyên đề đang có câu không nhận chuyên đề con (`PARENT_HAS_QUESTIONS`); đề thi vẫn gắn được chuyên đề cha. "Thuộc chuyên đề X" = thuộc X hoặc chuyên đề con của X: lọc câu hỏi / đề / video, quy tắc ngẫu nhiên, ma trận và kiểm tra D-30 đều tính cả chuyên đề con. Chuyên đề còn chuyên đề con không xóa được (`CATEGORY_HAS_CHILDREN`) (`04a`, `05`) |
| D-33 | **Loại đề** (`Exams.Kind`): `GRADED` đề tính điểm (giữ nguyên lượt thi, thời lượng, lịch) và `PRACTICE` đề luyện: làm lại không giới hạn (bỏ qua `MaxAttempts` và lượt cấp thêm, API học viên trả `maxAttempts = 0`), không tính giờ (`ExpiredAt` như thời lượng 0: chỉ bị chặn bởi thời điểm đóng đề, API trả `durationMinutes = 0`), được phép "xem lại ngay sau khi nộp" dù làm nhiều lượt. Lịch mở / đóng đề, quyền dự thi, chấm điểm giữ nguyên (`04a`, `05`) |
| D-34 | **Một lần đăng nhập tối đa `Jwt:SessionMaxHours` giờ (mặc định 12)**, tính từ lúc nhập mật khẩu: refresh token xoay vòng giữ nguyên mốc hết hạn của lần đăng nhập (không gia hạn trượt), hết mốc thì `POST /api/auth/refresh` trả `TOKEN_INVALID` và phải đăng nhập lại. `RefreshTokenDays` vẫn là trần; `SessionMaxHours = 0` = gia hạn trượt như trước (`07`, `09`) |
| D-35 | **PostgreSQL 17 thay SQL Server** (commit `9dbd1e5`, 09/10/2026; migration bắt đầu lại từ `InitialPostgreSql`). Provider Npgsql; `timestamptz`; `RowVersion` là `bytea` do ứng dụng ghi; khóa dòng bằng `SELECT … FOR UPDATE`; trùng khóa nhận biết qua SQLSTATE `23505`; tìm kiếm bằng `ILIKE`; mã câu hỏi từ `nextval` và `pg_advisory_xact_lock`. Test chạy trên PostgreSQL thật (`ELEARNING_TEST_POSTGRES` hoặc Testcontainers). Tầng Application tham chiếu gói Npgsql để dùng `EF.Functions.ILike` (nới D-23). **Chưa chuyển xong:** script backup / khôi phục và tài khoản DB quyền tối thiểu (`09` mục 6, 10) (`03`, `04a`, `08`, `09`, `10`) |
| D-36 | **Hiện trạng code — xóa cứng trong ngân hàng và danh mục** (nới D-16): `DELETE /api/questions/{id}` và `DELETE /api/question-categories/{id}` xóa cứng; đang được dùng thì trả 409 (`QUESTION_IN_USE`, `CATEGORY_HAS_QUESTIONS`) và chỉ xóa khi gọi lại với `?force=true`. Snapshot trong `ExamQuestions` giữ nguyên nội dung, chỉ mất liên kết `SourceQuestionId`; đề chỉ mất chuyên đề. `DELETE /api/groups/{id}` xóa nhóm kèm thành viên và các dòng gán đề của nhóm. Đều có audit (`QUESTION_DELETED`, `CATEGORY_DELETED`, `GROUP_DELETED`). Lượt thi, kết quả, snapshot, user, ảnh vẫn không bao giờ bị xóa (`02` mục 10, `05`) |
| D-37 | **Hiện trạng code — đổi `ReviewPolicy` sau khi publish:** `PATCH /api/exams/{id}/review-policy` đặt `ReviewPolicy` cho mọi version của đề (kể cả PUBLISHED / ARCHIVED) để mở xem lại bài sau khi thi; không qua các kiểm tra chéo của publish, audit `EXAM_UPDATED`. Là ngoại lệ thứ hai của nguyên tắc version bất biến, bên cạnh D-11; không ảnh hưởng điểm (`02` mục 7, `04a`, `05`) |
| D-38 | **Đề luyện gắn với video** (`VideoLessons.PracticeExamId`, chỉ đề loại `PRACTICE`). Học viên bấm "Luyện đề" (`POST /api/student/videos/{id}/practice`): đề giới hạn người thi mà học viên chưa thấy thì được gán trực tiếp cho học viên. Đã làm đề này thì trả `alreadyAttempted` để giao diện hỏi làm lại; làm lại (`restart = true`) = **hủy** (CANCELLED, không xóa cứng — D-16) các lượt cũ của chính học viên, lượt tự hủy không hiện ở trang đề và lịch sử của học viên, quản trị vẫn tra được. Xóa đề nháp thì chỉ gỡ liên kết khỏi video (`04a`, `05`) |
| D-39 | **Bài tập về nhà của video** (`VideoLessons.HomeworkUrl`, link PDF Google Drive / Docs, tối đa 1000 ký tự, mã lỗi `INVALID_HOMEWORK_URL`). Học viên bấm "Bài tập về nhà" (cạnh "Luyện đề") để xem PDF trong cửa sổ trên trang, có nút mở trong Drive. Thêm và sửa video đều nhập được; để trống khi sửa là gỡ (`04a`, `05`) |
| D-40 | **Khóa / mở khóa tài khoản hàng loạt** (`POST /api/users/bulk-status`, quyền `User.Update`, tối đa 1000 tài khoản mỗi lần). Trang Người dùng có ô tích ở đầu dòng, ô chọn cả trang, và "Tích cả lớp" (frontend lấy hết học viên của lớp rồi gửi danh sách id). Bỏ qua tài khoản của chính mình; khóa thì thu hồi mọi phiên đăng nhập; mỗi tài khoản đổi trạng thái có một dòng audit `UserStatusChanged` (`Bulk = true`). Khóa bắt buộc nhập lý do (`05`) |
| D-41 | **Tự động xóa cứng tài khoản khóa quá 90 ngày** (nới D-16 theo yêu cầu chủ sản phẩm 19/11/2026). `Users.DeactivatedAt` ghi lúc khóa (mở khóa → null); tài khoản đang khóa trước khi cập nhật tính từ ngày chạy migration `UserDeactivatedAt`. Job `LockedUserCleanupWorker` (mỗi `UserRetention:SweepIntervalMinutes`, mặc định 6 giờ; `UserRetention:DeleteLockedAfterDays` = 90, 0 = tắt) xóa **vĩnh viễn** tài khoản cùng lượt thi, câu trả lời, kết quả, lịch sử điểm, điểm danh, liên hệ, thành viên lớp / nhóm, gán đề, vai trò, phiên đăng nhập; cột người sửa / hủy / chấm / chủ nhiệm ở dữ liệu khác đặt NULL. **Không** xóa tài khoản là tác giả nội dung chung (lớp, đề, câu hỏi, chuyên đề, video, buổi điểm danh, ảnh, nhóm, sửa đáp án, gán đề cho người khác): giữ lại và ghi log Warning. `AuditLogs` giữ nguyên, thêm dòng `USER_DELETED` (`04a`, `09`) |
| D-42 | **Vai trò Trợ giảng** (`TEACHING_ASSISTANT`, "Trợ giảng", vai trò hệ thống do seed tạo). **Không có quyền mặc định**: quản trị tự tích quyền ở trang Vai trò & quyền và gán vai trò cho người dùng; chưa gắn trợ giảng vào lớp (`07`) |
| D-43 | **SĐT học sinh** (`StudentContacts.StudentPhone`, cùng quy tắc với SĐT phụ huynh). Trang chi tiết người dùng có thẻ "Liên hệ học sinh & phụ huynh" (`PUT /api/users/{id}/contact` `{ studentPhone, parentName, parentPhone }`, quyền `User.Update`; `UserDetailDto` thêm 3 trường); trang Điểm danh hiện "HS: …" và sửa được trong hộp Liên hệ. Ẩn danh hóa xóa cả SĐT học sinh. File import tài khoản có thêm 3 cột không bắt buộc `SĐT học sinh`, `Tên phụ huynh`, `SĐT phụ huynh`; cột Vai trò thêm số 3 = Trợ giảng (`04b`, `05`) |
| D-44 | **Thêm video từ Excel** (`GET /api/videos/import/template`, `POST /api/videos/import?dryRun=`, quyền `Video.Manage`, tối đa 5 MB / 200 video). Sheet `Video`: chỉ bắt buộc cột Link video — dán link YouTube là đủ, tiêu đề tự lấy từ YouTube (oEmbed; không có mô tả). Cột không bắt buộc: Tiêu đề (đặt tên khác; link Drive / file video thì bắt buộc), Link tài liệu PDF, Link bài tập về nhà, Mã chuyên đề, Lớp (mã, nhiều lớp cách nhau dấu phẩy), Mã đề luyện (đề loại PRACTICE). Không có cột Mô tả. Mỗi dòng qua đúng validator của thêm video. Tất cả hoặc không, giống import tài khoản (`05`) |
