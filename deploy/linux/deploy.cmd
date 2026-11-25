@echo off
rem Chạy trên máy Windows của bạn: build image Docker ở máy này, đẩy code lên GitHub,
rem gửi image lên server Linux rồi khởi động lại. Server KHÔNG build.
rem   deploy.cmd          build + triển khai
rem   deploy.cmd build    chỉ build và đóng gói image, không đụng GitHub hay server
rem Cần Docker Desktop đang chạy và khóa SSH đã cài lên server (xem deploy/linux/README.md).
rem Đổi server bằng biến môi trường DEPLOY_SERVER (ví dụ: setx DEPLOY_SERVER root@1.2.3.4), không cần sửa file này.
setlocal
if "%DEPLOY_SERVER%"=="" (set SERVER=root@103.56.162.22) else (set SERVER=%DEPLOY_SERVER%)
if "%DEPLOY_PORT%"=="" (set PORT=24700) else (set PORT=%DEPLOY_PORT%)
set APP_DIR=/opt/elearning
set ARCHIVE=%TEMP%\elearning-images.tar
set REMOTE_ARCHIVE=/root/elearning-images.tar
set BUILD_ONLY=
if /i "%~1"=="build" set BUILD_ONLY=1

cd /d "%~dp0\..\.."

for /f %%b in ('git rev-parse --abbrev-ref HEAD') do set BRANCH=%%b
for /f %%c in ('git rev-parse HEAD') do set COMMIT=%%c
echo === Nhanh: %BRANCH%  Commit: %COMMIT% ===

if defined BUILD_ONLY goto check_docker

if /i not "%BRANCH%"=="main" (
  echo.
  echo DUNG: server chi chay nhanh main. Hay merge nhanh %BRANCH% vao main truoc, roi chay lai tren nhanh main.
  echo Xem deploy/linux/README.md muc "Tu code den server".
  goto failed
)

rem Image build tu thu muc dang lam viec, nen phai sach de image dung voi commit tren server
git status --porcelain | findstr /r "." >nul
if not errorlevel 1 (
  echo.
  echo DUNG: con file chua commit. Commit hoac cat tam bang git stash -u roi chay lai.
  git status --short
  goto failed
)

:check_docker
docker info >nul 2>&1
if errorlevel 1 (
  echo DUNG: Docker Desktop chua chay. Mo Docker Desktop, doi san sang roi chay lai.
  goto failed
)

echo === Build image api ===
docker build --platform linux/amd64 -f backend/Dockerfile --target api -t elearning-api:local .
if errorlevel 1 goto build_failed
echo === Build image migrator ===
docker build --platform linux/amd64 -f backend/Dockerfile --target migrator -t elearning-migrator:local .
if errorlevel 1 goto build_failed
echo === Build image web ===
docker build --platform linux/amd64 -f frontend/elearning-web/Dockerfile -t elearning-web:local .
if errorlevel 1 goto build_failed

echo === Dong goi image ===
docker save -o "%ARCHIVE%" elearning-api:local elearning-migrator:local elearning-web:local
if errorlevel 1 (
  echo Dong goi image that bai.
  goto failed
)
for %%f in ("%ARCHIVE%") do echo File image: %%~ff - %%~zf byte

if defined BUILD_ONLY (
  echo.
  echo === Xong: chi build, chua day gi len server ===
  goto done
)

echo === Day code len GitHub ===
git push origin %BRANCH%
if errorlevel 1 (
  echo Push that bai.
  goto failed
)

echo === Gui image len server ===
scp -C -P %PORT% -o StrictHostKeyChecking=accept-new "%ARCHIVE%" %SERVER%:%REMOTE_ARCHIVE%
if errorlevel 1 (
  echo Gui image that bai.
  goto failed
)

echo === Cap nhat server ===
ssh -p %PORT% -o StrictHostKeyChecking=accept-new %SERVER% "cd %APP_DIR% && git fetch --prune origin && git reset --hard origin/%BRANCH% && bash deploy/linux/load-images.sh %COMMIT% %REMOTE_ARCHIVE%"
if errorlevel 1 (
  echo Cap nhat server that bai.
  goto failed
)
del "%ARCHIVE%"

echo.
echo === Xong ===

:done
pause
exit /b 0

:build_failed
echo Build that bai: chua day gi len GitHub hay server.

:failed
pause
exit /b 1
