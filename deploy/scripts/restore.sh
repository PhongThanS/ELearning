#!/usr/bin/env bash
# Khôi phục một chuỗi backup vào database đích (docs/09-van-hanh.md mục 6).
#   deploy/scripts/restore.sh --target ELearningDb_RestoreTest [--replace] full.bak [diff.bak] [log1.trn log2.trn ...]
# - Tên file là tên trong BACKUP_DIR (không kèm đường dẫn).
# - Mọi file trừ file cuối được restore WITH NORECOVERY; file cuối WITH RECOVERY.
# - Không có --replace thì từ chối ghi đè database đang tồn tại.
# Khôi phục đè database đang chạy: dừng API trước (docker compose stop api), chạy với --target ELearningDb --replace --with-media,
# rồi bật lại API và dùng chức năng gia hạn cho các lượt thi bị ảnh hưởng.
# --with-media: chép ảnh câu hỏi còn thiếu từ BACKUP_DIR/media về MEDIA_DIR (chỉ thêm, không ghi đè), rồi đặt lại quyền.
set -euo pipefail

cd "$(dirname "$0")/../.."
set -a
# shellcheck disable=SC1091
source .env
set +a
export MSYS_NO_PATHCONV=1

TARGET=""
REPLACE=""
WITH_MEDIA=""
FILES=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --target) TARGET="$2"; shift 2 ;;
    --replace) REPLACE=", REPLACE"; shift ;;
    --with-media) WITH_MEDIA=1; shift ;;
    *) FILES+=("$1"); shift ;;
  esac
done
if [[ -z "$TARGET" || ${#FILES[@]} -eq 0 ]]; then
  echo "Dùng: $0 --target <db> [--replace] full.bak [diff.bak] [log.trn ...]" >&2
  exit 2
fi

SOURCE_DB="${DB_NAME:-ELearningDb}"
DIR="/var/opt/mssql/backup"
DATA="/var/opt/mssql/data"

sqlcmd() {
  docker compose exec -T -e SQLCMDPASSWORD="$SQL_SA_PASSWORD" sqlserver \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "$1"
}

START=$(date +%s)
LAST=$((${#FILES[@]} - 1))
for i in "${!FILES[@]}"; do
  FILE="${FILES[$i]}"
  RECOVERY=$([[ $i -eq $LAST ]] && echo "RECOVERY" || echo "NORECOVERY")
  if [[ "$FILE" == *.trn ]]; then
    SQL="RESTORE LOG [$TARGET] FROM DISK = N'$DIR/$FILE' WITH $RECOVERY, CHECKSUM"
  elif [[ $i -eq 0 ]]; then
    # File đầu là bản full: đặt file dữ liệu / log theo tên database đích (tên logic do CREATE DATABASE mặc định sinh)
    SQL="RESTORE DATABASE [$TARGET] FROM DISK = N'$DIR/$FILE' WITH $RECOVERY, CHECKSUM$REPLACE,
         MOVE N'$SOURCE_DB' TO N'$DATA/$TARGET.mdf', MOVE N'${SOURCE_DB}_log' TO N'$DATA/${TARGET}_log.ldf'"
  else
    SQL="RESTORE DATABASE [$TARGET] FROM DISK = N'$DIR/$FILE' WITH $RECOVERY, CHECKSUM"
  fi
  echo "restore $FILE ($RECOVERY)"
  sqlcmd "$SQL"
done
if [[ -n "$WITH_MEDIA" ]]; then
  deploy/scripts/media-sync.sh "${BACKUP_DIR:-./backups}/media" "${MEDIA_DIR:-./media}"
  docker compose run --rm --no-deps media-init
fi
echo "Khôi phục xong $TARGET trong $(( $(date +%s) - START )) giây."
