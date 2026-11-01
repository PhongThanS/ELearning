@echo off
rem Bam dup de cap nhat EClass tren may chu (chay update.ps1 cung thu muc).
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update.ps1" %*
echo.
pause
