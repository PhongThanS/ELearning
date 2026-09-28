# 07 — Bảo mật

## 1. Yêu cầu bắt buộc

- HTTPS ở mọi môi trường ngoài máy dev; bật HSTS ở production.
- Mật khẩu và refresh token phải được băm.
- Mọi endpoint cần bảo vệ đều có `[Authorize]` kèm policy. Mặc định là **từ chối**: dùng `FallbackPolicy = RequireAuthenticatedUser`, endpoint công khai phải khai báo `[AllowAnonymous]` rõ ràng.
- Validation ở server cho mọi input.
- Không tin client về: điểm, timer, role, trạng thái nộp bài.
- Không trả đáp án đúng cho học viên trước khi chính sách cho phép.
- Không để lộ stack trace; thông báo đăng nhập sai luôn chung chung.
- Có rate limit; có audit các thao tác quan trọng.

## 2. Mật khẩu

- Băm bằng `PasswordHasher<User>` của ASP.NET Core (PBKDF2). Khi `VerifyHashedPassword` trả về `SuccessRehashNeeded` thì băm lại.
- **Cấm** dùng `SHA256(password)`, `MD5(password)`, `Base64(password)` hoặc bất kỳ cách lưu có thể đảo ngược nào.
- Chính sách mật khẩu: tối thiểu 8 ký tự, tối đa 128, không được trùng username. Không bắt buộc ký tự đặc biệt *(cần xác nhận)*.
- **Khóa tài khoản:** sai 5 lần liên tiếp thì khóa 15 phút (`AccessFailedCount`, `LockoutEnd`). Đăng nhập thành công thì reset bộ đếm.
- Admin đặt lại mật khẩu: sinh mật khẩu tạm ngẫu nhiên, bật `MustChangePassword`, đổi `SecurityStamp`, thu hồi mọi refresh token.

## 3. Token (D-15)

### 3.1 Access token
- JWT ký bằng **HS256** với khóa ngẫu nhiên ≥ 256 bit, lấy từ secret store. Khi có nhiều dịch vụ cần xác minh token thì chuyển sang RS256.
- Hiệu lực **15 phút**. Claims: `sub`, `name`, `role` (chỉ để phục vụ UX), `sstamp` (SecurityStamp), `jti`.
- Kiểm tra `iss`, `aud`, `exp`, `nbf`; `ClockSkew = 30s`.
- Frontend giữ access token trong bộ nhớ.

### 3.2 Refresh token
- Là 32 byte ngẫu nhiên (`RandomNumberGenerator`), mã hóa base64url. **DB chỉ lưu SHA-256 hex.**
- Hiệu lực 7 ngày.
- Gửi qua cookie `elearning_rt` với các thuộc tính: `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`.
- **Xoay vòng:** mỗi lần refresh thì thu hồi token cũ (`ROTATED`, ghi `ReplacedByTokenId`) và cấp token mới cùng `FamilyId`.
- **Phát hiện dùng lại:** nếu một token đã bị thu hồi mà vẫn được gửi lên, thu hồi **cả family** (`REUSE_DETECTED`), ghi audit `REFRESH_TOKEN_REUSE`, buộc đăng nhập lại.
- Thu hồi mọi token của user khi: đổi mật khẩu, admin đặt lại mật khẩu, vô hiệu hóa tài khoản, đổi vai trò.

### 3.3 CSRF
- Endpoint `/api/auth/refresh` và `/api/auth/logout` xác thực bằng cookie. Hai endpoint này được bảo vệ bằng:
  - `SameSite=Strict`.
  - Header bắt buộc `X-Requested-With: XMLHttpRequest`.
  - Kiểm tra `Origin` phải nằm trong danh sách cho phép.
- Các API còn lại xác thực bằng header `Authorization`, nên không bị CSRF.

### 3.4 Thu hồi tức thời
Mỗi request, middleware kiểm tra user từ cache 5 phút:
- `IsActive = true`.
- `sstamp` trong token trùng `SecurityStamp` hiện tại.

Nhờ đó, vô hiệu hóa tài khoản hoặc đổi quyền có hiệu lực tối đa sau 5 phút (xóa cache ngay khi đổi thì hiệu lực tức thì), thay vì phải chờ tới 15 phút.

## 4. Phân quyền (D-14)

- Mỗi permission là một policy (`[Authorize(Policy = Permissions.ExamPublish)]`), được đăng ký tự động từ danh sách hằng số.
- Permission của user được tra ở server (join `UserRoles`, `RolePermissions`) và cache bằng `IMemoryCache` 5 phút, key theo `UserId`. Xóa cache khi đổi vai trò hoặc permission.
- **Không nhét permission vào JWT**, vì token sẽ to và quyền đổi không có hiệu lực ngay.
- Frontend lấy danh sách permission từ `/api/auth/me`, chỉ để ẩn/hiện UI.
- Policy `StudentOnly`: có role `STUDENT` và tài khoản đang hoạt động.
- **Kiểm tra quyền sở hữu** (lượt thi thuộc về ai) nằm ở application service, không nằm ở attribute. Tài nguyên của người khác → 404.
- Vai trò tương lai `TEACHER` (chỉ quản lý đề của mình): thêm kiểm tra theo tài nguyên (`CreatedBy`, hoặc bảng sở hữu). Thiết kế hiện tại không cản trở việc này.

