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
/student/profile

Quản trị  (/admin/*)
/admin                        → chuyển hướng tới /admin/dashboard
/admin/dashboard
/admin/users   /admin/users/:id
/admin/groups  /admin/groups/:id
/admin/roles
/admin/categories
/admin/questions   /admin/questions/create   /admin/questions/:id/edit
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
- Version PUBLISHED / ARCHIVED chỉ ở chế độ đọc, trừ thao tác "Sửa đáp án / Hủy câu" (cần permission `Exam.Regrade`, có hộp thoại nhập lý do, hiển thị số lượt bị ảnh hưởng trước khi xác nhận).

### 3.4 Kết quả và lượt thi
- Bảng kết quả: lọc, chuyển giữa "điểm chính thức" và "tất cả lượt", export Excel.
- Chi tiết lượt thi:
  - Từng câu với câu trả lời, đáp án đúng, điểm.
  - Dòng thời gian sự kiện (`AttemptEvents`); IP, user agent.
  - Nút gia hạn / buộc nộp / hủy lượt (đều bắt buộc nhập lý do).

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
- **Ảnh (D-27):** `MarkdownView` chỉ hiển thị `![mô tả](media:<id>)` bằng URL trong bảng `media` của DTO (cung cấp qua `<MediaUrls value={dto.media}>` hoặc prop `media`); ảnh không có trong bảng hiện "(ảnh không hiển thị được)" và không tải gì; ảnh ngoài (`http…`) bị bỏ. Ảnh co theo chiều rộng khung (màn hình 360 px), lựa chọn đáp án hiển thị ảnh inline tối đa 10rem. Trình soạn câu hỏi có nút "Chèn ảnh" cho đề bài, từng lựa chọn và phần giải thích; tab Xem trước hiển thị cả lựa chọn và giải thích.

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
