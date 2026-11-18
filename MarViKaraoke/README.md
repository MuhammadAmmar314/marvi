# 🎤 MarVi Karaoke Pro (.NET 8 WPF Edition)

Aplikasi desktop karaoke profesional berbasis **C# / .NET 8 (WPF)** menggunakan engine audio standar industri dunia **`ManagedBass` (un4seen BASS)** dan **`NAudio`**.

---

## ✨ Fitur Utama Arsitektur .NET 8

1. **Engine Audio Standar Industri (`ManagedBass` / BASS.NET)**:
   - **Real-Time Pitch Shifter Murni (`BASS_FX`)**: Mengubah nada dasar (-6 s/d +6 semi-tones) tanpa mengubah kecepatan audio dengan latensi ultra-rendah.
   - **Kontrol Tempo**: Mengatur kecepatan lagu (0.75x - 1.25x) tanpa mengubah nada dasar.
   - **Real-Time DSP Vocal Remover**: Menghilangkan vokal penyanyi menggunakan *Center-Channel Phase Cancellation* dengan *Bass & Treble Preservation*.
   - **Multiplex Channel Selector**: Mode *Hapus Vokal DSP*, *Kiri Saja (Musik Karaoke)*, *Kanan Saja (Vokal)*, dan *Stereo Penuh*.
   - **FFT Spectrum Visualizer**: Analisis spektrum frekuensi audio 40fps langsung dari kartu suara.

2. **Mesin Lirik Kata per Kata (`LrcParser.cs`)**:
   - Mendukung lirik `.lrc` standar, *Enhanced LRC* (`<mm:ss.xx>kata`), dan tag durasi `<dur:3.5>`.
   - Menghitung ritme suku kata bahasa (*syllables*) secara dinamis agar animasi teks tidak tertahan di jeda musik.
   - Teks menyala kata demi kata (*word-by-word neon glow*) dengan akselerasi GPU DirectX.

3. **Layar TV / Dual Screen (`SingerWindow.xaml`)**:
   - Menampilkan jendela lirik khusus layar TV kedua (HDMI).
   - Mendukung Fullscreen (F11) dan auto-deteksi monitor kedua.

4. **Koleksi Musik Lokal**:
   - Terhubung langsung dengan folder `songs/` komputer Anda.
   - Ekstraksi metadata ID3 (Judul, Artis, Durasi) via `TagLibSharp`.

---

## 🚀 Cara Menjalankan Proyek

### 1. Prasyarat: Pasang .NET 8 SDK
Unduh dan pasang **.NET 8.0 SDK (x64)** dari situs resmi Microsoft:
👉 **[Download .NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**

Atau buka PowerShell sebagai Administrator dan jalankan:
```powershell
winget install Microsoft.DotNet.SDK.8
```

### 2. Jalankan via Terminal / CLI
Buka terminal di folder proyek ini dan jalankan:
```bash
cd MarViKaraoke
dotnet restore
dotnet run
```

### 3. Atau Buka Menggunakan Visual Studio
- Buka file **`MarViKaraoke.sln`** di **Visual Studio 2022** (atau JetBrains Rider / VS Code).
- Tekan tombol **`F5`** untuk menjalankan aplikasi.
