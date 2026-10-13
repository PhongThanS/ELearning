# syntax=docker/dockerfile:1
# Image giám sát (docs/09-van-hanh.md mục 7) có sẵn script, để máy chủ chỉ cần kéo image, không cần mã nguồn.
# Build context: thư mục gốc repo.
#   docker build -f deploy/monitor/Dockerfile .
FROM curlimages/curl:8.14.1
COPY deploy/scripts/monitor.sh deploy/scripts/notify.sh /scripts/
ENTRYPOINT ["/bin/sh", "/scripts/monitor.sh"]
