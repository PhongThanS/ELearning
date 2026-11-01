<#
  Cập nhật EClass trên máy chủ Windows bằng một lệnh (deploy/server/README.md mục 5).
  Đặt file này cạnh docker-compose.yml và .env (ví dụ C:\elearning), rồi chạy:
      powershell -ExecutionPolicy Bypass -File C:\elearning\update.ps1
  hoặc bấm đúp update.cmd.

  Các bước: bật Docker nếu đang tắt -> sao lưu database -> tải docker-compose.yml mới nhất -> kéo image mới ->
  khởi động lại (database tự cập nhật qua "migrate") -> kiểm tra web / API -> dọn image cũ -> tự cập nhật script.
  Không đụng tới .env (mật khẩu, token) và dữ liệu. Có bước nào lỗi thì dừng và báo, không làm tiếp.

  Tham số:
    -SkipBackup   bỏ qua sao lưu database (không khuyến nghị)
    -KeepCompose  giữ nguyên docker-compose.yml đang có, không tải bản mới
#>
param(
    [switch]$SkipBackup,
    [switch]$KeepCompose
)

# Không dùng "Stop": PowerShell 5.1 coi mọi dòng docker ghi ra stderr (tiến trình pull…) là lỗi.
# Lỗi được kiểm tra tường minh qua $LASTEXITCODE (lệnh docker) và try/catch (-ErrorAction Stop).
$ErrorActionPreference = "Continue"
$Repo = "https://raw.githubusercontent.com/PhongThanS/ELearning/main/deploy/server"
$Dir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Dir

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
New-Item -ItemType Directory -Force (Join-Path $Dir "logs") | Out-Null
Start-Transcript -Path (Join-Path $Dir "logs\update-$stamp.log") | Out-Null

function Step($text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }
function Ok($text) { Write-Host "    [OK] $text" -ForegroundColor Green }
function Warn($text) { Write-Host "    [!] $text" -ForegroundColor Yellow }
function Fail($text) {
    Write-Host ""
    Write-Host "[LỖI] $text" -ForegroundColor Red
    Write-Host "Nhật ký: $Dir\logs\update-$stamp.log"
    Stop-Transcript | Out-Null
    exit 1
}

# Gọi lệnh ngoài (docker…), dừng nếu mã thoát khác 0
function Run([string]$what, [scriptblock]$cmd) {
    & $cmd
    if ($LASTEXITCODE -ne 0) { Fail "$what (mã lỗi $LASTEXITCODE)" }
}

# Đọc giá trị trong .env (bỏ dòng chú thích); trả $default nếu không có
function EnvValue([string]$name, [string]$default) {
    $line = Get-Content (Join-Path $Dir ".env") -Encoding UTF8 |
        Where-Object { $_ -match "^\s*$name\s*=" } | Select-Object -Last 1
    if (-not $line) { return $default }
    $value = ($line -split "=", 2)[1].Trim().Trim('"')
    if ([string]::IsNullOrWhiteSpace($value)) { return $default }
    return $value
}

Write-Host "Cập nhật EClass — $Dir — $(Get-Date -Format 'dd/MM/yyyy HH:mm')" -ForegroundColor White

# ---------------------------------------------------------------- 0. Kiểm tra thư mục
Step "Kiểm tra thư mục cài đặt"
if (-not (Test-Path (Join-Path $Dir ".env"))) { Fail "Không thấy file .env trong $Dir. Đặt update.ps1 cạnh .env và docker-compose.yml." }
if (-not (Test-Path (Join-Path $Dir "docker-compose.yml"))) { Fail "Không thấy docker-compose.yml trong $Dir." }
Ok "Có .env và docker-compose.yml"

