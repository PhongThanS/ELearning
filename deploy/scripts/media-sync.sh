#!/usr/bin/env bash
# Chép file ảnh câu hỏi còn thiếu từ thư mục nguồn sang thư mục đích (D-27, docs/09-van-hanh.md mục 6).
#   deploy/scripts/media-sync.sh <nguồn> <đích>
# Ảnh bất biến, tên theo SHA-256, không bao giờ bị xóa, nên chỉ cần chép file chưa có: sao lưu tăng dần,
# chạy lại bao nhiêu lần cũng được, và khôi phục chỉ thêm file, không ghi đè. Bỏ qua file tạm đang ghi (.upload-*).
set -euo pipefail

SRC="${1:?Dùng: $0 <nguồn> <đích>}"
DST="${2:?Dùng: $0 <nguồn> <đích>}"
if [[ ! -d "$SRC" ]]; then
  echo "media-sync: chưa có thư mục $SRC, bỏ qua"
  exit 0
fi

copied=0
while IFS= read -r -d '' file; do
  rel="${file#"$SRC"/}"
  if [[ ! -e "$DST/$rel" ]]; then
    mkdir -p "$DST/$(dirname "$rel")"
    cp -p "$file" "$DST/$rel.partial"
    mv "$DST/$rel.partial" "$DST/$rel"
    copied=$((copied + 1))
  fi
done < <(find "$SRC" -mindepth 2 -maxdepth 2 -type f ! -name '.upload-*' ! -name '*.partial' -print0)
echo "media-sync: chép $copied ảnh mới từ $SRC sang $DST"
