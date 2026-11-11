# 06 — Frontend

Cấu trúc thư mục và thư viện: xem `03-kien-truc.md` mục 1.2 và 7.

## 1. Route

```text
Công khai
/login
/register                     (ẩn nếu tắt tự đăng ký)
/forgot-password              (MVP: hướng dẫn liên hệ admin)
/change-password              (bắt buộc khi mustChangePassword = true)

Học viên  (/student/*)
/student                      → chuyển hướng tới /student/exams
/student/exams                danh sách đề được thi
/student/exams/:examId        chi tiết đề, hướng dẫn, nút Bắt đầu / Tiếp tục
/student/attempts/:attemptId  exam player
/student/results/:attemptId   kết quả / xem lại bài
/student/history
/student/classes              lớp của tôi (D-28)
/student/classes/:classId     đề thi được gán cho lớp
/student/profile

Quản trị  (/admin/*)
/admin                        → chuyển hướng tới /admin/dashboard
/admin/dashboard
/admin/users   /admin/users/:id
/admin/groups  /admin/groups/:id              menu "Nhóm người dùng" (UserGroups)
/admin/classes /admin/classes/:id            menu "Danh sách lớp học" (Classrooms): lớp học, thêm / rút học viên (D-28);
                                              flyout ở menu liệt kê toàn bộ lớp, ô tìm theo mã hoặc tên
/admin/videos/:id/watch                       xem bài giảng toàn màn hình (tab mới)
/admin/attendance?class=&date=               menu "Điểm danh" (D-31); tài khoản chỉ có Attendance.Take vào thẳng trang này
/admin/roles
/admin/categories
/admin/questions   /admin/questions/create   /admin/questions/:id/edit
/admin/questions/import-markdown              nhập đề từ file .md (D-29); prompt + file mẫu ở /huong-dan/; chọn .docx gốc ở bước ảnh để lấy hình; sửa câu / chọn độ khó ngay lúc xem trước (`markdownBlocks.ts`)
/admin/exams       /admin/exams/create       /admin/exams/:id
/admin/exams/:id/versions/:versionId          exam builder (các bước)
/admin/exams/:id/assignments
/admin/exams/:id/attempts
/admin/exams/:id/results
/admin/attempts/:attemptId                    chi tiết lượt thi, sự kiện, thao tác admin
/admin/audit-logs
```

**Bảo vệ route:**
- `ProtectedRoute` (đã đăng nhập), `RoleRoute`, `PermissionRoute` (dựa trên `permissions` từ `/api/auth/me`).
- Đây **chỉ là UX**. Backend luôn kiểm tra quyền.
- User có `mustChangePassword = true` bị chuyển hướng tới `/change-password` với mọi route khác.

## 2. Xác thực phía client

- Access token giữ **trong bộ nhớ** (module-level, hoặc `AuthContext` + `ref`). **Không** lưu vào `localStorage` / `sessionStorage`.
- Khi tải trang: gọi `POST /api/auth/refresh`, trình duyệt tự gửi cookie. Thành công thì có access token; thất bại thì chuyển tới `/login`.
- Chỉ **một request refresh** được chạy tại một thời điểm; các request 401 khác chờ chung promise đó.
- Đăng xuất: gọi `/api/auth/logout`, xóa token trong bộ nhớ, xóa cache của TanStack Query, xóa backup bài làm trong `sessionStorage`.
- Đồng bộ đăng xuất giữa các tab qua `BroadcastChannel('auth')`.

## 3. Màn hình quản trị

### 3.1 Dashboard
Tổng số user / học viên, số đề / số đề đang mở, lượt thi hôm nay, điểm trung bình, tỉ lệ đạt, số lượt đang làm.

"Hôm nay" được tính theo múi giờ `Asia/Ho_Chi_Minh`; backend nhận khoảng thời gian UTC tương ứng.

### 3.2 Ngân hàng câu hỏi
- Tìm kiếm, lọc theo danh mục / loại / trạng thái, phân trang, sắp xếp.
- Tạo, sửa, clone, bật/tắt, xem trước.
- Trình soạn thảo:
  ```text
  QuestionEditor
  ├── QuestionBasicFields      (mã, danh mục, điểm mặc định, định dạng nội dung)
  ├── ContentEditor            (textarea + tab Xem trước Markdown)
  ├── QuestionTypeSelector
  ├── SingleChoiceEditor | MultipleChoiceEditor | TrueFalseEditor | FillInEditor
  └── ExplanationEditor
  ```
