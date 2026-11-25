#!/usr/bin/env bash
# Chạy TRÊN server Linux: nạp image đã build ở máy dev, sao lưu DB, khởi động lại. KHÔNG build trên server.
# Gọi từ máy dev bằng deploy/linux/deploy.cmd, hoặc chạy tay:
#   bash /opt/elearning/deploy/linux/load-images.sh <commit> [file image .tar]
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/elearning}"
EXPECTED="${1:?Thiếu mã commit đã dùng để build image}"
ARCHIVE="${2:-/root/elearning-images.tar}"
cd "$APP_DIR"

# Chỉ một lần cập nhật tại một thời điểm
exec 9>/tmp/elearning-deploy.lock
flock -n 9 || { echo "LỖI: đang có một lần cập nhật khác chạy." >&2; exit 1; }
if pgrep -f '[u]pdate\.sh' >/dev/null; then
  echo "LỖI: update.sh (build trên server) đang chạy." >&2
  exit 1
fi

[ -f "$ARCHIVE" ] || { echo "LỖI: không thấy file image $ARCHIVE." >&2; exit 1; }
# docker-compose.yml và script giám sát lấy từ mã nguồn trên server: phải cùng commit với image
HEAD_SHA="$(git rev-parse HEAD)"
if [ "$HEAD_SHA" != "$EXPECTED" ]; then
  echo "LỖI: server đang ở commit $HEAD_SHA, image được build từ $EXPECTED." >&2
  exit 1
fi

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
if [ "${IMAGE_TAG:-local}" != "local" ]; then
  echo "LỖI: .env đặt IMAGE_TAG=$IMAGE_TAG, image gửi lên mang tag 'local'." >&2
  exit 1
fi

echo "==> Sao lưu database trước khi cập nhật"
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
if [ -n "$("${COMPOSE[@]}" ps -q postgres)" ]; then
  "${COMPOSE[@]}" exec -T postgres pg_dump -U "${POSTGRES_USER:-postgres}" -d "${DB_NAME:-elearning_db}" -Fc \
    -f "/backups/before-update-$STAMP.dump"
  # Giữ 10 bản gần nhất
  ls -1t "$BACKUP_DIR"/before-update-*.dump 2>/dev/null | tail -n +11 | xargs -r rm -f
else
  echo "!! PostgreSQL chưa chạy (lần cài đầu): bỏ qua sao lưu"
fi

echo "==> Giữ image đang chạy với tag 'prev' để quay lại khi cần"
for pair in api:api web:web migrator:migrate; do
  name="${pair%%:*}"
  container="$("${COMPOSE[@]}" ps -a -q "${pair##*:}" | head -n 1)"
  if [ -n "$container" ]; then
    docker tag "$(docker inspect --format '{{.Image}}' "$container")" "elearning-$name:prev" \
      || echo "!! Không gắn được tag prev cho $name"
  fi
done

echo "==> Nạp image mới"
docker load -i "$ARCHIVE"

echo "==> Khởi động lại (migration chạy ở service migrate)"
"${COMPOSE[@]}" up -d --no-build --remove-orphans

echo "==> Chờ API sẵn sàng"
for i in $(seq 1 40); do
  if curl -fsS "http://localhost:${HTTP_PORT}/health/ready" >/dev/null 2>&1 \
     || curl -fsSk "https://localhost/health/ready" >/dev/null 2>&1; then
    echo "OK: hệ thống đã chạy."
    rm -f "$ARCHIVE"
    docker image prune -f >/dev/null
    exit 0
  fi
  sleep 3
done

echo "LỖI: /health/ready không trả về OK. Xem log: docker compose logs --tail=100 api migrate" >&2
exit 1
