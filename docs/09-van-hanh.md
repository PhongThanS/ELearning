# 09 — Vận hành và triển khai

## 1. Cấu hình

`appsettings.json` (không chứa bí mật):
```json
{
  "ConnectionStrings": { "DefaultConnection": "" },
  "Jwt": { "Issuer": "ELearning", "Audience": "ELearning.Web", "AccessTokenMinutes": 15, "RefreshTokenDays": 7, "SessionMaxHours": 12 },
  "Auth": { "AllowSelfRegistration": false, "LockoutMaxFailedAttempts": 5, "LockoutMinutes": 15, "RefreshReuseGraceSeconds": 30 },
  "Exam": { "SubmitGraceSeconds": 30, "ExpirationSweepIntervalSeconds": 60, "ExpirationSweepBatchSize": 100 },
  "App": { "BusinessTimeZone": "Asia/Ho_Chi_Minh", "PublicOrigin": "https://elearning.example.vn" },
  "Media": { "RootPath": "media", "MaxBytes": 2097152, "LinkLifetimeHours": 6 },
  "Monitoring": { "ErrorRateWindowMinutes": 5, "ErrorRatePercent": 1, "ErrorRateMinRequests": 50, "OverdueAttemptMinutes": 5, "SnapshotIntervalSeconds": 60 },
  "RateLimits": { },
  "Seed": { "AdminUserName": "admin", "AdminEmail": "admin@elearning.local" },
  "ReverseProxy": { "KnownProxies": [], "KnownNetworks": [] },
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
- Chuỗi kết nối (PostgreSQL, định dạng Npgsql). Mặc định cho dev trong `appsettings.Development.json`:
  `Host=localhost;Port=5432;Database=elearning_db;Username=postgres;Password=postgres;Include Error Detail=true`
  Máy khác thì ghi đè bằng user-secrets hoặc biến môi trường `ConnectionStrings__DefaultConnection`.
- `Include Error Detail=true` đưa giá trị dữ liệu vào thông báo lỗi của PostgreSQL (và vào log). Hữu ích khi dev; các file compose hiện cũng bật ở Production — nên tắt khi dữ liệu thật nhạy cảm.
- Tùy chọn ít dùng: `Exam:ExpirationWorkerEnabled` (tắt job tự nộp trên một instance), `Monitoring:SnapshotEnabled`.
- `UserRetention:DeleteLockedAfterDays` (mặc định 90, 0 = tắt), `UserRetention:SweepIntervalMinutes` (360), `UserRetention:BatchSize` (100): tự động **xóa vĩnh viễn** tài khoản khóa quá hạn cùng toàn bộ bài thi, điểm, điểm danh của họ (D-41). Muốn giữ lịch sử của học sinh nghỉ thì đặt 0 hoặc không khóa tài khoản.

## 2. Thứ tự khởi động backend

```text
Tạo builder
→ Cấu hình + validate options
→ Serilog
→ DbContext (EnableRetryOnFailure, converter UTC/enum)
→ TimeProvider.System
→ Xác thực (JWT) → Phân quyền (policy theo permission, FallbackPolicy)
→ Application services, validators, graders
→ BackgroundService: AttemptExpirationWorker, MonitoringSnapshotWorker
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
- PostgreSQL 17 cài trên máy hoặc chạy trong Docker (`docker run -d --name elearning-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine`).
- Tạo schema: `dotnet ef database update -p src/ELearning.Infrastructure -s src/ELearning.Api` (trong `backend/`).
- API chạy bằng `dotnet run --project src/ELearning.Api --launch-profile http` (cổng 5136), hoặc từ Visual Studio / `dotnet watch`.
- Frontend chạy bằng `npm run dev` (cổng 5173; Vite proxy `/api` và `/health` → `http://localhost:5136`).

`docker-compose.yml` ở gốc repo chạy cả hệ thống (`postgres`, `migrate`, `seed`, `media-init`, `api`, `web`, `monitor`) ở chế độ Production, dùng khi muốn chạy thử cả stack (mục 5.1).

