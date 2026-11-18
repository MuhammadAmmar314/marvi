# =====================================================================
# MarVi Karaoke Pro - Windows Installer Script
# =====================================================================

[CmdletBinding()]
param(
    [string]$TargetDir = "",
    [switch]$NoLaunch,
    [switch]$Silent
)

$ErrorActionPreference = "Stop"

function Show-Header {
    Clear-Host
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "             MarVi Karaoke Pro - Windows Installer        " -ForegroundColor Green
    Write-Host "          Sistem Karaoke Modern dengan AI Vocal Remover   " -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host ""
}

if (-not $Silent) {
    Show-Header
}

# 1. Tentukan Direktori Tujuan Default
if (-not $TargetDir) {
    $TargetDir = Join-Path $env:LOCALAPPDATA "Programs\MarViKaraoke"
}

# 2. Tentukan Source Folder
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Cek apakah dijalankan dari temp folder zip
if ($ScriptDir -like "*AppData\Local\Temp*") {
    Write-Host "[PERINGATAN] Anda menjalankan installer langsung dari dalam file ZIP tanpa mengekstraknya!" -ForegroundColor Yellow
    Write-Host "Harap Ekstrak file ZIP terlebih dahulu (Klik Kanan ZIP -> Extract All / Ekstrak Semua)." -ForegroundColor Yellow
    Write-Host "Lalu jalankan Setup_MarVi_Karaoke.bat dari folder hasil ekstrak." -ForegroundColor Yellow
    Write-Host ""
}

$Candidates = @(
    (Join-Path $ScriptDir "..\App"),
    (Join-Path $ScriptDir "App"),
    (Join-Path $ScriptDir "..\publish\MarViKaraoke"),
    (Join-Path $ScriptDir "publish\MarViKaraoke"),
    (Join-Path $ScriptDir ".."),
    $ScriptDir,
    "c:\Users\HYPE\Documents\ammar\marvi\publish\MarViKaraoke",
    "c:\Users\HYPE\Documents\ammar\marvi\dist\MarVi_Karaoke_Pro_v2.0\App"
)

$SourceDir = $null
foreach ($c in $Candidates) {
    if (Test-Path $c) {
        $exe = Join-Path $c "MarViKaraoke.exe"
        if (Test-Path $exe) {
            $SourceDir = (Resolve-Path $c).Path
            break
        }
    }
}

# Jika belum ketemu di list kandidat, cari file MarViKaraoke.exe di sekitar folder installer (maksimal 2 tingkat)
if (-not $SourceDir) {
    $parent = Split-Path -Parent $ScriptDir
    if (Test-Path $parent) {
        $found = Get-ChildItem -Path $parent -Filter "MarViKaraoke.exe" -Recurse -Depth 2 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            $SourceDir = $found.DirectoryName
        }
    }
}

if (-not $SourceDir) {
    Write-Host "[ERROR] File binari MarViKaraoke.exe tidak ditemukan di lokasi installer!" -ForegroundColor Red
    Write-Host "Lokasi saat ini: $ScriptDir" -ForegroundColor Yellow
    Write-Host "Pastikan folder 'App' atau 'publish\MarViKaraoke' ada bersama installer." -ForegroundColor Yellow
    Write-Host "Jika Anda mengunduh file ZIP, pastikan file ZIP sudah di-Extract All (Ekstrak Semua) terlebih dahulu." -ForegroundColor Cyan
    Write-Host ""
    if (-not $Silent) { Read-Host "Tekan Enter untuk keluar..." }
    exit 1
}

Write-Host "[1/6] Sumber instalasi ditemukan: $SourceDir" -ForegroundColor Green

# 3. Hentikan aplikasi jika sedang berjalan
$running = Get-Process -Name "MarViKaraoke" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[2/6] Menutup proses MarViKaraoke yang sedang berjalan..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
} else {
    Write-Host "[2/6] Memeriksa status proses aplikasi... OK" -ForegroundColor Green
}

