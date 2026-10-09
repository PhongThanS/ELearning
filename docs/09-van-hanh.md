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
- Chuỗi kết nối dev (PostgreSQL local hoặc trong Docker; đã có sẵn trong `appsettings.Development.json`):
  `Host=localhost;Port=5432;Database=elearning_db;Username=postgres;Password=<từ user-secrets>;Include Error Detail=true`
- Trong Docker Compose, chuỗi kết nối lấy từ `POSTGRES_USER`, `POSTGRES_PASSWORD`, `DB_NAME` của `.env` (host là service `postgres`).

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
             → Authentication (JwtBearer + kiểm tra user/SecurityStamp) → RateLimiter → Authorization
→ MapControllers, MapHealthChecks
→ Run
```

## 3. Chạy local

**Cách khuyến nghị:**
- PostgreSQL chạy trong Docker (ví dụ `docker run -d --name my-postgres -e POSTGRES_PASSWORD=<mật khẩu> -e POSTGRES_DB=elearning_db -p 5432:5432 -v pgdata:/var/lib/postgresql/data postgres:17-alpine`).
- API chạy từ Visual Studio hoặc `dotnet watch`.
- Frontend chạy bằng `npm run dev` (Vite proxy `/api` → `https://localhost:7xxx`).

`docker-compose.yml` có các service `postgres`, `migrate`, `seed`, `api`, `web` (web chỉ dùng khi muốn chạy thử cả stack).

Seed dữ liệu cho Development (chạy khi `--seed` hoặc lần đầu ở Development):
- Tài khoản `admin`, `student01`, `student02` với mật khẩu dev lấy từ user-secrets; tất cả có `MustChangePassword = false`.
- Nhóm `DEMO`, gồm `student01` và `student02`.
- Danh mục: `C#`, `PostgreSQL`, `ASP.NET`, `ReactJS`, `JavaScript`.
- Đề demo "C# Basic": 10 câu đủ 4 loại, có code block, 60 phút, `AccessMode = ASSIGNED` gán nhóm `DEMO`.

**Dữ liệu demo** (máy dev / Docker local): `ADMIN_PASSWORD='...' node deploy/scripts/demo-data.mjs` tạo qua API 5 danh mục, 41 câu hỏi đủ 5 loại, 24 học viên `demo.hs01…demo.hs24` (mật khẩu chung: biến `DEMO_STUDENT_PASSWORD`, mặc định ghi trong script), 1 nhóm, 5 lớp học (có học viên thuộc nhiều lớp, một lớp đã tắt) và 7 đề ở các trạng thái: đang mở có lượt đã nộp / đang làm, nhiều lượt, có tự luận chờ chấm, PUBLIC, đã đóng, sắp diễn ra, bản nháp. Chạy lại được: thứ đã có thì dùng lại, đề đã có thì bỏ qua.

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
                      PostgreSQL
