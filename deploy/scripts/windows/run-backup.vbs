' Chạy deploy/scripts/backup.sh bằng Git Bash, ẩn cửa sổ (Task Scheduler trên Windows không có cron).
'   wscript.exe run-backup.vbs full|diff|log
' Thư mục làm việc của task phải là thư mục gốc repo (register-backup-tasks.ps1 đặt sẵn).
' Kết quả ghi vào backups\backup.log; mã thoát của backup.sh được trả về cho lịch sử task.
Option Explicit

Dim shell, backupType, bash, exitCode
Set shell = CreateObject("WScript.Shell")

If WScript.Arguments.Count <> 1 Then
    WScript.Quit 2
End If
backupType = WScript.Arguments(0)

' Đường dẫn Git Bash lấy từ registry của Git for Windows, không có thì dùng vị trí mặc định
bash = "C:\Program Files\Git\bin\bash.exe"
On Error Resume Next
bash = shell.RegRead("HKLM\SOFTWARE\GitForWindows\InstallPath") & "\bin\bash.exe"
If Err.Number <> 0 Then bash = "C:\Program Files\Git\bin\bash.exe"
On Error GoTo 0

exitCode = shell.Run("""" & bash & """ -c ""mkdir -p backups && deploy/scripts/backup.sh " & backupType & " >> backups/backup.log 2>&1""", 0, True)
WScript.Quit exitCode
