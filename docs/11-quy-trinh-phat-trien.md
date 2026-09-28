# 11 — Quy trình phát triển

## 1. Quy tắc cho AI agent

Mọi AI agent làm việc trên repository này phải tuân thủ:

1. Đọc `00-muc-luc.md` và các file liên quan tới task **trước khi** sửa kiến trúc, schema hoặc API.
2. Đọc `10-bay-ky-thuat.md` trước khi viết code cho phần có liên quan.
3. Không thay đổi ý nghĩa của database mà không cập nhật tài liệu.
4. Không đưa đáp án đúng hay giải thích vào DTO của học viên.
5. Không tính điểm chính thức ở frontend.
6. Không tin thời gian do client gửi để quyết định việc hết giờ.
7. Không sửa version đã publish, trừ qua `IAnswerKeyService` (D-11).
8. Không sửa câu trả lời hay kết quả lịch sử, trừ quy trình chấm lại (D-11).
9. Mọi timestamp đều là UTC; JSON luôn có `Z`.
10. Quy tắc nghiệp vụ nằm trong domain / application service, không nằm trong controller.
11. Dùng DTO; không trả entity EF ra API.
12. Validate ở cả frontend và backend; backend là bên quyết định.
13. Viết test cho mọi quy tắc chấm điểm và mọi chuyển trạng thái.
14. Giữ tương thích ngược của API khi có thể.
15. Service nhỏ, gắn kết; không dùng generic repository.
16. Không thêm hạ tầng (Redis, RabbitMQ, Hangfire…) hay thư viện có license thương mại (MediatR, AutoMapper, FluentAssertions ≥ 8) nếu chưa có quyết định.
17. Không âm thầm đổi ý nghĩa của loại câu hỏi.
18. Nếu yêu cầu mâu thuẫn với tính toàn vẹn của snapshot / lịch sử: ưu tiên giữ đúng lịch sử, dừng lại và báo.
19. Không dùng điểm do client gửi làm giá trị chính thức.
20. Không trả chi tiết exception nội bộ ở production.
21. Đổi schema thì cập nhật đồng bộ: entity, EF configuration, migration, DTO, API, test, **và tài liệu `04a` / `04b`**.
22. Gặp chỗ tài liệu chưa rõ: chọn phương án an toàn nhất, ghi vào mục "Quyết định / Giả định" của file liên quan và báo lại trong phần tóm tắt.

### 1.1 Quy tắc không được vi phạm

1. Không tin điểm từ client.
2. Không tin timer từ client.
3. Không lộ đáp án khi lượt thi đang diễn ra.
4. Không sửa version đã publish (ngoại lệ duy nhất: D-11, có audit).
5. Không dùng nội dung hiện tại của ngân hàng câu hỏi để chấm lượt thi cũ.
6. Không sửa câu trả lời của lượt thi đã nộp.
7. Không lưu mật khẩu dạng rõ.
8. Không lưu refresh token dạng rõ.
9. Không tạo trùng kết quả cho một lượt thi.
10. Không xóa lượt thi / kết quả / snapshot vì câu hỏi trong ngân hàng bị tắt.
11. Không bỏ kiểm tra quyền ở backend vì route frontend đã được bảo vệ.
12. Không dùng số thực dấu phẩy động cho điểm chính thức.
13. Không có truy vấn không giới hạn cho danh sách người dùng xem.
14. Không chuyển sang microservices khi chưa có lý do cụ thể về tải hoặc triển khai.

## 2. Mẫu giao việc cho AI agent

```text
TASK:
[mô tả rõ ràng]

CONTEXT:
Đọc docs/00-muc-luc.md, docs/10-bay-ky-thuat.md và: [các file liên quan]

REQUIREMENTS:
- ...

DO NOT:
- ...

ACCEPTANCE CRITERIA:
- ...

TESTS REQUIRED:
- ...
```

Ví dụ:
```text
TASK:
Triển khai chấm điểm tự động cho 4 loại câu hỏi MVP.

CONTEXT:
Đọc docs/00-muc-luc.md, docs/02-nghiep-vu.md (mục 2, 3), docs/03-kien-truc.md (mục 5), docs/10-bay-ky-thuat.md (mục 3, 4).

REQUIREMENTS:
- IQuestionGrader + 4 strategy; GradingService chọn grader theo Dictionary.
- Chấm từ ExamQuestions (D-01); câu IsVoided được điểm tối đa.
- Chuẩn hóa NFC, CaseSensitive, IgnoreAccent (kể cả đ/Đ); nhiều đáp án chấp nhận.
- Parse số theo regex, chấp nhận ',' và '.' làm dấu thập phân.

DO NOT:
- Tính điểm ở frontend; đọc bảng Questions khi chấm; dùng float/double.

ACCEPTANCE CRITERIA:
- Toàn bộ ca ở docs/08-kiem-thu.md mục 2 đều đạt.
- Submit lưu kết quả; submit song song vẫn là idempotent.

TESTS REQUIRED:
- Unit test cho từng grader, Normalizer, NumericAnswerParser.
- Integration test cho submit song song.
```

