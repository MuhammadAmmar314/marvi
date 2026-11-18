using System;
using System.IO;
using ManagedBass;
using ManagedBass.Fx;

namespace MarViKaraoke.Core
{
    public enum VocalChannelMode
    {
        Stereo,
        RemoveVocalDsp,
        LeftOnlyMusic,
        RightOnlyVocal
    }

    /// <summary>
    /// Biquad IIR Filter untuk frekuensi audio (Bass, Treble, & Peaking Notch EQ)
    /// </summary>
    public class BiquadFilter
    {
        private float _a0, _a1, _a2, _b1, _b2;
        private float _x1, _x2, _y1, _y2;

        public static BiquadFilter CreateLowPass(float sampleRate, float cutoffFrequency, float q = 0.707f)
        {
            var filter = new BiquadFilter();
            float w0 = 2.0f * MathF.PI * cutoffFrequency / sampleRate;
            float cosW0 = MathF.Cos(w0);
            float alpha = MathF.Sin(w0) / (2.0f * q);

            float b0 = (1.0f - cosW0) / 2.0f;
            float b1Val = 1.0f - cosW0;
            float b2Val = (1.0f - cosW0) / 2.0f;
            float a0Val = 1.0f + alpha;
            float a1Val = -2.0f * cosW0;
            float a2Val = 1.0f - alpha;

            filter._a0 = b0 / a0Val;
            filter._a1 = b1Val / a0Val;
            filter._a2 = b2Val / a0Val;
            filter._b1 = a1Val / a0Val;
            filter._b2 = a2Val / a0Val;
            return filter;
        }

        public static BiquadFilter CreateHighPass(float sampleRate, float cutoffFrequency, float q = 0.707f)
        {
            var filter = new BiquadFilter();
            float w0 = 2.0f * MathF.PI * cutoffFrequency / sampleRate;
            float cosW0 = MathF.Cos(w0);
            float alpha = MathF.Sin(w0) / (2.0f * q);

            float b0 = (1.0f + cosW0) / 2.0f;
            float b1Val = -(1.0f + cosW0);
            float b2Val = (1.0f + cosW0) / 2.0f;
            float a0Val = 1.0f + alpha;
            float a1Val = -2.0f * cosW0;
            float a2Val = 1.0f - alpha;

            filter._a0 = b0 / a0Val;
            filter._a1 = b1Val / a0Val;
            filter._a2 = b2Val / a0Val;
            filter._b1 = a1Val / a0Val;
            filter._b2 = a2Val / a0Val;
            return filter;
        }

        public static BiquadFilter CreatePeakingEq(float sampleRate, float centerFreq, float q, float gainDb)
        {
            var filter = new BiquadFilter();
            float A = MathF.Pow(10f, gainDb / 40f);
            float w0 = 2.0f * MathF.PI * centerFreq / sampleRate;
            float cosW0 = MathF.Cos(w0);
            float alpha = MathF.Sin(w0) / (2.0f * q);

            float b0 = 1.0f + alpha * A;
            float b1Val = -2.0f * cosW0;
            float b2Val = 1.0f - alpha * A;
            float a0Val = 1.0f + alpha / A;
            float a1Val = -2.0f * cosW0;
            float a2Val = 1.0f - alpha / A;

            filter._a0 = b0 / a0Val;
            filter._a1 = b1Val / a0Val;
            filter._a2 = b2Val / a0Val;
            filter._b1 = a1Val / a0Val;
            filter._b2 = a2Val / a0Val;
            return filter;
        }

        public float Process(float inSample)
        {
            float outSample = _a0 * inSample + _a1 * _x1 + _a2 * _x2 - _b1 * _y1 - _b2 * _y2;
            _x2 = _x1;
            _x1 = inSample;
            _y2 = _y1;
            _y1 = outSample;
            return outSample;
        }

        public void Reset()
        {
            _x1 = _x2 = _y1 = _y2 = 0f;
        }
    }

    public class AudioEngine : IDisposable
    {
        private int _stream = 0;
        private int _tempoStream = 0;
        private int _dspHandle = 0;
        private readonly DSPProcedure _vocalRemoveDspProc;

