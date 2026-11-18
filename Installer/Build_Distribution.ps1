# =====================================================================
# Script Pembuat Paket ZIP Distribusi MarVi Karaoke Pro
# =====================================================================

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$WorkspaceRoot = "c:\Users\HYPE\Documents\ammar\marvi"
$DistDir = Join-Path $WorkspaceRoot "dist"
$PkgDir = Join-Path $DistDir "MarVi_Karaoke_Pro_v2.0"
$ZipPath = Join-Path $DistDir "MarVi_Karaoke_Pro_v2.0_Setup.zip"

Write-Host "Mempersiapkan folder build distribusi..." -ForegroundColor Cyan

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}
if (-not (Test-Path $PkgDir)) {
    New-Item -ItemType Directory -Path $PkgDir -Force | Out-Null
}

# 1. Salin aplikasi hasil publish (kecuali songs)
Write-Host "Menyalin berkas aplikasi utama (publish)..." -ForegroundColor Cyan
$AppDir = Join-Path $PkgDir "App"
robocopy (Join-Path $WorkspaceRoot "publish\MarViKaraoke") $AppDir /E /XO /NP /NFL /NDL /R:1 /W:1 /XD "songs" | Out-Null

# 2. Siapkan folder songs dalam keadaan bersih (kosong)
$AppSongs = Join-Path $AppDir "songs"
if (Test-Path $AppSongs) {
    Remove-Item (Join-Path $AppSongs "*") -Force -Recurse -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $AppSongs -Force | Out-Null
}

# 3. Salin folder Installer
Write-Host "Menyalin modul installer..." -ForegroundColor Cyan
$InstallerTarget = Join-Path $PkgDir "Installer"
robocopy (Join-Path $WorkspaceRoot "Installer") $InstallerTarget /E /XO /NP /NFL /NDL /R:1 /W:1 /XF "Build_Distribution.*" | Out-Null

# 4. Salin Setup dan Uninstall launcher di root ZIP
Copy-Item (Join-Path $WorkspaceRoot "Setup_MarVi_Karaoke.bat") -Destination $PkgDir -Force
Copy-Item (Join-Path $WorkspaceRoot "Uninstall_MarVi_Karaoke.bat") -Destination $PkgDir -Force

# 5. Buat Petunjuk Instalasi
$ReadmeText = @"
=============================================================
             MARVI KARAOKE PRO - CARA PENGGUNAAN
=============================================================

1. CARA INSTALASI ATAU PEMBARUAN (UPDATE):
   - Cukup klik 2x file: 'Setup_MarVi_Karaoke.bat'
   - Installer akan otomatis memasang atau memperbarui aplikasi ke versi terbaru.
   - PENTING: Jika sebelumnya Anda sudah memiliki lagu, seluruh koleksi lagu
     dan lirik di folder 'songs' TIDAK AKAN HILANG (tetap aman tersimpan).
   - Shortcut di Desktop dan Start Menu akan otomatis dibuat/diperbarui.

2. CARA PORTABLE (TANPA INSTALASI):
   - Buka folder 'App'
   - Klik 2x file 'MarViKaraoke.exe'
   - Aplikasi langsung berjalan tanpa perlu instalasi!

3. CARA MENCAPOT / UNINSTALL:
   - Melalui Start Menu: Buka folder 'MarVi Karaoke Pro' -> klik 'Uninstall MarVi Karaoke Pro'
   - Melalui Windows: Buka Windows Settings -> Apps (Aplikasi Terinstal) -> Cari 'MarVi Karaoke Pro' -> Uninstall
   - Melalui Berkas: Cukup klik 2x file 'Uninstall_MarVi_Karaoke.bat'
   - Anda diberikan pilihan untuk menyimpan atau menghapus file lagu & lirik Anda.

Fitur Unggulan MarVi Karaoke Pro:
- Cari & Unduh Lagu YouTube secara multitasking (lagu yang diputar tidak terganggu)
- Otomatis sinkronisasi lirik kata per kata dari LRCLIB
- AI Vocal Remover terintegrasi (memisahkan instrumen & vokal otomatis)
- Pengaturan Nada Dasar (Pitch Shift -6 s/d +6 semitone)
- Dual-Screen Singer Window untuk monitor/TV eksternal
=============================================================
"@
Set-Content -Path (Join-Path $PkgDir "PETUNJUK_INSTALASI.txt") -Value $ReadmeText -Encoding UTF8

# 6. Kompresi ke file ZIP
Write-Host "Mengompresi ke file ZIP: $ZipPath..." -ForegroundColor Yellow
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($PkgDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   PAKET DISTRIBUSI MARVI KARAOKE PRO BERHASIL DIBUAT!   " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "File ZIP: $ZipPath" -ForegroundColor White
$zipItem = Get-Item $ZipPath
$zipSizeMb = [Math]::Round($zipItem.Length / 1MB, 2)
Write-Host "Ukuran  : $zipSizeMb MB" -ForegroundColor White
