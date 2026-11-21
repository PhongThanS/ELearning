# Việc cần làm ở frontend

Kết quả review nhanh ngày 17/11/2026 trên `frontend/elearning-web`. Số liệu lấy bằng lệnh đếm, chưa đọc kỹ từng file, nên trước khi làm mỗi mục hãy chạy lại lệnh "Tìm" để có danh sách chính xác.

Hiện trạng tốt, giữ nguyên: `typecheck` và `lint` sạch, không có `any` / `ts-ignore`, chỉ một instance Axios.

**Quy tắc chung cho mọi mục**
- Mỗi mục một commit (hoặc một PR), không gộp. Các mục 1–3 chỉ đổi cấu trúc, **không được đổi hành vi**.
- Sau mỗi bước chạy trong `frontend/elearning-web/`:
  ```bash
  npm run lint && npm run typecheck && npm run test && npm run build
  ```
- Thứ tự nên làm: 1 → 5 → 2 → 3 → 4 → 6. Mục 1 và 5 nhỏ, ít rủi ro; mục 3 nên làm sau mục 1 và 2 để file đã gọn hơn.

---

## 1. Gom `queryKey` về một chỗ (nhỏ, làm trước)

**Vấn đề:** khoảng 127 chỗ viết `queryKey: [...]` bằng tay. Gõ lệch một chuỗi thì `invalidateQueries` không khớp, màn hình hiện dữ liệu cũ mà không báo lỗi.

**Tìm:**
```bash
grep -rnE "queryKey: \[|invalidateQueries|setQueryData|removeQueries" src --include=*.ts --include=*.tsx
```

**Cách làm:**
1. Tạo `src/services/queryKeys.ts`, mỗi nhóm tài nguyên một object, khóa cha là tiền tố của khóa con:
   ```ts
   export const queryKeys = {
     exams: {
       all: ["exams"] as const,
       list: (params: object) => ["exams", "list", params] as const,
       detail: (id: string) => ["exams", "detail", id] as const,
       version: (id: string, versionId: string) => ["exams", "detail", id, "version", versionId] as const,
     },
     // questions, categories, classes, users, groups, videos, attendance, attempts…
   };
   ```
2. Liệt kê các khóa đang dùng (lệnh "Tìm"), ghi lại **đúng chuỗi hiện tại** vào file trên. Không đổi tên khóa trong bước này.
3. Thay từng feature một (`exams` → `questions` → …), mỗi feature chạy lại bộ kiểm tra.
4. Chỗ `invalidateQueries` đang liệt kê nhiều khóa con thì đổi sang khóa cha (`queryKeys.exams.all`).

**Xong khi:** lệnh "Tìm" không còn mảng khóa viết tay ngoài `queryKeys.ts`.

---

## 2. Đưa chuỗi hiển thị về `i18n/vi.json`

**Vấn đề:** ước lượng thô khoảng 418 chỗ có chữ tiếng Việt viết thẳng trong JSX, so với 374 lời gọi `t("…")`. Quy ước dự án (`CLAUDE.md`, `docs/06-frontend.md` mục 7) là mọi chuỗi hiển thị nằm trong `i18n/vi.json`.

**Tìm** (con số là ước lượng; lệnh chỉ bắt chữ nằm giữa hai thẻ, chưa bắt `placeholder`, `title`, `aria-label`, toast):
```bash
grep -rnE ">[^<{]*[àáạảãâăđêôơưéèẹ][^<{]*<" src --include=*.tsx
grep -rnE "(placeholder|title|aria-label|label)=\"[^\"]*[àáạảãâăđêôơư]" src --include=*.tsx
```

**Cách làm:**
1. Làm theo từng feature, bắt đầu từ file ít chuỗi nhất để chốt cách đặt khóa.
2. Đặt khóa theo `<feature>.<màn hình>.<ý nghĩa>`, ví dụ `exams.detail.publishButton`. Xem cấu trúc đang có trong `vi.json` và theo đúng kiểu đó; chuỗi dùng chung (Lưu, Hủy, Xóa, Đang tải…) dùng lại khóa `common.*` nếu đã có.
3. Chuỗi có biến dùng nội suy của i18next: `t("exams.list.count", { count })`, không nối chuỗi.
4. Không dịch lại nội dung: chép nguyên văn sang `vi.json`.
5. Test đang tìm phần tử theo chữ hiển thị vẫn chạy được vì chữ không đổi.

**Xong khi:** hai lệnh "Tìm" chỉ còn kết quả là nội dung không phải giao diện (dữ liệu mẫu trong test, comment).

---

## 3. Tách các file trang quá lớn

**Vấn đề:**

| File | Số dòng |
|---|---|
| `features/questions/QuestionPages.tsx` | 1.703 |
| `features/exams/ExamPages.tsx` | 1.509 |
| `features/admin/IdentityPages.tsx` | 1.254 |
| `features/attendance/AttendancePage.tsx` | 1.033 |
| `features/attempts/StudentPages.tsx` | 1.022 |

**Tìm** các component và hàm được khai báo trong một file:
```bash
grep -nE "^(export )?(function|const) [A-Z]" src/features/questions/QuestionPages.tsx
```

