# Triển khai máy chủ bằng image dựng sẵn

Máy chủ **không cần mã nguồn** và không build gì: chỉ cần Docker và 3 file trong thư mục này.
Image do GitHub Actions (`.github/workflows/release.yml`) build mỗi khi có code mới trên nhánh `main`
và đẩy lên GitHub Container Registry:

| Image | Vai trò |
|---|---|
| `ghcr.io/phongthans/elearning-api` | API (.NET) |
| `ghcr.io/phongthans/elearning-migrator` | Cập nhật cấu trúc database trước khi API chạy |
| `ghcr.io/phongthans/elearning-web` | Nginx: giao diện web + chuyển `/api` sang API |
| `ghcr.io/phongthans/elearning-monitor` | Giám sát (docs/09-van-hanh.md mục 7) |

Tag: `latest` = bản mới nhất của `main`; `sha-xxxxxxx` = đúng một commit (dùng để quay lại bản cũ);
`1.2.3` = khi đẩy tag `v1.2.3`.

## 1. Chuẩn bị máy chủ

- Cài Docker: Linux dùng Docker Engine (`curl -fsSL https://get.docker.com | sudo sh`);
  Windows 10/11 dùng Docker Desktop (cần bật ảo hóa trong BIOS, bật "Start Docker Desktop when you sign in").
- Tạo một thư mục, ví dụ `C:\elearning` hoặc `~/elearning`, chép vào đó 3 file:
  `docker-compose.yml`, `docker-compose.https.yml`, `.env.example`
  (tải từ GitHub: thư mục `deploy/server/` → từng file → nút *Download raw file*).

## 2. Quyền tải image

Repo đang **công khai** nên image cũng công khai: `docker compose pull` chạy được ngay, không cần đăng nhập.
Image không chứa bí mật (mật khẩu / khóa nằm trong `.env` trên máy chủ).

Nếu sau này chuyển repo sang riêng tư thì package cũng riêng tư; khi đó chọn một trong hai:

- **Đăng nhập trên máy chủ (giữ riêng tư):** GitHub → Settings → Developer settings → Personal access tokens →
  *Tokens (classic)* → tạo token chỉ có quyền `read:packages`. Trên máy chủ:
  ```bash
  docker login ghcr.io -u <tên GitHub>
  ```
  dán token khi được hỏi mật khẩu. Chỉ cần làm một lần.
- **Mở công khai:** GitHub → trang cá nhân → *Packages* → từng package `elearning-*` → *Package settings* →
  *Change visibility* → Public. Khi đó không cần `docker login`, nhưng ai cũng tải được image.

## 3. Cấu hình

```bash
cp .env.example .env
```

Sửa `.env` (Windows: `notepad .env`). Bắt buộc:

| Biến | Giá trị |
|---|---|
| `POSTGRES_PASSWORD` | chuỗi ngẫu nhiên (`openssl rand -base64 48`) |
| `JWT_SIGNING_KEY` | chuỗi ngẫu nhiên khác |
| `ADMIN_INITIAL_PASSWORD` | mật khẩu admin lần đầu (bắt buộc đổi khi đăng nhập) |
| `PUBLIC_ORIGIN` | địa chỉ người dùng gõ vào trình duyệt, ví dụ `http://192.168.1.20` |
| `HTTP_PORT` | `80` (hoặc cổng khác nếu 80 đã bị dùng) |

Không gửi file `.env` cho ai, không đưa lên git.

## 4. Chạy

```bash
docker compose pull
docker compose up -d
docker compose ps
```

`postgres`, `api`, `web`, `monitor` phải `running`; `migrate`, `seed`, `media-init` chạy một lần rồi `exited (0)`.
Mở `PUBLIC_ORIGIN` trên trình duyệt, đăng nhập `admin` / `ADMIN_INITIAL_PASSWORD`, đổi mật khẩu.

**HTTPS (có tên miền):** đặt `fullchain.pem`, `privkey.pem` vào thư mục `certs/` cạnh file compose,
đặt `SERVER_NAME=<tên miền>` và `PUBLIC_ORIGIN=https://<tên miền>` trong `.env`, rồi chạy mọi lệnh với cả hai file:
```bash
docker compose -f docker-compose.yml -f docker-compose.https.yml up -d
```

## 5. Cập nhật bản mới

**Windows — một lệnh (khuyến nghị):** đặt `update.ps1` và `update.cmd` cạnh `.env` (lần đầu tải về):

```powershell
cd C:\elearning
Invoke-WebRequest https://raw.githubusercontent.com/PhongThanS/ELearning/main/deploy/server/update.ps1 -OutFile update.ps1
Invoke-WebRequest https://raw.githubusercontent.com/PhongThanS/ELearning/main/deploy/server/update.cmd -OutFile update.cmd
```