```

- Frontend và API **cùng domain** (D-15).
- Nginx gửi `X-Forwarded-For` / `X-Forwarded-Proto`; API cấu hình `ForwardedHeaders` với `KnownProxies`.
- Trang lỗi tĩnh (502/503) không để lộ thông tin nội bộ.
- **Chỉ scale API ngang khi thực sự cần.** Khi chạy nhiều instance:
  - Cache permission / trạng thái user phải chuyển sang Redis, hoặc giảm TTL xuống 30 giây.
  - Rate limit chuyển sang Nginx hoặc dùng store dùng chung.
  - `AttemptExpirationWorker` chạy ở mọi instance vẫn an toàn nhờ chuyển trạng thái nguyên tử. Có thể thêm advisory lock (`pg_try_advisory_lock`) để giảm tranh chấp. Mỗi vòng quét lặp theo lô `Exam:ExpirationSweepBatchSize` tới khi hết lượt quá hạn.
  - Refresh token không phụ thuộc instance, vì trạng thái nằm trong DB.
- **Khung giờ deploy:** không deploy khi có đề đang mở theo lịch. Admin xem được lịch đề trong dashboard.

### 5.1 Triển khai bằng Docker Compose (M10, đã làm)

```text
docker-compose.yml          postgres → migrate → seed → api → web (+ media-init, monitor)
docker-compose.prod.yml     override: Nginx HTTPS (80 → 443, HSTS), gắn chứng chỉ
backend/Dockerfile          target api (aspnet, user không phải root) và migrator (EF migration bundle)
frontend/elearning-web/Dockerfile   build Vite → nginx (template envsubst)
deploy/nginx/               cấu hình Nginx: header bảo mật, cache, proxy /api, trang lỗi tĩnh
deploy/sql/init-app-login.sql       (còn từ thời SQL Server, không còn được compose gọi; xem mục 5.1 ghi chú)
deploy/scripts/             backup.sh, restore.sh, restore-test.sh, media-sync.sh, monitor.sh, notify.sh
.env.example                mọi bí mật / tham số (sao chép thành .env, không commit)
```

**Chạy thử trên một máy:**
```bash
cp .env.example .env        # đổi toàn bộ mật khẩu / khóa
docker compose up -d --build
# http://localhost:8080 — đăng nhập admin / ADMIN_INITIAL_PASSWORD, đổi mật khẩu lần đầu
```

**Thứ tự khởi động** (mỗi bước chỉ chạy khi bước trước thành công):
1. `postgres` healthy (`pg_isready -U $POSTGRES_USER -d $DB_NAME`); database `DB_NAME` do image tạo sẵn từ `POSTGRES_DB`.
2. `migrate`: migration bundle áp lên database (`--connection <chuỗi kết nối>`).
3. `seed`: `--seed` (role, permission, admin khởi tạo).
4. `media-init`: đặt quyền thư mục ảnh `MEDIA_DIR` (gắn vào `/var/lib/elearning/media`) cho user của API (uid 1654), kể cả file chép lại khi khôi phục.
5. `api` (Kestrel HTTP 8080, chỉ trong mạng nội bộ) và `web` (Nginx, cổng `HTTP_PORT`).

**Production có HTTPS:** đặt `fullchain.pem`, `privkey.pem` vào `deploy/certs/`, đặt `SERVER_NAME`, `PUBLIC_ORIGIN=https://<domain>`, rồi
`docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build`. Nếu TLS kết thúc ở load balancer phía trước thì giữ cấu hình HTTP và bật module realip của Nginx.

**Cập nhật phiên bản:** `docker compose build` → `docker compose up -d`. `migrate` / `seed` chạy lại (idempotent) trước khi `api` mới khởi động. Không deploy khi có đề đang mở (mục 5).

**Ghi chú:**
- API tin `X-Forwarded-For` chỉ từ mạng compose `172.28.0.0/24` (`ReverseProxy:KnownNetworks`); Nginx **ghi đè** header này bằng IP thật của client, nên client không giả mạo được IP (rate limit theo IP phụ thuộc vào điều này).
- Nginx: CSP `default-src 'self'` (chỉ `style-src` có `'unsafe-inline'` cho thuộc tính style của React), `X-Frame-Options DENY`, `nosniff`, HSTS chỉ qua HTTPS; `index.html` không cache, `/assets/*` cache 1 năm; `client_max_body_size 6m` cho import Excel; rate limit thô 200 request/giây/IP cho `/api`; trang 50x tĩnh.
- **Docker Desktop (Windows / Mac) chuyển tiếp cổng qua proxy**, nên mọi client hiện ra cùng một IP gateway (`172.28.0.1`) → rate limit theo IP gom chung. Chỉ dùng để chạy thử; máy chủ Linux (Docker Engine) giữ nguyên IP thật của client.
- Đã chạy thử trên máy dev: `docker compose up` → migrate / db-init / seed / api / web; bắt buộc đổi mật khẩu admin lần đầu; tài khoản app quyền tối thiểu sinh mã câu hỏi qua SEQUENCE; Excel (ClosedXML) chạy trên Linux; backup full + log → `restore-test.sh` khôi phục đủ dữ liệu trong 2 giây; cấu hình HTTPS (`nginx -t`, 301, HSTS) với chứng chỉ tự ký.
- **(D-29) Không còn bước `db-init` và tài khoản app quyền tối thiểu.** Compose hiện dùng chung `POSTGRES_USER` cho `migrate`, `seed` và `api`. Production nên tạo thêm role riêng cho API (chỉ `SELECT/INSERT/UPDATE/DELETE` trên bảng, `USAGE` trên sequence) và dùng role chủ sở hữu chỉ cho `migrate`; việc này chưa làm (xem "Quyết định / Giả định").
- Dữ liệu PostgreSQL nằm ở volume `pgdata`; thư mục `BACKUP_DIR` được gắn vào container `postgres` tại `/backups`.
- Docker Compose không đặt `DB_NAME` thì dùng mặc định `elearning_db`.