**Cách làm (mỗi file một commit):**
1. Mỗi trang (component được `routes.tsx` tải) ra một file riêng cùng thư mục, ví dụ `QuestionsPage.tsx`, `QuestionEditorPage.tsx`, `CategoriesPage.tsx`.
2. Hộp thoại và component con chỉ một trang dùng thì đi theo trang đó; từ hai trang trở lên dùng thì ra file riêng trong cùng feature.
3. Hàm thuần (tính toán, định dạng) ra file `.ts` cùng feature để viết test được.
4. **Giữ file `*Pages.tsx` cũ làm file xuất lại** (`export { QuestionsPage } from "./QuestionsPage";`). `app/pageLoaders.ts` và `routes.tsx` tải trang theo tên export của file này, nên giữ lại thì không phải sửa route và không đổi cách tách bundle.
5. Chỉ di chuyển code, không sửa logic trong cùng commit. Diff phải đọc được là "cắt – dán".

**Xong khi:** không còn file nào trên khoảng 500 dòng trong 5 file trên, và `npm run build` cho số chunk theo trang như trước.

---

## 4. Sinh kiểu DTO từ OpenAPI thay cho `types/api.ts` viết tay

**Vấn đề:** `src/types/api.ts` 1.243 dòng viết tay. Backend đổi DTO mà quên sửa file này thì TypeScript không báo, lỗi chỉ lộ lúc chạy.

**Cách làm:**
1. Backend đã phát tài liệu ở `/openapi/v1.json` (môi trường không phải Production). Chạy API dev rồi tải về:
   ```bash
   curl -s http://localhost:5136/openapi/v1.json -o openapi.json
   ```
2. Thêm công cụ sinh kiểu làm devDependency (ví dụ `openapi-typescript`; kiểm tra license MIT trước khi thêm, theo `docs/03-kien-truc.md` mục 1.3) và script:
   ```json
   "gen:api": "openapi-typescript openapi.json -o src/types/api.generated.ts"
   ```
3. **Không thay một lần.** Giữ `api.ts`, đổi dần từng nhóm kiểu thành alias trỏ sang kiểu sinh ra:
   ```ts
   import type { components } from "./api.generated";
   export type ExamDetail = components["schemas"]["ExamDetailDto"];
   ```
   Chỗ nào TypeScript báo lỗi sau khi đổi chính là chỗ kiểu viết tay đang lệch backend: ghi lại và sửa từng chỗ.
4. Kiểm tra enum sinh ra đúng dạng `UPPER_SNAKE_CASE` và ngày giờ là `string`. Nếu tài liệu OpenAPI mô tả enum là số thì sửa cấu hình OpenAPI ở backend trước, không sửa tay file sinh ra.
5. Khi đã ổn, thêm bước CI trong `frontend.yml`: sinh lại và `git diff --exit-code` để phát hiện DTO đổi mà chưa sinh lại. Bước này cần `openapi.json` được commit hoặc được sinh trong CI từ backend.

**Xong khi:** `api.ts` chỉ còn alias và các kiểu thuần frontend.

---

## 5. Dữ liệu trong `localStorage` trên máy dùng chung (nhỏ)

**Vấn đề:** hai nơi ghi `localStorage`, dữ liệu còn lại sau khi đăng xuất, người dùng sau trên cùng máy thấy được:
- `src/layouts/recentItems.ts`: danh sách mục vừa mở.
- `src/features/attendance/DailyReportModal.tsx`: phần ghi thêm của báo cáo ngày theo lớp.

Không phải token nên không vi phạm quy tắc bảo mật, nhưng phần bài làm đã chọn `sessionStorage` vì đúng lý do máy dùng chung (D-18).

**Cách làm (chọn một):**
- **Gắn khóa theo người dùng** (nên chọn, giữ được tiện ích): thêm id người dùng vào tên khóa, ví dụ `recent:<userId>:<kind>`. Người khác đăng nhập sẽ không đọc được khóa của người trước.
- **Xóa khi đăng xuất:** trong luồng logout (nơi đang xóa cache TanStack Query và backup bài làm) xóa thêm các khóa này. Phiên hết hạn thì giữ lại, giống cách xử lý backup bài làm.

Cần quyết định trước: phần ghi thêm của báo cáo ngày có cần còn lại sau khi đóng trình duyệt không. Nếu không, đổi sang `sessionStorage` là đủ.

**Xong khi:** đăng nhập bằng tài khoản thứ hai trên cùng trình duyệt không thấy mục vừa mở và ghi chú báo cáo của tài khoản thứ nhất. Thêm một test cho trường hợp này.

---

## 6. Bổ sung test

**Vấn đề:** 20 file test cho toàn bộ frontend.

**Cách làm:** không đặt mục tiêu phần trăm. Viết theo thứ tự rủi ro:
1. Hàm thuần tách ra ở mục 3 (rẻ nhất, không cần dựng component).
2. Các ca bắt buộc ở `docs/08-kiem-thu.md` mục 5 còn thiếu: route guard, trình soạn câu hỏi (đổi loại câu, số đáp án đúng), hiển thị lỗi publish, trang kết quả khi điểm bị ẩn.
3. Mỗi bug sửa từ nay kèm một test tái hiện (quy tắc trong `CLAUDE.md`).

E2E Playwright có sẵn 3 kịch bản nhưng chưa chạy trong CI; đưa vào CI là việc riêng (`docs/09-van-hanh.md` mục 8).

---

## Việc còn nợ ở backend / vận hành

Không thuộc file này, xem `docs/09-van-hanh.md` mục 6 và mục 10: backup định kỳ và thử khôi phục trên PostgreSQL, tài khoản DB quyền tối thiểu, `release.yml` chưa chờ test, `RegistrationSettingService` còn tự tạo bảng lúc chạy.