Seed dữ liệu cho Development (tự chạy mỗi lần API khởi động ở Development):
- Tài khoản `admin` (`Seed:AdminPassword`), `teacher01`, `student01`, `student02` (`Seed:DevPassword`; riêng `teacher01` dùng mật khẩu dev cố định trong `DatabaseSeeder`); tất cả có `MustChangePassword = false`. Mật khẩu dev mặc định nằm trong `appsettings.Development.json`.
- Nhóm `DEMO`, gồm `student01` và `student02`.
- Chuyên đề demo và câu hỏi demo (`DemoQuestions`).
- Đề demo "C# Basic": 10 câu, có code block, 60 phút, đạt 50%, 3 lượt, `AccessMode = ASSIGNED` gán nhóm `DEMO`, đã publish.

**Dữ liệu demo** (máy dev / Docker local): `ADMIN_PASSWORD='...' node deploy/scripts/demo-data.mjs` tạo qua API 5 danh mục, 41 câu hỏi đủ 5 loại, 24 học viên `demo.hs01…demo.hs24` (mật khẩu chung: biến `DEMO_STUDENT_PASSWORD`, mặc định ghi trong script), 1 nhóm, 5 lớp học (có học viên thuộc nhiều lớp, một lớp đã tắt) và 7 đề ở các trạng thái: đang mở có lượt đã nộp / đang làm, nhiều lượt, có tự luận chờ chấm, PUBLIC, đã đóng, sắp diễn ra, bản nháp. Chạy lại được: thứ đã có thì dùng lại, đề đã có thì bỏ qua.

**Không bao giờ chạy seed dev ở Production.** Production chỉ seed role, permission và một tài khoản admin khởi tạo; mật khẩu lấy từ biến môi trường, và `MustChangePassword = true`.

## 4. Migration

- EF Core migrations nằm trong `ELearning.Infrastructure/Persistence/Migrations`.
- Dev: `dotnet ef database update`.
- **Staging / Production:**
  - Migration bundle (`dotnet ef migrations bundle`) được build thành image `elearning-migrator` (`backend/Dockerfile`, target `migrator`).
  - Service `migrate` trong compose chạy bundle **trước** khi API mới khởi động; lỗi thì các service sau không chạy.
- **Không gọi `Database.Migrate()` lúc khởi động** ở Staging / Production, vì nhiều instance sẽ chạy migration cùng lúc, và tài khoản ứng dụng không nên có quyền DDL.
- Migration phải tương thích ngược với phiên bản API đang chạy (mở rộng trước, thu hẹp sau). Không đổi tên cột hay xóa cột trong cùng một lần deploy.
- **Tài khoản DB (hiện trạng):** `migrate`, `seed` và `api` đều kết nối bằng cùng một tài khoản `POSTGRES_USER` (mặc định `postgres`, superuser). Mục tiêu thiết kế là API dùng tài khoản riêng chỉ có quyền đọc / ghi dữ liệu, không có quyền DDL; phần này **chưa làm lại cho PostgreSQL** (`deploy/sql/init-app-login.sql` là script SQL Server cũ, không dùng được). Xem mục 10.
- Lịch sử migration bắt đầu từ `InitialPostgreSql`; database SQL Server cũ không nâng cấp được bằng migration.

## 5. Kiến trúc triển khai

```text
Internet
   ↓ HTTPS
Nginx (TLS, gzip, CSP/HSTS headers, rate limit thô)
   ├── /          → file tĩnh React (index.html không cache; asset có hash cache 1 năm)
   └── /api, /health → ASP.NET Core (Kestrel, HTTP nội bộ)
                          ↓
                      PostgreSQL 17  (+ thư mục ảnh câu hỏi MEDIA_DIR trên đĩa)
```

Máy chủ hiện tại không mở cổng ra Internet mà đi qua **Cloudflare Tunnel** (service `tunnel` trong `deploy/server/docker-compose.yml`, trỏ tới `http://web:80`); HTTPS do Cloudflare lo, Nginx chạy HTTP. Chi tiết: `deploy/server/README.md` mục 6.

- Frontend và API **cùng domain** (D-15).
- Nginx gửi `X-Forwarded-For` / `X-Forwarded-Proto`; API cấu hình `ForwardedHeaders` với `KnownProxies`.
- Trang lỗi tĩnh (502/503) không để lộ thông tin nội bộ.
- **Chỉ scale API ngang khi thực sự cần.** Khi chạy nhiều instance:
  - Cache permission / trạng thái user phải chuyển sang Redis, hoặc giảm TTL xuống 30 giây.
  - Rate limit chuyển sang Nginx hoặc dùng store dùng chung.
  - `AttemptExpirationWorker` chạy ở mọi instance vẫn an toàn nhờ chuyển trạng thái nguyên tử. Có thể thêm `pg_try_advisory_lock` để giảm tranh chấp. Mỗi vòng quét lặp theo lô `Exam:ExpirationSweepBatchSize` tới khi hết lượt quá hạn.
  - Refresh token không phụ thuộc instance, vì trạng thái nằm trong DB.
