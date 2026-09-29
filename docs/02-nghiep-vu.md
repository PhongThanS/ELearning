# 02 — Nghiệp vụ

## 1. Loại câu hỏi

MVP có đúng 4 loại. Mọi loại dùng chung một bảng; không tạo bảng riêng cho từng loại.

| `QuestionType` | Mô tả | Lưu đáp án đúng |
|---|---|---|
| `SINGLE_CHOICE` | Chọn 1 | `IsCorrect` trên option |
| `MULTIPLE_CHOICE` | Chọn nhiều | `IsCorrect` trên option |
| `TRUE_FALSE` | Đúng / Sai | `IsCorrect` trên option |
| `FILL_IN` | Điền đáp án | `AcceptedAnswers` (TEXT) hoặc `CorrectAnswerNumber` (NUMBER) |

Với `FILL_IN`, trường `AnswerDataType` nhận `TEXT` hoặc `NUMBER`.

### 1.1 Quy tắc hợp lệ

| Loại | Quy tắc |
|---|---|
| `SINGLE_CHOICE` | 2–10 option (khuyến nghị 4); đúng **1** option đúng |
| `MULTIPLE_CHOICE` | 2–10 option; **≥ 1** option đúng |
| `TRUE_FALSE` | Đúng 2 option, mã `TRUE` và `FALSE` (nhãn UI: "Đúng" / "Sai"); đúng 1 option đúng |
| `FILL_IN` + `TEXT` | Không có option; **≥ 1** đáp án chấp nhận; tối đa 20 |
| `FILL_IN` + `NUMBER` | Không có option; bắt buộc `CorrectAnswerNumber`; `NumericTolerance ≥ 0` |

**Quy tắc chung:**
- `OptionCode` là chữ cái in hoa `A`–`J` (hoặc `TRUE` / `FALSE`), duy nhất trong một câu.
- Nội dung câu hỏi: 1–10.000 ký tự. Nội dung option: 1–2.000 ký tự.
- Điểm (`Score`) nằm trong khoảng `0.25`–`100`, bước `0.25`.
- `Code` của câu hỏi là duy nhất và không được dùng lại, kể cả khi câu hỏi đã tắt. Nếu để trống, hệ thống tự sinh `Q000123`.

### 1.2 Nội dung câu hỏi (D-17)

- `ContentFormat`: `PLAIN` hoặc `MARKDOWN`.
- Markdown được hỗ trợ: đoạn văn, xuống dòng, **đậm**, *nghiêng*, `inline code`, code block có ngôn ngữ, danh sách, bảng.
- **HTML thô bị vô hiệu hóa** khi render (không bật `rehype-raw`). Liên kết chỉ nhận `http`/`https`. Chưa hỗ trợ hình ảnh (xem mục đính kèm ở tài liệu sau MVP).
- Lý do: đề demo là "C# Basic", cần hiển thị code nhiều dòng; plain text không đủ.

### 1.3 Import câu hỏi từ Excel (sau MVP, đã làm)

- File `.xlsx` tối đa 5 MB, tối đa 1.000 câu mỗi lần. Đọc sheet `CauHoi` (không có thì sheet đầu tiên). Cột được nhận theo **tiêu đề**, không theo vị trí.
- File mẫu (`GET /api/questions/import/template`) có sẵn sheet hướng dẫn và danh sách mã danh mục đang hoạt động.
- Cột: `Mã`, `Danh mục` (mã), `Loại`, `Định dạng`, `Nội dung`, `Điểm`, `Lựa chọn A` … `Lựa chọn J`, `Đáp án đúng`, `Kiểu đáp án`, `Đáp án chấp nhận` (cách nhau bởi `|` hoặc xuống dòng), `Đáp án số`, `Sai số`, `Phân biệt hoa thường`, `Bỏ qua dấu`, `Giải thích`.
- `Loại` nhận mã enum hoặc tên tiếng Việt. Đúng / Sai nhận `Đúng` / `Sai` / `TRUE` / `FALSE`. Số nhận dấu phẩy hoặc dấu chấm thập phân. Định dạng mặc định là `MARKDOWN`.
- Mỗi dòng đi qua **đúng validator và domain của chức năng tạo câu hỏi**. Không có quy tắc hợp lệ riêng cho import.
- Kiểm tra thêm: danh mục phải tồn tại và đang hoạt động; mã câu không trùng câu đã có và không trùng nhau trong file; lựa chọn phải liên tục từ A.
- **Tất cả hoặc không:** có một dòng lỗi thì không câu nào được tạo, kết quả trả lỗi theo từng dòng (số dòng Excel + tên cột). `dryRun=true` chỉ kiểm tra.
- Import thành công ghi một audit `QUESTIONS_IMPORTED` (số câu và danh sách mã).

