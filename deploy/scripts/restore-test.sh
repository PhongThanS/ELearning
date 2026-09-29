#!/usr/bin/env bash
# Thử khôi phục định kỳ (mỗi tháng, docs/09-van-hanh.md mục 6): lấy bản full mới nhất, bản diff mới nhất sau nó
# và mọi log sau đó, khôi phục vào <DB>_RestoreTest, kiểm tra dữ liệu, ghi lại thời gian, rồi xóa database thử.
#   deploy/scripts/restore-test.sh [--keep]
set -euo pipefail

cd "$(dirname "$0")/../.."
set -a
# shellcheck disable=SC1091
source .env
set +a
export MSYS_NO_PATHCONV=1

DB="${DB_NAME:-ELearningDb}"
TARGET="${DB}_RestoreTest"
HOST_DIR="${BACKUP_DIR:-./backups}"
KEEP="${1:-}"

# Tên file có mốc thời gian UTC dạng 20260929T010000Z nên sắp xếp theo tên là theo thời gian
FULL=$(ls "$HOST_DIR" | grep -E "^${DB}_full_.*\.bak$" | sort | tail -n 1 || true)
if [[ -z "$FULL" ]]; then
  echo "Chưa có bản backup full nào trong $HOST_DIR" >&2
  exit 1
fi
stamp() { sed -E 's/^.*_([0-9]{8}T[0-9]{6}Z)\..*$/\1/' <<<"$1"; }
BASE_TS=$(stamp "$FULL")
DIFF=$(ls "$HOST_DIR" | grep -E "^${DB}_diff_.*\.bak$" | sort | awk -v ts="$BASE_TS" -F'_' '{ s=$NF; sub(/\..*/, "", s); if (s > ts) print }' | tail -n 1 || true)
[[ -n "$DIFF" ]] && BASE_TS=$(stamp "$DIFF")
mapfile -t LOGS < <(ls "$HOST_DIR" | grep -E "^${DB}_log_.*\.trn$" | sort | awk -v ts="$BASE_TS" -F'_' '{ s=$NF; sub(/\..*/, "", s); if (s > ts) print }')

CHAIN=("$FULL")
[[ -n "$DIFF" ]] && CHAIN+=("$DIFF")
CHAIN+=("${LOGS[@]}")
echo "Chuỗi khôi phục: ${CHAIN[*]}"

sqlcmd() {
  docker compose exec -T -e SQLCMDPASSWORD="$SQL_SA_PASSWORD" sqlserver \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -W -Q "$1"
}

sqlcmd "IF DB_ID(N'$TARGET') IS NOT NULL BEGIN ALTER DATABASE [$TARGET] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TARGET]; END"
START=$(date +%s)
deploy/scripts/restore.sh --target "$TARGET" "${CHAIN[@]}"
ELAPSED=$(( $(date +%s) - START ))

echo "Kiểm tra dữ liệu (bản gốc | bản khôi phục):"
sqlcmd "SET NOCOUNT ON;
SELECT 'Users' AS [Bang], (SELECT COUNT(*) FROM [$DB].dbo.Users) AS [Goc], (SELECT COUNT(*) FROM [$TARGET].dbo.Users) AS [KhoiPhuc]
UNION ALL SELECT 'Questions', (SELECT COUNT(*) FROM [$DB].dbo.Questions), (SELECT COUNT(*) FROM [$TARGET].dbo.Questions)
UNION ALL SELECT 'ExamAttempts', (SELECT COUNT(*) FROM [$DB].dbo.ExamAttempts), (SELECT COUNT(*) FROM [$TARGET].dbo.ExamAttempts)
UNION ALL SELECT 'ExamResults', (SELECT COUNT(*) FROM [$DB].dbo.ExamResults), (SELECT COUNT(*) FROM [$TARGET].dbo.ExamResults)
UNION ALL SELECT 'Migrations', (SELECT COUNT(*) FROM [$DB].dbo.__EFMigrationsHistory), (SELECT COUNT(*) FROM [$TARGET].dbo.__EFMigrationsHistory);
DBCC CHECKDB (N'$TARGET') WITH NO_INFOMSGS;"

echo "RESTORE TEST OK: $TARGET khôi phục trong ${ELAPSED} giây ($(date -u +%FT%TZ)). Ghi kết quả này vào sổ vận hành."
if [[ "$KEEP" != "--keep" ]]; then
  sqlcmd "ALTER DATABASE [$TARGET] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TARGET];"
fi