- **Khung giờ deploy:** không deploy khi có đề đang mở theo lịch. Admin xem được lịch đề trong dashboard.

### 5.1 Triển khai bằng Docker Compose (M10, đã làm)

```text
docker-compose.yml          postgres → migrate → seed → media-init → api → web → monitor (build từ mã nguồn)
docker-compose.prod.yml     override: Nginx HTTPS (80 → 443, HSTS), gắn chứng chỉ
deploy/server/              compose cho máy chủ chỉ kéo image (+ docker-compose.https.yml, update.ps1, tunnel)
backend/Dockerfile          target api (aspnet, user không phải root) và migrator (EF migration bundle)
frontend/elearning-web/Dockerfile   build Vite → nginx (template envsubst)
deploy/nginx/               cấu hình Nginx: header bảo mật, cache, proxy /api, trang lỗi tĩnh
deploy/scripts/             monitor.sh, notify.sh, media-sync.sh, demo-data.mjs; backup.sh / restore*.sh (CHƯA chuyển sang PostgreSQL, mục 6)
deploy/sql/init-app-login.sql       script SQL Server cũ, không còn dùng
.env.example                mọi bí mật / tham số (sao chép thành .env, không commit)
```

**Chạy thử trên một máy:**
```bash
cp .env.example .env        # đổi toàn bộ mật khẩu / khóa
docker compose up -d --build
# http://localhost:8080 — đăng nhập admin / ADMIN_INITIAL_PASSWORD, đổi mật khẩu lần đầu
```

**Thứ tự khởi động** (mỗi bước chỉ chạy khi bước trước thành công):
1. `postgres` healthy (`pg_isready`). Dữ liệu nằm ở volume `pgdata`; `BACKUP_DIR` được gắn vào `/backups` trong container.
2. `migrate`: migration bundle, kết nối bằng `POSTGRES_USER`.
3. `seed`: `--seed` (role, permission, admin khởi tạo), rồi thoát.
4. `media-init`: đặt quyền thư mục ảnh `MEDIA_DIR` (gắn vào `/var/lib/elearning/media`) cho user của API (uid 1654), kể cả file chép lại khi khôi phục.
5. `api` (Kestrel HTTP 8080, chỉ trong mạng nội bộ) và `web` (Nginx, cổng `HTTP_PORT`).
6. `monitor` (mục 7.1). Trên máy chủ có thêm `tunnel` khi bật `COMPOSE_PROFILES=tunnel`.

**Production có HTTPS:** đặt `fullchain.pem`, `privkey.pem` vào `deploy/certs/`, đặt `SERVER_NAME`, `PUBLIC_ORIGIN=https://<domain>`, rồi
`docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build`. Nếu TLS kết thúc ở load balancer phía trước thì giữ cấu hình HTTP và bật module realip của Nginx.

**Máy chủ chỉ kéo image (không cần mã nguồn):** GitHub Actions `release.yml` build `elearning-api`, `elearning-migrator`, `elearning-web`, `elearning-monitor` và đẩy lên `ghcr.io/phongthans/*` (tag `latest`, `sha-xxxxxxx`, `x.y.z` khi đẩy tag `v*`). Máy chủ chỉ cần thư mục `deploy/server/` (compose + `.env`) rồi `docker compose pull && docker compose up -d`; chi tiết `deploy/server/README.md`. Template HTTPS và script giám sát nằm sẵn trong image.

**Cập nhật phiên bản:**
- Build từ mã nguồn: `docker compose build` → `docker compose up -d`.
- Máy chủ kéo image: `docker compose pull && docker compose up -d`, hoặc trên Windows chạy `update.cmd` / `update.ps1` (chờ GitHub Actions build xong image của commit mới nhất → `pg_dump` database vào `BACKUP_DIR/before-update-*.dump`, giữ 10 bản → tải compose mới → pull → up → kiểm tra `/health/ready` và đúng phiên bản).
- `migrate` / `seed` chạy lại (idempotent) trước khi `api` mới khởi động. Không deploy khi có đề đang mở (mục 5).
- Quay lại bản cũ: đặt `IMAGE_TAG=sha-xxxxxxx` trong `.env` rồi pull + up (chỉ khi migration của bản mới còn tương thích).