# 4. Buat Folder Tujuan & Deteksi Update
$isUpdate = Test-Path (Join-Path $TargetDir "MarViKaraoke.exe")
if ($isUpdate) {
    Write-Host "[3/6] Terdeteksi instalasi sebelumnya. Memperbarui aplikasi ke versi terbaru..." -ForegroundColor Green
    Write-Host "      (PENTING: Seluruh lagu dan rekaman Anda di folder 'songs' tetap aman dan tidak terhapus)" -ForegroundColor DarkCyan
} else {
    Write-Host "[3/6] Mempersiapkan folder instalasi baru: $TargetDir" -ForegroundColor Cyan
}

if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}

# 5. Salin Berkas Aplikasi & AI Tools
Write-Host "[4/6] Menyalin berkas aplikasi MarVi Karaoke Pro & AI Engine..." -ForegroundColor Cyan

# Gunakan robocopy untuk kecepatan & integritas berkas (kecualikan folder songs agar koleksi lagu tidak tertimpa/terhapus)
robocopy $SourceDir $TargetDir /E /XO /NP /NFL /NDL /R:1 /W:1 /XD "songs" | Out-Null

# Pastikan folder songs tersedia (dibiarkan kosong pada instalasi baru)
$TargetSongs = Join-Path $TargetDir "songs"
if (-not (Test-Path $TargetSongs)) {
    New-Item -ItemType Directory -Path $TargetSongs -Force | Out-Null
}

Write-Host "      Berkas aplikasi berhasil disalin." -ForegroundColor Green

# 6. Buat Shortcut Desktop & Start Menu
Write-Host "[5/6] Mendaftarkan Shortcut Desktop dan Start Menu..." -ForegroundColor Cyan

$ExePath = Join-Path $TargetDir "MarViKaraoke.exe"

try {
    $WshShell = New-Object -ComObject WScript.Shell

    # Desktop Shortcut
    $DesktopFolder = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
    if (-not $DesktopFolder -or -not (Test-Path $DesktopFolder)) {
        $DesktopFolder = Join-Path $env:USERPROFILE "Desktop"
    }

    $IconLocation = "$ExePath,0"
    $AppIco = Join-Path $TargetDir "Assets\app.ico"
    if (Test-Path $AppIco) {
        $IconLocation = "$AppIco,0"
    }

    if (Test-Path $DesktopFolder) {
        $DesktopShortcutPath = Join-Path $DesktopFolder "MarVi Karaoke Pro.lnk"
        $Shortcut = $WshShell.CreateShortcut($DesktopShortcutPath)
        $Shortcut.TargetPath = $ExePath
        $Shortcut.WorkingDirectory = $TargetDir
        $Shortcut.Description = "MarVi Karaoke Pro - Karaoke Pintar dengan AI Vocal Remover"
        $Shortcut.IconLocation = "$ExePath,0"
        $Shortcut.Save()
        Write-Host "      Shortcut Desktop dibuat: $DesktopShortcutPath" -ForegroundColor Green
    }

    # Start Menu Shortcut
    $StartMenuFolder = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Programs)
    if (-not $StartMenuFolder -or -not (Test-Path $StartMenuFolder)) {
        $StartMenuFolder = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
    }

    if (Test-Path $StartMenuFolder) {
        $MarViStartFolder = Join-Path $StartMenuFolder "MarVi Karaoke Pro"
        if (-not (Test-Path $MarViStartFolder)) {
            New-Item -ItemType Directory -Path $MarViStartFolder -Force | Out-Null
        }
        $StartMenuShortcutPath = Join-Path $MarViStartFolder "MarVi Karaoke Pro.lnk"
        $StartShortcut = $WshShell.CreateShortcut($StartMenuShortcutPath)
        $StartShortcut.TargetPath = $ExePath
        $StartShortcut.WorkingDirectory = $TargetDir
        $StartShortcut.Description = "MarVi Karaoke Pro"
        $StartShortcut.IconLocation = "$ExePath,0"
        $StartShortcut.Save()
        Write-Host "      Shortcut Start Menu dibuat: $StartMenuShortcutPath" -ForegroundColor Green

        # Shortcut Uninstall di Start Menu
        $StartUninstallPath = Join-Path $MarViStartFolder "Uninstall MarVi Karaoke Pro.lnk"
        $UninstallShortcut = $WshShell.CreateShortcut($StartUninstallPath)
        $UninstallShortcut.TargetPath = (Join-Path $TargetDir "Uninstall.bat")
        $UninstallShortcut.WorkingDirectory = $TargetDir
        $UninstallShortcut.Description = "Uninstall MarVi Karaoke Pro"
        $UninstallShortcut.IconLocation = "shell32.dll,31"
        $UninstallShortcut.Save()
        Write-Host "      Shortcut Uninstall dibuat: $StartUninstallPath" -ForegroundColor Green
    }

    # Refresh Windows Explorer Icon Cache agar logo langsung tampil tanpa restart
    try {
        Start-Process -FilePath "ie4uinit.exe" -ArgumentList "-show" -WindowStyle Hidden -ErrorAction SilentlyContinue
    } catch {}
} catch {
    Write-Host "      [Peringatan] Pembuatan shortcut dilewati: $_" -ForegroundColor Yellow
}

