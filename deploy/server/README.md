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

Package trên ghcr.io mặc định **riêng tư**. Chọn một trong hai:

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

```bash
docker compose pull
docker compose up -d
```

Database tự cập nhật (`migrate`) trước khi API mới chạy. Không cập nhật khi đang có đề thi mở.

**Quay lại bản cũ:** đặt `IMAGE_TAG=sha-xxxxxxx` (mã commit, xem tab *Actions* hoặc trang package trên GitHub)
trong `.env`, rồi `docker compose pull && docker compose up -d`. Lưu ý: chỉ quay lại được khi bản mới chưa đổi
cấu trúc database theo cách bản cũ không đọc được.

## 6. Lưu ý

- Dữ liệu nằm trong volume Docker `elearning_pgdata` và thư mục `media/` (ảnh câu hỏi). Xóa volume là mất dữ liệu.
- Backup: script trong `deploy/scripts/` hiện vẫn viết cho SQL Server, **chưa dùng được với PostgreSQL**;
  vì vậy `BACKUP_*_MAX_AGE_*` để rỗng để monitor không báo lỗi backup.
- Docker Desktop (Windows / Mac) làm mọi người dùng hiện ra cùng một IP → giới hạn tần suất theo IP bị gộp chung.
  Máy chủ Linux không bị.
- Giữ `deploy/server/docker-compose.yml` đồng bộ với `docker-compose.yml` ở gốc repo khi thêm / sửa service.