Từ đó mỗi lần cập nhật chỉ cần **bấm đúp `update.cmd`** (hoặc `powershell -ExecutionPolicy Bypass -File C:\elearning\update.ps1`).
Không cần mã nguồn hay đẩy gì từ máy khác: code chỉ cần nằm trên nhánh `main` (GitHub Actions tự build image).
Script tự: bật Docker Desktop nếu đang tắt → hỏi GitHub commit mới nhất của `main` và **chờ build xong** (tối đa 30 phút;
commit chỉ sửa tài liệu thì không cần build) → sao lưu database vào `BACKUP_DIR` (`before-update-*.dump`, giữ 10 bản) →
tải `docker-compose.yml` mới nhất (bản cũ giữ ở `docker-compose.yml.bak-*`, không hợp lệ thì giữ nguyên bản cũ) →
`docker compose pull` + `up -d` → chờ `migrate` / `seed` xong → kiểm tra `/health/ready`, trang web, tunnel → dọn image cũ
→ tự cập nhật `update.ps1`. Bước nào lỗi thì dừng và in lệnh xem log; nhật ký mỗi lần chạy ở `logs\update-*.log`.
Cuối cùng in phiên bản đang chạy và xác nhận đúng commit mới nhất. Không đụng tới `.env`. Tham số: `-SkipBackup`,
`-KeepCompose`, `-NoWait` (không chờ GitHub build, lấy ngay bản đã build gần nhất).

Khôi phục từ bản sao lưu (ví dụ): `docker compose exec -T postgres pg_restore -U postgres -d elearning_db --clean /backups/before-update-<thời gian>.dump`.

**Thủ công (mọi hệ điều hành):**

```bash
docker compose pull
docker compose up -d
```

Database tự cập nhật (`migrate`) trước khi API mới chạy. Không cập nhật khi đang có đề thi mở.

**Quay lại bản cũ:** đặt `IMAGE_TAG=sha-xxxxxxx` (mã commit, xem tab *Actions* hoặc trang package trên GitHub)
trong `.env`, rồi `docker compose pull && docker compose up -d`. Lưu ý: chỉ quay lại được khi bản mới chưa đổi
cấu trúc database theo cách bản cũ không đọc được.

## 6. Đưa ra Internet bằng Cloudflare Tunnel

Không cần mở cổng trên modem, có HTTPS tự động. Cần một tên miền đã chuyển về Cloudflare.

1. Cloudflare → Zero Trust → Networks → Tunnels → *Create a tunnel* → Cloudflared → đặt tên → copy **token**
   (chuỗi sau `--token` trong lệnh mẫu).
2. *Public Hostname*: subdomain `thi`, domain của bạn, Service **HTTP**, URL **`web:80`** → Save.
3. Trong `.env`: bỏ `#` ở `COMPOSE_PROFILES=tunnel`, dán token vào `CLOUDFLARE_TUNNEL_TOKEN=`,
   đặt `PUBLIC_ORIGIN=https://thi.<tên miền>`.
4. `docker compose up -d` rồi `docker compose logs tunnel` phải có `Registered tunnel connection`.

**Có máy vào rất chậm, máy khác lại nhanh** (tải file lên vài chục giây, ping vài trăm ms tới vài giây): người dùng
vào Cloudflare ở điểm nào (Singapore, Hồng Kông…) là do nhà mạng của họ; tunnel mặc định chạy QUIC (UDP), nhà mạng VN
hay bóp UDP đi quốc tế nên chặng Cloudflare → tunnel → máy chủ qua một số điểm bị nghẽn. Cách xử lý:

1. Xem tunnel đang nối tới đâu, giao thức gì: `docker compose logs tunnel | Select-String "Registered tunnel connection"`
   (`location=sin…/hkg…`, `protocol=quic`).
2. Chuyển sang TCP: thêm dòng `CLOUDFLARE_TUNNEL_PROTOCOL=http2` vào `.env`, chạy `docker compose up -d tunnel`; log
   phải có `protocol=http2`. Nếu tunnel không nối được (mạng chặn TCP cổng 7844) thì xóa dòng đó và chạy lại lệnh.
3. Đo lại từ máy chậm: `https://<tên miền>/cdn-cgi/trace` cho biết điểm Cloudflare (`colo=`); trong DevTools → Network,
   cột Time của request API trừ `Server-Timing` (`app;dur`) là phần đường mạng.

Không dùng `docker-compose.https.yml` cùng tunnel (HTTPS do Cloudflare lo).

## 7. Lưu ý

- Dữ liệu nằm trong volume Docker `elearning_pgdata` và thư mục `media/` (ảnh câu hỏi). Xóa volume là mất dữ liệu.
- Backup: script trong `deploy/scripts/` hiện vẫn viết cho SQL Server, **chưa dùng được với PostgreSQL**;
  vì vậy `BACKUP_*_MAX_AGE_*` để rỗng để monitor không báo lỗi backup.
- Docker Desktop (Windows / Mac) làm mọi người dùng hiện ra cùng một IP → giới hạn tần suất theo IP bị gộp chung.
  Máy chủ Linux không bị.
- Giữ `deploy/server/docker-compose.yml` đồng bộ với `docker-compose.yml` ở gốc repo khi thêm / sửa service.
