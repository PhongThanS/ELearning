# syntax=docker/dockerfile:1
# Build context: thư mục gốc repo (cần .editorconfig vì build bật warnings-as-errors).
#   docker build -f backend/Dockerfile --target api .
#   docker build -f backend/Dockerfile --target migrator .

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore trước để cache layer khi chỉ đổi mã nguồn
COPY .editorconfig ./
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/dotnet-tools.json backend/
COPY backend/src/ELearning.Api/ELearning.Api.csproj backend/src/ELearning.Api/
COPY backend/src/ELearning.Application/ELearning.Application.csproj backend/src/ELearning.Application/
COPY backend/src/ELearning.Domain/ELearning.Domain.csproj backend/src/ELearning.Domain/
COPY backend/src/ELearning.Infrastructure/ELearning.Infrastructure.csproj backend/src/ELearning.Infrastructure/
COPY backend/src/ELearning.Shared/ELearning.Shared.csproj backend/src/ELearning.Shared/
RUN dotnet restore backend/src/ELearning.Api/ELearning.Api.csproj
RUN cd backend && dotnet tool restore

COPY backend/src backend/src
RUN dotnet publish backend/src/ELearning.Api/ELearning.Api.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

# Migration bundle (docs/09-van-hanh.md mục 4): áp migration bằng bước riêng, không lúc API khởi động
FROM build AS bundle
RUN cd backend && dotnet ef migrations bundle \
      -p src/ELearning.Infrastructure -s src/ELearning.Api --configuration Release \
      -o /app/efbundle --force

# ---------- API ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1
COPY --from=build /app/publish .
# Thư mục ảnh câu hỏi (D-27): docker-compose gắn MEDIA_DIR vào đây; service media-init đặt quyền cho APP_UID
ENV Media__RootPath=/var/lib/elearning/media
RUN mkdir -p /var/lib/elearning/media && chown "$APP_UID" /var/lib/elearning/media
# Người dùng không phải root có sẵn trong image .NET
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ELearning.Api.dll"]

# ---------- Migration ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle .
USER $APP_UID
# Connection string truyền qua --connection (xem docker-compose.yml)
ENTRYPOINT ["./efbundle"]