- **Mở câu đã có thì ở tab Xem trước** (tab đầu): một khung gộp đề bài, đáp án (lựa chọn đúng nền xanh + nhãn "Đáp án đúng"; câu Đúng/Sai ghi Đúng / Sai từng mệnh đề; câu điền đáp án hiện đáp án số kèm sai số hoặc các đáp án chữ được chấp nhận) và lời giải, hiển thị như học viên thấy (công thức, hình); các khung soạn đáp án / giải thích bên dưới ẩn đi. Tab **"Nội dung"** mới hiện ô soạn mã (`$…$`) của đề cùng khung soạn đáp án và giải thích. Câu mới mở thẳng tab Nội dung; Lưu bị lỗi nhập liệu thì tự chuyển sang tab Nội dung để thấy ô báo lỗi.
- Chỉ render editor ứng với `questionType`. Đổi loại câu thì hỏi xác nhận vì dữ liệu option sẽ bị xóa.
- Option: thêm / xóa / kéo thả sắp xếp; đánh dấu đúng. Với chọn một thì dùng radio, tức là chỉ được chọn 1.
- `FillInEditor`:
  - Với `TEXT`: danh sách đáp án chấp nhận (1–20), công tắc `CaseSensitive`, `IgnoreAccent`, và ô "Thử đáp án" để kiểm tra chuẩn hóa ngay trên client (cùng thuật toán với server; kết quả cuối cùng vẫn do server chấm).
  - Với `NUMBER`: đáp án và sai số.
- Zod schema khớp với quy tắc ở `02-nghiep-vu.md` mục 1.1.

### 3.3 Exam builder
```text
ExamBuilder
├── Bước 1  ExamBasicInfo      tên, mã, mô tả, hướng dẫn, khung giờ, số lượt, cách tính điểm thi lại
├── Bước 2  QuestionPicker     tìm trong ngân hàng, lọc, chọn nhiều → thêm
│           SelectedQuestionList  sắp xếp kéo thả, sửa điểm, xóa, cảnh báo "câu gốc đã thay đổi", đồng bộ
├── Bước 3  ExamSettings       thời lượng, % đạt, ScoreVisibility, ReviewPolicy
├── Bước 4  AccessSettings     PUBLIC / ASSIGNED, chọn nhóm / người
├── Bước 5  ExamPreview        render bằng đúng component ExamPlayer ở chế độ preview (không lưu, không tính giờ)
└── Bước 6  PublishDialog      gọi /validate, hiển thị toàn bộ lỗi kèm liên kết tới bước tương ứng, xác nhận publish
```
- Khi đề đã publish, builder mở version DRAFT (nếu có) hoặc có nút "Tạo phiên bản mới".
- Bảng "Phiên bản" ở trang chi tiết đề có thao tác theo dòng (hiện theo quyền): version DRAFT → "Xuất bản" (`Exam.Publish`;
  hộp thoại gọi `validate` trước, chỉ cho xác nhận khi hợp lệ, báo version PUBLISHED hiện tại sẽ chuyển ARCHIVED);
  version PUBLISHED → "Đóng" khi đề `PUBLISHED`, "Mở lại" khi đề `CLOSED` (`Exam.Close`; đóng / mở là trạng thái của đề).
  Version ARCHIVED không có thao tác. Publish khi đề đang `CLOSED` không tự mở lại đề.
- Version PUBLISHED / ARCHIVED chỉ ở chế độ đọc, trừ thao tác "Sửa đáp án / Hủy câu" (cần permission `Exam.Regrade`, có hộp thoại nhập lý do, hiển thị số lượt bị ảnh hưởng trước khi xác nhận).

### 3.4 Kết quả và lượt thi
- Bảng kết quả: lọc, chuyển giữa "điểm chính thức" và "tất cả lượt", export Excel.
- Chi tiết lượt thi:
  - Từng câu với câu trả lời, đáp án đúng, điểm.
  - Dòng thời gian sự kiện (`AttemptEvents`); IP, user agent.
  - Nút gia hạn / buộc nộp / hủy lượt (đều bắt buộc nhập lý do).

### 3.5 Kho video bài giảng