## 2. Chấm điểm

Mỗi câu chỉ có hai kết quả: đúng (điểm tối đa) hoặc sai (0 điểm). Chấm từng phần là tính năng sau MVP; schema không cần đổi khi thêm.

| Loại | Đúng khi |
|---|---|
| `SINGLE_CHOICE`, `TRUE_FALSE` | Chọn đúng 1 option và đó là option đúng |
| `MULTIPLE_CHOICE` | Tập option đã chọn **bằng đúng** tập option đúng (không phân biệt thứ tự) |
| `FILL_IN` + `TEXT` | `Normalize(bài làm)` trùng `Normalize(một trong các đáp án chấp nhận)` |
| `FILL_IN` + `NUMBER` | Parse được và `|bài làm − đáp án| ≤ NumericTolerance` |

**Ví dụ `MULTIPLE_CHOICE`** với đáp án đúng `{A, C, D}`:
- `{A, C, D}` → đúng.
- `{C, A, D}` → đúng (không phụ thuộc thứ tự).
- `{A, C}` → sai.
- `{A, B, C, D}` → sai.
- `{}` → sai.

**Các trường hợp khác:**
- Không trả lời → sai, 0 điểm, `IsAnswered = false`.
- Mã option không thuộc câu hỏi → bị từ chối ngay lúc lưu (422), không đi tới bước chấm.
- Câu bị hủy (`IsVoided = true`, D-11) → mọi thí sinh được điểm tối đa, kể cả khi không trả lời.

**Tổng kết (dùng `decimal`, không dùng float):**
```text
MaxScore    = Σ ExamQuestion.Score
TotalScore  = Σ AttemptAnswer.Score
Percentage  = round(TotalScore / MaxScore × 100, 2, MidpointRounding.AwayFromZero)
Passed      = PassPercentage is null ? null : Percentage >= PassPercentage
CorrectCount = số câu có IsCorrect = true (câu bị hủy tính là đúng)
```
Publish không cho phép `MaxScore = 0`, nên phép chia luôn hợp lệ.

## 3. Chuẩn hóa đáp án điền (D-12)

### 3.1 Văn bản — `Normalize(s)`
1. `null` → chuỗi rỗng.
2. Chuẩn hóa Unicode về **NFC**: `s.Normalize(NormalizationForm.FormC)`. Bắt buộc, vì "Hà Nội" gõ bằng Unikey (dựng sẵn) và "Hà Nội" copy từ Word/macOS (tổ hợp) khác nhau về byte.
3. Bỏ khoảng trắng đầu và cuối; gộp mọi chuỗi khoảng trắng (kể cả tab, NBSP ` `) thành 1 dấu cách.
4. Nếu `CaseSensitive = false` (mặc định): `ToLowerInvariant()`.
5. Nếu `IgnoreAccent = true` (mặc định `false`):
   - Tách dấu bằng NFD và bỏ các ký tự thuộc `UnicodeCategory.NonSpacingMark`.
   - **Map thủ công `đ → d`, `Đ → D`**, vì hai chữ này không tách được bằng NFD.
   - Chuẩn hóa lại về NFC.

Ví dụ: `"  Hà   Nội "` khớp với `"hà nội"`. Nếu `IgnoreAccent = true` thì cũng khớp `"ha noi"`.

`CaseSensitive` và `IgnoreAccent` được cấu hình theo từng câu hỏi.

### 3.2 Nhiều đáp án chấp nhận
Mỗi câu `FILL_IN` + `TEXT` có từ 1 đến 20 đáp án chấp nhận (bảng `QuestionAcceptedAnswers`). Bài làm khớp một trong số đó là đúng.

