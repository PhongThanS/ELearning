@echo off
rem Chạy trên máy Windows của bạn: đẩy code lên GitHub rồi bảo server Linux cập nhật.
rem Cần đã cài khóa SSH lên server (xem deploy/linux/README.md).
rem Đổi server bằng biến môi trường DEPLOY_SERVER (ví dụ: setx DEPLOY_SERVER root@1.2.3.4), không cần sửa file này.
setlocal
if "%DEPLOY_SERVER%"=="" (set SERVER=root@103.56.162.22) else (set SERVER=%DEPLOY_SERVER%)
set REMOTE_SCRIPT=/opt/elearning/deploy/linux/update.sh

cd /d "%~dp0\..\.."

for /f %%b in ('git rev-parse --abbrev-ref HEAD') do set BRANCH=%%b
echo === Nhanh hien tai: %BRANCH% ===

if /i not "%BRANCH%"=="main" (
  echo.
  echo DUNG: server chi chay nhanh main. Hay merge nhanh %BRANCH% vao main truoc, roi chay lai tren nhanh main.
  echo Xem deploy/linux/README.md muc "Tu code den server".
  pause
  exit /b 1
)

git status --porcelain | findstr /r "." >nul
if %errorlevel%==0 (
  echo.
  echo CANH BAO: con file chua commit. Chi code da commit moi duoc dua len server.
  git status --short
  echo.
  choice /m "Van tiep tuc"
  if errorlevel 2 exit /b 1
)

echo === Day code len GitHub ===
git push origin %BRANCH%
if errorlevel 1 (
  echo Push that bai.
  pause
  exit /b 1
)

echo === Cap nhat server ===
ssh %SERVER% "bash %REMOTE_SCRIPT%"
if errorlevel 1 (
  echo Cap nhat server that bai.
  pause
  exit /b 1
)

echo.
echo === Xong ===
pause
