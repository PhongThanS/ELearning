# Đăng ký lịch backup trên máy Windows chạy Docker Desktop (tương đương crontab trong deploy/scripts/backup.sh):
#   full  hằng ngày 01:00 · diff mỗi 6 giờ · log mỗi 15 phút
# Task chạy dưới tài khoản người dùng hiện tại khi đã đăng nhập (Docker Desktop cũng chạy trong phiên đăng nhập),
# chạy bù nếu lỡ lịch, ẩn cửa sổ. Log: backups\backup.log.
#   powershell -ExecutionPolicy Bypass -File deploy\scripts\windows\register-backup-tasks.ps1
# Gỡ:
#   Get-ScheduledTask -TaskPath '\ELearning\' | Unregister-ScheduledTask -Confirm:$false
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
)

$ErrorActionPreference = 'Stop'
$vbs = Join-Path $PSScriptRoot 'run-backup.vbs'
$taskPath = '\ELearning\'
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 30) -MultipleInstances IgnoreNew
$forever = New-TimeSpan -Days 3650

function Register-Backup([string]$name, [string]$type, $trigger, [string]$description) {
    $action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument "`"$vbs`" $type" -WorkingDirectory $Root
    Register-ScheduledTask -TaskPath $taskPath -TaskName $name -Action $action -Trigger $trigger `
        -Settings $settings -Description $description -Force | Out-Null
    Write-Host "Đã đăng ký $taskPath$name"
}

$today = (Get-Date).Date
Register-Backup 'Backup full' 'full' (New-ScheduledTaskTrigger -Daily -At '01:00') `
    'ELearning: backup full SQL Server + ảnh câu hỏi, hằng ngày 01:00 (docs/09-van-hanh.md mục 6)'
Register-Backup 'Backup diff' 'diff' (New-ScheduledTaskTrigger -Once -At $today.AddHours(0) `
        -RepetitionInterval (New-TimeSpan -Hours 6) -RepetitionDuration $forever) `
    'ELearning: backup differential mỗi 6 giờ'
Register-Backup 'Backup log' 'log' (New-ScheduledTaskTrigger -Once -At $today `
        -RepetitionInterval (New-TimeSpan -Minutes 15) -RepetitionDuration $forever) `
    'ELearning: backup transaction log mỗi 15 phút (RPO 15 phút)'

Get-ScheduledTask -TaskPath $taskPath | Get-ScheduledTaskInfo |
    Select-Object TaskName, NextRunTime | Format-Table -AutoSize