- Form bài giảng: dán link YouTube thì tự lấy tiêu đề video (`/api/videos/youtube-info`) điền vào ô tiêu đề khi ô trống hoặc
  đang là tiêu đề tự điền trước đó; người dùng đã tự gõ tiêu đề thì chỉ hiện nút "Dùng tiêu đề này". Ô "Mô tả / Ghi chú" cũng
  được điền tiêu đề video theo cùng quy tắc (trống hoặc đang là giá trị tự điền). Ảnh bìa lấy từ mã video.
- Ô "Lớp học được xem" và bộ lọc lớp lấy **toàn bộ** lớp học (`classesApi.listAll`, gọi lần lượt từng trang 100 lớp;
  hook `useAllClassrooms`), tìm theo mã hoặc tên. Video gắn với Lớp học (`Classrooms`), không phải Nhóm người dùng.
  Form chọn được **nhiều lớp** (`components/common/MultiSearchableSelect.tsx`: lớp đã chọn là thẻ, bấm × để bỏ);
  không chọn lớp nào = bài chung. Thẻ / trang xem hiện nhãn từng lớp (`LessonClassBadges`, quá 3 lớp gộp "+N lớp").
- Form bài giảng không có ô thời lượng và thứ tự sắp xếp: tạo mới gửi mặc định (`durationMinutes` null, `displayOrder` 0),
  sửa thì gửi lại giá trị đang lưu của bài giảng.
- Thẻ bài giảng: ảnh bìa YouTube / trang đầu tệp Drive; bài có tài liệu đi kèm gắn nhãn "Kèm tài liệu PDF".
- Bấm xem bài giảng mở **tab mới** tới trang xem toàn màn hình (`features/videos/VideoWatchPage.tsx`, không có menu):
  admin `/admin/videos/:id/watch` (quyền `Video.View`, `GET /api/videos/{id}`), học viên `/student/videos/:id/watch`
  (`GET /api/student/videos/{id}`). Tab mới tự lấy phiên đăng nhập qua refresh cookie.
- Xem bài có cả video và tài liệu (`documentUrl`): video bên trái, tài liệu Drive bên phải;
  thanh giữa (`features/videos/SplitView.tsx`, `role="separator"`) kéo bằng chuột / cảm ứng, phím mũi tên ±5%,
  Home / End, nhấp đúp để chia đều; tỉ lệ 15–85% nhưng mỗi khung không nhỏ hơn 240px (xếp dọc: 160px) để không bị che mất.
  Video YouTube / file trực tiếp luôn giữ tỉ lệ 16:9 và co vừa khung (`.lesson-media-fit`, container query), không bị cắt.
  Màn hẹp hơn 768px xếp trên / dưới. Khi kéo, hai khung không nhận chuột (`pointer-events: none`) để iframe YouTube / Drive
  không giữ mất sự kiện.
- Bài chỉ có tài liệu Drive (link ở `videoUrl`): xem toàn màn hình như trang xem của Drive.

## 4. Exam player (học viên)

### 4.1 Bố cục
```text
+------------------------------------------------------------+
| C# Basic                     ● Đã lưu        ⏱ 38:25       |
+----------------------------------+-------------------------+
| Câu 3/10                  [☆ Đánh dấu] | Danh sách câu     |
|                                  | [1✓][2✓][3 ][4⚑][5 ]    |
| Nội dung câu hỏi (Markdown)      | [6 ][7 ][8 ][9 ][10]    |
|                                  |                         |
| ( ) A. ...                       | Chú thích:              |
| (•) B. ...                       | ✓ Đã trả lời  ⚑ Xem lại |
|                                  | □ Chưa trả lời          |
| [← Câu trước]      [Câu sau →]   | Đã trả lời: 6/10        |
+----------------------------------+-------------------------+
|                         [ NỘP BÀI ]                        |
+------------------------------------------------------------+
```
- Trên mobile: danh sách câu thu vào một nút mở `Offcanvas`; đồng hồ luôn cố định ở đầu màn hình.
- Trạng thái câu được thể hiện bằng **biểu tượng và chữ**, không chỉ bằng màu.

### 4.2 Component
```text
ExamPlayer
├── ExamHeader
│   ├── ExamTimer
│   └── SaveStatusIndicator      (Đã lưu / Đang lưu / Chưa lưu – mất kết nối)
├── QuestionPanel
│   └── AnswerInput → SingleChoiceInput | MultipleChoiceInput | TrueFalseInput | FillInInput
├── QuestionNavigator
├── MarkForReviewToggle
└── SubmitExamDialog             (liệt kê câu chưa trả lời và câu đánh dấu xem lại)
```

