# 05 — API

## 1. Quy ước chung

- **Base path:** `/api`. Chưa đánh version trên URL. Khi phá vỡ tương thích thì thêm `/api/v2`.
- **JSON:** thuộc tính dạng `camelCase`. Enum dạng `UPPER_SNAKE_CASE` (`JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)`).
- **Thời gian:** ISO 8601 UTC, **luôn có hậu tố `Z`**, ví dụ `"2026-09-26T03:00:00.000Z"`.
- **Số điểm:** `decimal`, trả về dạng JSON number với tối đa 2 chữ số thập phân.
- **URL dùng danh từ.** Endpoint hành động chỉ dùng cho chuyển trạng thái (`/publish`, `/close`, `/start`, `/submit`…).
- **Mọi endpoint phải định nghĩa:** method, route, yêu cầu xác thực, permission, request, response, lỗi validation, lỗi nghiệp vụ. OpenAPI được sinh từ backend.

## 2. Hợp đồng response (D-13)

**Mọi** response, kể cả lỗi validation mặc định của ASP.NET Core, đều dùng wrapper sau. Cấu hình `InvalidModelStateResponseFactory` và middleware xử lý exception để đảm bảo điều này.

```json
{ "success": true, "data": { }, "message": null, "errors": [], "traceId": "00-4bf9..." }
```

Lỗi validation (400):
```json
{
  "success": false, "data": null, "message": "Dữ liệu không hợp lệ.",
  "errors": [ { "field": "options", "code": "SINGLE_CHOICE_REQUIRES_ONE_CORRECT", "message": "Câu chọn một phải có đúng 1 đáp án đúng." } ],
  "traceId": "00-..."
}
```

Lỗi nghiệp vụ (422):
```json
{
  "success": false, "data": null, "message": "Lượt thi đã hết giờ.",
  "errors": [ { "field": null, "code": "ATTEMPT_EXPIRED", "message": "Lượt thi đã hết giờ." } ],
  "traceId": "00-..."
}
```

- `message` là tiếng Việt, **chỉ dùng để hiển thị**. Frontend quyết định hành vi dựa trên `errors[].code`.
- **204** không có body, chỉ dùng cho `logout`.

## 3. HTTP status

| Status | Khi nào |
|---|---|
| 200 | Thành công, kể cả khi `start` trả về lượt đang làm (`resumed: true`) và khi `submit` gọi lại (idempotent) |
| 201 | Tạo mới tài nguyên (kèm header `Location`) |
| 204 | Thành công, không có body |
| 400 | Request sai định dạng, hoặc vi phạm quy tắc của một trường (FluentValidation) |
| 401 | Chưa xác thực, hoặc token hết hạn / không hợp lệ |
| 403 | Đã xác thực nhưng thiếu permission |
| 404 | Không tồn tại, **hoặc tồn tại nhưng thuộc người khác / không có quyền xem** (không để lộ sự tồn tại) |
| 409 | Xung đột trạng thái hoặc dữ liệu: `RowVersion` cũ, trùng `Code`, trạng thái không cho phép thao tác |
| 422 | Vi phạm quy tắc nghiệp vụ (đề chưa mở, hết lượt, hết giờ, publish không hợp lệ…) |
| 429 | Vượt rate limit (kèm header `Retry-After`) |
| 500 | Lỗi không lường trước. Trả `INTERNAL_ERROR` và `traceId`, **không có stack trace** |

**Quy tắc phân biệt 400 và 422:** lỗi có thể xác định chỉ bằng cách nhìn request (thiếu trường, sai độ dài, số option đúng sai) → 400. Lỗi cần trạng thái hệ thống để xác định → 422.

## 4. Danh mục mã lỗi