**Ghi chú:**
- API tin `X-Forwarded-For` chỉ từ mạng compose `172.28.0.0/24` (`ReverseProxy:KnownNetworks`); Nginx **ghi đè** header này bằng IP thật của client, nên client không giả mạo được IP (rate limit theo IP phụ thuộc vào điều này).
- Nginx: CSP `default-src 'self'` (chỉ `style-src` có `'unsafe-inline'` cho thuộc tính style của React), `X-Frame-Options DENY`, `nosniff`, HSTS chỉ qua HTTPS; `index.html` không cache, `/assets/*` cache 1 năm; `client_max_body_size 6m` cho import Excel, riêng `/api/questions/import-markdown/docx-*` (file đề Word, API nhận tối đa 30 MB) là `31m`; rate limit thô 200 request/giây/IP cho `/api`; trang 50x tĩnh.
- **Docker Desktop (Windows / Mac) chuyển tiếp cổng qua proxy**, nên mọi client hiện ra cùng một IP gateway (`172.28.0.1`) → rate limit theo IP gom chung. Chỉ dùng để chạy thử; máy chủ Linux (Docker Engine) giữ nguyên IP thật của client.
- CI (`docker.yml`) dựng cả stack này trên mỗi thay đổi liên quan: kiểm tra file compose, build image, `/health/ready`, header CSP, `/health/alerts` bị chặn từ ngoài, một vòng `monitor`, và 4 kịch bản k6 ở chế độ smoke.
- Image `postgres:17-alpine`; không có giới hạn dung lượng theo license như SQL Server Express.

## 6. Backup và khôi phục

> **Hiện trạng (sau khi chuyển sang PostgreSQL, D-35): chưa có backup định kỳ.**
> - Bản sao lưu duy nhất đang chạy là `pg_dump` do `update.ps1` thực hiện **mỗi lần cập nhật** (`BACKUP_DIR/before-update-<thời gian>.dump`, giữ 10 bản).
> - `deploy/scripts/backup.sh`, `restore.sh`, `restore-test.sh` và `deploy/scripts/windows/register-backup-tasks.ps1` vẫn viết cho SQL Server (`sqlcmd`, service `sqlserver`, file `.bak` / `.trn`) và **không chạy được** với compose hiện tại.
> - Vì vậy mục tiêu RPO / RTO bên dưới **chưa được đáp ứng**, và chưa có lần thử khôi phục nào trên PostgreSQL. Để `BACKUP_FULL_MAX_AGE_HOURS` và `BACKUP_LOG_MAX_AGE_MINUTES` rỗng trong `.env` để `monitor` không báo lỗi backup giả.

**Sao lưu và khôi phục thủ công (dùng được ngay):**
```bash
# Sao lưu (định dạng custom, nén) vào BACKUP_DIR của máy chủ
docker compose exec -T postgres pg_dump -U postgres -d elearning_db -Fc -f /backups/manual-$(date +%Y%m%d-%H%M).dump
# Khôi phục đè lên database đang có (dừng api trước: docker compose stop api)
docker compose exec -T postgres pg_restore -U postgres -d elearning_db --clean --if-exists /backups/<tên file>.dump
```
Thư mục ảnh `MEDIA_DIR` phải được chép cùng (`deploy/scripts/media-sync.sh <MEDIA_DIR> <đích>` chép tăng dần, dùng được độc lập với database).

**Mục tiêu thiết kế** (giữ làm đích khi viết lại script): RPO 15 phút (5 phút trong ngày thi), RTO 1 giờ *(cần xác nhận)*.

| Loại (thiết kế SQL Server cũ) | Tần suất | Tương đương trên PostgreSQL |
|---|---|---|
| Full | Hằng ngày 01:00 (giờ VN), giữ 30 ngày | `pg_dump -Fc` hằng ngày, hoặc `pg_basebackup` |
| Differential | Mỗi 6 giờ | Không có khái niệm tương đương; dùng `pg_dump` dày hơn hoặc WAL |
| Transaction log | Mỗi 15 phút (5 phút ngày thi lớn) | Lưu trữ WAL liên tục (`archive_mode`) + khôi phục theo thời điểm (PITR) |