## 3. Thứ tự ưu tiên triển khai

| Ưu tiên | Nội dung |
|---|---|
| P0 | Database + Domain + Xác thực (gồm permission, nhóm) |
| P1 | Ngân hàng câu hỏi |
| P2 | Đề thi + version + snapshot + publish + gán đề |
| P3 | Lượt thi + timer + autosave + job tự nộp |
| P4 | Chấm điểm + kết quả + chính sách hiển thị |
| P5 | Giao diện admin (React) |
| P6 | Giao diện thi của học viên (React) |
| P7 | Chấm lại, thao tác admin trên lượt thi, export, báo cáo |
| P8 | Hoàn thiện kiểm thử + tăng cường bảo mật + load test |
| P9 | Triển khai + giám sát |
| P10 | Tính năng sau MVP |

Chỉ bắt đầu P5 / P6 khi API và hợp đồng của P0–P4 đã đủ ổn định để frontend tích hợp.

## 4. Milestone

| # | Milestone | Nội dung |
|---|---|---|
| M1 | Hạ tầng | Solution, `Directory.Build.props`, DbContext, converter UTC / enum, migration đầu tiên, health check, Serilog, OpenAPI, CI |
| M2 | Identity | Đăng ký (có cờ), đăng nhập, refresh (cookie, xoay vòng, phát hiện dùng lại), khóa tài khoản, role / permission / policy, nhóm, `/me` |
| M3 | Ngân hàng câu hỏi | Danh mục, CRUD câu hỏi 4 loại, đáp án chấp nhận, Markdown, validation, tìm kiếm, phân trang |
| M4 | Đề thi | Tạo đề / version, thêm / xóa / sắp xếp / đồng bộ câu, cấu hình, preview, validate, publish, đóng / mở lại, gán đề |
| M5 | Lượt thi | Start (song song an toàn), lưu đáp án (`clientSeq`, batch, khóa dòng), sự kiện, nộp bài, ân hạn, `AttemptExpirationWorker` |
| M6 | Chấm điểm | 4 strategy, chuẩn hóa, kết quả, chính sách hiển thị, điểm chính thức |
| M7 | Frontend | Admin (câu hỏi, builder, kết quả), học viên (danh sách, player, kết quả, lịch sử) |
| M8 | Vận hành admin | Gia hạn / buộc nộp / hủy / cấp thêm lượt, sửa đáp án / hủy câu / chấm lại, export Excel, dashboard, audit log |
| M9 | Kiểm thử | Unit, integration, API, frontend, E2E, load test |
| M10 | Triển khai | Docker, Nginx, migration bundle, backup + thử khôi phục, giám sát, cảnh báo |

## 5. Definition of Done

**Xác thực:**
- Đăng ký (khi được bật), đăng nhập và logout hoạt động.
- Refresh có xoay vòng; token bị dùng lại thì thu hồi cả family.
- Mật khẩu được băm; khóa tài khoản sau 5 lần sai.
- `/me` trả về permission; 401 / 403 / 404 trả đúng.
- Có rate limit đăng nhập; vô hiệu hóa user thì request bị chặn.

**Ngân hàng câu hỏi:**
- Tạo được đủ 4 loại câu; validation đúng; có nhiều đáp án chấp nhận.
- Sửa, clone, bật/tắt được; sắp xếp được option.
- Lọc, tìm kiếm, phân trang hoạt động; xem trước Markdown (có code block).

**Đề thi:**
- Tạo được đề nháp và version; thêm / xóa / sắp xếp / đồng bộ được câu hỏi; preview đúng như học viên thấy.
- Đề không hợp lệ thì không publish được, và có đủ danh sách lỗi.
- Version đã publish là bất biến; tối đa 1 version DRAFT và 1 PUBLISHED.
- Publish V2 không ảnh hưởng lượt thi trên V1.
- Gán đề hoạt động; đóng / mở lại hoạt động.