### 3.3 Số
- Client gửi **chuỗi thô** (`answerText`); server là nơi duy nhất parse số.
- Định dạng hợp lệ: `^[+-]?\d{1,20}([.,]\d{1,10})?$`, áp dụng sau khi trim.
- Dấu `,` và `.` đều được hiểu là **dấu thập phân**. **Không** chấp nhận dấu phân cách hàng nghìn và ký hiệu mũ.
- Parse bằng cách thay `,` bằng `.` rồi gọi `decimal.Parse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)`.
- **Tuyệt đối không** gọi `decimal.Parse(s, CultureInfo.InvariantCulture)` với style mặc định, vì `"3,5"` sẽ bị hiểu thành `35` (xem `10-bay-ky-thuat.md`).
- Chuỗi sai định dạng → lỗi 422 `INVALID_NUMBER_FORMAT` khi lưu. Frontend kiểm tra cùng regex để báo lỗi ngay trên ô nhập.
- Lưu cả chuỗi gốc (`AnswerText`) và giá trị đã parse (`AnswerNumber`).

## 4. Đề thi và version

### 4.4 Xáo câu hỏi / đáp án (sau MVP, đã làm)

- Cấu hình theo **version** (`ShuffleQuestions`, `ShuffleOptions`), sửa được khi version còn DRAFT như các cấu hình khác.
- Thứ tự được **chốt lúc bắt đầu lượt thi** vào `AttemptQuestions.QuestionOrder` / `OptionOrder` (ví dụ `C,A,D,B`). Tải lại trang, thi tiếp trên máy khác hay xem lại bài đều thấy đúng thứ tự đó. Mỗi lượt thi có thứ tự riêng.
- Chỉ xáo đáp án của câu chọn một / chọn nhiều. Câu Đúng / Sai giữ thứ tự Đúng → Sai; câu điền không có lựa chọn.
- Học viên luôn thấy nhãn **theo vị trí trên màn hình** (A, B, C…); client vẫn gửi **mã gốc** của lựa chọn nên chấm điểm, sửa đáp án và thống kê không phụ thuộc thứ tự. Màn hình admin hiển thị mã gốc.

### 4.1 Phân chia trường (D-03)

| Nằm ở `Exams` (sửa được sau publish, có audit) | Nằm ở `ExamVersions` (bất biến sau publish) |
|---|---|
| `Code`, `Name`, `Description`, `Instructions` | `DurationMinutes` |
| `StartAt`, `EndAt` | `PassPercentage` |
| `MaxAttempts` | `ScoreVisibility`, `ReviewPolicy` |
| `AccessMode`, danh sách gán đề | `ShuffleQuestions`, `ShuffleOptions` (mặc định `false`, xem mục 4.4) |
| `RetakeScoringPolicy` (khóa khi đã có lượt thi) | Danh sách câu hỏi và điểm |

### 4.2 Trạng thái đề thi (D-04)

```text
            publish (version đầu tiên)
  DRAFT ─────────────────────────────► PUBLISHED ◄──── reopen ────┐
    │                                      │                      │
    │ delete (chỉ khi chưa từng publish)   └──── close ────► CLOSED
    ▼
 (xóa cứng)
```

| Trạng thái | Ý nghĩa |
|---|---|
| `DRAFT` | Chưa từng publish; sửa tự do; xóa được |
| `PUBLISHED` | Có đúng 1 version PUBLISHED; học viên bắt đầu được nếu nằm trong `StartAt`–`EndAt` |
| `CLOSED` | Không cho bắt đầu lượt mới; mở lại được |

### 4.3 Trạng thái version

```text
DRAFT ──publish──► PUBLISHED ──(publish version mới)──► ARCHIVED
```

- Mỗi đề có **tối đa 1 version DRAFT** và **tối đa 1 version PUBLISHED**, được enforce bằng filtered unique index.
- Tạo version mới: `POST /api/exams/{id}/versions`, copy câu hỏi, điểm và cấu hình từ version PUBLISHED hiện tại (hoặc tạo trống).
- Publish version N:
  - Version PUBLISHED cũ chuyển sang `ARCHIVED`.
  - Lượt thi đang làm trên version cũ **làm tiếp bình thường** và được chấm theo version cũ.
  - Lượt mới dùng version N.
- Chỉ xóa được version khi nó đang ở `DRAFT`.

### 4.4 Câu hỏi trong draft version (D-02)

