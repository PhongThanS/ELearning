# 08 — Kiểm thử

## 1. Chiến lược

| Tầng | Công cụ | Phạm vi |
|---|---|---|
| Unit (backend) | xUnit, AwesomeAssertions, NSubstitute, `FakeTimeProvider` | Domain, grader, chuẩn hóa, parse số, validator, chuyển trạng thái, chính sách hiển thị |
| Integration | xUnit, Testcontainers (SQL Server thật), EF Core migration thật | Repository, transaction, khóa dòng, filtered unique index, job tự nộp, chấm lại |
| API | `WebApplicationFactory` + Testcontainers | Hợp đồng HTTP, status, mã lỗi, phân quyền, DTO không lộ đáp án |
| Frontend | Vitest, React Testing Library, MSW | Form, route guard, editor câu hỏi, exam player, autosave, timer |
| E2E | Playwright | Luồng đầy đủ của admin và học viên |
| Load | k6 (`load-tests/`) | Chỉ tiêu phi chức năng ở `01-tong-quan.md` mục 6 |

**Quy tắc:**
- Test integration và API chạy trên **SQL Server thật** (Testcontainers), **không** dùng EF InMemory hay SQLite. Filtered index, `UPDLOCK`, `ROWVERSION` và collation chỉ hoạt động đúng trên SQL Server.
- Mọi test phụ thuộc thời gian đều dùng `FakeTimeProvider`. Cấm `DateTime.UtcNow` trong code nghiệp vụ.
- Mỗi bug được sửa phải kèm một test tái hiện bug đó.

## 2. Unit test — chấm điểm (bắt buộc)

| Loại | Các ca |
|---|---|
| SINGLE_CHOICE | Đúng; sai; không trả lời; chọn 2 option (dữ liệu hỏng → sai); câu bị hủy → điểm tối đa |
| MULTIPLE_CHOICE | Đúng tập; thiếu 1; thừa 1; khác thứ tự; chọn rỗng; chọn tất cả khi đáp án là tập con |
| TRUE_FALSE | Chọn TRUE khi đáp án TRUE; chọn FALSE khi đáp án TRUE; không trả lời |
| FILL_IN TEXT | Khớp chính xác; khác hoa thường; khoảng trắng thừa / tab / NBSP; **NFC khớp NFD**; khớp đáp án chấp nhận thứ 2; sai; rỗng; `CaseSensitive = true` mà khác hoa thường → sai; `IgnoreAccent = true`: "Hà Nội" khớp "ha noi", **"Đà Nẵng" khớp "da nang"** |
| FILL_IN NUMBER | Khớp chính xác; trong sai số; đúng biên sai số; ngoài sai số; `"3,5"` = `3.5`; `"-0,25"`; số âm |
| Parse số | `"3,5"` → 3.5 (**không phải 35**); `"1,000.5"` → lỗi; `"1e3"` → lỗi; `" 42 "` → 42; `"+7"` → 7; `"abc"` → lỗi; `""` → chưa trả lời |
| Tổng kết | Tổng điểm; phần trăm làm tròn `AwayFromZero`; `Passed` đúng tại biên (= ngưỡng thì đạt); `PassPercentage` null → `Passed` null; câu bị hủy được tính vào `CorrectCount` |

## 3. Unit test — nghiệp vụ

- **Publish:** kiểm tra từng lỗi ở `02-nghiep-vu.md` mục 4.5; trả về đủ danh sách lỗi; `AFTER_SUBMIT` + `MaxAttempts > 1` → lỗi.
- **Chuyển trạng thái:** mọi chuyển trạng thái hợp lệ và không hợp lệ của Exam, ExamVersion, Attempt.
- **Tính `ExpiredAt`:** không có `EndAt`; `EndAt` cắt ngắn thời gian; gia hạn vượt `EndAt`.
- **Ân hạn:** lưu đáp án tại `ExpiredAt + 29s` → chấp nhận; tại `+31s` → `ATTEMPT_EXPIRED`.
- **Khả dụng:** trước `StartAt`, sau `EndAt`, `CLOSED`, hết lượt, `ExtraAttempts`, lượt `CANCELLED` không được tính, `ASSIGNED` mà không được gán.
- **Chính sách hiển thị:** từng tổ hợp `ScoreVisibility` × `ReviewPolicy` × thời điểm.
- **Điểm chính thức:** `HIGHEST` và `LATEST`; hai lượt bằng điểm; lượt `CANCELLED` bị loại.
- **Validator câu hỏi:** mọi quy tắc ở `02-nghiep-vu.md` mục 1.1.

