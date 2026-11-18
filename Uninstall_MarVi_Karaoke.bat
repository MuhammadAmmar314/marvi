@echo off
setlocal
title Uninstall MarVi Karaoke Pro

set "TARGET_DIR=%LOCALAPPDATA%\Programs\MarViKaraoke"

if exist "%TARGET_DIR%\Uninstall.bat" (
    call "%TARGET_DIR%\Uninstall.bat"
    goto :END
)

if exist "%TARGET_DIR%\uninstall.ps1" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%TARGET_DIR%\uninstall.ps1"
    goto :END
)

cls
echo ==============================================================
echo [INFO] APLIKASI MARVI KARAOKE PRO BELUM TERPASANG
echo ==============================================================
echo Folder instalasi '%TARGET_DIR%' tidak ditemukan.
echo Aplikasi belum pernah di-install atau sudah dicopot sebelumnya.
echo ==============================================================
echo.
pause

:END
