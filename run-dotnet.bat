@echo off
title MarVi Karaoke Pro (.NET 8 WPF)
echo ========================================================
echo        MEMULAI MARVI KARAOKE PRO (.NET 8 WPF)
echo ========================================================
echo.

set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_CMD=dotnet"

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    if exist "C:\Program Files\dotnet\dotnet.exe" (
        set "DOTNET_CMD=C:\Program Files\dotnet\dotnet.exe"
    ) else (
        echo [ERROR] .NET 8 SDK belum terpasang di komputer Anda.
        echo Silakan unduh dan pasang .NET 8.0 SDK terlebih dahulu dari:
        echo https://dotnet.microsoft.com/download/dotnet/8.0
        echo.
        echo Tekan tombol apa saja untuk membuka halaman unduhan di browser...
        pause >nul
        start https://dotnet.microsoft.com/download/dotnet/8.0
        exit /b
    )
)

set "PROJECT_DIR=%~dp0MarViKaraoke"
set "PROJECT_FILE=%PROJECT_DIR%\MarViKaraoke.csproj"

if not exist "%PROJECT_FILE%" (
    echo [ERROR] Tidak dapat menemukan berkas proyek di: %PROJECT_FILE%
    pause
    exit /b
)

echo Direktori Proyek: %PROJECT_DIR%
echo Menjalankan MarVi Karaoke Pro (.NET 8 WPF)...
echo.

cd /d "%PROJECT_DIR%"
"%DOTNET_CMD%" run --project "%PROJECT_FILE%"

if %errorlevel% neq 0 (
    echo.
    echo [INFO] Terjadi kendala saat menjalankan aplikasi.
    pause
)