### 4.3 State
```ts
type AnswerDraft = {
  selectedOptions: string[];
  answerText: string | null;
  isMarkedForReview: boolean;
  clientSeq: number;          // seq của lần sửa gần nhất
  syncedSeq: number;          // seq đã được server xác nhận
};

type ExamPlayerState = {
  attemptId: string;
  currentIndex: number;
  answers: Record<string, AnswerDraft>;      // key = attemptQuestionId
  clockOffsetMs: number;                     // serverTime − Date.now() lúc nhận response
  expiredAt: string;
  connection: 'online' | 'offline';
  submitting: boolean;
};
```
- Dùng `useReducer`.
- **Không** lưu `remainingSeconds` trong state; giá trị này được suy ra mỗi lần render: `remaining = expiredAt − (Date.now() + clockOffsetMs)`.
- Một câu "chưa lưu" khi `clientSeq > syncedSeq`.

### 4.4 Autosave (D-18)
1. Người dùng thay đổi câu trả lời: cập nhật state ngay, tăng `clientSeq`, ghi backup.
2. Debounce **500 ms** cho mỗi câu. Hết debounce thì đưa câu vào hàng đợi gửi.
3. Hàng đợi gom mọi câu chưa lưu thành một batch `PUT /answers` (tối đa 50 phần tử). **Mỗi thời điểm chỉ có một request lưu đang chạy.**
4. Thành công: cập nhật `syncedSeq`, `clockOffsetMs`, `expiredAt` từ response.
5. Lỗi mạng hoặc 5xx: thử lại theo backoff (1s, 2s, 4s, tối đa 15s), giữ nguyên dữ liệu và hiển thị "Chưa lưu". Khi có sự kiện `online`, gửi lại ngay.
6. Lỗi `ATTEMPT_EXPIRED` / `ATTEMPT_NOT_IN_PROGRESS`: dừng lưu, tải lại lượt thi, chuyển sang trang kết quả.
7. `clientSeq` được sinh bằng `max(lastSeq + 1, Date.now())`. Như vậy seq tăng dần ngay cả qua các lần tải lại trang, và giữa các tab trên cùng một máy.

**Backup cục bộ:**
- Key `exam_attempt_{attemptId}` trong **`sessionStorage`** (không dùng `localStorage`, vì phòng máy dùng chung).
- Chỉ lưu câu trả lời và `clientSeq` của học viên. **Không bao giờ** lưu đáp án đúng, token hay điểm.
- Khi tải lại trang:
  - Lấy lượt thi từ server.
  - Với từng câu, nếu backup có `clientSeq` lớn hơn giá trị server trả về thì dùng bản backup và đưa vào hàng đợi gửi lại.
- Xóa backup khi nộp bài thành công hoặc khi đăng xuất.

**Nhiều tab:**
- Dùng `BroadcastChannel('attempt_{id}')` để phát hiện tab khác đang mở cùng lượt thi.
- Hiển thị cảnh báo "Bài thi đang mở ở một tab khác" và ghi sự kiện `MULTI_TAB_DETECTED`.
- Nhờ `clientSeq`, dữ liệu vẫn đúng ngay cả khi học viên bỏ qua cảnh báo.

### 4.5 Timer
- Tính lại mỗi giây từ `expiredAt` và `clockOffsetMs`. Không lưu bộ đếm cục bộ.
- Cảnh báo ở mốc 5 phút và 1 phút: thông báo `role="status"`; ở mốc 1 phút đồng hồ chuyển màu **và** có chữ "Sắp hết giờ".
- Khi `remaining ≤ 0`:
  - Hiển thị lớp phủ "Đã hết giờ, đang nộp bài…".
  - Đẩy hàng đợi lưu (nếu còn trong khoảng ân hạn) rồi gọi `submit`.
  - Backend vẫn là bên quyết định cuối cùng; nếu tab bị đóng, job nền sẽ nộp.
- Mỗi 60 giây gọi nhẹ `GET /attempts/{id}` (hoặc dùng response của autosave) để hiệu chỉnh `clockOffsetMs` và nhận gia hạn từ admin.

### 4.6 Nộp bài
- `SubmitExamDialog` hiển thị số câu chưa trả lời, số câu đánh dấu xem lại, và cảnh báo nếu còn câu chưa lưu.
- Khi xác nhận:
  - Lưu nốt mọi câu còn trong hàng đợi.
  - Gọi `submit` và vô hiệu hóa nút.
  - Lỗi mạng thì thử lại. `submit` là idempotent nên an toàn khi gọi lặp.