# ---------------------------------------------------------------- 1. Docker
Step "Kiểm tra Docker"
docker info --format "{{.ServerVersion}}" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    $desktop = Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe"
    if (-not (Test-Path $desktop)) { Fail "Docker chưa chạy và không tìm thấy Docker Desktop." }
    Warn "Docker đang tắt, đang bật Docker Desktop (tối đa 3 phút)…"
    Start-Process $desktop
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 3
        docker info --format "{{.ServerVersion}}" 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    }
    if (-not $ready) { Fail "Docker Desktop không khởi động được. Mở Docker Desktop bằng tay rồi chạy lại." }
}
Ok "Docker đang chạy"

# ---------------------------------------------------------------- 2. Sao lưu database
$dbUser = EnvValue "POSTGRES_USER" "postgres"
$dbName = EnvValue "DB_NAME" "elearning_db"
$backupDir = EnvValue "BACKUP_DIR" "./backups"
if ($SkipBackup) {
    Step "Sao lưu database"
    Warn "Bỏ qua theo tham số -SkipBackup"
} else {
    Step "Sao lưu database trước khi cập nhật"
    $running = docker compose ps --status running --services 2>$null
    if ($running -contains "postgres") {
        $file = "before-update-$stamp.dump"
        # Thư mục BACKUP_DIR của máy chủ được gắn vào /backups trong container postgres
        Run "Sao lưu database" { docker compose exec -T postgres pg_dump -U $dbUser -d $dbName -Fc -f "/backups/$file" }
        $hostDir = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($backupDir)) { $backupDir } else { Join-Path $Dir $backupDir }))
        $saved = Join-Path $hostDir $file
        if (Test-Path $saved) {
            Ok ("Đã sao lưu: {0} ({1:N1} MB)" -f $saved, ((Get-Item $saved).Length / 1MB))
            # Giữ 10 bản sao lưu trước-cập-nhật gần nhất
            Get-ChildItem $hostDir -Filter "before-update-*.dump" | Sort-Object LastWriteTime -Descending |
                Select-Object -Skip 10 | Remove-Item -Force
        } else {
            Warn "Lệnh sao lưu chạy xong nhưng không thấy file $saved — kiểm tra BACKUP_DIR trong .env"
        }
    } else {
        Warn "Database chưa chạy (lần cài đầu?) — bỏ qua sao lưu"
    }
}

# ---------------------------------------------------------------- 3. docker-compose.yml mới nhất
Step "Tải cấu hình docker-compose.yml mới nhất"
if ($KeepCompose) {
    Warn "Giữ nguyên docker-compose.yml theo tham số -KeepCompose"
} else {
    $current = Join-Path $Dir "docker-compose.yml"
    $incoming = Join-Path $Dir "docker-compose.yml.new"
    try {
        Invoke-WebRequest "$Repo/docker-compose.yml" -OutFile $incoming -UseBasicParsing -ErrorAction Stop
    } catch {
        Fail "Không tải được docker-compose.yml từ GitHub: $($_.Exception.Message)"
    }
    if ((Get-FileHash $incoming).Hash -eq (Get-FileHash $current).Hash) {
        Remove-Item $incoming
        Ok "Không đổi"
    } else {
        docker compose -f $incoming config --quiet 2>$null
        if ($LASTEXITCODE -ne 0) {
            Remove-Item $incoming
            Fail "docker-compose.yml mới không hợp lệ với .env hiện tại — giữ bản cũ. Chạy lại với -KeepCompose để cập nhật image."
        }
        Copy-Item $current "$current.bak-$stamp" -ErrorAction Stop
        Move-Item $incoming $current -Force -ErrorAction Stop
        Ok "Đã cập nhật (bản cũ: docker-compose.yml.bak-$stamp)"
    }
}

# ---------------------------------------------------------------- 4. Image mới + khởi động lại
Step "Tải image mới (có thể mất vài phút)"
Run "Tải image" { docker compose pull --quiet }
Ok "Đã tải xong"

Step "Khởi động lại (database tự cập nhật trước khi API chạy)"
Run "Khởi động" { docker compose up -d --remove-orphans }