**Lượt thi:**
- Học viên chỉ thấy đề được phép; chỉ bắt đầu được trong khung giờ.
- `MaxAttempts` và `ExtraAttempts` được áp dụng; không có 2 lượt đang làm song song.
- `ExpiredAt` do server tính, có tính đến `EndAt`.
- Autosave hoạt động; không mất câu trả lời khi F5 hoặc mất mạng; không bị ghi đè sai thứ tự; "đánh dấu xem lại" được lưu.
- Lượt thi hết giờ không sửa được; job tự nộp hoạt động; submit là idempotent.

**Chấm điểm:**
- Chấm đúng 4 loại; `MULTIPLE_CHOICE` so sánh đúng tập; chuẩn hóa NFC / hoa thường / dấu hoạt động.
- Sai số với số và parse `"3,5"` đúng.
- Tổng điểm, phần trăm, đạt / không đạt đúng; kết quả được lưu.
- Chính sách hiển thị điểm / đáp án được áp dụng; chấm lại hoạt động và có lịch sử.

**Production:** xem `09-van-hanh.md` mục 9.

## 6. Kịch bản nghiệm thu chính

```text
Admin đăng nhập → tạo danh mục → tạo đủ 4 loại câu hỏi (có câu Markdown chứa code)
→ tạo đề → chọn câu → cấu hình (60 phút, đạt 50%, xem lại AFTER_EXAM_END) → gán nhóm DEMO
→ xem trước → publish
→ Học viên (thuộc DEMO) đăng nhập → thấy đề → bắt đầu → backend tạo lượt thi
→ trả lời → autosave → F5 → câu trả lời còn nguyên → nộp bài
→ backend kiểm tra và chấm → lưu kết quả → học viên thấy điểm
→ Admin thấy kết quả, export Excel
→ Admin sửa một câu hỏi trong ngân hàng → kết quả cũ và phần xem lại bài không đổi
→ Admin sửa đáp án trong version → điểm được chấm lại, có lịch sử
→ Học viên không thuộc DEMO không nhìn thấy đề
```

## 7. Checklist cuối

**Backend**
- [ ] Solution build với `-warnaserror`; bật nullable.
- [ ] Kết nối SQL Server; migration từ DB rỗng chạy được; không có model thay đổi mà thiếu migration.
- [ ] Seed dev / prod tách riêng.
- [ ] JWT, refresh cookie, permission policy, `FallbackPolicy` hoạt động.
- [ ] OpenAPI (dev / staging), health check, exception middleware, Serilog đều hoạt động.
- [ ] Converter UTC và enum được áp dụng toàn cục.
- [ ] `AttemptExpirationWorker` chạy.

**Database** — mọi bảng trong `04a` và `04b`, gồm:
- [ ] Filtered unique index: `UX_ExamVersions_OnePublished`, `UX_ExamVersions_OneDraft`, `UX_ExamAttempts_OneInProgress`, `UX_ExamQuestions_Source`.
- [ ] CHECK constraint, `ROWVERSION`, FK `Restrict`.

**Nghiệp vụ**
- [ ] 4 loại câu, đáp án chấp nhận, chuẩn hóa NFC, parse số kiểu Việt.
- [ ] Version, snapshot, publish, đóng / mở lại, gán đề.
- [ ] Start / lưu đáp án / nộp bài / tự nộp, ân hạn, `clientSeq`.
- [ ] Chính sách hiển thị, điểm chính thức, chấm lại, thao tác admin.

**Bảo mật**
- [ ] Băm mật khẩu, băm refresh token, phát hiện dùng lại token, khóa tài khoản.
- [ ] Không có đáp án trong API của học viên (có test).
- [ ] Rate limit, CSP / HSTS, CORS / cùng origin, lỗi an toàn.
- [ ] Audit đủ danh sách ở `07-bao-mat.md`.

**Kiểm thử**
- [ ] Unit, integration (Testcontainers), API, frontend, E2E, load test.
- [ ] Các ca song song: start, submit, lưu đáp án khi đang submit.
- [ ] Hết giờ, hết lượt, version bất biến, chấm lại.

## 8. Quyết định / Giả định

- **Thêm P7** (vận hành admin) và **M8**, vì spec gốc chưa có phần này.
- **Kịch bản nghiệm thu** bổ sung: gán đề, chấm lại, học viên ngoài nhóm không thấy đề.
- **Quy tắc agent số 21 và 22** được thêm để tài liệu luôn đồng bộ với code.
