# syntax=docker/dockerfile:1
# Build context: thư mục gốc repo.
#   docker build -f frontend/elearning-web/Dockerfile .

FROM node:24-alpine AS build
WORKDIR /app
COPY frontend/elearning-web/package.json frontend/elearning-web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/elearning-web/ ./
RUN npm run build

# Nginx phục vụ file tĩnh và proxy /api, /health sang API (docs/09-van-hanh.md mục 5)
FROM nginx:1.29-alpine
RUN rm /etc/nginx/conf.d/default.conf
COPY deploy/nginx/snippets/ /etc/nginx/snippets/
COPY deploy/nginx/conf.d/ /etc/nginx/templates/
COPY deploy/nginx/errors/ /usr/share/nginx/errors/
COPY --from=build /app/dist /usr/share/nginx/html
# Image nginx chính thức render /etc/nginx/templates/*.template bằng envsubst lúc khởi động
ENV API_UPSTREAM=api:8080
EXPOSE 80 443
