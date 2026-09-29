#!/bin/sh
# Giám sát tối thiểu (docs/09-van-hanh.md mục 7). Chạy trong container "monitor" của docker-compose.yml,
# mỗi MONITOR_INTERVAL_SECONDS giây kiểm tra:
#   api-ready          /health/ready của API (kết nối SQL Server)
#   error-rate         /health/alerts: tỉ lệ 5xx > 1% trong 5 phút
#   attempt-backlog    /health/alerts: có lượt thi quá hạn > 5 phút chưa được nộp
#   expiration-worker  /health/alerts: job tự nộp ngừng quét
#   web                Nginx trả trang chủ
#   backup-full / backup-log   có file backup đủ mới (khi đặt BACKUP_FULL_MAX_AGE_HOURS / BACKUP_LOG_MAX_AGE_MINUTES)
# Gửi webhook (notify.sh) khi một mục chuyển sang lỗi, nhắc lại mỗi MONITOR_REPEAT_MINUTES khi vẫn lỗi, và khi hết lỗi.
# MONITOR_ONCE=1: kiểm tra một lần, thoát 1 nếu có mục lỗi (dùng trong CI hoặc cron).
set -u

API_URL="${MONITOR_API_URL:-http://api:8080}"
WEB_URL="${MONITOR_WEB_URL:-http://web}"
INTERVAL="${MONITOR_INTERVAL_SECONDS:-60}"
REPEAT_MINUTES="${MONITOR_REPEAT_MINUTES:-30}"
BACKUP_DIR="${MONITOR_BACKUP_DIR:-/backups}"
DB="${DB_NAME:-ELearningDb}"
FULL_MAX_HOURS="${BACKUP_FULL_MAX_AGE_HOURS:-}"
LOG_MAX_MINUTES="${BACKUP_LOG_MAX_AGE_MINUTES:-}"
NOTIFY="$(dirname "$0")/notify.sh"
STATE_DIR="${MONITOR_STATE_DIR:-/tmp/elearning-monitor}"
mkdir -p "$STATE_DIR"

failures=0

# report <mục> <mô tả lỗi | rỗng nếu ổn>
report() {
  name=$1
  problem=$2
  file="$STATE_DIR/$name"
  previous=$(cat "$file" 2>/dev/null || echo ok)
  now=$(date +%s)
  if [ -z "$problem" ]; then
    if [ "$previous" != ok ]; then
      sh "$NOTIFY" "ĐÃ ỔN: $name" || true
    fi
    echo ok > "$file"
    return
  fi

  failures=$((failures + 1))
  echo "$(date -u +%FT%TZ) $name: $problem"
  case "$previous" in
    ok)
      sh "$NOTIFY" "LỖI: $name — $problem" || true
      echo "fail $now" > "$file"
      ;;
    fail*)
      if [ $((now - ${previous#fail })) -ge $((REPEAT_MINUTES * 60)) ]; then
        sh "$NOTIFY" "VẪN LỖI: $name — $problem" || true
        echo "fail $now" > "$file"
      fi
      ;;
  esac
}

# Trạng thái của một check trong JSON /health/alerts (writer giữ thứ tự name → status)
alert_status() {
  printf '%s' "$2" | grep -o "\"name\":\"$1\",\"status\":\"[A-Za-z]*\"" | sed 's/.*"status":"\([A-Za-z]*\)"/\1/'
}

check_alert() {
  name=$1
  message=$2
  status=$(alert_status "$name" "$alerts")
  case "$status" in
    Healthy) report "$name" "" ;;
    "") report "$name" "không đọc được /health/alerts" ;;
    *) report "$name" "$message" ;;
  esac
}

# Có file <DB>_<loại>_* mới hơn N phút trong thư mục backup
check_backup() {
  name=$1
  pattern=$2
  minutes=$3
  if [ -z "$minutes" ]; then
    return
  fi
  if [ -n "$(find "$BACKUP_DIR" -maxdepth 1 -type f -name "$pattern" -mmin "-$minutes" 2>/dev/null | head -n 1)" ]; then
    report "$name" ""
  else
    report "$name" "không có file $pattern mới trong $minutes phút ở $BACKUP_DIR (cron backup không chạy hoặc lỗi)"
  fi
}

run_checks() {
  failures=0
  if curl -fsS -m 5 -o /dev/null "$API_URL/health/ready"; then
    report api-ready ""
  else
    report api-ready "API không sẵn sàng hoặc không kết nối được SQL Server ($API_URL/health/ready)"
  fi

  # 503 khi có cảnh báo, nên không dùng -f
  alerts=$(curl -sS -m 10 "$API_URL/health/alerts" 2>/dev/null || true)
  check_alert error-rate "tỉ lệ lỗi 5xx vượt ngưỡng (mặc định 1% trong 5 phút)"
  check_alert attempt-backlog "có lượt thi quá hạn hơn 5 phút chưa được nộp — kiểm tra job tự nộp và log API"
  check_alert expiration-worker "job tự nộp (AttemptExpirationWorker) đã ngừng quét"

  if curl -fsS -m 5 -o /dev/null "$WEB_URL/"; then
    report web ""
  else
    report web "Nginx không phản hồi ($WEB_URL)"
  fi

  if [ -n "$FULL_MAX_HOURS" ]; then
    check_backup backup-full "${DB}_full_*.bak" $((FULL_MAX_HOURS * 60))
  fi
  check_backup backup-log "${DB}_log_*.trn" "$LOG_MAX_MINUTES"
}

if [ "${MONITOR_ONCE:-}" = 1 ]; then
  run_checks
  echo "$(date -u +%FT%TZ) monitor: $failures mục lỗi"
  [ "$failures" -eq 0 ]
  exit
fi

echo "$(date -u +%FT%TZ) monitor: kiểm tra $API_URL và $WEB_URL mỗi $INTERVAL giây"
while true; do
  run_checks
  sleep "$INTERVAL"
done