# 6. Daftarkan ke Windows Settings (Add or Remove Programs)
try {
    $RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MarViKaraoke"
    if (-not (Test-Path $RegPath)) {
        New-Item -Path $RegPath -Force | Out-Null
    }
    Set-ItemProperty -Path $RegPath -Name "DisplayName" -Value "MarVi Karaoke Pro" -Force
    Set-ItemProperty -Path $RegPath -Name "DisplayVersion" -Value "2.0.0" -Force
    Set-ItemProperty -Path $RegPath -Name "Publisher" -Value "MarVi MEDIA PRODUCTIONS" -Force
    Set-ItemProperty -Path $RegPath -Name "DisplayIcon" -Value "$ExePath,0" -Force
    Set-ItemProperty -Path $RegPath -Name "UninstallString" -Value "`"$TargetDir\Uninstall.bat`"" -Force
    Set-ItemProperty -Path $RegPath -Name "InstallLocation" -Value "$TargetDir" -Force
    Set-ItemProperty -Path $RegPath -Name "NoModify" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $RegPath -Name "NoRepair" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $RegPath -Name "EstimatedSize" -Value 260000 -Type DWord -Force
    Write-Host "      Terdaftar di Windows Settings (Add or Remove Programs)." -ForegroundColor Green
} catch {
    Write-Host "      [Peringatan] Registrasi uninstaller Windows dilewati: $_" -ForegroundColor Yellow
}

# 7. Tulis script Uninstaller di Target Directory
$UninstallPs1 = @'
param([switch]$Silent, [switch]$DeleteSongs)

$TargetDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $TargetDir -or -not (Test-Path $TargetDir)) {
    $TargetDir = Join-Path $env:LOCALAPPDATA "Programs\MarViKaraoke"
}
$AppName = "MarVi Karaoke Pro"

Clear-Host
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "         Pencopotan Pemasangan (Uninstall)               " -ForegroundColor Yellow
Write-Host "                  $AppName                                " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

if (-not $Silent) {
    Write-Host "Anda akan mencopot pemasangan $AppName dari komputer Anda." -ForegroundColor Yellow
    Write-Host "Lokasi terpasang: $TargetDir" -ForegroundColor Gray
    Write-Host ""
    $confirm = Read-Host "Apakah Anda yakin ingin melanjutkan pencopotan? (Y/n)"
    if ($confirm -eq 'n' -or $confirm -eq 'N') {
        Write-Host "Pencopotan dibatalkan." -ForegroundColor Green
        Start-Sleep -Seconds 1
        exit 0
    }

    Write-Host ""
    Write-Host "PILIHAN KOLEKSI LAGU & LIRIK:" -ForegroundColor Cyan
    Write-Host "[1] Simpan koleksi lagu saya (Direkomendasikan)" -ForegroundColor Green
    Write-Host "[2] Hapus semua file termasuk seluruh lagu di folder 'songs'" -ForegroundColor Red
    $songChoice = Read-Host "Pilih opsi (1/2) [Default: 1]"
    if ($songChoice -eq '2') {
        $DeleteSongs = $true
    }
}

Write-Host ""
Write-Host "[1/4] Menghentikan proses aplikasi..." -ForegroundColor Cyan
$running = Get-Process -Name "MarViKaraoke" -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

Write-Host "[2/4] Menghapus Shortcut Desktop & Start Menu..." -ForegroundColor Cyan
# Desktop
$desktopLnk = Join-Path ([System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)) "$AppName.lnk"
if (-not $desktopLnk -or -not (Test-Path $desktopLnk)) { $desktopLnk = Join-Path $env:USERPROFILE "Desktop\$AppName.lnk" }
if (Test-Path $desktopLnk) { Remove-Item $desktopLnk -Force -ErrorAction SilentlyContinue }

# Start Menu
$startMenuDir = Join-Path ([System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Programs)) $AppName
if (-not $startMenuDir -or -not (Test-Path $startMenuDir)) { $startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName" }
if (Test-Path $startMenuDir) { Remove-Item $startMenuDir -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host "[3/4] Menghapus registrasi dari Windows Settings..." -ForegroundColor Cyan
$regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MarViKaraoke"
if (Test-Path $regKey) {
    Remove-Item -Path $regKey -Force -Recurse -ErrorAction SilentlyContinue
}

Write-Host "[4/4] Menghapus berkas aplikasi..." -ForegroundColor Cyan

# Jalankan skrip pembersih dari folder Temp agar berkas di TargetDir tidak terkunci
$randId = Get-Random
$tempCleaner = Join-Path $env:TEMP "MarVi_Cleanup_$randId.bat"
$deleteSongsFlag = if ($DeleteSongs) { "1" } else { "0" }

$cleanerLines = @(
    "@echo off",
    "timeout /t 1 /nobreak >nul",
    "if `"$deleteSongsFlag`"==`"1`" (",
    "    rmdir /s /q `"$TargetDir`" >nul 2>&1",
    ") else (",
    "    for /d %%p in (`"$TargetDir\*`") do (",
    "        if /i not `"%%~nxp`"==`"songs`" rmdir /s /q `"%%p`" >nul 2>&1",
    "    )",
    "    for %%f in (`"$TargetDir\*.*`") do (",
    "        del /f /q `"%%f`" >nul 2>&1",
    "    )",
    ")",
    "del /f /q `"%~f0`" >nul 2>&1"
)
[System.IO.File]::WriteAllLines($tempCleaner, $cleanerLines)
Start-Process -FilePath $tempCleaner -WindowStyle Hidden

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "     MARVI KARAOKE PRO TELAH BERHASIL DICOPOT!            " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
if (-not $DeleteSongs) {
    Write-Host "Catatan: Koleksi lagu Anda tetap aman tersimpan di:" -ForegroundColor Yellow
    Write-Host "  $TargetDir\songs" -ForegroundColor White
}
Write-Host ""
if (-not $Silent) {
    Read-Host "Tekan Enter untuk menutup..."
}
'@
Set-Content -Path (Join-Path $TargetDir "uninstall.ps1") -Value $UninstallPs1 -Encoding UTF8

$UninstallBat = @"
@echo off
setlocal
title Uninstall MarVi Karaoke Pro
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
"@
Set-Content -Path (Join-Path $TargetDir "Uninstall.bat") -Value $UninstallBat -Encoding ASCII

# 7. Selesai
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
if ($isUpdate) {
    Write-Host "      SELAMAT! MARVI KARAOKE PRO BERHASIL DIUPDATE!       " -ForegroundColor Green
} else {
    Write-Host "      SELAMAT! MARVI KARAOKE PRO BERHASIL DI-INSTALL!     " -ForegroundColor Green
}
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Lokasi Terpasang : $TargetDir" -ForegroundColor White
if ($DesktopShortcutPath) {
    Write-Host "Shortcut Desktop : $DesktopShortcutPath" -ForegroundColor White
}
if ($StartMenuShortcutPath) {
    Write-Host "Start Menu       : $StartMenuShortcutPath" -ForegroundColor White
}
Write-Host ""

if (-not $NoLaunch -and -not $Silent) {
    $launchChoice = Read-Host "Apakah Anda ingin membuka MarVi Karaoke Pro sekarang? (Y/n)"
    if ($launchChoice -ne 'n' -and $launchChoice -ne 'N') {
        Write-Host "Membuka MarVi Karaoke Pro..." -ForegroundColor Cyan
        Start-Process -FilePath $ExePath -WorkingDirectory $TargetDir
    }
}
