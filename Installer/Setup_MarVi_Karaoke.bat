@echo off
setlocal
title MarVi Karaoke Pro - Setup and Installer

:: 1. Deteksi jika dieksekusi langsung dari dalam ZIP (Temp preview folder)
echo "%~dp0" | findstr /i "Temp1_ .zip" >nul
if %ERRORLEVEL% equ 0 goto :RUNNING_FROM_ZIP

:: 2. Cek apakah folder App ada (memastikan zip telah diekstrak lengkap)
if not exist "%~dp0App\MarViKaraoke.exe" goto :MISSING_APP_FILES

:: 3. Jalankan skrip installer PowerShell
if exist "%~dp0Installer\install.ps1" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Installer\install.ps1"
    goto :END
)
if exist "%~dp0install.ps1" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
    goto :END
)

:MISSING_APP_FILES
cls
echo ==============================================================
echo [PERINGATAN] BERKAS APLIKASI BELUM LENGKAP ATAU BELUM DIEKSTRAK!
echo ==============================================================
echo File Setup_MarVi_Karaoke.bat tidak menemukan folder 'App'.
echo Kemungkinan file dibuka langsung dari dalam file ZIP atau belum
echo diekstrak seluruh isinya.
echo.
echo Langkah mudah:
echo 1. Klik kanan pada file zip 'MarVi_Karaoke_Pro_v2.0_Setup.zip'
echo 2. Pilih 'Extract All...' atau 'Ekstrak Semua...'
echo 3. Centang 'Show extracted files when complete', lalu klik Extract
echo 4. Masuk ke folder hasil ekstrak, lalu klik ganda 'Setup_MarVi_Karaoke.bat'
echo ==============================================================
echo.
pause
exit /b 1

:RUNNING_FROM_ZIP
cls
echo ==============================================================
echo [PERINGATAN] ANDA MEMBUKA FILE DARI DALAM FILE ZIP LANGSUNG!
echo ==============================================================
echo Installer membutuhkan seluruh berkas aplikasi untuk diekstrak.
echo Jika dibuka langsung dari dalam zip, Windows hanya menyalin file
echo bat ini ke folder sementara (Temp), sehingga instalasi gagal.
echo.
echo Langkah mudah:
echo 1. Tutup jendela hitam ini.
echo 2. Klik Kanan pada file zip 'MarVi_Karaoke_Pro_v2.0_Setup.zip'
echo 3. Pilih 'Extract All...' atau 'Ekstrak Semua...'
echo 4. Masuk ke folder baru hasil ekstrak, lalu klik ganda 'Setup_MarVi_Karaoke.bat'
echo ==============================================================
echo.
pause
exit /b 1

:END
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Terjadi kendala saat instalasi. Kode error: %ERRORLEVEL%
    pause
)
