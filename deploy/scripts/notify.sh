#!/bin/sh
# Gửi một cảnh báo tới webhook (Slack, Mattermost, Google Chat, Teams…) — docs/09-van-hanh.md mục 7.
#   deploy/scripts/notify.sh "Backup log thất bại"
# ALERT_WEBHOOK_URL rỗng: chỉ ghi ra stderr. ALERT_WEBHOOK_FIELD: tên trường văn bản (mặc định "text"; Discord dùng "content").
# Viết cho POSIX sh để chạy được cả trên máy chủ lẫn trong container monitor (curlimages/curl).
set -eu

message="[ELearning${ALERT_ENV_NAME:+ $ALERT_ENV_NAME}] $*"
echo "$(date -u +%FT%TZ) ALERT $message" >&2

if [ -z "${ALERT_WEBHOOK_URL:-}" ]; then
  exit 0
fi

# Thoát ký tự đặc biệt cho chuỗi JSON (\ và "; bỏ ký tự điều khiển)
escaped=$(printf '%s' "$message" | tr -d '\000-\010\013-\037' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' | awk 'BEGIN{ORS="\\n"} {print}' | sed 's/\\n$//')
field="${ALERT_WEBHOOK_FIELD:-text}"

if ! curl -fsS -m 10 -H 'Content-Type: application/json' -d "{\"$field\":\"$escaped\"}" "$ALERT_WEBHOOK_URL" > /dev/null; then
  echo "$(date -u +%FT%TZ) không gửi được cảnh báo tới webhook" >&2
  exit 1
fi