## 4. Integration / API test (bắt buộc)

**Luồng chính:**
1. Đăng ký, đăng nhập, refresh (xoay vòng), logout.
2. Tạo câu hỏi đủ 4 loại.
3. Tạo đề, thêm câu, publish.
4. Học viên start, lưu đáp án, submit, xem kết quả.

**Bảo mật và phân quyền:**
- Học viên gọi API admin → 403.
- Học viên xem lượt thi của người khác → 404.
- Học viên xem đề `ASSIGNED` không được gán → 404.
- **JSON** của `GET /student/attempts/{id}` và `POST /start` **không chứa** `isCorrect`, `acceptedAnswers`, `correctAnswerNumber`, `numericTolerance`, `explanation` (duyệt đệ quy mọi khóa).
- Kết quả với `ReviewPolicy = NEVER` → không có `questions`.
- Dùng lại refresh token đã xoay vòng → 401, cả family bị thu hồi.
- Đăng nhập sai 5 lần → `ACCOUNT_LOCKED`.
- Vô hiệu hóa user → request tiếp theo (sau khi xóa cache) trả 401.
- Vượt rate limit login → 429.

**Tính toàn vẹn:**
- Lượt thi đã nộp không nhận thêm câu trả lời → 409 `ATTEMPT_NOT_IN_PROGRESS`.
- Lượt thi hết giờ không nhận câu trả lời, và được tự nộp.
- Hết lượt → `MAX_ATTEMPTS_EXCEEDED`.
- **Gọi `start` song song** 10 request → chỉ tạo đúng 1 lượt; mọi response trả cùng `attemptId`.
- **Gọi `submit` song song** 10 request → đúng 1 `ExamResults`; mọi response giống nhau.
- **Lưu đáp án và submit chạy đồng thời** → câu trả lời hoặc có trong kết quả, hoặc bị từ chối; không có trạng thái lửng.
- **Lưu đáp án sai thứ tự:** gửi `clientSeq = 20` rồi `clientSeq = 19` → giữ bản 20.
- Sửa version đã publish → 409 `VERSION_IMMUTABLE`.
- Publish V2 khi đang có lượt thi trên V1 → lượt V1 nộp bình thường và được chấm theo V1.
- **Sửa câu hỏi trong ngân hàng sau khi thi** → kết quả cũ không đổi, xem lại bài vẫn hiện nội dung cũ.
- Hai admin cùng sửa một đề → người sau nhận 409 `CONCURRENCY_CONFLICT`.
- **Job tự nộp:** tạo lượt thi, tua `FakeTimeProvider` quá `ExpiredAt + grace`, chạy một vòng worker → `AUTO_SUBMITTED`, có kết quả. Chạy 2 worker song song → không chấm trùng.
- **Chấm lại:** sửa đáp án → điểm đổi, `ExamResultHistory` có bản ghi, `GradingRevision = 2`, câu trả lời của học viên không đổi. Hủy câu → mọi người được điểm tối đa câu đó.
- **Đóng đề** với `forceSubmitInProgress = true` → mọi lượt đang làm được nộp.
- **DateTime:** mọi thời gian trong JSON đều kết thúc bằng `Z` (test duyệt response).
- **Migration:** áp dụng từ DB rỗng thành công; script idempotent chạy được hai lần.

## 5. Frontend test

