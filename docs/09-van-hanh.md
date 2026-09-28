# 09 — Vận hành và triển khai

## 1. Cấu hình

`appsettings.json` (không chứa bí mật):
```json
{
  "ConnectionStrings": { "DefaultConnection": "" },
  "Jwt": { "Issuer": "ELearning", "Audience": "ELearning.Web", "AccessTokenMinutes": 15, "RefreshTokenDays": 7 },
  "Auth": { "AllowSelfRegistration": false, "LockoutMaxFailedAttempts": 5, "LockoutMinutes": 15 },
  "Exam": { "SubmitGraceSeconds": 30, "ExpirationSweepIntervalSeconds": 60, "ExpirationSweepBatchSize": 100 },
  "App": { "BusinessTimeZone": "Asia/Ho_Chi_Minh", "PublicOrigin": "https://elearning.example.vn" },
  "RateLimits": { },
  "Serilog": { },
  "AllowedHosts": "*"
}
```

- **Môi trường:** `appsettings.{Development|Staging|Production}.json`.
- **Bí mật** (`ConnectionStrings:DefaultConnection`, `Jwt:SigningKey`):
  - Dev: `dotnet user-secrets`.
  - Server: biến môi trường (`Jwt__SigningKey`) hoặc secret store của nền tảng triển khai.
  - **Không commit bí mật vào Git.**
- Options được bind vào class có validation (`ValidateDataAnnotations().ValidateOnStart()`). Thiếu `Jwt:SigningKey` hoặc khóa ngắn hơn 32 byte thì ứng dụng **không khởi động**.
- Chuỗi kết nối dev (SQL Server trong Docker):
  `Server=localhost,1433;Database=ELearningDb;User Id=sa;Password=<từ user-secrets>;TrustServerCertificate=True;Encrypt=True`

## 2. Thứ tự khởi động backend

```text
Tạo builder
→ Cấu hình + validate options
→ Serilog
→ DbContext (EnableRetryOnFailure, converter UTC/enum)
→ TimeProvider.System
→ Xác thực (JWT) → Phân quyền (policy theo permission, FallbackPolicy)
→ Application services, validators, graders
→ BackgroundService: AttemptExpirationWorker
→ Rate limiter, ForwardedHeaders, CORS (nếu cần), OpenAPI (không bật ở Production)
→ Health checks
→ Build
→ Middleware: ForwardedHeaders → ExceptionHandling → HSTS/HTTPS → Serilog request logging
             → RateLimiter → Authentication → UserStatusCheck → Authorization
→ MapControllers, MapHealthChecks
→ Run
```

## 3. Chạy local

**Cách khuyến nghị:**
- SQL Server chạy trong Docker.
- API chạy từ Visual Studio hoặc `dotnet watch`.
- Frontend chạy bằng `npm run dev` (Vite proxy `/api` → `https://localhost:7xxx`).

`docker-compose.yml` có các service `sqlserver`, `api`, `web` (web chỉ dùng khi muốn chạy thử cả stack).

Seed dữ liệu cho Development (chạy khi `--seed` hoặc lần đầu ở Development):
- Tài khoản `admin`, `student01`, `student02` với mật khẩu dev lấy từ user-secrets; tất cả có `MustChangePassword = false`.
- Nhóm `DEMO`, gồm `student01` và `student02`.
- Danh mục: `C#`, `SQL Server`, `ASP.NET`, `ReactJS`, `JavaScript`.
- Đề demo "C# Basic": 10 câu đủ 4 loại, có code block, 60 phút, `AccessMode = ASSIGNED` gán nhóm `DEMO`.

**Không bao giờ chạy seed dev ở Production.** Production chỉ seed role, permission và một tài khoản admin khởi tạo; mật khẩu lấy từ biến môi trường, và `MustChangePassword = true`.

## 4. Migration

- EF Core migrations nằm trong `ELearning.Infrastructure/Persistence/Migrations`.
- Dev: `dotnet ef database update`.
- **Staging / Production:**
  - CI sinh migration bundle (`dotnet ef migrations bundle`) hoặc script idempotent (`dotnet ef migrations script --idempotent`) vào `database/scripts/`.
  - Áp dụng bằng một bước triển khai riêng, **trước** khi khởi động phiên bản API mới.
- **Không gọi `Database.Migrate()` lúc khởi động** ở Staging / Production, vì nhiều instance sẽ chạy migration cùng lúc, và tài khoản ứng dụng không nên có quyền DDL.
- Migration phải tương thích ngược với phiên bản API đang chạy (mở rộng trước, thu hẹp sau). Không đổi tên cột hay xóa cột trong cùng một lần deploy.
- Tài khoản DB của ứng dụng có quyền `db_datareader`, `db_datawriter`, `EXECUTE`. Tài khoản migration là tài khoản riêng.

## 5. Kiến trúc triển khai

```text
Internet
   ↓ HTTPS
Nginx (TLS, gzip, CSP/HSTS headers, rate limit thô)
   ├── /          → file tĩnh React (index.html không cache; asset có hash cache 1 năm)
   └── /api, /health → ASP.NET Core (Kestrel, HTTP nội bộ)
                          ↓
                      SQL Server
```