- Khi admin thêm câu hỏi vào draft version, hệ thống **copy ngay** nội dung, option, đáp án và điểm từ ngân hàng vào `ExamQuestions` / `ExamQuestionOptions` / `ExamQuestionAcceptedAnswers`, đồng thời lưu `SourceQuestionId` và `SourceRowVersion`.
- Khi còn DRAFT:
  - Sửa được điểm và thứ tự.
  - Xóa được câu.
  - Không thêm trùng một câu hỏi nguồn (filtered unique index).
- Nếu câu hỏi trong ngân hàng thay đổi sau khi được thêm (so sánh `RowVersion`): UI hiển thị cảnh báo "Câu hỏi gốc đã thay đổi". Admin có thể chạy "Đồng bộ từ ngân hàng" cho từng câu hoặc toàn bộ.
- Khi publish, `ExamQuestions` bị khóa. **Publish không tự động đồng bộ**: admin publish đúng những gì đang thấy trong preview.

### 4.5 Kiểm tra khi publish

Không cho publish nếu gặp bất kỳ lỗi nào sau đây (trả về danh sách đầy đủ, không dừng ở lỗi đầu tiên):

1. `Name` rỗng; `DurationMinutes` ngoài khoảng 1–600.
2. Version không có câu hỏi, hoặc có hơn 500 câu.
3. Có câu hỏi vi phạm quy tắc ở mục 1.1.
4. `MaxScore = 0`.
5. `StartAt ≥ EndAt`, hoặc `EndAt` đã ở quá khứ.
6. `PassPercentage` ngoài khoảng 0–100.
7. `ReviewPolicy = AFTER_EXAM_END` hoặc `ScoreVisibility = AFTER_EXAM_END` nhưng `EndAt` là null.
8. `ReviewPolicy = AFTER_SUBMIT` nhưng `MaxAttempts > 1` (người thi xem đáp án rồi làm lại).
9. `AccessMode = ASSIGNED` nhưng chưa gán cho nhóm hoặc người nào.

Các bước publish, trong **một transaction**:
1. Khóa đề (kiểm tra `RowVersion`).
2. Chạy toàn bộ kiểm tra ở trên.
3. Tính và lưu `MaxScore`, `QuestionCount` vào version.
4. Chuyển version cũ sang `ARCHIVED`; version mới sang `PUBLISHED`, ghi `PublishedAt` và `PublishedBy`.
5. Chuyển `Exam.Status` sang `PUBLISHED`.
6. Ghi audit `EXAM_PUBLISHED`.
7. Commit.

Quy tắc chéo được kiểm tra lại mỗi khi sửa `Exams`: `MaxAttempts` so với `ReviewPolicy`, và `EndAt` so với chính sách hiển thị.

### 4.6 Đóng và mở lại (D-06)

- `POST /api/exams/{id}/close` với body `{ "forceSubmitInProgress": false }`:
  - Chặn lượt mới.
  - Lượt đang làm tiếp tục tới `ExpiredAt` của chúng.
  - Nếu `forceSubmitInProgress = true`: mọi lượt `IN_PROGRESS` được nộp ngay với trạng thái `AUTO_SUBMITTED` và `SubmitReason = FORCED_BY_ADMIN`.
- `POST /api/exams/{id}/reopen`: `CLOSED → PUBLISHED`, dùng lại version PUBLISHED hiện có.

## 5. Quyền dự thi (D-10)

- `Exam.AccessMode`:
  - `PUBLIC`: mọi user đang hoạt động có role `STUDENT`.
  - `ASSIGNED`: chỉ những user được gán trực tiếp, hoặc thuộc một nhóm được gán (bảng `ExamAssignments`).
- Nhóm người dùng (`UserGroups`, `UserGroupMembers`) do admin quản lý. Một user có thể thuộc nhiều nhóm.
- Danh sách đề của học viên chỉ gồm những đề họ có quyền thi. Gọi API cho đề không có quyền → **404** (không để lộ đề có tồn tại).
- Tự đăng ký được điều khiển bằng `Auth:AllowSelfRegistration`: mặc định `true` ở Development, `false` ở Production.

## 6. Lượt thi

### 6.1 Trạng thái