## 6. Backup và khôi phục

Mục tiêu: RPO 15 phút (5 phút trong ngày thi), RTO 1 giờ *(cần xác nhận)*.

| Loại | Tần suất | Giữ lại |
|---|---|---|
| Full | Hằng ngày 01:00 (giờ VN) | 30 ngày |
| Differential | Mỗi 6 giờ | 7 ngày |
| Transaction log | Mỗi 15 phút; **mỗi 5 phút** trong ngày có kỳ thi lớn | 7 ngày |

- Với PostgreSQL: bản full dùng `pg_dump -Fc`; muốn RPO 15 phút phải bật lưu trữ WAL (`archive_mode`, `archive_command`) và dùng khôi phục tại thời điểm (PITR) với `pg_basebackup`. Chưa cấu hình trong repo (xem bên dưới).
- Lưu backup ở một vị trí khác với máy chủ DB (ổ riêng và bản sao offsite), có mã hóa.
- **Thử khôi phục mỗi tháng** vào một server riêng, có ghi lại thời gian khôi phục thực tế. Backup chưa từng được khôi phục thử thì chưa được coi là chiến lược backup hợp lệ.
- Sự cố DB giữa giờ thi: khôi phục xong thì admin dùng chức năng **gia hạn** cho các lượt thi bị ảnh hưởng. `ExpiredAt` không tự dừng khi hệ thống ngừng hoạt động.

> **Lưu ý (D-29): các script backup / khôi phục bên dưới là bản viết cho SQL Server** (`BACKUP DATABASE`, `RESTORE VERIFYONLY`, `sqlcmd`, `DBCC CHECKDB`, thư mục `/var/opt/mssql/backup`) và **chưa được chuyển sang PostgreSQL**. Chạy chúng với stack PostgreSQL sẽ lỗi. Cho tới khi viết lại, backup thủ công bằng:
> ```bash
> docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" -Fc "$DB_NAME" > backups/elearning_$(date -u +%Y%m%dT%H%M%SZ).dump
> docker compose exec -T postgres pg_restore -U "$POSTGRES_USER" -d <db đích> --clean --if-exists < backups/<file>.dump
> ```
> Đồng thời `monitor.sh` kiểm tra tuổi file backup theo quy ước tên của script cũ nên cảnh báo thiếu backup có thể không chính xác.