Chỉ `pg_dump` theo lịch thì RPO bằng khoảng cách giữa hai lần dump; muốn đạt 15 phút phải bật lưu trữ WAL.

- Lưu backup ở một vị trí khác với máy chủ DB (ổ riêng và bản sao offsite), có mã hóa.
- **Thử khôi phục mỗi tháng** vào một server riêng, có ghi lại thời gian khôi phục thực tế. Backup chưa từng được khôi phục thử thì chưa được coi là chiến lược backup hợp lệ.
- Sự cố DB giữa giờ thi: khôi phục xong thì admin dùng chức năng **gia hạn** cho các lượt thi bị ảnh hưởng. `ExpiredAt` không tự dừng khi hệ thống ngừng hoạt động.

**Công cụ của bản SQL Server (M10) — KHÔNG còn chạy được, giữ lại làm đặc tả cho bản PostgreSQL cần viết lại** (cùng hành vi: kiểm tra bản sao lưu, xóa bản quá hạn, báo webhook khi lỗi, thử khôi phục vào database tạm và so số dòng):
- `deploy/scripts/backup.sh full|diff|log`: backup `WITH CHECKSUM`, kiểm tra `RESTORE VERIFYONLY`, xóa bản quá hạn (full 30 ngày, diff / log 7 ngày). Lịch chạy bằng cron của máy chủ (ví dụ ở đầu script).
- **Máy Windows chạy Docker Desktop** (không có cron): `powershell -ExecutionPolicy Bypass -File deployscriptswindowsegister-backup-tasks.ps1` đăng ký 3 task trong Task Scheduler (`ELearningBackup full|diff|log`, cùng lịch như trên). Task chạy `backup.sh` qua Git Bash, ẩn cửa sổ (`run-backup.vbs`), chạy bù khi lỡ lịch, ghi log vào `backupsackup.log`; chỉ chạy khi người dùng đã đăng nhập (Docker Desktop cũng vậy), nên bật "Start Docker Desktop when you sign in".
- `deploy/scripts/restore.sh --target <db> [--replace] full.bak [diff.bak] [log.trn ...]`: khôi phục một chuỗi backup.
- `deploy/scripts/restore-test.sh`: lấy full mới nhất + diff mới nhất sau nó + mọi log sau đó, khôi phục vào `<DB>_RestoreTest`, so số dòng các bảng chính với bản gốc, chạy `DBCC CHECKDB`, in thời gian khôi phục, rồi xóa database thử. **Chạy mỗi tháng** và ghi kết quả vào sổ vận hành.
- File backup nằm ở `BACKUP_DIR` trên máy chủ; cần đồng bộ ra nơi khác (offsite).
- **Ảnh câu hỏi (D-27):** mỗi lần `backup.sh` chạy (full / diff / log), sau khi backup database xong, `media-sync.sh` chép ảnh mới từ `MEDIA_DIR` sang `BACKUP_DIR/media`. Ảnh bất biến, tên theo SHA-256 và không bị xóa, nên bản sao là tăng dần, không cần chính sách giữ lại, và luôn đủ cho mọi bản backup database (RPO của ảnh bằng RPO của log backup).
- Khôi phục: `restore.sh ... --with-media` chép ảnh còn thiếu từ `BACKUP_DIR/media` về `MEDIA_DIR` (chỉ thêm, không ghi đè) rồi chạy `media-init`. `restore-test.sh` kiểm tra mọi dòng `MediaFiles` của bản khôi phục có file trong bản sao ảnh; thiếu thì thất bại.

## 7. Giám sát

**Tối thiểu cho MVP:**
- Log có cấu trúc (JSON), có `traceId`, gom về một nơi (Seq, Elastic hoặc file + công cụ đọc).
- Health check:
  - `/health/live`: tiến trình còn sống.
  - `/health/ready`: kết nối được PostgreSQL.
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
| `elearning.db.errors` | Exception từ database dẫn tới 500 (không tính trùng khóa / xung đột dữ liệu) |
| `elearning.attempts.auto_submitted`, `…auto_submit_failures`, `…sweep.duration` | Mỗi vòng `AttemptExpirationWorker` |
| `elearning.attempts.in_progress`, `…overdue`, `…oldest_overdue` (giây) | Đo mỗi phút; `oldest_overdue` là **độ trễ worker** |