```text
IN_PROGRESS ──submit──────────────────────► SUBMITTED
     │ ──hết giờ / job / admin buộc nộp──► AUTO_SUBMITTED
     └──admin hủy────────────────────────► CANCELLED
```

- Không có chuyển trạng thái nào đi ra từ `SUBMITTED`, `AUTO_SUBMITTED`, `CANCELLED`.
- `SubmitReason`: `STUDENT`, `TIME_EXPIRED`, `FORCED_BY_ADMIN`.

### 6.2 Điều kiện được bắt đầu

Đề **khả dụng** với một user khi thỏa **tất cả**:
- `Exam.Status = PUBLISHED`.
- `now ≥ StartAt` (nếu có) và `now < EndAt` (nếu có).
- User có quyền dự thi (mục 5).
- Số lượt đã dùng < `MaxAttempts + ExtraAttempts` của user.

### 6.3 Đếm lượt và lượt song song (D-07)

- `MaxAttempts` nằm trong khoảng 1–50. MVP **không có** "không giới hạn".
- Số lượt đã dùng = số lượt có trạng thái **khác** `CANCELLED`. Vì vậy admin hủy một lượt tương đương với trả lại lượt đó cho học viên.
- Admin cấp thêm lượt cho từng người qua bảng `ExamUserOverrides.ExtraAttempts`.
- Mỗi `(UserId, ExamId)` có **tối đa 1 lượt `IN_PROGRESS`**, được enforce bằng filtered unique index.
- Gọi `start` khi đã có lượt đang làm → trả về lượt đó (HTTP 200, `resumed: true`) thay vì tạo mới. UI hiển thị nút "Tiếp tục bài đang làm".

### 6.4 Các bước start (một transaction)

1. Xác thực user; tải đề cùng version PUBLISHED.
2. Kiểm tra điều kiện ở mục 6.2. Nếu đã có lượt `IN_PROGRESS` → trả về lượt đó.
3. `AttemptNumber = max(AttemptNumber) + 1` (tính cả lượt `CANCELLED`, vì số thứ tự không được dùng lại).
4. `StartedAt = now`; `ExpiredAt = min(StartedAt + DurationMinutes, EndAt ?? +∞)` (D-05).
5. Tạo `AttemptQuestions`: mỗi `ExamQuestion` một dòng, lưu `QuestionOrder` và `OptionOrder` (thứ tự hiển thị option; `null` nếu không xáo).
6. Tạo sẵn `AttemptAnswers` cho mọi câu, với `IsAnswered = false`.
7. Lưu `StartedIp` và `StartedUserAgent`; ghi audit `ATTEMPT_STARTED`.
8. Commit và trả về DTO an toàn cho học viên (xem `05-api.md`).

Nếu hai request start chạy song song, request thua sẽ vi phạm unique index. Bắt lỗi này và trả về lượt đang làm.

Trước khi start, UI cảnh báo khi thời gian thực tế sẽ ít hơn `DurationMinutes`, ví dụ: "Đề đóng lúc 10:00, bạn chỉ còn 12 phút".

### 6.5 Tính giờ và ân hạn (D-05)

- Server tạo `StartedAt` và `ExpiredAt`. Frontend chỉ hiển thị đồng hồ đếm ngược, tính từ `expiredAt` và `serverTime` trong response.
- **Ân hạn** `Exam:SubmitGraceSeconds = 30`: request lưu đáp án hoặc nộp bài mà server nhận được khi `now ≤ ExpiredAt + 30s` vẫn được chấp nhận. Mục đích là bù độ trễ mạng.
- Sau khoảng ân hạn: request lưu đáp án bị từ chối với lỗi `ATTEMPT_EXPIRED`, và lượt thi được tự nộp ngay trong chính request đó.
- **Job nền** `AttemptExpirationWorker` (`BackgroundService`, chạy mỗi 60 giây):
  - Tìm các lượt `IN_PROGRESS` có `ExpiredAt + grace < now`, mỗi lần tối đa 100 lượt.
  - Nộp từng lượt trong transaction riêng, với `AUTO_SUBMITTED` và `SubmitReason = TIME_EXPIRED`.
  - Nhờ chuyển trạng thái nguyên tử, chạy nhiều instance cũng không bị chấm trùng.