**Công cụ (M10, đã làm, bản SQL Server)** — chạy từ thư mục gốc, đọc `.env`:
- `deploy/scripts/backup.sh full|diff|log`: backup `WITH CHECKSUM`, kiểm tra `RESTORE VERIFYONLY`, xóa bản quá hạn (full 30 ngày, diff / log 7 ngày). Lịch chạy bằng cron của máy chủ (ví dụ ở đầu script).
- **Máy Windows chạy Docker Desktop** (không có cron): `powershell -ExecutionPolicy Bypass -File deployscriptswindows
egister-backup-tasks.ps1` đăng ký 3 task trong Task Scheduler (`ELearningBackup full|diff|log`, cùng lịch như trên). Task chạy `backup.sh` qua Git Bash, ẩn cửa sổ (`run-backup.vbs`), chạy bù khi lỡ lịch, ghi log vào `backupsackup.log`; chỉ chạy khi người dùng đã đăng nhập (Docker Desktop cũng vậy), nên bật "Start Docker Desktop when you sign in".
- `deploy/scripts/restore.sh --target <db> [--replace] full.bak [diff.bak] [log.trn ...]`: khôi phục một chuỗi backup.
- `deploy/scripts/restore-test.sh`: lấy full mới nhất + diff mới nhất sau nó + mọi log sau đó, khôi phục vào `<DB>_RestoreTest`, so số dòng các bảng chính với bản gốc, chạy `DBCC CHECKDB`, in thời gian khôi phục, rồi xóa database thử. **Chạy mỗi tháng** và ghi kết quả vào sổ vận hành.
- File backup nằm ở `BACKUP_DIR` trên máy chủ; cần đồng bộ ra nơi khác (offsite) và mã hóa ở đó (file `pg_dump` không được mã hóa sẵn).
- **Ảnh câu hỏi (D-27):** mỗi lần `backup.sh` chạy (full / diff / log), sau khi backup database xong, `media-sync.sh` chép ảnh mới từ `MEDIA_DIR` sang `BACKUP_DIR/media`. Ảnh bất biến, tên theo SHA-256 và không bị xóa, nên bản sao là tăng dần, không cần chính sách giữ lại, và luôn đủ cho mọi bản backup database (RPO của ảnh bằng RPO của log backup).
- Khôi phục: `restore.sh ... --with-media` chép ảnh còn thiếu từ `BACKUP_DIR/media` về `MEDIA_DIR` (chỉ thêm, không ghi đè) rồi chạy `media-init`. `restore-test.sh` kiểm tra mọi dòng `MediaFiles` của bản khôi phục có file trong bản sao ảnh; thiếu thì thất bại.

## 7. Giám sát

**Tối thiểu cho MVP:**
- Log có cấu trúc (JSON), có `traceId`, gom về một nơi (Seq, Elastic hoặc file + công cụ đọc).
- Health check:
  - `/health/live`: tiến trình còn sống.
  - `/health/ready`: kết nối được PostgreSQL trong 2 giây.
- Chỉ số theo dõi: thời gian xử lý request (p50 / p95 theo endpoint), số lỗi 5xx, số lần xác thực thất bại, số lỗi chấm điểm, số lỗi DB, số lượt `IN_PROGRESS`, số lượt được tự nộp mỗi vòng worker, **độ trễ worker** (lượt thi quá hạn lâu nhất chưa được xử lý).
- **Cảnh báo:**
  - Tỉ lệ 5xx > 1% trong 5 phút.
  - `/health/ready` lỗi.
  - Có lượt thi quá hạn hơn 5 phút mà chưa được nộp.
  - Backup thất bại.

**Sau MVP:** OpenTelemetry (trace + metrics), dashboard Grafana.

### 7.1 Cách làm (M10, D-26)

**Chỉ số** — `OperationalMetrics` (Application) ghi ra meter `ELearning` và giữ tổng trong cửa sổ trượt 5 phút:

| Chỉ số | Nguồn |
|---|---|
| Thời gian xử lý request p50 / p95 theo endpoint, số request theo status | Meter có sẵn `Microsoft.AspNetCore.Hosting` (`http.server.request.duration`, tag `http.route`, `http.response.status_code`) và log request của Serilog |
| `elearning.http.server_errors` | Response 5xx (middleware ngoài cùng, không tính `/health/*`) |
| `elearning.auth.login_failures` | Đăng nhập sai mật khẩu, tài khoản không tồn tại hoặc bị khóa |
| `elearning.grading.failures` | `GradingService` ném lỗi |
| `elearning.db.errors` | Exception cơ sở dữ liệu (`NpgsqlException` / `PostgresException`) dẫn tới 500 (không tính trùng khóa / xung đột dữ liệu) |
| `elearning.attempts.auto_submitted`, `…auto_submit_failures`, `…sweep.duration` | Mỗi vòng `AttemptExpirationWorker` |
| `elearning.attempts.in_progress`, `…overdue`, `…oldest_overdue` (giây) | Đo mỗi phút; `oldest_overdue` là **độ trễ worker** |