| Code | HTTP | Ý nghĩa |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Lỗi validation chung (chi tiết nằm trong `errors[]`) |
| `INVALID_CREDENTIALS` | 401 | Sai tài khoản hoặc mật khẩu (thông báo chung, không nói sai phần nào) |
| `ACCOUNT_LOCKED` | 401 | Tài khoản tạm khóa do đăng nhập sai nhiều lần |
| `ACCOUNT_DISABLED` | 401 | Tài khoản đã bị vô hiệu hóa |
| `TOKEN_INVALID` | 401 | Access / refresh token không hợp lệ hoặc hết hạn |
| `REGISTRATION_DISABLED` | 403 | Đang tắt chức năng tự đăng ký |
| `FORBIDDEN` | 403 | Thiếu permission |
| `NOT_FOUND` | 404 | Chung. Có thêm biến thể `EXAM_NOT_FOUND`, `ATTEMPT_NOT_FOUND`, `QUESTION_NOT_FOUND` |
| `CONCURRENCY_CONFLICT` | 409 | Dữ liệu đã bị người khác sửa |
| `DUPLICATE_CODE` | 409 | Trùng `Code` |
| `ATTEMPT_NOT_IN_PROGRESS` | 409 | Lượt thi đã nộp hoặc đã hủy |
| `VERSION_IMMUTABLE` | 409 | Sửa version không còn ở DRAFT |
| `EXAM_NOT_DRAFT` | 409 | Thao tác chỉ cho phép khi đề ở DRAFT (ví dụ xóa đề) |
| `DRAFT_VERSION_EXISTS` | 409 | Đã có version DRAFT |
| `RETAKE_POLICY_LOCKED` | 409 | Đổi `RetakeScoringPolicy` khi đề đã có lượt thi |
| `EXAM_NOT_AVAILABLE` | 422 | Đề chưa mở, đã đóng hoặc ngoài khung giờ |
| `MAX_ATTEMPTS_EXCEEDED` | 422 | Hết lượt |
| `ATTEMPT_EXPIRED` | 422 | Hết giờ (đã qua ân hạn); lượt thi đã được tự nộp |
| `INVALID_OPTION` | 422 | Mã option không thuộc câu hỏi |
| `INVALID_ANSWER_SHAPE` | 422 | Câu trả lời không khớp loại câu hỏi |
| `INVALID_NUMBER_FORMAT` | 422 | Câu điền số sai định dạng |
| `PUBLISH_VALIDATION_FAILED` | 422 | Không publish được; `errors[]` liệt kê toàn bộ lỗi |
| `INVALID_STATE_TRANSITION` | 422 | Chuyển trạng thái không hợp lệ |
| `RESULT_NOT_AVAILABLE` | 422 | Chưa đến lúc được xem điểm / xem lại bài |
| `RATE_LIMITED` | 429 | Gửi quá nhiều request |
| `INTERNAL_ERROR` | 500 | Lỗi hệ thống |

Mã lỗi là hằng số trong `ELearning.Shared.ErrorCodes`. Frontend có file map tương ứng sang thông điệp i18n.

## 5. Phân trang, sắp xếp, lọc

- Request: `?page=1&pageSize=20&sortBy=createdAt&sortDir=desc&keyword=...`
- `page ≥ 1`. `pageSize` từ 1 đến **100**; giá trị lớn hơn bị ép về 100.
- `sortBy` chỉ nhận các giá trị trong **whitelist** của từng endpoint; giá trị lạ → 400.
- Response:
```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 150, "totalPages": 8 }
```
- Không endpoint nào được trả danh sách không giới hạn.
- Tìm kiếm dùng tham số hóa (`LIKE @kw` với ký tự `%`, `_`, `[` đã được escape).

## 6. Endpoint

Ký hiệu cột "Quyền": `—` = không cần đăng nhập; `Auth` = đã đăng nhập; `Student` = policy `StudentOnly`; còn lại là permission code.

### 6.1 Xác thực
| Method | Route | Quyền | Ghi chú |
|---|---|---|---|
| POST | `/api/auth/register` | — | Chỉ khi `AllowSelfRegistration = true`; gán role `STUDENT` |
| POST | `/api/auth/login` | — | Trả access token; set cookie refresh token |
| POST | `/api/auth/refresh` | cookie | Xoay vòng refresh token; yêu cầu header `X-Requested-With: XMLHttpRequest` |
| POST | `/api/auth/logout` | cookie | Thu hồi refresh token hiện tại; 204 |
| GET | `/api/auth/me` | Auth | User, roles, **permissions**, `mustChangePassword` |
| POST | `/api/auth/change-password` | Auth | Thu hồi mọi refresh token khác |
| POST | `/api/auth/forgot-password` | — | Luôn trả 200 (không để lộ email có tồn tại). Gửi email là tính năng sau MVP; MVP: admin đặt lại mật khẩu |

