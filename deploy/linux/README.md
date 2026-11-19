# Triển khai lên server Linux (Docker, build từ mã nguồn)

Server chạy `docker compose` ở `/opt/elearning`, build trực tiếp từ mã nguồn trên GitHub.
Một lệnh trên máy dev cập nhật bản mới nhất lên server.

| File | Chạy ở đâu | Việc làm |
|---|---|---|
| `deploy.cmd` | máy Windows của dev | `git push` nhánh hiện tại, rồi gọi `update.sh` trên server qua SSH |
| `update.sh` | server Linux | `pg_dump` trước khi cập nhật (giữ 10 bản), `git reset --hard origin/<nhánh>`, `docker compose up -d --build`, chờ `/health/ready`, dọn cache |

## Cập nhật hằng ngày

1. Commit code. Chỉ code đã commit và push mới lên được server.
2. Bấm đúp `deploy\linux\deploy.cmd` (hoặc chạy trong terminal ở thư mục gốc repo).
3. Chờ dòng `OK: hệ thống đã chạy.` (2–5 phút).

Không cập nhật khi đang có kỳ thi diễn ra: API khởi động lại vài giây (`docs/09-van-hanh.md` mục 5).

## Cài đặt cho một người mới (làm một lần)

Mỗi người dùng **khóa SSH riêng**; không chia sẻ chung một khóa hay mật khẩu root.

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
   type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh root@<server> "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys"
   ```

5. Kiểm tra: `ssh root@<server> echo ok` in `ok` mà không hỏi mật khẩu là xong.
6. Server khác mặc định? Đặt biến môi trường một lần (mở lại terminal để có hiệu lực):

   ```powershell
   setx DEPLOY_SERVER root@<ip-hoac-domain>
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
- Cập nhật lỗi: `ssh root@<server> "cd /opt/elearning && docker compose logs --tail=100 api"`.
- Quay lại bản cũ: trên server `git reset --hard <commit>` rồi `docker compose up -d --build`; nếu migration mới không tương thích thì khôi phục từ file `.dump` (`docs/09-van-hanh.md` mục 6).
- Dung lượng ổ đĩa: `update.sh` đã dọn image và build cache cũ; không dùng `docker system prune --volumes` vì sẽ xóa database.