- Frontend và API **cùng domain** (D-15).
- Nginx gửi `X-Forwarded-For` / `X-Forwarded-Proto`; API cấu hình `ForwardedHeaders` với `KnownProxies`.
- Trang lỗi tĩnh (502/503) không để lộ thông tin nội bộ.
- **Chỉ scale API ngang khi thực sự cần.** Khi chạy nhiều instance:
  - Cache permission / trạng thái user phải chuyển sang Redis, hoặc giảm TTL xuống 30 giây.
  - Rate limit chuyển sang Nginx hoặc dùng store dùng chung.
  - `AttemptExpirationWorker` chạy ở mọi instance vẫn an toàn nhờ chuyển trạng thái nguyên tử. Có thể thêm `sp_getapplock` để giảm tranh chấp.
  - Refresh token không phụ thuộc instance, vì trạng thái nằm trong DB.
- **Khung giờ deploy:** không deploy khi có đề đang mở theo lịch. Admin xem được lịch đề trong dashboard.

## 6. Backup và khôi phục

Mục tiêu: RPO 15 phút (5 phút trong ngày thi), RTO 1 giờ *(cần xác nhận)*.

| Loại | Tần suất | Giữ lại |
|---|---|---|
| Full | Hằng ngày 01:00 (giờ VN) | 30 ngày |
| Differential | Mỗi 6 giờ | 7 ngày |
| Transaction log | Mỗi 15 phút; **mỗi 5 phút** trong ngày có kỳ thi lớn | 7 ngày |

- Recovery model: **FULL**.
- Lưu backup ở một vị trí khác với máy chủ DB (ổ riêng và bản sao offsite), có mã hóa.
- **Thử khôi phục mỗi tháng** vào một server riêng, có ghi lại thời gian khôi phục thực tế. Backup chưa từng được khôi phục thử thì chưa được coi là chiến lược backup hợp lệ.
- Sự cố DB giữa giờ thi: khôi phục xong thì admin dùng chức năng **gia hạn** cho các lượt thi bị ảnh hưởng. `ExpiredAt` không tự dừng khi hệ thống ngừng hoạt động.

## 7. Giám sát

**Tối thiểu cho MVP:**
- Log có cấu trúc (JSON), có `traceId`, gom về một nơi (Seq, Elastic hoặc file + công cụ đọc).
- Health check:
  - `/health/live`: tiến trình còn sống.
  - `/health/ready`: kết nối được SQL Server trong 2 giây.
- Chỉ số theo dõi: thời gian xử lý request (p50 / p95 theo endpoint), số lỗi 5xx, số lần xác thực thất bại, số lỗi chấm điểm, số lỗi DB, số lượt `IN_PROGRESS`, số lượt được tự nộp mỗi vòng worker, **độ trễ worker** (lượt thi quá hạn lâu nhất chưa được xử lý).
- **Cảnh báo:**
  - Tỉ lệ 5xx > 1% trong 5 phút.
  - `/health/ready` lỗi.
  - Có lượt thi quá hạn hơn 5 phút mà chưa được nộp.
  - Backup thất bại.

**Sau MVP:** OpenTelemetry (trace + metrics), dashboard Grafana.

## 8. CI/CD

GitHub Actions (hoặc tương đương), chạy trên mọi PR:
1. Backend: `dotnet restore` → `dotnet build -warnaserror` → unit test → integration / API test (Testcontainers) → `dotnet format --verify-no-changes`.
2. Frontend: `npm ci` → `npm run lint` → `npm run typecheck` → `npm run test` → `npm run build`.
3. Kiểm tra migration: tạo DB rỗng, áp migration, so sánh xem model có thay đổi mà chưa có migration không (`dotnet ef migrations has-pending-model-changes`).
4. Quét lỗ hổng dependency: `dotnet list package --vulnerable`, `npm audit --audit-level=high`.

Trên nhánh `main`:
- Build image và sinh migration bundle.
- Deploy lên Staging, chạy E2E smoke.
- Deploy Production là bước duyệt thủ công.

## 9. Definition of Done — Production

- HTTPS và HSTS hoạt động; bí mật nằm ngoài repo; options được validate khi khởi động.
- Migration được áp dụng bằng bundle / script, không áp lúc khởi động.
- Backup chạy theo lịch và **đã thử khôi phục** ít nhất một lần.
- Log và health check hoạt động; có cảnh báo tối thiểu.
- Rate limit hoạt động; CORS bị giới hạn (hoặc không cần vì cùng origin).
- Build production của frontend hoạt động; Nginx proxy đúng; trang lỗi an toàn.
- Toàn bộ test tự động đều đạt; load test đạt ngưỡng.

## 10. Quyết định / Giả định

- **Không gọi `Database.Migrate()` khi khởi động.** Spec gốc chỉ ghi "migration được kiểm soát".
- **Chốt một domain, Nginx route `/api`**, để cookie `SameSite=Strict` hoạt động và không cần CORS.
- **Lịch backup, RPO / RTO và ngưỡng cảnh báo** là giả định *(cần xác nhận)* với đơn vị vận hành.
- **Seq / Elastic** là gợi ý; công cụ gom log cụ thể tùy hạ tầng.