Response của login và refresh:
```json
{ "accessToken": "eyJ...", "expiresAt": "2026-09-26T03:15:00.000Z",
  "user": { "id": "...", "userName": "student01", "fullName": "Nguyễn Văn A",
            "roles": ["STUDENT"], "permissions": [], "mustChangePassword": false } }
```
Refresh token **không nằm trong body**; nó được set qua `Set-Cookie` (xem `07-bao-mat.md`).

### 6.2 Người dùng, nhóm, vai trò
| Method | Route | Quyền |
|---|---|---|
| GET | `/api/users` (lọc `keyword`, `roleCode`, `groupId`, `isActive`) | `User.View` |
| GET | `/api/users/{id}` | `User.View` |
| POST | `/api/users` | `User.Create` |
| PUT | `/api/users/{id}` | `User.Update` |
| PATCH | `/api/users/{id}/status` `{ isActive, reason }` | `User.Update` |
| PUT | `/api/users/{id}/roles` `{ roleIds }` | `Role.Assign` |
| POST | `/api/users/{id}/reset-password` → trả mật khẩu tạm, bật `MustChangePassword` | `User.ResetPassword` |
| POST | `/api/users/{id}/anonymize` `{ reason }` | `User.Anonymize` |
| GET/POST | `/api/groups` | `Group.View` / `Group.Manage` |
| GET/PUT | `/api/groups/{id}` | `Group.View` / `Group.Manage` |
| PATCH | `/api/groups/{id}/status` | `Group.Manage` |
| GET | `/api/groups/{id}/members` | `Group.View` |
| POST | `/api/groups/{id}/members` `{ userIds }` | `Group.Manage` |
| DELETE | `/api/groups/{id}/members/{userId}` | `Group.Manage` |
| GET | `/api/roles`, `/api/permissions` | `Role.View` |
| POST/PUT | `/api/roles`, `/api/roles/{id}` | `Role.Manage` (không sửa được role `IsSystem`) |
| PUT | `/api/roles/{id}/permissions` `{ permissionIds }` | `Role.Manage` |

### 6.3 Danh mục và câu hỏi
| Method | Route | Quyền |
|---|---|---|
| GET | `/api/question-categories` (có `isActive`, phân trang) | `Category.View` |
| GET/POST/PUT | `/api/question-categories[/{id}]` | `Category.View` / `Category.Manage` |
| PATCH | `/api/question-categories/{id}/status` | `Category.Manage` |
| GET | `/api/questions` (lọc `categoryId`, `questionType`, `keyword`, `isActive`; sort `code`, `createdAt`, `updatedAt`) | `Question.View` |
| GET | `/api/questions/{id}` | `Question.View` |
| POST | `/api/questions` | `Question.Create` |
| PUT | `/api/questions/{id}` (kèm `rowVersion`) | `Question.Update` |
| PATCH | `/api/questions/{id}/status` | `Question.Update` |
| POST | `/api/questions/{id}/clone` | `Question.Create` |

Không có `DELETE` cho câu hỏi và danh mục (D-16).

Ví dụ tạo câu hỏi:
```json
{
  "categoryId": "...", "code": "Q001", "content": "Thủ đô Việt Nam là?", "contentFormat": "PLAIN",
  "questionType": "SINGLE_CHOICE", "defaultScore": 1, "explanation": null,
  "options": [
    { "optionCode": "A", "content": "Hà Nội", "isCorrect": true },
    { "optionCode": "B", "content": "Huế", "isCorrect": false }
  ]
}
```
```json
{ "code": "Q002", "content": "Thủ đô Việt Nam?", "questionType": "FILL_IN", "answerDataType": "TEXT",
  "acceptedAnswers": ["Hà Nội", "Thành phố Hà Nội"], "caseSensitive": false, "ignoreAccent": true, "defaultScore": 1 }
```
```json
{ "code": "Q003", "content": "2 + 2 = ?", "questionType": "FILL_IN", "answerDataType": "NUMBER",
  "correctAnswerNumber": 4, "numericTolerance": 0, "defaultScore": 1 }
```

