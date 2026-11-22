# Triển khai lên server Linux (Docker, build từ mã nguồn)

Server chạy `docker compose` ở `/opt/elearning`, build trực tiếp từ mã nguồn trên GitHub.
Một lệnh trên máy dev cập nhật bản mới nhất lên server.

| File | Chạy ở đâu | Việc làm |
|---|---|---|
| `deploy.cmd` | máy Windows của dev | `git push` nhánh hiện tại, rồi gọi `update.sh` trên server qua SSH |
| `update.sh` | server Linux | `pg_dump` trước khi cập nhật (giữ 10 bản), `git reset --hard origin/<nhánh>`, `docker compose up -d --build`, chờ `/health/ready`, dọn cache |

## Từ code đến server (quy trình cho mọi thành viên)

**Nguyên tắc:** server luôn chạy nhánh `main` của GitHub. Code chưa vào `main` thì chưa lên server.

### Lần đầu: lấy mã nguồn về máy

```powershell
git clone https://github.com/PhongThanS/ELearning.git
cd ELearning
git config user.name "Tên của bạn"
git config user.email "email@cua-ban"
```

GitHub không nhận mật khẩu tài khoản khi push qua HTTPS. Khi được hỏi, đăng nhập bằng trình duyệt (Git Credential Manager) hoặc dùng Personal Access Token (GitHub → Settings → Developer settings → Personal access tokens, quyền `repo`). Người quản trị repo cần cấp cho bạn quyền ghi (Settings → Collaborators).

### Mỗi lần có thay đổi

1. **Cập nhật code mới nhất** trước khi làm:
   ```powershell
   git checkout main
   git pull --rebase origin main
   ```
2. **Sửa code**, rồi kiểm tra trước khi đẩy (theo `CLAUDE.md`): backend `dotnet build ELearning.sln` và `dotnet test`, frontend `npm run lint && npm run typecheck && npm run test && npm run build`.
3. **Commit** (thông điệp ngắn, nói rõ làm gì):
   ```powershell
   git add <các file đã sửa>
   git commit -m "feat(api): mô tả ngắn"
   ```
   Không `git add .` bừa: dễ kéo theo file tạm, `.env`, file backup. Không commit bí mật.
4. **Đẩy lên GitHub:**
   ```powershell
   git pull --rebase origin main
   git push origin main
   ```
   Nếu `pull --rebase` báo xung đột: mở file bị báo, giữ phần đúng, `git add <file>`, `git rebase --continue`; muốn bỏ thì `git rebase --abort`. Không dùng `git push --force` trên `main`.
5. **Cập nhật server:** bấm đúp `deploy\linux\deploy.cmd` (bước dưới).

Làm việc theo nhánh riêng (khuyến nghị khi thay đổi lớn hoặc cần người khác xem): `git checkout -b feature/ten-ngan`, push nhánh đó, mở Pull Request vào `main`, chờ CI xanh (`backend`, `frontend`, `docker` trong tab Actions) rồi merge. Sau khi merge, `git checkout main && git pull` và chạy `deploy.cmd`. `deploy.cmd` từ chối chạy khi đang ở nhánh khác `main`.

## Cập nhật server (bước deploy)

1. Đảm bảo đang ở nhánh `main` và code đã commit.
2. Bấm đúp `deploy\linux\deploy.cmd` (hoặc chạy trong terminal ở thư mục gốc repo). Script cảnh báo nếu còn file chưa commit, push `main`, rồi cập nhật server qua SSH.
3. Chờ dòng `OK: hệ thống đã chạy.` (2–5 phút). Thấy `LỖI` thì xem mục "Lưu ý" bên dưới.

Không cập nhật khi đang có kỳ thi diễn ra: API khởi động lại vài giây (`docs/09-van-hanh.md` mục 5). Trước khi deploy nên xem tab Actions trên GitHub để chắc commit mới nhất đã xanh, vì server không tự kiểm tra test.

## Cài đặt cho một người mới (làm một lần)

Mỗi người dùng **khóa SSH riêng**; không chia sẻ chung một khóa hay mật khẩu root.

0. Cách nhanh: chạy `deploy\linux\setup-ssh-key.cmd` một lần (tạo khóa và cài lên server, chỉ hỏi mật khẩu root một lần), rồi bỏ qua các bước 3–5 bên dưới.
1. Máy Windows cần có Git và OpenSSH Client (có sẵn trong Windows 10/11).
2. Có quyền ghi (push) vào repo GitHub.
3. Tạo khóa SSH (bỏ qua nếu đã có `~/.ssh/id_ed25519`):

   ```powershell
   ssh-keygen -t ed25519 -f $env:USERPROFILE\.ssh\id_ed25519
   ```