- Đọc trực tiếp: `dotnet-counters monitor -n ELearning.Api --counters ELearning,Microsoft.AspNetCore.Hosting`.
- Mỗi `Monitoring:SnapshotIntervalSeconds` (60 giây) API ghi một dòng log `Monitoring: …` có cấu trúc với các số trên, để công cụ gom log vẽ biểu đồ. Khi có điều kiện cảnh báo, ghi thêm dòng `ALERT <mục>` ở mức Warning.

**Cảnh báo** — `/health/alerts` trả 503 khi một check lỗi, kèm số liệu từng check:

| Check | Lỗi khi |
|---|---|
| `database` | Không kết nối được PostgreSQL (giống `/health/ready`) |
| `error-rate` | 5xx > `ErrorRatePercent` (1%) trong `ErrorRateWindowMinutes` (5 phút), khi có ít nhất `ErrorRateMinRequests` (50) request |
| `attempt-backlog` | Lượt `IN_PROGRESS` quá hạn (đã hết ân hạn) lâu nhất đã quá `ExpiredAt` hơn `OverdueAttemptMinutes` (5 phút) |
| `expiration-worker` | Job tự nộp chạy trên instance này nhưng không xong vòng quét nào trong 3 chu kỳ (`Exam:ExpirationSweepIntervalSeconds`) |

- `/health/alerts` **bị Nginx chặn** (404) từ bên ngoài; chỉ gọi trong mạng nội bộ (`http://api:8080`).
- Container `monitor` (`deploy/scripts/monitor.sh`, image `curlimages/curl`) mỗi phút kiểm tra `/health/ready`, `/health/alerts`, trang chủ qua Nginx, và có file backup mới (`BACKUP_FULL_MAX_AGE_HOURS` = 26, `BACKUP_LOG_MAX_AGE_MINUTES` = 30). Khi một mục chuyển sang lỗi, còn lỗi sau mỗi 30 phút, hoặc hết lỗi, nó gửi webhook `ALERT_WEBHOOK_URL` (Slack, Mattermost, Google Chat, Teams; Discord đặt `ALERT_WEBHOOK_FIELD=content`). Không đặt webhook thì cảnh báo nằm trong `docker compose logs monitor`.
- `deploy/scripts/backup.sh` lỗi ở bất kỳ bước nào → gửi webhook ngay (`deploy/scripts/notify.sh`) và thoát với mã lỗi. Cron không chạy thì mục `backup-full` / `backup-log` của `monitor` báo.
- Kiểm tra nhanh: `docker compose run --rm -e MONITOR_ONCE=1 monitor` (thoát 1 nếu có mục lỗi; CI chạy bước này).

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

- **(M2) Seed:**
  - Development tự seed khi khởi động: role, permission, admin, `student01`/`student02`, nhóm DEMO.
  - Môi trường khác chạy `dotnet ELearning.Api.dll --seed` sau bước migration. Lệnh này chỉ seed role, permission và admin khởi tạo (mật khẩu lấy từ `Seed__AdminPassword`, bắt buộc đổi ở lần đăng nhập đầu), rồi thoát.
- **(M2) `appsettings.Development.json` chứa khóa JWT và mật khẩu demo dùng riêng cho dev** (có thể ghi đè bằng user-secrets). `appsettings.json` để trống `Jwt:SigningKey`; thiếu khóa thì ứng dụng không khởi động.
- **(M2) Thứ tự middleware:** Authentication chạy trước RateLimiter, để rate limit phân vùng được theo UserId.