### 6.4 Đề thi và version
| Method | Route | Quyền |
|---|---|---|
| GET | `/api/exams` (lọc `status`, `keyword`) | `Exam.View` |
| GET | `/api/exams/{id}` (gồm version PUBLISHED / DRAFT hiện tại) | `Exam.View` |
| POST | `/api/exams` (tạo đề DRAFT kèm version 1 DRAFT) | `Exam.Create` |
| PUT | `/api/exams/{id}` (các trường của `Exams`, kèm `rowVersion`) | `Exam.Update` |
| DELETE | `/api/exams/{id}` (chỉ khi chưa từng publish) | `Exam.Delete` |
| POST | `/api/exams/{id}/clone` (**clone thành một đề mới**, lấy version mới nhất) | `Exam.Create` |
| POST | `/api/exams/{id}/close` `{ forceSubmitInProgress }` | `Exam.Close` |
| POST | `/api/exams/{id}/reopen` | `Exam.Close` |
| GET | `/api/exams/{id}/versions` | `Exam.View` |
| POST | `/api/exams/{id}/versions` `{ copyFromVersionId? }` (tạo version DRAFT mới) | `Exam.Update` |
| GET | `/api/exams/{id}/versions/{versionId}` | `Exam.View` |
| PUT | `/api/exams/{id}/versions/{versionId}` (cấu hình version, chỉ khi DRAFT) | `Exam.Update` |
| DELETE | `/api/exams/{id}/versions/{versionId}` (chỉ khi DRAFT) | `Exam.Update` |
| GET | `/api/exams/{id}/versions/{versionId}/preview` (hiển thị đúng như học viên thấy) | `Exam.View` |
| POST | `/api/exams/{id}/versions/{versionId}/validate` (kiểm tra thử, không publish) | `Exam.Update` |
| POST | `/api/exams/{id}/versions/{versionId}/publish` | `Exam.Publish` |
| GET | `/api/exams/{id}/assignments` | `Exam.View` |
| PUT | `/api/exams/{id}/assignments` `{ groupIds, userIds }` | `Exam.Assign` |
| PUT | `/api/exams/{id}/user-overrides/{userId}` `{ extraAttempts, note }` | `Attempt.Manage` |

### 6.5 Câu hỏi trong version (chỉ khi version là DRAFT)
| Method | Route | Quyền |
|---|---|---|
| GET | `/api/exams/{id}/versions/{versionId}/questions` (kèm cờ `sourceChanged`) | `Exam.View` |
| POST | `/api/exams/{id}/versions/{versionId}/questions` `{ questionIds: [...], score? }` | `Exam.Update` |
| PATCH | `/api/exams/{id}/versions/{versionId}/questions/{examQuestionId}` `{ score }` | `Exam.Update` |
| DELETE | `/api/exams/{id}/versions/{versionId}/questions/{examQuestionId}` | `Exam.Update` |
| PUT | `/api/exams/{id}/versions/{versionId}/questions/order` `{ examQuestionIds: [...] }` (thứ tự đầy đủ) | `Exam.Update` |
| POST | `/api/exams/{id}/versions/{versionId}/questions/sync` `{ examQuestionIds?: [...] }` (đồng bộ từ ngân hàng) | `Exam.Update` |

`{examQuestionId}` là Id của dòng `ExamQuestions`, **không phải** Id câu hỏi trong ngân hàng.

### 6.6 Sửa đáp án / chấm lại (D-11)
| Method | Route | Quyền |
|---|---|---|
| POST | `/api/exams/{id}/versions/{versionId}/questions/{examQuestionId}/answer-key` `{ correctOptionCodes? , acceptedAnswers?, correctAnswerNumber?, numericTolerance?, reason }` | `Exam.Regrade` |
| POST | `/api/exams/{id}/versions/{versionId}/questions/{examQuestionId}/void` `{ reason }` | `Exam.Regrade` |
| GET | `/api/exams/{id}/answer-key-corrections` | `Exam.View` |

Response: số lượt đã chấm lại, và số học viên có điểm hoặc trạng thái đạt/không đạt thay đổi.

