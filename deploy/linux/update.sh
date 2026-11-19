#!/usr/bin/env bash
# Chạy TRÊN server Linux: lấy code mới nhất, sao lưu DB, build lại, kiểm tra sức khỏe.
# Gọi từ máy dev bằng deploy/linux/deploy.cmd, hoặc chạy tay: bash /opt/elearning/deploy/linux/update.sh
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/elearning}"
cd "$APP_DIR"

# Có chứng chỉ trong deploy/certs thì dùng cấu hình HTTPS, không thì HTTP mặc định
COMPOSE=(docker compose -f docker-compose.yml)
if [ -f deploy/certs/fullchain.pem ]; then
  COMPOSE+=(-f docker-compose.prod.yml)
fi

# Override riêng của server (không commit), ví dụ mở cổng DB cho SSH tunnel
if [ -f docker-compose.override.yml ]; then
  COMPOSE+=(-f docker-compose.override.yml)
fi

set -a; . ./.env; set +a
HTTP_PORT="${HTTP_PORT:-8080}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"

echo "==> Sao lưu database trước khi cập nhật"
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
"${COMPOSE[@]}" exec -T postgres pg_dump -U "${POSTGRES_USER:-postgres}" -d "${DB_NAME:-elearning_db}" -Fc \
  -f "/backups/before-update-$STAMP.dump" || echo "!! Không sao lưu được (bỏ qua nếu là lần đầu)"
# Giữ 10 bản gần nhất
ls -1t "$BACKUP_DIR"/before-update-*.dump 2>/dev/null | tail -n +11 | xargs -r rm -f

echo "==> Lấy code mới nhất"
git fetch --prune origin
git reset --hard "origin/$(git rev-parse --abbrev-ref HEAD)"
git log -1 --oneline

echo "==> Build và khởi động lại"
"${COMPOSE[@]}" up -d --build --remove-orphans

echo "==> Chờ API sẵn sàng"
for i in $(seq 1 30); do
  if curl -fsS "http://localhost:${HTTP_PORT}/health/ready" >/dev/null 2>&1 \
     || curl -fsSk "https://localhost/health/ready" >/dev/null 2>&1; then
    echo "OK: hệ thống đã chạy."
    docker image prune -f >/dev/null
    docker builder prune -f --filter "until=72h" >/dev/null
    exit 0
  fi
  sleep 3
done

echo "LỖI: /health/ready không trả về OK. Xem log: docker compose logs --tail=100 api" >&2
exit 1