- **Không gọi `Database.Migrate()` khi khởi động.** Spec gốc chỉ ghi "migration được kiểm soát".
- **Chốt một domain, Nginx route `/api`**, để cookie `SameSite=Strict` hoạt động và không cần CORS.
- **Lịch backup, RPO / RTO và ngưỡng cảnh báo** là giả định *(cần xác nhận)* với đơn vị vận hành.
- **Seq / Elastic** là gợi ý; công cụ gom log cụ thể tùy hạ tầng.
- **(M10) Docker Compose là cách triển khai tham chiếu** (một máy chủ). Kubernetes / nhiều instance API chưa làm; khi cần xem mục 5 (cache permission, rate limit dùng chung).
- **(M10) Tài khoản DB tách biệt:** migration dùng `sa` (hoặc tài khoản có quyền DDL), API và seed dùng `APP_DB_LOGIN` không có quyền DDL. `NEXT VALUE FOR` cần `UPDATE` trên sequence nên được cấp riêng.
- **(M10) `ReverseProxy:KnownNetworks` (CIDR)** được thêm bên cạnh `KnownProxies`, vì IP container Nginx thay đổi mỗi lần tạo lại.
- **(M10) Lịch backup chạy bằng cron của máy chủ**, không thêm container scheduler (không thêm hạ tầng khi chưa có quyết định).
- **(D-27) Ảnh lưu trên đĩa (bind mount `MEDIA_DIR`)**, không dùng object storage để không thêm hạ tầng; chạy nhiều instance API thì `MEDIA_DIR` phải là thư mục dùng chung (NFS / SMB) hoặc chuyển sang object storage (cần quyết định mới).
- **(M10) D-26 — Giám sát không thêm hạ tầng:** chỉ số qua `System.Diagnostics.Metrics` + dòng log `Monitoring` mỗi phút; cảnh báo qua health check `/health/alerts` + container `monitor` gửi webhook. Chưa dùng Prometheus / OpenTelemetry / Grafana (sau MVP); khi thêm, meter `ELearning` dùng lại được nguyên vẹn.
- **(M10) Tổng trong cửa sổ trượt là của từng instance.** Chạy nhiều instance API thì mỗi instance tự tính tỉ lệ 5xx; `attempt-backlog` đọc từ database nên đúng cho cả hệ thống.
- **(M10) `error-rate` cần tối thiểu 50 request trong cửa sổ**, để lúc vắng (ví dụ 1 lỗi / 10 request) không báo nhầm. Các ngưỡng là giả định *(cần xác nhận)*, đổi ở section `Monitoring`.
- **(D-29) Chuyển sang PostgreSQL:** compose dùng `postgres:17-alpine` (healthcheck `pg_isready`), bỏ các service `sqlserver` và `db-init`. Chuỗi kết nối: `Host=postgres;Port=5432;Database=${DB_NAME};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Include Error Detail=true`. Biến `.env`: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `DB_NAME` (mặc định `elearning_db`); không còn `MSSQL_*`, `SA_PASSWORD`, `APP_DB_LOGIN`.
- **(D-29) Việc còn tồn đọng** *(cần làm trước khi đưa production)*: (1) viết lại `deploy/scripts/backup.sh`, `restore.sh`, `restore-test.sh`, `monitor.sh` (kiểm tra backup), `deploy/scripts/windows/register-backup-tasks.ps1` theo `pg_dump` / `pg_restore` (+ WAL archiving nếu cần RPO 15 phút); (2) tạo role DB quyền tối thiểu cho API thay cho dùng chung `POSTGRES_USER`; (3) dọn `deploy/sql/init-app-login.sql`; (4) rà `frontend/elearning-web/e2e/` và `load-tests/lib/seed.js` còn nhắc SQL Server. Giữa chừng, dùng lệnh `pg_dump` / `pg_restore` ở mục 6.
- **(D-29) Chuyển dữ liệu từ SQL Server cũ:** không có công cụ tự động. Tạo database PostgreSQL mới bằng migration bundle rồi nạp dữ liệu (ví dụ xuất CSV từng bảng và `COPY`), hoặc dùng `pgloader`; chạy `--seed` sau cùng nếu bảng role / permission còn trống.