### 6.7 Học viên
| Method | Route | Quyền |
|---|---|---|
| GET | `/api/student/exams` | Student |
| GET | `/api/student/exams/{examId}` | Student |
| POST | `/api/student/exams/{examId}/start` | Student |
| GET | `/api/student/attempts/{attemptId}` | Student (chủ sở hữu) |
| PUT | `/api/student/attempts/{attemptId}/answers` | Student (chủ sở hữu) |
| POST | `/api/student/attempts/{attemptId}/events` | Student (chủ sở hữu) |
| POST | `/api/student/attempts/{attemptId}/submit` | Student (chủ sở hữu) |
| GET | `/api/student/attempts/{attemptId}/result` | Student (chủ sở hữu) |
| GET | `/api/student/history` (phân trang) | Student |

**Danh sách đề của học viên** — mỗi phần tử:
```json
{ "examId": "...", "code": "CS-BASIC", "name": "C# Basic", "startAt": "...", "endAt": "...",
  "durationMinutes": 60, "questionCount": 10, "maxAttempts": 2, "usedAttempts": 1,
  "remainingAttempts": 1, "inProgressAttemptId": null, "officialScore": 8.5,
  "availability": "AVAILABLE" }
```
`availability` nhận một trong: `NOT_STARTED`, `AVAILABLE`, `IN_PROGRESS`, `NO_ATTEMPTS_LEFT`, `ENDED`, `CLOSED`.

**Start / lấy lượt thi** — DTO an toàn cho học viên:
```json
{
  "attemptId": "...", "examId": "...", "examName": "C# Basic", "attemptNumber": 1,
  "status": "IN_PROGRESS", "resumed": false,
  "startedAt": "2026-09-26T02:00:00.000Z", "expiredAt": "2026-09-26T03:00:00.000Z",
  "serverTime": "2026-09-26T02:00:00.120Z",
  "questions": [
    { "id": "<attemptQuestionId>", "order": 1, "content": "2 + 2 = ?", "contentFormat": "PLAIN",
      "type": "SINGLE_CHOICE", "answerDataType": null, "score": 1,
      "options": [ { "code": "A", "content": "3" }, { "code": "B", "content": "4" } ],
      "answer": { "selectedOptions": ["B"], "answerText": null, "isMarkedForReview": false, "clientSeq": 17 } }
  ]
}
```

**Tuyệt đối không có trong DTO khi lượt thi còn `IN_PROGRESS`:**
- `isCorrect` (của option hoặc câu).
- `acceptedAnswers`, `correctAnswerNumber`, `numericTolerance`.
- `explanation`.
- `sourceQuestionId`, `examQuestionId`.
- Điểm đã chấm của từng câu hoặc tổng điểm.

`id` của câu là `AttemptQuestions.Id`.

**Lưu đáp án** — một câu hoặc một lô:
```json
{ "answers": [
    { "questionId": "...", "clientSeq": 18, "selectedOptions": ["A","C"], "isMarkedForReview": false },
    { "questionId": "...", "clientSeq": 19, "answerText": "3,5" }
] }
```
- Tối đa 50 phần tử mỗi request.
- Response gồm `serverTime`, `expiredAt` và kết quả từng phần tử `{ questionId, applied, clientSeq }`. `applied = false` khi `clientSeq` không mới hơn giá trị đang lưu.
- Để xóa câu trả lời: gửi `selectedOptions: []` hoặc `answerText: ""`.

**Sự kiện:** `{ "events": [ { "type": "VISIBILITY_HIDDEN", "clientTime": "..." } ] }`, tối đa 50 sự kiện mỗi request.

**Nộp bài:** body rỗng. Trả về `StudentResultDto`, đã áp dụng chính sách hiển thị (D-09):
```json
{ "attemptId": "...", "status": "SUBMITTED", "submittedAt": "...", "durationSeconds": 2710,
  "scoreVisible": true, "totalScore": 8.5, "maxScore": 10, "percentage": 85.00,
  "correctCount": 8, "totalQuestion": 10, "passed": true,
  "reviewAvailable": false, "reviewAvailableAt": "2026-09-30T10:00:00.000Z", "questions": null }
```
- Khi `scoreVisible = false`: các trường điểm là `null`.
- Khi `reviewAvailable = true`: `questions` gồm, cho từng câu, câu trả lời đã chọn, đáp án đúng, `isCorrect`, `score`, `explanation`.