- Admin **gia hạn** cho một lượt: `ExpiredAt += N phút`, cộng dồn vào `TimeExtensionMinutes`, có audit. Được phép vượt quá `EndAt`.

### 6.6 Lưu đáp án

- Mỗi request lưu một câu hoặc một lô (batch).
- Backend kiểm tra:
  - Lượt thi thuộc user hiện tại (nếu không → 404).
  - Trạng thái là `IN_PROGRESS`.
  - Chưa hết giờ (tính cả ân hạn).
  - Câu hỏi thuộc lượt thi.
  - Định dạng câu trả lời khớp loại câu hỏi.
- **Thứ tự ghi (D-18):** mỗi lần ghi mang theo `clientSeq`, một số tăng dần do client sinh ra. Server chỉ áp dụng khi `clientSeq > AttemptAnswers.ClientSeq`; nếu không thì bỏ qua (`applied: false`). Điều này chống việc request cũ đến sau ghi đè request mới, và chống xung đột khi mở hai tab. `clientSeq` **chỉ dùng để sắp thứ tự**, không mang ý nghĩa thời gian.
- Lưu `IsMarkedForReview` cùng câu trả lời.
- Lưu đáp án và nộp bài **khóa dòng attempt** (`UPDLOCK`, D-21), nên hai thao tác này không chạy chen nhau.

### 6.7 Nộp bài (idempotent)

Trong một transaction:
1. Khóa dòng attempt bằng `UPDLOCK, ROWLOCK`; kiểm tra quyền sở hữu.
2. Nếu trạng thái khác `IN_PROGRESS` → trả về kết quả đã có. Không chấm lại, không báo lỗi.
3. Nếu `now > ExpiredAt + grace` → trạng thái `AUTO_SUBMITTED` / `TIME_EXPIRED`; ngược lại là `SUBMITTED` / `STUDENT`.
4. Chấm từng câu, lưu `IsCorrect`, `Score`, `GradedAt` vào `AttemptAnswers`.
5. Tạo `ExamResults`.
6. Cập nhật trạng thái, `SubmittedAt`, `SubmittedIp`.
7. Ghi audit (`ATTEMPT_SUBMITTED` hoặc `ATTEMPT_AUTO_SUBMITTED`).
8. Commit.

## 7. Kết quả và chính sách hiển thị (D-09)

**`ScoreVisibility`** — khi nào học viên thấy điểm:

| Giá trị | Ý nghĩa |
|---|---|
| `IMMEDIATE` | Ngay sau khi nộp |
| `AFTER_EXAM_END` | Sau `EndAt` |
| `HIDDEN` | Không bao giờ; học viên chỉ thấy "Đã nộp bài" |

**`ReviewPolicy`** — khi nào học viên xem lại bài, đáp án đúng và giải thích:

| Giá trị | Ý nghĩa |
|---|---|
| `NEVER` | Không bao giờ |
| `AFTER_SUBMIT` | Ngay sau khi nộp; chỉ cho phép khi `MaxAttempts = 1` |
| `AFTER_EXAM_END` | Sau `EndAt` |
| `AFTER_LAST_ATTEMPT` | Khi học viên đã dùng hết lượt, hoặc sau `EndAt` |

**Quy tắc:**
- Xem lại bài luôn kèm điểm. Nếu `ReviewPolicy` cho phép nhưng `ScoreVisibility` chưa cho phép, thì phải chờ cả hai.
- Trong khi lượt thi còn `IN_PROGRESS`, **không bao giờ** trả về: `IsCorrect`, đáp án chấp nhận, `CorrectAnswerNumber`, `NumericTolerance`, `Explanation`, điểm từng câu.
- Admin luôn thấy đầy đủ.

### 7.1 Điểm chính thức khi thi nhiều lượt (D-08)

- `Exam.RetakeScoringPolicy` nhận `HIGHEST` (mặc định) hoặc `LATEST`. Không sửa được sau khi đề đã có lượt thi đầu tiên.
- Điểm chính thức của một học viên được tính khi truy vấn từ `ExamResults` (bỏ qua lượt `CANCELLED`), không lưu thành cột riêng.

## 8. Chấm lại và hủy câu (D-11)

Đây là **ngoại lệ có kiểm soát duy nhất** đối với nguyên tắc version bất biến.