- Đọc trực tiếp: `dotnet-counters monitor -n ELearning.Api --counters ELearning,Microsoft.AspNetCore.Hosting`.
- **Chậm do máy chủ hay do mạng?** Mọi response có header `Server-Timing: app;dur=<ms>` (thời gian API tự xử lý, `ServerTimingMiddleware`): xem ở DevTools → Network → Timing, hoặc `performance.getEntriesByType("resource")[i].serverTiming`. Thời gian chờ trên trình duyệt trừ `app;dur` là phần đường mạng (Cloudflare, tunnel). Dòng log request của Serilog ghi mức **Warning** khi xử lý quá 500 ms (`RequestLogLevel`), mức Error khi 5xx; lọc request chậm trên server: `docker compose logs api --no-log-prefix --since 24h | Select-String '"@l":"Warning"' | Select-String 'responded'`.
- Mỗi `Monitoring:SnapshotIntervalSeconds` (60 giây) API ghi một dòng log `Monitoring: …` có cấu trúc với các số trên, để công cụ gom log vẽ biểu đồ. Khi có điều kiện cảnh báo, ghi thêm dòng `ALERT <mục>` ở mức Warning.

**Cảnh báo** — `/health/alerts` trả 503 khi một check lỗi, kèm số liệu từng check:

| Check | Lỗi khi |
|---|---|
| `database` | Không kết nối được PostgreSQL (giống `/health/ready`) |
| `error-rate` | 5xx > `ErrorRatePercent` (1%) trong `ErrorRateWindowMinutes` (5 phút), khi có ít nhất `ErrorRateMinRequests` (50) request |
| `attempt-backlog` | Lượt `IN_PROGRESS` quá hạn (đã hết ân hạn) lâu nhất đã quá `ExpiredAt` hơn `OverdueAttemptMinutes` (5 phút) |
| `expiration-worker` | Job tự nộp chạy trên instance này nhưng không xong vòng quét nào trong 3 chu kỳ (`Exam:ExpirationSweepIntervalSeconds`) |

- `/health/alerts` **bị Nginx chặn** (404) từ bên ngoài; chỉ gọi trong mạng nội bộ (`http://api:8080`).
- Container `monitor` (`deploy/scripts/monitor.sh`, image `curlimages/curl`) mỗi phút kiểm tra `/health/ready`, `/health/alerts`, trang chủ qua Nginx, và có file backup mới (`BACKUP_FULL_MAX_AGE_HOURS`, `BACKUP_LOG_MAX_AGE_MINUTES`). **Phần kiểm tra backup còn tìm file `<DB>_full_*.bak` / `<DB>_log_*.trn` của SQL Server**, nên hiện phải để rỗng hai biến này (bỏ qua kiểm tra); `.env.example` ở gốc repo vẫn ghi 26 / 30 và cần xóa giá trị khi dùng. Khi một mục chuyển sang lỗi, còn lỗi sau mỗi 30 phút, hoặc hết lỗi, nó gửi webhook `ALERT_WEBHOOK_URL` (Slack, Mattermost, Google Chat, Teams; Discord đặt `ALERT_WEBHOOK_FIELD=content`). Không đặt webhook thì cảnh báo nằm trong `docker compose logs monitor`.
- `deploy/scripts/notify.sh` gửi một cảnh báo qua webhook (dùng được độc lập). Cảnh báo "backup thất bại" phụ thuộc `backup.sh`, hiện chưa hoạt động (mục 6).
- Kiểm tra nhanh: `docker compose run --rm -e MONITOR_ONCE=1 monitor` (thoát 1 nếu có mục lỗi; CI chạy bước này).

## 8. CI/CD

GitHub Actions, 4 workflow trong `.github/workflows/`, chạy theo đường dẫn file thay đổi (push lên `main` và mọi PR):

| Workflow | Nội dung |
|---|---|
| `backend.yml` | `dotnet restore` → build Release (warnings as errors) → `dotnet format --verify-no-changes` → `dotnet ef migrations has-pending-model-changes` → `dotnet test` (PostgreSQL bằng Testcontainers) → `dotnet list package --vulnerable` |
| `frontend.yml` | `npm ci` → lint → typecheck → unit test → build → `npm audit --audit-level=high --omit=dev` |
| `docker.yml` | Kiểm tra các file compose, build image, dựng cả stack, smoke test, một vòng `monitor`, 4 kịch bản k6 chế độ smoke |
| `release.yml` | Build và đẩy 4 image (`elearning-api`, `-migrator`, `-web`, `-monitor`) lên `ghcr.io/phongthans/*`: tag `latest` và `sha-<7 ký tự>` khi push `main`, `x.y.z` khi push tag `v*` |