### 6.8 Admin: lượt thi, kết quả, báo cáo
| Method | Route | Quyền |
|---|---|---|
| GET | `/api/admin/exams/{examId}/attempts` (lọc `status`, `userId`, `keyword`) | `Attempt.View` |
| GET | `/api/admin/exams/{examId}/results` (`official=true` chỉ lấy điểm chính thức) | `Result.View` |
| GET | `/api/admin/exams/{examId}/results/export` → file `.xlsx` | `Result.Export` |
| GET | `/api/admin/attempts/{attemptId}` (chi tiết, đáp án, sự kiện) | `Attempt.View` |
| POST | `/api/admin/attempts/{attemptId}/extend` `{ minutes, reason }` | `Attempt.Manage` |
| POST | `/api/admin/attempts/{attemptId}/force-submit` `{ reason }` | `Attempt.Manage` |
| POST | `/api/admin/attempts/{attemptId}/cancel` `{ reason }` | `Attempt.Manage` |
| GET | `/api/admin/dashboard` | `Report.View` |
| GET | `/api/admin/reports/question-statistics?examId=&versionId=` | `Report.View` |
| GET | `/api/admin/audit-logs` (lọc `userId`, `action`, `entityName`, `entityId`, `from`, `to`) | `Audit.View` |

**Thống kê câu hỏi** (MVP): số lượt trả lời, số đúng, số sai, số bỏ trống, tỉ lệ đúng, điểm trung bình, và phân bố lựa chọn đối với câu trắc nghiệm.

### 6.9 Hệ thống
| Method | Route | Quyền |
|---|---|---|
| GET | `/health/live` | — (tiến trình còn sống) |
| GET | `/health/ready` | — (kiểm tra SQL Server) |
| GET | `/openapi/v1.json`, `/swagger` | Chỉ ở Development / Staging |

## 7. Ma trận phân quyền theo vai trò mặc định

Kiểm tra quyền luôn dựa trên **permission**. Bảng dưới chỉ thể hiện seed mặc định.

| Nhóm chức năng | ADMIN | STUDENT |
|---|---|---|
| Xác thực, hồ sơ, đổi mật khẩu | Có | Có |
| Người dùng / nhóm / vai trò | Có | Không |
| Danh mục / câu hỏi | Có | Không |
| Quản lý đề, version, publish, gán đề | Có | Không |
| Sửa đáp án / chấm lại | Có | Không |
| Danh sách đề được thi, start, lưu đáp án, nộp bài | Không | Có (chỉ đề được phép) |
| Kết quả của chính mình | Không | Có (theo chính sách hiển thị) |
| Tất cả kết quả, export, thao tác trên lượt thi | Có | Không |
| Báo cáo, dashboard, audit log | Có | Không |

ADMIN không thi thay học viên. Muốn thử đề thì dùng `preview`.

## 8. Quyết định / Giả định

- **D-13:** spec gốc lẫn lộn giữa `ApiResponse` và ProblemDetails; chốt dùng `ApiResponse` cho mọi response và thêm `traceId`.
- **Thêm 429** vào bảng status, vì spec gốc có rate limit nhưng không có mã lỗi tương ứng.
- **404 cho tài nguyên của người khác** thay vì 403, để không để lộ Id.
- **`DELETE /api/questions/{id}` bị bỏ**, thay bằng `PATCH /status` (D-16).
- **`POST /api/exams/{id}/clone`** được chốt là clone thành đề mới. Tạo version mới dùng `POST /versions`.
- **`/api/auth/refresh-token` đổi tên thành `/api/auth/refresh`.**
- **Câu điền số gửi dạng chuỗi (`answerText`)**, không gửi JSON number, để server kiểm soát định dạng và giữ chuỗi gốc (D-12).
- **Quên mật khẩu qua email là tính năng sau MVP**; trong MVP, admin đặt lại mật khẩu cho user.
- **Giới hạn 50 phần tử mỗi batch** (câu trả lời, sự kiện) là giả định *(cần xác nhận)*.
