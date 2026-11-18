# 🎤 MarVi Karaoke Pro v2.0 (.NET 8 WPF + AI Vocal Remover)

Sistem Karaoke Desktop Modern & Cerdas berbasis Windows (.NET 8 WPF) yang dilengkapi dengan:
- **Pemisah Vokal & Musik Berbasis AI (ONNX Runtime):** Otomatis memisahkan instrumen dan vokal saat mengunduh lagu.
- **Pencarian & Unduh YouTube Multitasking:** Unduh dan proses lagu baru di latar belakang tanpa mengganggu lagu yang sedang diputar.
- **Sinkronisasi Lirik Otomatis (LRCLIB):** Sinkronisasi teks lirik akurat kata per kata secara otomatis dari API resmi LRCLIB.
- **Pengaturan Nada Dasar (Pitch Shift):** Ubah tinggi/rendah nada lagu (-6 s/d +6 semitone) secara realtime sesuai jangkauan vokal penyanyi.
- **Dual-Screen / Singer Window:** Tampilan khusus penyanyi untuk monitor kedua atau TV karaoke.

---

## 🚀 Cara Instalasi & Menjalankan

### Cara 1: Instalasi Otomatis (Direkomendasikan)
1. Cukup klik ganda (double-click) berkas [Setup_MarVi_Karaoke.bat](file:///C:/Users/HYPE/Documents/ammar/marvi/Setup_MarVi_Karaoke.bat).
2. Installer akan otomatis menyalin seluruh berkas aplikasi dan model AI ke komputer Anda.
3. Shortcut **MarVi Karaoke Pro** akan otomatis dibuat di **Desktop** dan **Start Menu Windows**.

### Cara 2: Mode Portable (Tanpa Instalasi)
Anda dapat langsung menjalankan aplikasi tanpa instalasi:
- Buka folder [publish/MarViKaraoke](file:///C:/Users/HYPE/Documents/ammar/marvi/publish/MarViKaraoke)
- Klik ganda berkas **MarViKaraoke.exe**

### Cara 3: Paket Distribusi ZIP
Tersedia file arsip mandiri yang siap dipindahkan ke flashdisk atau PC lain:
- [dist/MarVi_Karaoke_Pro_v2.0_Setup.zip](file:///C:/Users/HYPE/Documents/ammar/marvi/dist/MarVi_Karaoke_Pro_v2.0_Setup.zip) (335 MB, sudah termasuk model AI dan lagu demo).

---

## 📁 Struktur Direktori Bersih

```text
marvi/
├── MarViKaraoke/           # Source code aplikasi C# .NET 8 WPF
│   ├── Core/               # AudioEngine (BASS FX), LrcParser
│   ├── Services/           # YouTubeService, LyricsService (LRCLIB)
│   ├── ViewModels/         # MainViewModel (Multitasking Queue, Library)
│   ├── Views/              # MainWindow, SingerWindow
│   └── tools/              # AI Vocal Remover standalone & ONNX Model
├── publish/MarViKaraoke/   # Hasil build siap pakai (Release)
├── dist/                   # Paket rilis & installer ZIP
├── songs/                  # Folder pustaka audio & lirik (.wav / .mp3 / .lrc)
├── Installer/              # Skrip installer & builder ZIP
├── Setup_MarVi_Karaoke.bat # Peluncur instalasi 1-klik
├── MarViKaraoke.sln        # Solution Visual Studio / dotnet
└── README.md               # Dokumentasi ini
```

---

## 🛠️ Pencopotan Pemasangan (Uninstall)
Jika diinstal melalui installer, jalankan `Uninstall.bat` yang ada di direktori instalasi aplikasi atau hapus shortcut desktop yang telah dibuat.