        private float _volume = 0.9f;
        private float _vocalAttenuation = 0f; // 0.0 (normal) to 1.0 (vocal muted)
        private VocalChannelMode _vocalMode = VocalChannelMode.RemoveVocalDsp;
        private int _pitchSemitones = 0;
        private float _tempoRate = 1.0f;

        private int _channels = 2;
        private float _sampleRate = 44100f;

        // Filter untuk separasi frekuensi stereo
        private BiquadFilter _bassFilter = BiquadFilter.CreateLowPass(44100, 160f, 0.707f);
        private BiquadFilter _trebleFilter = BiquadFilter.CreateHighPass(44100, 7500f, 0.707f);

        // Filter Notch untuk melenyapkan sisa reverb & harmoni vokal stereo
        private BiquadFilter _notchFilterL = BiquadFilter.CreatePeakingEq(44100, 1000f, 0.6f, -18f);
        private BiquadFilter _notchFilterR = BiquadFilter.CreatePeakingEq(44100, 1000f, 0.6f, -18f);

        public event Action? SongEnded;

        public bool IsInitialized { get; private set; }
        public bool IsPlaying => _tempoStream != 0 && Bass.ChannelIsActive(_tempoStream) == PlaybackState.Playing;
        public string GetLastError() => Bass.LastError.ToString();

        public AudioEngine()
        {
            _vocalRemoveDspProc = new DSPProcedure(VocalRemoveDspCallback);
            InitBass();
        }

        private void InitBass()
        {
            if (IsInitialized) return;
            // Aktifkan mode FloatDSP secara global di BASS
            Bass.Configure(Configuration.FloatDSP, true);
            // Buffer playback 1000ms agar audio tidak pernah patah-patah meskipun CPU sedang memproses AI
            Bass.Configure(Configuration.PlaybackBufferLength, 1000);
            // Inisialisasi audio output device
            if (Bass.Init(-1, 44100, DeviceInitFlags.Default, IntPtr.Zero))
            {
                IsInitialized = true;
            }
        }

        public bool LoadSong(string filePath)
        {
            Stop();

            if (!IsInitialized) InitBass();

            string actualPath = filePath;

            // 1. Buat stream decode dengan 32-bit Float
            _stream = Bass.CreateStream(actualPath, 0, 0, BassFlags.Decode | BassFlags.Float);

            // 2. Jika gagal, coba decode tanpa Float
            if (_stream == 0)
            {
                _stream = Bass.CreateStream(actualPath, 0, 0, BassFlags.Decode);
            }

            // 3. Jika masih gagal (misal format khusus seperti .webm yang butuh Windows Media Foundation),
            // konversikan transparan via NAudio ke file .wav
            if (_stream == 0 && File.Exists(actualPath))
            {
                try
                {
                    string wavCache = Path.ChangeExtension(actualPath, ".cache.wav");
                    if (!File.Exists(wavCache))
                    {
                        using var reader = new NAudio.Wave.MediaFoundationReader(actualPath);
                        NAudio.Wave.WaveFileWriter.CreateWaveFile(wavCache, reader);
                    }
                    actualPath = wavCache;
                    _stream = Bass.CreateStream(actualPath, 0, 0, BassFlags.Decode | BassFlags.Float);
                    if (_stream == 0)
                    {
                        _stream = Bass.CreateStream(actualPath, 0, 0, BassFlags.Decode);
                    }
                }
                catch { }
            }

            if (_stream == 0) return false;

            // Dapatkan informasi audio asli (jumlah kanal dan frekuensi)
            var info = Bass.ChannelGetInfo(_stream);
            _channels = Math.Max(1, info.Channels);
            _sampleRate = info.Frequency > 0 ? (float)info.Frequency : 44100f;

            // Inisialisasi ulang filter sesuai sample rate aktual lagu
            _bassFilter = BiquadFilter.CreateLowPass(_sampleRate, 160f, 0.707f);
            _trebleFilter = BiquadFilter.CreateHighPass(_sampleRate, 7500f, 0.707f);
            _notchFilterL = BiquadFilter.CreatePeakingEq(_sampleRate, 1000f, 0.6f, -18f);
            _notchFilterR = BiquadFilter.CreatePeakingEq(_sampleRate, 1000f, 0.6f, -18f);

            // 2. Buat tempo stream dengan BassFlags.Float
            _tempoStream = BassFx.TempoCreate(_stream, BassFlags.Float);
            if (_tempoStream == 0)
            {
                _tempoStream = BassFx.TempoCreate(_stream, BassFlags.Default);
            }
            if (_tempoStream == 0)
            {
                Bass.StreamFree(_stream);
                return false;
            }

            // Set Atribut Awal
            SetVolume(_volume);
            SetPitch(_pitchSemitones);
            SetTempo(_tempoRate);

            // 3. Pasang Vocal Remover DSP Callback
            _dspHandle = Bass.ChannelSetDSP(_tempoStream, _vocalRemoveDspProc, IntPtr.Zero, 1);

            // 4. Callback saat lagu selesai
            Bass.ChannelSetSync(_tempoStream, SyncFlags.End, 0, (handle, channel, data, user) =>
            {
                SongEnded?.Invoke();
            });

            return true;
        }