- Permission: `Exam.Regrade`.
- Thao tác trên một `ExamQuestion` của version PUBLISHED hoặc ARCHIVED:
  - **Sửa đáp án:** đổi `IsCorrect` của option, đáp án chấp nhận, `CorrectAnswerNumber` hoặc `NumericTolerance`. Không được sửa nội dung câu hỏi, nội dung option, điểm hay loại câu.
  - **Hủy câu:** đặt `IsVoided = true`; mọi thí sinh được điểm tối đa.
- Bắt buộc nhập lý do. Hệ thống ghi `AnswerKeyCorrections` (đáp án cũ, đáp án mới, lý do, người sửa, thời điểm).
- Sau đó chấm lại **mọi** lượt đã nộp của version này:
  - Lưu điểm cũ vào `ExamResultHistory`.
  - Cập nhật `AttemptAnswers` và `ExamResults`, tăng `ExamResults.GradingRevision`, ghi `RegradedAt`.
- Câu trả lời của học viên **không bao giờ** bị sửa.
- Nếu một version có tới 2.000 lượt thi: chấm lại đồng bộ, chia lô 200 lượt, mỗi lô một transaction. Nhiều hơn thì sau MVP chuyển sang job nền.
- Ghi audit `EXAM_REGRADED`.

## 9. Thao tác admin trên lượt thi

| Thao tác | Điều kiện | Hiệu ứng |
|---|---|---|
| Gia hạn | `IN_PROGRESS` | `ExpiredAt += N` (1–240 phút) |
| Buộc nộp | `IN_PROGRESS` | Nộp với `AUTO_SUBMITTED` / `FORCED_BY_ADMIN` |
| Hủy lượt | Mọi trạng thái trừ `CANCELLED` | `CANCELLED`, không tính vào số lượt đã dùng; kết quả (nếu có) được giữ lại nhưng bị loại khỏi điểm chính thức |
| Cấp thêm lượt | Theo học viên và đề | `ExamUserOverrides.ExtraAttempts += N` |

Mọi thao tác đều yêu cầu lý do và ghi audit.

## 10. Xóa dữ liệu (D-16)

- Câu hỏi, danh mục, người dùng, nhóm: **không xóa cứng**. Dùng `IsActive` (bật/tắt). Không có endpoint `DELETE` cho các đối tượng này.
- Đề thi: xóa cứng được **chỉ khi chưa từng publish**. Sau đó chỉ đóng được.
- Không bao giờ xóa: lượt thi, câu trả lời, kết quả, snapshot.
- `Code` (câu hỏi, danh mục, đề) không bao giờ được dùng lại.
- Khi user yêu cầu xóa dữ liệu cá nhân: **ẩn danh hóa** (xem `07-bao-mat.md`), giữ nguyên lượt thi.

## 11. Quyết định / Giả định