### 4.1 Danh sách permission

```text
User.View  User.Create  User.Update  User.ResetPassword  User.Anonymize
Group.View  Group.Manage
Role.View  Role.Manage  Role.Assign
Category.View  Category.Manage
Question.View  Question.Create  Question.Update
Exam.View  Exam.Create  Exam.Update  Exam.Delete  Exam.Publish  Exam.Close  Exam.Assign  Exam.Regrade
Attempt.View  Attempt.Manage
Result.View  Result.Export
Report.View
Audit.View
```

So với spec gốc:
- Bỏ `User.Delete` và `Question.Delete` (D-16).
- Bỏ `Exam.Result`, vì tên khó hiểu; thay bằng `Result.View` và `Result.Export`.

## 5. Rate limit

Dùng `Microsoft.AspNetCore.RateLimiting`. Key theo IP với các endpoint chưa đăng nhập, theo `UserId` với các endpoint còn lại.

| Policy | Áp dụng | Giới hạn *(cần xác nhận)* |
|---|---|---|
| `auth-login` | `/api/auth/login`, `/register`, `/forgot-password` | 10 request / phút / IP |
| `auth-refresh` | `/api/auth/refresh` | 30 request / phút / IP |
| `attempt-write` | lưu đáp án, sự kiện | 60 request / phút / user (token bucket, cho phép dồn 20) |
| `attempt-action` | start, submit | 10 request / phút / user |
| `default` | mọi endpoint khác | 300 request / phút / user |

- Vượt giới hạn → 429 `RATE_LIMITED`, kèm `Retry-After`.
- Khi chạy sau Nginx: cấu hình `ForwardedHeaders` với `KnownProxies` / `KnownNetworks` để lấy đúng IP thật. **Không** tin header `X-Forwarded-For` từ nguồn bất kỳ.

## 6. Chống lộ đáp án

- DTO của học viên là các class **riêng**, không có trường đáp án. Không dùng chung DTO với admin, không dùng thuộc tính `[JsonIgnore]` có điều kiện.
- Map DTO tường minh, không dùng AutoMapper, để không vô tình kéo theo trường nhạy cảm.
- Query của học viên chỉ `Select` các cột được phép; không tải `IsCorrect` hay đáp án chấp nhận lên bộ nhớ khi không cần.
- Mọi endpoint của học viên phải có **test** khẳng định JSON trả về không chứa các khóa: `isCorrect`, `acceptedAnswers`, `correctAnswerNumber`, `numericTolerance`, `explanation`. Riêng endpoint kết quả chỉ được chứa các khóa này khi chính sách hiển thị cho phép.
- Preview của admin dùng endpoint khác, có permission `Exam.View`.

## 7. Input và nội dung

- Nội dung câu hỏi hỗ trợ Markdown, render **không có HTML thô**. Server không cần sanitize HTML, nhưng phải:
  - Giới hạn độ dài.
  - Loại bỏ ký tự điều khiển (trừ `\n`, `\t`).
  - Chuẩn hóa NFC.
- Nếu sau này cho phép HTML: sanitize ở server theo whitelist tag (ví dụ `HtmlSanitizer`), sanitize thêm ở client, loại bỏ script và thuộc tính sự kiện.
- **Content Security Policy** (qua Nginx):
  ```text
  default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;
  connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'
  ```
- Header bổ sung: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`.
- SQL: chỉ dùng truy vấn tham số hóa (EF Core, Dapper với tham số). Không nối chuỗi từ input.
- Upload file (sau MVP): kiểm tra MIME và kích thước, lưu ở object storage, truy cập qua signed URL.

## 8. CORS

- Production: frontend và API **cùng origin** (Nginx route `/api`), nên không cần CORS. Nếu buộc phải khác origin thì chỉ cho phép đúng origin của frontend, `AllowCredentials`, **không** dùng `AllowAnyOrigin`.
- Development: dùng Vite proxy nên cũng cùng origin. Có thể thêm `http://localhost:5173` vào CORS dự phòng.

## 9. Audit

Audit được ghi **trong cùng transaction** với thao tác. `OldValue` / `NewValue` là JSON đã loại bỏ các trường: `PasswordHash`, `SecurityStamp`, token, và toàn bộ đáp án đúng khi ghi thao tác của học viên.