# migrate / seed chạy một lần rồi thoát: phải thoát với mã 0
foreach ($svc in "migrate", "seed") {
    $exit = $null
    for ($i = 0; $i -lt 60; $i++) {
        $status = docker compose ps -a --format "{{.Service}}|{{.State}}|{{.ExitCode}}" 2>$null |
            Where-Object { $_ -like "$svc|*" } | Select-Object -First 1
        if ($status) {
            $parts = $status -split "\|"
            if ($parts[1] -eq "exited") { $exit = [int]$parts[2]; break }
        }
        Start-Sleep -Seconds 2
    }
    if ($null -eq $exit) { Fail "$svc chưa chạy xong sau 2 phút. Xem: docker compose logs $svc" }
    if ($exit -ne 0) { Fail "$svc lỗi (mã $exit). Xem: docker compose logs $svc --tail 50" }
    Ok "$svc xong"
}

# ---------------------------------------------------------------- 5. Kiểm tra
Step "Kiểm tra web và API"
$port = EnvValue "HTTP_PORT" "8080"
$local = if ($port -eq "80") { "http://localhost" } else { "http://localhost:$port" }
$healthy = $false
for ($i = 0; $i -lt 40; $i++) {
    try {
        $r = Invoke-WebRequest "$local/health/ready" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        if ($r.StatusCode -eq 200) { $healthy = $true; break }
    } catch { }
    Start-Sleep -Seconds 3
}
if (-not $healthy) { Fail "API chưa sẵn sàng sau 2 phút ($local/health/ready). Xem: docker compose logs api --tail 50" }
Ok "API sẵn sàng"
try {
    $page = Invoke-WebRequest "$local/" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    Ok "Web trả về $($page.StatusCode)"
} catch { Fail "Web không trả lời tại $local/ : $($_.Exception.Message)" }

$services = docker compose ps --services --status running 2>$null
if ((EnvValue "COMPOSE_PROFILES" "") -match "tunnel") {
    if ($services -contains "tunnel") { Ok "Cloudflare Tunnel đang chạy" } else { Warn "Cloudflare Tunnel KHÔNG chạy — xem: docker compose logs tunnel --tail 20" }
}

# Lấy nhãn dạng JSON rồi đọc trong PowerShell: PowerShell 5.1 làm hỏng dấu " khi truyền --format cho docker
$labels = docker image inspect "$(EnvValue 'IMAGE_REGISTRY' 'ghcr.io/phongthans')/elearning-api:$(EnvValue 'IMAGE_TAG' 'latest')" --format "{{json .Config.Labels}}" 2>$null
if ($LASTEXITCODE -eq 0 -and $labels) {
    $revision = ($labels | ConvertFrom-Json)."org.opencontainers.image.revision"
    if ($revision) { Ok ("Phiên bản: {0}" -f $revision.Substring(0, [Math]::Min(7, $revision.Length))) }
}

# ---------------------------------------------------------------- 6. Dọn dẹp + tự cập nhật script
Step "Dọn image cũ không còn dùng"
docker image prune -f 2>$null | Out-Null
Ok "Xong"

try {
    $selfNew = Join-Path $Dir "update.ps1.new"
    Invoke-WebRequest "$Repo/update.ps1" -OutFile $selfNew -UseBasicParsing -ErrorAction Stop
    if ((Get-FileHash $selfNew).Hash -ne (Get-FileHash $MyInvocation.MyCommand.Path).Hash) {
        Move-Item $selfNew $MyInvocation.MyCommand.Path -Force
        Ok "Script update.ps1 đã được cập nhật cho lần chạy sau"
    } else {
        Remove-Item $selfNew
    }
} catch {
    Warn "Không tải được bản mới của update.ps1 (không ảnh hưởng lần cập nhật này)"
}

Write-Host ""
Write-Host "HOÀN TẤT. Web: $(EnvValue 'PUBLIC_ORIGIN' $local)" -ForegroundColor Green
Write-Host "Người dùng đang mở web: nhấn Ctrl+F5 nếu giao diện chưa đổi."
Stop-Transcript | Out-Null
