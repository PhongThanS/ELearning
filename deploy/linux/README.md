# Triển khai lên server Linux (Docker, build ở máy dev)

Server chạy `docker compose` ở `/opt/elearning`. **Image Docker được build ở máy dev** rồi gửi lên server;
server chỉ nạp image và khởi động lại. Một lệnh trên máy dev làm toàn bộ.

| File | Chạy ở đâu | Việc làm |
|---|---|---|
| `deploy.cmd` | máy Windows của dev | build 3 image (`elearning-api`, `elearning-migrator`, `elearning-web`) cho `linux/amd64`, đóng gói, `git push` `main`, gửi image bằng `scp`, gọi `load-images.sh` trên server |
| `deploy.cmd build` | máy Windows của dev | chỉ build và đóng gói (`%TEMP%\elearning-images.tar`), không đụng GitHub hay server |
| `load-images.sh` | server Linux | kiểm tra đúng commit, `pg_dump` (giữ 10 bản), giữ image cũ với tag `prev`, `docker load`, `docker compose up -d --no-build`, chờ `/health/ready` |
| `update.sh` | server Linux | **dự phòng:** build ngay trên server. Ngày 25/11/2026 bước đóng gói migration bị treo trên server, nên không dùng làm cách chính |

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
5. **Cập nhật server:** bấm đúp `deploy\linux\deploy.cmd` (build ở máy bạn rồi gửi lên, xem bước dưới).

Làm việc theo nhánh riêng (khuyến nghị khi thay đổi lớn hoặc cần người khác xem): `git checkout -b feature/ten-ngan`, push nhánh đó, mở Pull Request vào `main`, chờ CI xanh (`backend`, `frontend`, `docker` trong tab Actions) rồi merge. Sau khi merge, `git checkout main && git pull` và chạy `deploy.cmd`. `deploy.cmd` từ chối chạy khi đang ở nhánh khác `main`.

## Cập nhật server (bước deploy)

1. Mở **Docker Desktop** trên máy dev và đợi sẵn sàng.
2. Đảm bảo đang ở nhánh `main` và **mọi thay đổi đã commit** (image build từ thư mục đang làm việc nên script dừng nếu còn file chưa commit).
3. Bấm đúp `deploy\linux\deploy.cmd`, hoặc chạy ở thư mục gốc repo:

   ```powershell
   deploy\linux\deploy.cmd
   ```

   Thứ tự: build → đóng gói → push `main` → gửi image → cập nhật server. Build lỗi thì chưa có gì bị đẩy đi.
4. Chờ dòng `OK: hệ thống đã chạy.` rồi `=== Xong ===`. Thời gian phụ thuộc chủ yếu vào tốc độ tải lên (file image cỡ 150 MB). Thấy `LỖI` / `DUNG` thì xem mục "Lưu ý" bên dưới.

Muốn thử build trước (không đẩy gì lên server):

```powershell
deploy\linux\deploy.cmd build
```

Không cập nhật khi đang có kỳ thi diễn ra: API khởi động lại vài giây (`docs/09-van-hanh.md` mục 5). Trước khi deploy phải tự chạy test (hoặc xem tab Actions trên GitHub), vì script không kiểm tra test.

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
- Cập nhật lỗi: `ssh -p 24700 root@<server> "cd /opt/elearning && docker compose logs --tail=100 api migrate"`.
- Quay lại bản ngay trước đó (không cần build): trên server

  ```bash
  cd /opt/elearning && git reset --hard <commit cũ>
  for n in api web migrator; do docker tag elearning-$n:prev elearning-$n:local; done
  docker compose up -d --no-build   # thêm các file -f docker-compose.prod.yml / override nếu server đang dùng
  ```

  Nếu migration mới không tương thích với bản cũ thì khôi phục từ file `.dump` (`docs/09-van-hanh.md` mục 6).
- Build ngay trên server (dự phòng, khi máy dev không có Docker): `ssh -p 24700 root@<server> "bash /opt/elearning/deploy/linux/update.sh"`. Nếu đứng quá 10 phút ở `migrate bundle` thì dừng và build ở máy dev.
- Dung lượng ổ đĩa: script đã dọn image cũ (giữ tag `prev`) và xóa file image sau khi cập nhật thành công; không dùng `docker system prune --volumes` vì sẽ xóa database.

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