- **(M8) Chấm lại chạy theo 3 bước:** (1) đổi đáp án và ghi `AnswerKeyCorrections` trong một transaction; (2) chấm lại theo lô 200 lượt, mỗi lô một transaction; (3) ghi số lượt bị ảnh hưởng. Nếu tiến trình dừng giữa bước 2, những lượt chưa chấm lại vẫn giữ điểm cũ; chạy lại chấm lại (hủy câu / sửa đáp án lần nữa) sẽ đồng bộ. Mọi lượt có kết quả của version đều được chấm lại, kể cả lượt đã hủy.
- **(M8) Sửa đáp án** không đổi được mã lựa chọn, nội dung hay điểm; đáp án mới phải hợp lệ theo quy tắc của loại câu (ví dụ câu chọn một vẫn phải có đúng 1 đáp án).
- **(M8) Export Excel** tối đa 20.000 dòng, thời gian theo múi giờ nghiệp vụ.
- **(M5) Start trả 201 khi tạo lượt mới, 200 kèm `resumed: true` khi trả về lượt đang làm**, kể cả khi hai request start chạy song song (bắt cả lỗi unique lẫn trường hợp request kia vừa tạo xong giữa hai lần đọc).
- **(M5) Lưu đáp án theo lô là "tất cả hoặc không":** một phần tử sai dạng (`INVALID_OPTION`, `INVALID_ANSWER_SHAPE`, `INVALID_NUMBER_FORMAT`) → 422 cho cả lô, không lưu gì; `field` chỉ rõ `answers[i]`. Gửi `selectedOptions: []` hoặc `answerText: ""` = xóa câu trả lời.
- **(M5) Nộp muộn:** học viên bấm nộp sau ân hạn → ghi `AUTO_SUBMITTED` / `TIME_EXPIRED`, và `SubmittedAt = ExpiredAt` (thời gian làm bài không vượt quá hạn).
- **(M5) Sự kiện của lượt đã kết thúc** được bỏ qua (trả `accepted: 0`), không báo lỗi.
- **(M6) Câu chọn nhiều không chọn gì luôn sai**, kể cả khi dữ liệu hỏng có tập đáp án rỗng.
- **(M6) Điểm chính thức trên danh sách đề của học viên** chỉ hiện khi `ScoreVisibility` cho phép; lượt bị hủy bị loại.
- **(M4) Thêm câu hỏi đã bị tắt vào đề:** bị từ chối (`QUESTION_INACTIVE`). Thêm trùng câu hỏi nguồn: bỏ qua, không báo lỗi.
- **(M4) Xóa câu khỏi bản nháp** không đánh lại số thứ tự (có thể có khoảng trống); thứ tự hiển thị luôn sắp theo `QuestionOrder`, và lượt thi đánh số lại 1..N khi bắt đầu.
- **(M4) Xóa version nháp** chỉ được khi đề đã từng publish; đề chưa publish chỉ có một version, muốn bỏ thì xóa cả đề.
- **(M4) Clone đề:** tạo đề DRAFT mới, copy cấu hình và câu hỏi từ version đang publish (hoặc version mới nhất). Không copy lịch thi và danh sách gán. Mã mặc định là `<mã cũ>-COPY`.
- **(M4) Sửa đề đã publish** được kiểm tra chéo với version đang publish: `AFTER_SUBMIT` chỉ cho 1 lượt; `AFTER_EXAM_END` cần `EndAt`; `ASSIGNED` phải có người được gán.

- **D-01 — Chỉ snapshot một lần.**
  - Spec gốc copy nội dung hai lần (vào `ExamQuestions` và `AttemptQuestions`), nhưng `AttemptQuestions` lại không lưu đáp án cho câu trắc nghiệm, còn cột `SourceExamQuestionId` thì cho phép NULL.
  - Vì `ExamQuestions` đã bất biến, `AttemptQuestions` chỉ cần tham chiếu `ExamQuestionId NOT NULL` và lưu thứ tự. Kết quả cũ vẫn đúng tuyệt đối, dữ liệu giảm khoảng 50 lần.
  - Random pool sau này: snapshot cả pool vào version lúc publish; mỗi lượt chọn một tập con.
- **D-02:** chọn "copy khi thêm" thay vì tạo bảng draft riêng, vì chỉ cần một luồng dữ liệu và preview đúng với những gì sẽ publish.
- **D-03:** `PassScore` (điểm tuyệt đối) đổi thành `PassPercentage`, vì `MaxScore` thay đổi theo version.
- **D-05:**
  - Chọn cắt giờ theo `EndAt` để công bằng với đề thi theo lịch.
  - Ân hạn 30 giây là giả định *(cần xác nhận)*.
  - Job nền dùng `BackgroundService` có sẵn, không cần Hangfire.
- **D-07:**
  - Spec §144 để ngỏ cách đếm lượt; chốt là tính mọi lượt trừ `CANCELLED`.
  - Spec §145 để ngỏ "trả về lượt cũ hay 409"; chốt là trả về lượt cũ.
  - Bỏ "không giới hạn" để tránh các trường hợp biên với `AFTER_LAST_ATTEMPT`.
- **D-09:** kết hợp `AFTER_SUBMIT` với `MaxAttempts > 1` bị cấm, vì học viên xem đáp án rồi làm lại.
- **D-12:** thêm `IgnoreAccent` ngay trong MVP, vì chi phí thấp và nhu cầu thực tế cao với tiếng Việt.
- **Số lượng:** giới hạn 2–10 option, tối đa 500 câu / đề và 600 phút / đề là giả định *(cần xác nhận)*.
- **Audit:** spec gốc §31 và §119 lệch nhau. Danh sách hợp nhất nằm ở `07-bao-mat.md`.