- `beforeunload` cảnh báo khi còn câu chưa lưu.

### 4.7 Sự kiện lượt thi
- Lắng nghe:
  - `visibilitychange`, `blur`, `focus`: có debounce, gộp các sự kiện cách nhau dưới 1 giây.
  - `online`, `offline`, `fullscreenchange`.
  - `paste` trong ô điền đáp án.
- Gửi theo lô mỗi 10 giây hoặc khi có ≥ 20 sự kiện.
- Không chặn thao tác của học viên. Đây là công cụ ghi nhận, **không phải** chống gian lận tuyệt đối.
- Hiển thị một dòng thông báo ở trang bắt đầu: "Hệ thống ghi nhận việc rời khỏi trang thi."

### 4.8 Kết quả học viên
- Điểm, điểm tối đa, phần trăm, số câu đúng / tổng, thời gian làm bài, thời điểm nộp, Đạt / Không đạt (nếu có cấu hình).
- Nếu điểm bị ẩn: "Đã nộp bài. Điểm sẽ được công bố lúc …" (dùng `reviewAvailableAt` hoặc `endAt`).
- Nếu được xem lại: từng câu với lựa chọn của học viên, đáp án đúng, giải thích.

### 4.9 Trang điểm danh (D-31)

- Chọn lớp (ô tìm) và ngày (nút ‹ ›, không chọn ngày tương lai); lớp / ngày nằm trên URL để mở lại đúng buổi.
- **Lưu tự động:** mỗi thay đổi đánh dấu học sinh "chờ lưu", gom lại gửi sau 0,8 giây (một request `PUT` cho nhiều thay đổi); đổi lớp / ngày hoặc xuất Excel thì gửi ngay; lỗi thì giữ lại để gửi lần sau và hiện "Thử lại". Đang chờ lưu mà đóng tab → trình duyệt hỏi lại.
- Dữ liệu buổi không tải lại ngầm (`staleTime: Infinity`, không refetch khi focus) để không ghi đè thao tác đang làm.
- Thẻ thống kê (sĩ số / có mặt / muộn / có phép / không phép) bấm để lọc; thêm lọc "chưa điểm danh", "BTVN cần lưu ý" (thiếu, không làm hoặc điểm < 5); sắp xếp theo STT hoặc điểm.
- Điểm: nút − / +, gõ được dấu phẩy, tự làm tròn bước 0,5; `Enter` / `↓` / `↑` sang dòng khác; phím `/` tìm học sinh. Nhập điểm khi chưa chọn mức BTVN thì gợi ý mức (≥ 8,5 Xuất sắc, ≥ 5 Đạt, còn lại Làm thiếu).
- "Tất cả có mặt" / "100% Đạt BTVN" chỉ áp cho học sinh chưa điểm danh / chưa chấm, không ghi đè lựa chọn đã có.
- Nhận xét nhanh (chip) theo trạng thái và học lực; không thêm câu đã có.
- Báo cáo Zalo, tổng kết tháng: soạn ở client (`attendanceLogic.ts`), giáo viên sao chép.
- Biểu đồ tháng: SVG tự vẽ, dải màu ≥ 8 / 6,5 / 5 / < 5 (không thêm thư viện biểu đồ).

## 5. Quy tắc UX chung

- Chỉ dùng Bootstrap 5 / `react-bootstrap`.
- **Form:**
  - Mọi trường đều có label; lỗi hiển thị ngay dưới trường.
  - Vô hiệu hóa nút submit khi đang gửi; có trạng thái loading.
  - Toast thông báo thành công / thất bại.
  - Hỏi xác nhận trước thao tác không đảo ngược được.
