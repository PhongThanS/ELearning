#!/usr/bin/env bash
# Backup SQL Server trong docker compose (docs/09-van-hanh.md mục 6).
#   deploy/scripts/backup.sh full   # hằng ngày 01:00
#   deploy/scripts/backup.sh diff   # mỗi 6 giờ
#   deploy/scripts/backup.sh log    # mỗi 15 phút (5 phút trong ngày thi lớn)
# File ghi vào BACKUP_DIR (mặc định ./backups), kiểm tra bằng RESTORE VERIFYONLY, xóa bản quá hạn giữ lại.
# Ví dụ crontab (giờ máy chủ = giờ VN):
#   0 1 * * *    cd /opt/elearning && deploy/scripts/backup.sh full >> backups/backup.log 2>&1
#   0 */6 * * *  cd /opt/elearning && deploy/scripts/backup.sh diff >> backups/backup.log 2>&1
#   */15 * * * * cd /opt/elearning && deploy/scripts/backup.sh log  >> backups/backup.log 2>&1
set -euo pipefail

cd "$(dirname "$0")/../.."
set -a
# shellcheck disable=SC1091
source .env
set +a
export MSYS_NO_PATHCONV=1 # Git Bash trên Windows: không đổi đường dẫn /var/opt/...

TYPE="${1:-full}"
DB="${DB_NAME:-ELearningDb}"
HOST_DIR="${BACKUP_DIR:-./backups}"
CONTAINER_DIR="/var/opt/mssql/backup"
TS="$(date -u +%Y%m%dT%H%M%SZ)"

case "$TYPE" in
  full) FILE="${DB}_full_${TS}.bak"; SQL="BACKUP DATABASE [$DB] TO DISK = N'$CONTAINER_DIR/$FILE' WITH CHECKSUM, INIT, NAME = N'$DB full $TS'"; KEEP_DAYS=30 ;;
  diff) FILE="${DB}_diff_${TS}.bak"; SQL="BACKUP DATABASE [$DB] TO DISK = N'$CONTAINER_DIR/$FILE' WITH DIFFERENTIAL, CHECKSUM, INIT, NAME = N'$DB diff $TS'"; KEEP_DAYS=7 ;;
  log)  FILE="${DB}_log_${TS}.trn";  SQL="BACKUP LOG [$DB] TO DISK = N'$CONTAINER_DIR/$FILE' WITH CHECKSUM, INIT, NAME = N'$DB log $TS'"; KEEP_DAYS=7 ;;
  *) echo "Dùng: $0 full|diff|log" >&2; exit 2 ;;
esac

sqlcmd() {
  # Mật khẩu qua biến môi trường SQLCMDPASSWORD, không nằm trên dòng lệnh
  docker compose exec -T -e SQLCMDPASSWORD="$SQL_SA_PASSWORD" sqlserver \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "$1"
}

echo "[$(date -u +%FT%TZ)] backup $TYPE → $FILE"
sqlcmd "$SQL"
sqlcmd "RESTORE VERIFYONLY FROM DISK = N'$CONTAINER_DIR/$FILE' WITH CHECKSUM"

# Giữ lại theo chính sách: full 30 ngày, diff / log 7 ngày
find "$HOST_DIR" -maxdepth 1 -type f -name "${DB}_${TYPE}_*" -mtime +"$KEEP_DAYS" -print -delete
echo "[$(date -u +%FT%TZ)] xong: $HOST_DIR/$FILE"
# Sau bước này, đồng bộ HOST_DIR ra nơi lưu trữ khác (offsite) và mã hóa ở đó (xem docs/09-van-hanh.md mục 6).
