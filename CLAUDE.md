# CLAUDE.md — ELearning

Hệ thống thi trực tuyến: .NET 10 Web API + EF Core 10 + SQL Server, frontend React + TypeScript + Vite + Bootstrap 5.
Trả lời người dùng bằng **tiếng Việt**. Code, database, API, enum dùng **tiếng Anh**.

## Tài liệu (nguồn chuẩn)

Bộ tài liệu nằm ở `docs/`. **Đọc `docs/00-muc-luc.md` trước**, sau đó chỉ đọc những file liên quan tới task:

| Làm việc với… | Đọc |
|---|---|
| Quy tắc nghiệp vụ, trạng thái, chấm điểm, tính giờ | `docs/02-nghiep-vu.md` |
| Cấu trúc solution, service, thư viện | `docs/03-kien-truc.md` |
| Schema, index, ràng buộc | `docs/04a-csdl-danh-muc-de-thi.md`, `docs/04b-csdl-luot-thi-ket-qua.md` |
| Endpoint, DTO, mã lỗi | `docs/05-api.md` |
| React, exam player, autosave, timer | `docs/06-frontend.md` |
| Xác thực, phân quyền, audit | `docs/07-bao-mat.md` |
| Test | `docs/08-kiem-thu.md` |
| Cấu hình, migration, deploy | `docs/09-van-hanh.md` |
| **Mọi task viết code** | `docs/10-bay-ky-thuat.md` (các lỗi kỹ thuật bắt buộc phải tránh) |

Quyết định đã chốt có mã `D-xx` (sổ quyết định ở `docs/00-muc-luc.md`). Không làm khác một quyết định nếu chưa ghi lại quyết định mới.

## Quy tắc không được vi phạm

1. Không tin điểm, timer, role hay trạng thái nộp bài do client gửi. Backend chấm điểm và tính giờ (`ExpiredAt` do server tính).
2. **Không bao giờ** trả cho học viên các trường `isCorrect`, `acceptedAnswers`, `correctAnswerNumber`, `numericTolerance`, `explanation` khi lượt thi đang làm, hoặc khi chính sách hiển thị (`ScoreVisibility` / `ReviewPolicy`) chưa cho phép. DTO của học viên là type riêng, map tường minh.
3. Version đề đã publish là bất biến. Ngoại lệ duy nhất: sửa đáp án / hủy câu qua `IAnswerKeyService` (D-11), có audit và lịch sử điểm.
4. Chấm điểm đọc từ snapshot `ExamQuestions`, **không bao giờ** đọc từ bảng `Questions` (D-01).
5. Không sửa câu trả lời của lượt thi đã nộp. Nộp bài phải idempotent: gọi lại thì trả kết quả cũ, không chấm lại.
6. Không xóa cứng câu hỏi, danh mục, user, lượt thi, kết quả hay snapshot (D-16).
7. Điểm dùng `decimal`, không dùng `float` / `double`.
8. Mọi timestamp là UTC; JSON luôn có hậu tố `Z` (có converter UTC cho EF và Dapper).
9. Không truy vấn danh sách không giới hạn; `pageSize` tối đa 100.
10. Backend luôn kiểm tra quyền, kể cả khi route frontend đã được bảo vệ. Tài nguyên của người khác → trả 404.
11. Không lưu mật khẩu hay refresh token dạng rõ. Không đưa access token vào `localStorage`.

## Quy ước code

**Backend:**
- Các tầng: `Api → Application → Domain`; `Infrastructure` implement interface. Domain không phụ thuộc gì.
- Controller mỏng; quy tắc chuyển trạng thái nằm trong entity.
- Không dùng generic repository, MediatR, AutoMapper, FluentAssertions ≥ 8 (dùng AwesomeAssertions).
- Không thêm Redis / RabbitMQ / Hangfire / SignalR khi chưa có quyết định.
- Thời gian lấy qua `TimeProvider` (cấm gọi `DateTime.UtcNow` trong code nghiệp vụ).
- Transaction bọc trong `CreateExecutionStrategy().ExecuteAsync(...)`. Lưu đáp án và nộp bài khóa dòng attempt bằng `UPDLOCK, ROWLOCK`.
- Enum lưu và trả về dạng `UPPER_SNAKE_CASE`. Mọi FK dùng `DeleteBehavior.Restrict`.
- Response luôn là `ApiResponse`; mã lỗi lấy từ `ErrorCodes` (xem `docs/05-api.md`).
- Nullable bật; build với `-warnaserror`; async có `CancellationToken`; không có `async void`.

**Frontend:**
- `strict: true`, không dùng `any`.
- Một instance Axios duy nhất; TanStack Query cho server state.
- Chuỗi hiển thị đặt trong `i18n/vi.json`.
- Markdown render không có HTML thô (không dùng `rehype-raw`, không dùng `dangerouslySetInnerHTML`).

**Khi đổi schema:** cập nhật đồng bộ entity, EF configuration, migration, DTO, API, test **và** `docs/04a` / `docs/04b`.

## Kiểm thử

- Integration / API test chạy trên SQL Server thật (Testcontainers). Không dùng EF InMemory hay SQLite.
- Mỗi quy tắc chấm điểm và mỗi chuyển trạng thái đều có test. Mỗi bug được sửa kèm một test tái hiện.
- Endpoint của học viên phải có test khẳng định JSON trả về không chứa trường đáp án.

## Lệnh thường dùng

Backend (chạy trong thư mục `backend/`; build đã bật warnings-as-errors trong `Directory.Build.props`):

```bash
dotnet tool restore
dotnet build ELearning.sln
dotnet format ELearning.sln --verify-no-changes
dotnet ef migrations add <Name> -p src/ELearning.Infrastructure -s src/ELearning.Api -o Persistence/Migrations
dotnet ef database update -p src/ELearning.Infrastructure -s src/ELearning.Api
dotnet run --project src/ELearning.Api --launch-profile http
```

Test cần SQL Server thật. Máy dev này có SQL Server local nhưng **không có Docker**, nên phải đặt biến trước khi chạy:

```bash
export ELEARNING_TEST_SQL="Server=localhost;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test ELearning.sln
```

Frontend (`frontend/elearning-web/`, từ M7): `npm run dev`, `npm run lint && npm run typecheck && npm run test`.

**Quy ước về package và analyzer:**
- Version package được quản lý tập trung ở `backend/Directory.Packages.props`; `PackageReference` trong csproj không ghi version.
- Analyzer rule được cấu hình ở `.editorconfig` gốc; migration EF được loại khỏi analyzer.

## Khi tài liệu chưa rõ

Chọn phương án an toàn nhất, ghi vào mục "Quyết định / Giả định" của file docs liên quan, rồi báo lại trong phần tóm tắt cuối task. Nếu yêu cầu mâu thuẫn với tính toàn vẹn của snapshot / lịch sử thì **dừng lại và hỏi**.