| Nhóm | Action |
|---|---|
| Xác thực | `USER_LOGIN`, `USER_LOGIN_FAILED`, `USER_LOCKED_OUT`, `USER_LOGOUT`, `USER_REGISTER`, `PASSWORD_CHANGED`, `PASSWORD_RESET_BY_ADMIN`, `REFRESH_TOKEN_REUSE` |
| Người dùng | `USER_CREATED`, `USER_UPDATED`, `USER_STATUS_CHANGED`, `USER_ROLES_CHANGED`, `USER_ANONYMIZED` |
| Nhóm / vai trò | `GROUP_CREATED`, `GROUP_UPDATED`, `GROUP_MEMBERS_CHANGED`, `ROLE_CREATED`, `ROLE_UPDATED`, `ROLE_PERMISSIONS_CHANGED` |
| Ngân hàng câu hỏi | `CATEGORY_CREATED`, `CATEGORY_UPDATED`, `CATEGORY_STATUS_CHANGED`, `QUESTION_CREATED`, `QUESTION_UPDATED`, `QUESTION_STATUS_CHANGED` |
| Đề thi | `EXAM_CREATED`, `EXAM_UPDATED`, `EXAM_DELETED`, `EXAM_VERSION_CREATED`, `EXAM_PUBLISHED`, `EXAM_CLOSED`, `EXAM_REOPENED`, `EXAM_ASSIGNMENTS_CHANGED` |
| Đáp án | `ANSWER_KEY_CORRECTED`, `QUESTION_VOIDED`, `EXAM_REGRADED` |
| Lượt thi | `ATTEMPT_STARTED`, `ATTEMPT_SUBMITTED`, `ATTEMPT_AUTO_SUBMITTED`, `ATTEMPT_EXTENDED`, `ATTEMPT_FORCE_SUBMITTED`, `ATTEMPT_CANCELLED`, `ATTEMPTS_GRANTED` |
| Kết quả | `RESULT_CREATED`, `RESULTS_EXPORTED` |

Không audit từng lần lưu đáp án, vì khối lượng quá lớn; dữ liệu đó đã nằm trong `AttemptAnswers.SaveCount` và `AnsweredAt`.

## 10. Chống gian lận

Không tuyên bố chống gian lận ở mức trình duyệt là tuyệt đối. Các biện pháp dưới đây mang tính **răn đe và ghi nhận**.

- **MVP:**
  - Tính giờ và nộp bài ở server; giới hạn lượt; không lộ đáp án.
  - Chính sách xem đáp án (D-09); ghi `AttemptEvents`, IP, user agent.
  - Phát hiện nhiều tab; `sessionStorage` cho phòng máy dùng chung.
- **Sau MVP:** xáo thứ tự câu / đáp án, pool câu hỏi, yêu cầu toàn màn hình, giới hạn IP / mạng cho phòng thi, cảnh báo lượt thi bất thường.

## 11. Dữ liệu cá nhân

Tham chiếu: Nghị định 13/2023/NĐ-CP và Luật Bảo vệ dữ liệu cá nhân (cần pháp chế xác nhận chi tiết).

- Chỉ thu thập: username, email, họ tên, IP / user agent khi thi.
- Form đăng ký có ô đồng ý điều khoản và chính sách dữ liệu.
- **Ẩn danh hóa** (`POST /api/users/{id}/anonymize`), thực hiện khi user yêu cầu xóa dữ liệu:
  - `FullName = "Người dùng đã xóa"`.
  - `UserName` và `Email` đổi thành `deleted-{Id}`.
  - `PasswordHash = NULL`, `IsActive = 0`, `AnonymizedAt = now`.
  - Xóa IP / user agent trong `ExamAttempts`, `AttemptEvents`, `RefreshTokens` của user này.
  - Giữ nguyên lượt thi và kết quả, vì không còn liên kết được tới người thật.
- **Thời gian lưu:** lượt thi và kết quả giữ vô thời hạn *(cần xác nhận)*; `AttemptEvents` 1 năm; `AuditLogs` 2 năm.

## 12. Logging

- **Ghi:** khởi động, request lỗi, xác thực thất bại, thao tác nghiệp vụ quan trọng, lỗi chấm điểm, lỗi DB, job tự nộp (số lượt đã xử lý).
- **Không bao giờ ghi:** mật khẩu, access / refresh token, header `Authorization`, cookie, nội dung câu trả lời, đáp án đúng.
- Cấu hình Serilog destructuring để che các thuộc tính có tên `Password`, `Token`, `Secret`.

## 13. Quyết định / Giả định

- **D-14:** permission được tra ở server có cache, thay vì để trong JWT.
- **D-15:** refresh token nằm trong cookie `SameSite=Strict`, access token trong bộ nhớ, như phương án "bảo mật cao" của spec gốc §79. Phương án này yêu cầu frontend và API cùng site; điều kiện đó được đảm bảo bằng Nginx và Vite proxy.
- **Thêm `SecurityStamp`, khóa tài khoản, phát hiện dùng lại refresh token.** Spec gốc có yêu cầu xoay vòng nhưng chưa có cơ chế bảo vệ.
- **D-10:** `Auth:AllowSelfRegistration = false` ở Production. Tài khoản do admin tạo (import Excel là tính năng sau MVP).
- **Danh sách audit hợp nhất** từ spec gốc §31 và §119, bổ sung các thao tác mới (chấm lại, gán đề, thao tác trên lượt thi).
- **Giới hạn rate limit và chính sách mật khẩu** là giả định *(cần xác nhận)*.