**Hiện trạng cần biết:**
- `release.yml` chạy **độc lập** với ba workflow kiểm tra: image `latest` được đẩy kể cả khi test của commit đó thất bại. `update.ps1` chỉ chờ `release.yml` thành công. Trước khi cập nhật máy chủ, tự xem tab Actions để chắc `backend` / `frontend` / `docker` của commit đó đều xanh.
- Không có môi trường Staging tự động và không có bước duyệt: "deploy" là người vận hành chạy `update.cmd` trên máy chủ.
- E2E Playwright chưa chạy trong CI (`08-kiem-thu.md` mục 8).

## 9. Definition of Done — Production

- HTTPS và HSTS hoạt động; bí mật nằm ngoài repo; options được validate khi khởi động.
- Migration được áp dụng bằng bundle / script, không áp lúc khởi động.
- Backup chạy theo lịch và **đã thử khôi phục** ít nhất một lần. *(Chưa đạt trên PostgreSQL — mục 6.)*
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
- **(M10 → D-35) Tài khoản DB tách biệt — chưa có trên PostgreSQL.** Bản SQL Server có `db-init` tạo login riêng cho API (không có quyền DDL). Sau khi chuyển sang PostgreSQL, mọi service dùng chung `POSTGRES_USER`. Khi làm lại: tạo role riêng cho API với `SELECT/INSERT/UPDATE/DELETE` trên bảng và `USAGE` trên sequence `"QuestionCodeSequence"`; `migrate` giữ tài khoản chủ sở hữu schema. Lưu ý `RegistrationSettingService` đang chạy `CREATE TABLE IF NOT EXISTS "SystemSettings"` lúc chạy, cần quyền tạo bảng hoặc phải bỏ đoạn đó.
- **(D-35) Việc còn nợ sau khi chuyển PostgreSQL:** (1) viết lại backup / restore / thử khôi phục và lịch chạy; (2) sửa phần kiểm tra backup của `monitor.sh`; (3) tài khoản DB quyền tối thiểu; (4) tắt `Include Error Detail` ở Production; (5) sửa connection string mặc định của `e2e/scripts/start-api.mjs`; (6) xóa `deploy/sql/init-app-login.sql` và các gói / hằng SQL Server còn sót.
- **(M10) `ReverseProxy:KnownNetworks` (CIDR)** được thêm bên cạnh `KnownProxies`, vì IP container Nginx thay đổi mỗi lần tạo lại.
- **(M10) Lịch backup dự kiến chạy bằng cron / Task Scheduler của máy chủ**, không thêm container scheduler (không thêm hạ tầng khi chưa có quyết định). Hiện chưa có lịch nào chạy (mục 6).
- **(D-27) Ảnh lưu trên đĩa (bind mount `MEDIA_DIR`)**, không dùng object storage để không thêm hạ tầng; chạy nhiều instance API thì `MEDIA_DIR` phải là thư mục dùng chung (NFS / SMB) hoặc chuyển sang object storage (cần quyết định mới).
- **(M10) D-26 — Giám sát không thêm hạ tầng:** chỉ số qua `System.Diagnostics.Metrics` + dòng log `Monitoring` mỗi phút; cảnh báo qua health check `/health/alerts` + container `monitor` gửi webhook. Chưa dùng Prometheus / OpenTelemetry / Grafana (sau MVP); khi thêm, meter `ELearning` dùng lại được nguyên vẹn.
- **(M10) Tổng trong cửa sổ trượt là của từng instance.** Chạy nhiều instance API thì mỗi instance tự tính tỉ lệ 5xx; `attempt-backlog` đọc từ database nên đúng cho cả hệ thống.
- **(M10) `error-rate` cần tối thiểu 50 request trong cửa sổ**, để lúc vắng (ví dụ 1 lỗi / 10 request) không báo nhầm. Các ngưỡng là giả định *(cần xác nhận)*, đổi ở section `Monitoring`.