- **Bảng:** phân trang phía server, sắp xếp theo whitelist, cuộn ngang khi màn hình hẹp.
- **Xử lý lỗi:** map `errors[].code` sang thông điệp i18n. Lỗi 409 `CONCURRENCY_CONFLICT` hiển thị "Dữ liệu đã được người khác cập nhật" kèm nút tải lại.
- **Ngày giờ:** hiển thị theo `Asia/Ho_Chi_Minh`, định dạng `dd/MM/yyyy HH:mm`. Ô nhập ngày giờ được hiểu theo giờ Việt Nam rồi đổi sang UTC trước khi gửi.
- **Markdown:** một component `MarkdownView` duy nhất dùng `react-markdown` + `remark-gfm`, không có `rehype-raw`. Liên kết mở tab mới với `rel="noopener noreferrer"`. Code block dùng font monospace, cuộn ngang được.
- **Ảnh (D-27):** `MarkdownView` chỉ hiển thị `![mô tả](media:<id>)` bằng URL trong bảng `media` của DTO (cung cấp qua `<MediaUrls value={dto.media}>` hoặc prop `media`); ảnh không có trong bảng hiện "(ảnh không hiển thị được)" và không tải gì; ảnh ngoài (`http…`) bị bỏ. Ảnh co theo chiều rộng khung (màn hình 360 px), lựa chọn đáp án hiển thị ảnh inline tối đa 10rem. Trình soạn câu hỏi có nút "Chèn ảnh" cho đề bài, từng lựa chọn và phần giải thích; tab Xem trước hiển thị cả lựa chọn và giải thích. Ảnh mới chọn là **ảnh chờ lưu** (`features/questions/pendingImages.ts`): xem trước bằng data URL (CSP đã cho `img-src data:`), có danh sách "Ảnh đã chọn — chưa lưu lên máy chủ" với nút Bỏ; bấm Lưu thì tải lên các ảnh còn dùng, thay mã tạm bằng id thật, rồi lưu câu hỏi (02-nghiep-vu mục 1.4).
- **Giao diện (theme):** Bootstrap 5 biên dịch từ Sass (`src/theme.scss`) + `src/styles.css`. Giữ màu nhận diện SClass (navy `#0c2363`, cyan `#00d2ff`, đỏ ở khẩu hiệu) cho logo, thanh trên, nút chính; thêm mảng **pastel** (vàng chanh, tím nhạt, xanh trời, hồng…) cho nền loang, ô icon, đầu thẻ — tham khảo phong cách landing page giáo dục (bản cập nhật 2026-11). Màu nhấn theo khu chức năng: `.accent-blue|indigo|violet|pink|sky|teal|green|amber|orange` (đặt `--accent`, `--accent-soft`, `--accent-strong`). **Font:** tiêu đề, số liệu, nút, menu dùng **Be Vietnam Pro**; nội dung đọc (câu hỏi, bảng, form) giữ **Arial**. Font và **Bootstrap Icons** đóng gói trong bản build (không tải từ CDN nên CSP không đổi). Thành phần dùng chung: `page-title` (tiêu đề trang có gạch màu), `sclass-hero` / `page-hero` (banner navy có quầng cyan + hình nổi), `stat-card` + `stat-icon` (thẻ số liệu, icon trên nền màu nhạt), `exam-card` + `exam-card-band` (thẻ đề / lớp có mảng pastel phía trên, icon nổi ở mép), `section-title` + `section-title-icon`. Bố cục: admin có sidebar trắng, icon trong ô màu nhạt, mục đang chọn nền navy; Tổng quan có banner chào theo giờ + nút đi nhanh theo quyền; học viên có thanh điều hướng navy, banner chào, thẻ đề có thanh tiến độ lượt thi; trang đăng nhập / đăng ký: nền pastel loang, giới thiệu (tiêu đề có từ tô màu, 3 điểm nổi bật đánh số) bên trái, form bo lớn với nhãn nổi bên phải. Hiệu ứng trôi nhẹ tắt khi người dùng bật giảm chuyển động (`prefers-reduced-motion`). **Trang làm bài và trang kết quả** (`src/styles-exam.css`): thanh trên kính mờ có thanh tiến độ số câu đã trả lời, đồng hồ dạng viên (xanh trời → vàng khi còn ≤ 5 phút → đỏ nhấp nháy khi còn ≤ 1 phút), thẻ câu hỏi có dải màu trên cùng, phương án bấm chỗ nào trong ô cũng chọn, bảng câu dạng lưới (xanh mint: đã trả lời, chấm vàng: đánh dấu xem lại) kèm vòng tròn % đã làm, khối "Nộp bài" cuối trang; trang kết quả có phần đầu theo màu kết quả (xanh: đạt, hồng: chưa đạt, xanh dương: đề không xét đạt / chưa công bố điểm) với vòng tròn %, lời chúc / động viên, 4 thẻ số liệu (điểm, câu đúng, câu chưa đúng, đạt), dãy số câu để nhảy tới và thẻ xem lại có dải màu đúng / sai bên trái. **Video và bảng quản trị** (`src/styles-pages.css`): trang video của học viên dùng banner `sclass-hero` (kèm số bài giảng); thanh lọc `filter-bar`; thẻ video `video-card` nhấc lên khi rê chuột, ảnh phóng nhẹ, nút phát nổi lên, nền khung ảnh navy → tím, lớp tối dần ở đáy ảnh cho nhãn dễ đọc. Bảng `DataTable` / `.table-card`: bỏ sọc, hàng thoáng hơn, rê chuột nền tím nhạt, cột sắp xếp được có icon (`bi-arrow-down-up`, cột đang sắp xếp tô xanh), nhãn trạng thái dạng viên nền nhạt (thay nền đậm), nút thao tác bo góc nền trắng; `SearchBox` có icon kính lúp; phân trang (`Pager`, trang video) hiện tối đa 5 số trang quanh trang hiện tại (`pageWindow`), trang đang chọn nền navy; `Empty` có icon hộp thư trên nền tím nhạt. **Hiệu ứng chung** (`src/styles-effects.css`, áp cho mọi trang): hàng lọc (Row chứa `SearchBox`) và thẻ `.filter-bar` có nền pastel + viền gradient xanh → tím → vàng; ô nhập rê chuột viền tím nhạt, đang gõ viền tím chàm có quầng sáng. **Bảng theo phong cách "Data Table v3"**: tiêu đề cột chữ in hoa nhỏ màu xám tím trên nền `#f4f2ff`; hàng nền trắng chỉ kẻ ngang mảnh; rê chuột nền tím rất nhạt (đổi `--bs-table-bg-state`, không dùng bóng đổ — Bootstrap vẽ nền ô bằng `box-shadow` nên hiệu ứng bóng / chuyển tiếp bóng làm lóe mảng tím ở ô đầu); hàng có ô tích được chọn nền tím nhạt cả hàng (`:has(:checked)`); ô tích vuông bo góc màu tím chàm; mã / tên dạng liên kết màu tím chàm; nhãn trạng thái có chấm màu trước chữ; các hàng hiện mờ dần khi bảng tải / đổi trang / đổi lọc. **Màn hẹp (< 768px)**: `DataTable` (class `table-stack`) đổi mỗi hàng thành một thẻ, mỗi ô ghi tên cột bên trái (`data-label` = tiêu đề cột dạng chữ), ô trống bị ẩn, ô tích / nút thao tác không có nhãn; không còn cuộn ngang. Màn rộng: khung `.table-card` cuộn ngang bên trong khi bảng rộng hơn khung (không được để `overflow: hidden` — cột cuối bị cắt và cả trang tràn ngang); khung là container (`container-type: inline-size`) nên cột phụ `d-xxl-table-cell` / `d-xl-table-cell` ẩn khi **khung bảng** hẹp hơn 1360 / 1000px; khung phải có `position: relative` (chữ ẩn `.visually-hidden` và KaTeX MathML trong ô là `position: absolute`, thiếu khung chứa định vị thì thoát khỏi vùng cuộn và làm trang tràn ngang) (cửa sổ 1400px trừ menu trái 260px chỉ còn ~1140px cho bảng). Nút chính có vệt sáng lướt qua (trừ nút xổ menu); trang Điểm danh: thẻ thống kê nổi khi rê chuột / khi đang lọc, nút lọc đang chọn nền tím chàm, nhóm nút trạng thái bo viên. Khi bật giảm chuyển động: không có vệt sáng.