        public void Play()
        {
            if (_tempoStream != 0)
                Bass.ChannelPlay(_tempoStream);
        }

        public void Pause()
        {
            if (_tempoStream != 0)
                Bass.ChannelPause(_tempoStream);
        }

        public void Stop()
        {
            if (_tempoStream != 0)
            {
                Bass.ChannelStop(_tempoStream);
                if (_dspHandle != 0)
                {
                    Bass.ChannelRemoveDSP(_tempoStream, _dspHandle);
                    _dspHandle = 0;
                }
                Bass.StreamFree(_tempoStream);
                _tempoStream = 0;
                _stream = 0;
            }
        }

        public double GetPositionSeconds()
        {
            if (_tempoStream == 0) return 0;
            long posBytes = Bass.ChannelGetPosition(_tempoStream, PositionFlags.Bytes);
            return Bass.ChannelBytes2Seconds(_tempoStream, posBytes);
        }

        public double GetDurationSeconds()
        {
            if (_tempoStream == 0) return 0;
            long lenBytes = Bass.ChannelGetLength(_tempoStream, PositionFlags.Bytes);
            return Bass.ChannelBytes2Seconds(_tempoStream, lenBytes);
        }

        public void Seek(double seconds)
        {
            if (_tempoStream != 0)
            {
                long posBytes = Bass.ChannelSeconds2Bytes(_tempoStream, seconds);
                Bass.ChannelSetPosition(_tempoStream, posBytes);
            }
        }

        public void SetVolume(float volume)
        {
            _volume = Math.Clamp(volume, 0f, 1f);
            if (_tempoStream != 0)
                Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Volume, _volume);
        }

