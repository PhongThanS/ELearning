@echo off
rem Chạy MỘT LẦN trên máy Windows của bạn: tạo khóa SSH và cài lên server.
rem Sau đó deploy.cmd tự đăng nhập, không hỏi mật khẩu nữa.
rem Bạn chỉ phải nhập mật khẩu root của server một lần, ở bước cài khóa.
setlocal
if "%DEPLOY_SERVER%"=="" (set SERVER=root@103.56.162.22) else (set SERVER=%DEPLOY_SERVER%)
if "%DEPLOY_PORT%"=="" (set PORT=24700) else (set PORT=%DEPLOY_PORT%)
set KEY=%USERPROFILE%\.ssh\id_ed25519

if not exist "%USERPROFILE%\.ssh" mkdir "%USERPROFILE%\.ssh"

if exist "%KEY%" (
  echo Da co khoa SSH: %KEY%
) else (
  echo === Tao khoa SSH moi ===
  ssh-keygen -t ed25519 -N "" -f "%KEY%"
  if errorlevel 1 (
    echo Khong tao duoc khoa.
    pause
    exit /b 1
  )
)

echo.
echo === Cai khoa len server %SERVER% (cong %PORT%) ===
echo Nhap mat khau root cua server khi duoc hoi (go khong hien ky tu).
type "%KEY%.pub" | ssh -p %PORT% -o StrictHostKeyChecking=accept-new %SERVER% "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys"
if errorlevel 1 (
  echo Cai khoa that bai. Kiem tra mat khau, cong va ket noi mang.
  pause
  exit /b 1
)

echo.
echo === Kiem tra dang nhap khong can mat khau ===
ssh -p %PORT% -o BatchMode=yes %SERVER% echo OK_KHONG_CAN_MAT_KHAU
if errorlevel 1 (
  echo Van bi hoi mat khau. Chup man hinh loi gui nguoi quan tri.
  pause
  exit /b 1
)

echo.
echo === Xong. Tu nay bam deploy.cmd la len server, khong hoi mat khau. ===
pause