## 6. Accessibility

- Điều hướng được hoàn toàn bằng bàn phím, focus hiển thị rõ.
- Nhóm lựa chọn dùng `fieldset` / `legend`; option là `input type="radio"` hoặc `checkbox` thật.
- Danh sách câu là các `button` có `aria-label`, ví dụ "Câu 4, chưa trả lời, đã đánh dấu xem lại". Không dùng màu làm tín hiệu duy nhất.
- Lỗi form gắn qua `aria-describedby`; toast và cảnh báo thời gian dùng `role="status"`.
- Độ tương phản tối thiểu WCAG AA.

## 7. i18n

- `react-i18next`; ngôn ngữ mặc định `vi`, có sẵn cấu trúc cho `en`.
- Mọi chuỗi hiển thị nằm trong `i18n/*.json`. Enum từ API được map sang nhãn qua i18n; không hiển thị giá trị thô `SINGLE_CHOICE`.

## 8. Quyết định / Giả định

- **(D-27) Bảng ảnh truyền qua context `MediaUrls`** thay vì sửa chuỗi Markdown ở server: nội dung giữ nguyên `media:<id>` nên trình soạn câu hỏi sửa và lưu lại được mà không dính URL đã ký.
- **Trang xem lại bài của học viên render nội dung lựa chọn bằng `MarkdownView inline`** (trước đây là chữ thuần), để ảnh và định dạng giống lúc làm bài.