4. Gửi **file `.pub`** (khóa công khai, không phải file không có đuôi) cho người quản trị server. Người quản trị thêm vào server:

   ```bash
   echo "<nội dung file .pub>" >> /root/.ssh/authorized_keys
   ```

   Hoặc nếu bạn đã vào được server bằng mật khẩu:

   ```powershell
   type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh -p 24700 root@<server> "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys"
   ```

5. Kiểm tra: `ssh -p 24700 root@<server> echo ok` in `ok` mà không hỏi mật khẩu là xong.
6. Server khác mặc định? Đặt biến môi trường một lần (mở lại terminal để có hiệu lực):

   ```powershell
   setx DEPLOY_SERVER root@<ip-hoac-domain>
   setx DEPLOY_PORT <cong-ssh>   # mặc định 24700
   ```

Thu hồi quyền: xóa dòng khóa của người đó trong `/root/.ssh/authorized_keys` trên server.

## Cài đặt server (một lần, người quản trị)

```bash
curl -fsSL https://get.docker.com | sh
apt install -y git
git clone https://github.com/PhongThanS/ELearning.git /opt/elearning
cd /opt/elearning
cp .env.example .env          # đổi POSTGRES_PASSWORD, JWT_SIGNING_KEY, ADMIN_INITIAL_PASSWORD, PUBLIC_ORIGIN
docker compose up -d --build
```

- Repo private: chạy `git config --global credential.helper store`, rồi `git pull` một lần và nhập Personal Access Token để server tự nhớ.
- RAM 4 GB nên thêm swap 2 GB để build không bị OOM: `fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile && echo '/swapfile none swap sw 0 0' >> /etc/fstab`.
- HTTPS trực tiếp (không Cloudflare): đặt `fullchain.pem`, `privkey.pem` vào `deploy/certs/`, đặt `SERVER_NAME` và `PUBLIC_ORIGIN=https://<domain>` trong `.env`. `update.sh` tự dùng `docker-compose.prod.yml` khi thấy `deploy/certs/fullchain.pem`.

## Lưu ý

- `.env`, `media/`, `backups/` nằm ngoài git nên không bị `git reset --hard` ảnh hưởng. **Không sửa file trực tiếp trong `/opt/elearning`**: mọi thay đổi code phải qua git.
- Migration tự chạy ở mỗi lần cập nhật (service `migrate`). Bản `.dump` trước cập nhật nằm ở `/opt/elearning/backups/before-update-*.dump`.
- Cập nhật lỗi: `ssh -p 24700 root@<server> "cd /opt/elearning && docker compose logs --tail=100 api"`.
- Quay lại bản cũ: trên server `git reset --hard <commit>` rồi `docker compose up -d --build`; nếu migration mới không tương thích thì khôi phục từ file `.dump` (`docs/09-van-hanh.md` mục 6).
- Dung lượng ổ đĩa: `update.sh` đã dọn image và build cache cũ; không dùng `docker system prune --volumes` vì sẽ xóa database.

## Kết nối DB từ máy local (SSH tunnel)

Không mở cổng 5432 ra Internet. Mở cổng chỉ trên `127.0.0.1` của server rồi đi qua SSH.

1. Trên server, tạo `/opt/elearning/docker-compose.override.yml` (file này nằm ngoài git, `update.sh` tự nạp):

   ```yaml
   services:
     postgres:
       ports:
         - "127.0.0.1:5432:5432"
   ```

   Áp dụng: `cd /opt/elearning && docker compose -f docker-compose.yml -f docker-compose.override.yml up -d postgres`

2. Trên máy local, mở tunnel (giữ cửa sổ này mở):

   ```powershell
   ssh -p 24700 -N -L 15432:127.0.0.1:5432 root@<server>
   ```

3. Kết nối bằng DBeaver / pgAdmin / psql tới `Host=localhost Port=15432 Database=elearning_db`, user `postgres`, mật khẩu là `POSTGRES_PASSWORD` trong `.env` của server.

Chỉ nên đọc dữ liệu hoặc chạy truy vấn chẩn đoán; không sửa dữ liệu bằng tay (xem `CLAUDE.md`, quy tắc về snapshot và lịch sử).