        /// <summary>
        /// Mengatur Nada Dasar (-6 s/d +6 semi-tones) tanpa mengubah kecepatan audio.
        /// </summary>
        public void SetPitch(int semitones)
        {
            _pitchSemitones = Math.Clamp(semitones, -6, 6);
            if (_tempoStream != 0)
                Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Pitch, _pitchSemitones);
        }

        /// <summary>
        /// Mengatur Tempo (0.75x s/d 1.25x) tanpa mengubah nada dasar.
        /// </summary>
        public void SetTempo(float rate)
        {
            _tempoRate = Math.Clamp(rate, 0.75f, 1.25f);
            float percentage = (_tempoRate - 1.0f) * 100.0f; // e.g. 1.0 -> 0%, 1.25 -> 25%
            if (_tempoStream != 0)
                Bass.ChannelSetAttribute(_tempoStream, ChannelAttribute.Tempo, percentage);
        }

        /// <summary>
        /// Mengatur peredaman vokal (0.0 = normal, 1.0 = vokal hening maksimal)
        /// </summary>
        public void SetVocalAttenuation(float value)
        {
            _vocalAttenuation = Math.Clamp(value, 0f, 1f);
        }

        public void SetVocalMode(VocalChannelMode mode)
        {
            _vocalMode = mode;
        }

        /// <summary>
        /// Dual-Stage Real-Time Vocal Remover DSP:
        /// Tahap 1: Center-Channel Nulling dengan Bass Preservation (<160Hz) & Treble Preservation (>7.5kHz).
        /// Tahap 2: Peaking EQ Notch (-18dB pada 250Hz - 3500Hz) untuk menekan sisa reverb dan harmoni stereo.
        /// Mendukung format Stereo dan Mono secara otomatis.
        /// </summary>
        private unsafe void VocalRemoveDspCallback(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            if (_vocalMode == VocalChannelMode.Stereo)
                return;

            if (_vocalMode == VocalChannelMode.RemoveVocalDsp && _vocalAttenuation <= 0.01f)
                return;

            float* samples = (float*)buffer.ToPointer();
            int sampleCount = length / 4; // float32 = 4 bytes

            float wet = _vocalAttenuation;
            float dry = 1.0f - wet;

            // JIKA AUDIO MONO (1 Channel)
            if (_channels == 1)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    float mono = samples[i];
                    // Pada lagu mono, gunakan notch filter vokal (peaking eq) untuk meredam frekuensi vokal
                    float notchMono = _notchFilterL.Process(mono);
                    samples[i] = (mono * dry) + (notchMono * wet);
                }
                return;
            }

            // JIKA AUDIO STEREO (2 Channel)
            for (int i = 0; i < sampleCount; i += 2)
            {
                float left = samples[i];
                float right = samples[i + 1];

                if (_vocalMode == VocalChannelMode.LeftOnlyMusic)
                {
                    // Gunakan kanal kiri saja untuk kedua speaker (standar VCD/karaoke track)
                    samples[i] = left;
                    samples[i + 1] = left;
                }
                else if (_vocalMode == VocalChannelMode.RightOnlyVocal)
                {
                    // Gunakan kanal kanan saja untuk kedua speaker
                    samples[i] = right;
                    samples[i + 1] = right;
                }
                else
                {
                    // TAHAP 1: Center-Channel Nulling
                    float center = (left + right) * 0.5f;

                    // Isolasi Bass (<160Hz) dan Treble (>7.5kHz) agar ritme drum & simbal tetap utuh
                    float centerBass = _bassFilter.Process(center);
                    float centerTreble = _trebleFilter.Process(center);

                    // Sinyal vokal utama di kanal tengah
                    float centerVocal = center - centerBass - centerTreble;

                    // Kurangkan vokal tengah dari kanal kiri dan kanan
                    float cancelL = left - (centerVocal * 1.0f);
                    float cancelR = right - (centerVocal * 1.0f);

                    // TAHAP 2: Reverb & Stereo Bleed Suppression
                    // Melewatkan sinyal yang sudah dikurangi vokal tengah ke notch filter vokal
                    // untuk menekan reverb stereo atau harmoni vokal yang tersebar di sisi kiri & kanan
                    float notchL = _notchFilterL.Process(cancelL);
                    float notchR = _notchFilterR.Process(cancelR);

                    // Interpolasi bertahap:
                    // Jika redaman < 0.6: gunakan cancelL murni
                    // Jika redaman >= 0.6: padukan dengan notchL untuk keheningan vokal maksimal
                    float deepCutFactor = Math.Clamp((wet - 0.4f) / 0.6f, 0f, 1f);
                    float processedL = (cancelL * (1f - deepCutFactor)) + (notchL * deepCutFactor);
                    float processedR = (cancelR * (1f - deepCutFactor)) + (notchR * deepCutFactor);

                    // TAHAP 3: Wet/Dry Output
                    float outL = (left * dry) + (processedL * wet);
                    float outR = (right * dry) + (processedR * wet);

                    samples[i] = Math.Clamp(outL, -1.0f, 1.0f);
                    samples[i + 1] = Math.Clamp(outR, -1.0f, 1.0f);
                }
            }
        }

        public float[] GetFftData()
        {
            var fft = new float[128];
            if (_tempoStream != 0)
                Bass.ChannelGetData(_tempoStream, fft, (int)DataFlags.FFT256);
            return fft;
        }

        public void Dispose()
        {
            Stop();
            if (IsInitialized)
            {
                Bass.Free();
                IsInitialized = false;
            }
        }
    }
}