- Form đăng nhập: validation, lỗi `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`.
- Route guard: chưa đăng nhập → `/login`; thiếu permission → trang 403; `mustChangePassword` → `/change-password`.
- `QuestionEditor`: đổi loại câu; quy tắc số option đúng; đáp án chấp nhận; câu điền số.
- Exam builder: thêm / xóa / sắp xếp câu; hiển thị lỗi publish.
- **Exam player:**
  - Chuyển câu; đánh dấu xem lại.
  - Autosave có debounce; hàng đợi chỉ gửi một request tại một thời điểm.
  - Mất mạng → "Chưa lưu" → có mạng → gửi lại.
  - Khôi phục từ `sessionStorage` khi backup có `clientSeq` lớn hơn.
- **Timer:** tính từ `clockOffsetMs` (giả lập đồng hồ máy lệch 10 phút vẫn hiển thị đúng); hết giờ thì gọi submit.
- Hộp thoại nộp bài liệt kê câu chưa trả lời.
- Trang kết quả: điểm bị ẩn; xem lại bài.

## 6. E2E (Playwright)

**Học viên:**
```text
Đăng nhập → thấy đề được gán → Bắt đầu → trả lời vài câu (đủ 4 loại)
→ tải lại trang → câu trả lời còn nguyên → mất mạng giả lập → trả lời → có mạng → đã lưu
→ Nộp bài → thấy kết quả theo chính sách
```

**Admin:**
```text
Đăng nhập → tạo danh mục → tạo 4 loại câu hỏi → tạo đề → thêm câu → cấu hình
→ gán nhóm → xem trước → publish → (học viên thi) → xem kết quả → export Excel
→ sửa đáp án một câu → điểm học viên được cập nhật
```

**Hết giờ:** đề 1 phút, bắt đầu và không làm gì → lượt thi tự nộp, trang kết quả hiển thị.

## 7. Load test (k6)

| Kịch bản | Mô tả | Ngưỡng đạt |
|---|---|---|
| `exam-start-burst` | 500 VU đăng nhập rồi `start` trong vòng 60 giây | p95 start < 1s; không lỗi 5xx; mỗi VU đúng 1 lượt thi |
| `exam-steady` | 500 VU, mỗi VU lưu đáp án mỗi 5–15 giây trong 30 phút | p95 lưu đáp án < 300 ms; lỗi < 0.1% |
| `exam-submit-wave` | 500 VU nộp bài trong 2 phút | p95 submit < 2s |
| `expiry-sweep` | 500 lượt hết giờ cùng lúc | Job xử lý hết trong < 5 phút |

- Chạy trên môi trường staging có cấu hình giống production, trước mỗi release lớn và trước kỳ thi quy mô lớn đầu tiên.
- Ghi kết quả vào `docs/` hoặc artifact CI.

## 8. Quyết định / Giả định

- **(M1) Nguồn SQL Server cho test** (`tests/ELearning.TestSupport/SqlServerTestDatabase.cs`):
  - Nếu có biến môi trường `ELEARNING_TEST_SQL`: dùng server đó, tạo database tạm `ELearningTest_<guid>`, áp migration, xóa khi xong.
  - Nếu không có: dùng Testcontainers (cần Docker, như trên CI).
  - Lý do: máy dev hiện có SQL Server local nhưng không có Docker. Vẫn đúng yêu cầu "SQL Server thật".

- **Bắt buộc SQL Server thật cho integration test.** Spec gốc cho phép "database kiểm thử" chung chung; chốt Testcontainers vì các cơ chế quan trọng chỉ có trên SQL Server.
- **Thêm load test k6** (spec gốc không có) cùng các ca kiểm thử tương tranh (start / submit / lưu đáp án song song).
- **Thêm ca kiểm thử cho NFC, dấu thập phân kiểu Việt, `đ`, và hậu tố `Z`** của thời gian, là các lỗi dễ gặp ở `10-bay-ky-thuat.md`.