- **Backup dùng `sessionStorage`** thay cho `localStorage` như spec gốc §112, để tránh lộ bài trên máy dùng chung. Đánh đổi: đóng hẳn tab thì mất backup, nhưng dữ liệu đã lưu lên server vẫn còn.
- **Bỏ `remainingSeconds` khỏi state** (spec gốc §71), vì đây là giá trị suy ra được.
- **Thêm `serverTime`, `clockOffsetMs`, hàng đợi lưu đơn luồng, `clientSeq`** để xử lý tình trạng request đến sai thứ tự và mở nhiều tab.
- **"Đánh dấu xem lại" được lưu lên server**, vì spec gốc chỉ giữ trong state nên bị mất khi F5.
- **Debounce 500 ms**, nằm trong khoảng 300–700 ms của spec gốc.
- **Route kết quả đổi thành số nhiều** (`/student/results/:attemptId`) và bổ sung các route còn thiếu: `/admin/categories`, `/admin/groups`, `/admin/roles`, `/admin/audit-logs`, `/admin/attempts/:id`, `/student/profile`, `/change-password`.

**Bổ sung khi triển khai (M7):**

- **Code splitting theo route** bằng `React.lazy`: màn hình quản trị, player và `react-markdown` không nằm trong bundle đăng nhập. Học viên chỉ tải phần của học viên.
- **Tải ngầm trang sau khi đăng nhập** (`app/pageLoaders.ts`, `prefetchPages`): vào khu quản trị / học viên thì lần lượt tải sẵn chunk các trang của khu đó khi trình duyệt rảnh (học viên: phòng thi trước), để chuyển trang không phải chờ tải qua mạng (mỗi request qua Cloudflare + tunnel tốn 100–300 ms). Bỏ qua khi bật tiết kiệm dữ liệu / mạng 2G; lỗi tải ngầm bỏ qua, trang vẫn tải khi mở.
- **Nội dung lựa chọn đáp án render inline** (`MarkdownView inline`). Chế độ này chỉ giữ `strong`, `em`, `del`, `code`, `a`, `br`; đoạn văn thành `span`; khối code bị gỡ thành `code` inline. Lý do: `<label>` không được chứa `div` / `p` / `pre`. Nội dung lựa chọn cần khối code thì đưa lên đề bài.
- **Cột "Nội dung" trong bảng** (danh sách câu hỏi, builder, thống kê câu hỏi) hiển thị bản rút gọn văn bản thuần (`markdownExcerpt`), không render Markdown.
- **Điều hướng sau đăng nhập:**
  - Chủ động đăng xuất (kể cả đăng xuất từ tab khác) → đăng nhập lại luôn về trang chủ theo vai trò.
  - Phiên hết hạn → chỉ quay lại trang đang xem nếu **cùng người dùng** đăng nhập lại (`resolveLoginRedirect`). Tránh việc người dùng sau trên máy dùng chung bị đưa vào trang của người trước.
  - Deep link lúc chưa đăng nhập vẫn được giữ.
- **Backup bài làm trong `sessionStorage`:** chỉ xóa khi chủ động đăng xuất. Phiên hết hạn giữa giờ thi thì giữ lại, để đăng nhập lại không mất câu trả lời chưa lưu. Backup gắn với `attemptId`, và server luôn kiểm tra chủ sở hữu lượt thi.
- **Giờ hiển thị và ô nhập giờ luôn theo giờ Việt Nam** (`Asia/Ho_Chi_Minh`), không theo múi giờ máy. Ô `datetime-local` được hiểu là giờ Việt Nam rồi đổi sang UTC trước khi gửi.
- **Thông báo lỗi lấy theo `code`** trong `i18n/vi.json` (`errors.<CODE>`). Không có bản dịch thì dùng `message` của server.
- **Dev server:** Vite proxy `/api` và `/health` sang `http://localhost:5136`. Cookie refresh vì thế là same-origin, giống môi trường production sau Nginx.
