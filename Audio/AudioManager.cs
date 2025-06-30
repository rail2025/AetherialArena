using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AetherialArena.Audio
{
    public class AudioManager : IDisposable
    {
        private readonly Plugin plugin;
        private readonly Configuration configuration;

        private readonly WaveOutEvent bgmOutputDevice;
        private readonly WaveOutEvent sfxOutputDevice;
        private readonly MixingSampleProvider sfxMixer;
        private VolumeSampleProvider? bgmVolumeProvider;
        private FadeInOutSampleProvider? bgmFadeProvider; // Store the fade provider directly
        private readonly VolumeSampleProvider masterSfxVolumeProvider;

        private IWavePlayer? interruptingSfxDevice;
        private CancellationTokenSource fadeTokenSource = new();

        private string? currentBgmPath;
        private bool isBgmLooping;
        private string? previousBgmPath;
        private bool wasBgmLooping;

        private readonly ConcurrentDictionary<string, byte[]> audioCache = new();

        public AudioManager(Plugin plugin)
        {
            this.plugin = plugin;
            this.configuration = plugin.Configuration;

            bgmOutputDevice = new WaveOutEvent();
            bgmOutputDevice.PlaybackStopped += OnBgmPlaybackStopped;

            var mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            sfxMixer = new MixingSampleProvider(mixerFormat) { ReadFully = true };
            masterSfxVolumeProvider = new VolumeSampleProvider(sfxMixer) { Volume = configuration.SfxVolume };

            sfxOutputDevice = new WaveOutEvent();
            sfxOutputDevice.Init(masterSfxVolumeProvider);
            sfxOutputDevice.Play();
        }

        public void SetBgmVolume(float volume)
        {
            if (bgmVolumeProvider != null)
            {
                bgmVolumeProvider.Volume = volume;
            }
        }

        public void SetSfxVolume(float volume)
        {
            masterSfxVolumeProvider.Volume = volume;
        }

        private byte[]? GetAudioData(string resourceName)
        {
            if (audioCache.TryGetValue(resourceName, out var cachedData))
                return cachedData;

            var assembly = Assembly.GetExecutingAssembly();
            var fullResourceName = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(fullResourceName))
            {
                Plugin.Log.Error($"Audio resource '{resourceName}' not found.");
                return null;
            }

            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            if (stream == null) return null;

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var data = memoryStream.ToArray();
            audioCache[resourceName] = data;
            return data;
        }

        public void PlaySfx(string sfxName)
        {
            if (configuration.IsSfxMuted) return;

            var audioData = GetAudioData(sfxName);
            if (audioData == null) return;

            var memoryStream = new MemoryStream(audioData);
            WaveStream readerStream;

            if (sfxName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                readerStream = new Mp3FileReader(memoryStream);
            else if (sfxName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                readerStream = new WaveFileReader(memoryStream);
            else
            {
                memoryStream.Dispose();
                return;
            }

            ISampleProvider soundToPlay = readerStream.ToSampleProvider();

            if (soundToPlay.WaveFormat.SampleRate != sfxMixer.WaveFormat.SampleRate ||
                soundToPlay.WaveFormat.Channels != sfxMixer.WaveFormat.Channels)
            {
                var resampler = new WdlResamplingSampleProvider(soundToPlay, sfxMixer.WaveFormat.SampleRate);
                soundToPlay = resampler.WaveFormat.Channels != sfxMixer.WaveFormat.Channels
                    ? new MonoToStereoSampleProvider(resampler)
                    : resampler;
            }

            sfxMixer.AddMixerInput(new SoundFxStream(soundToPlay, memoryStream, readerStream));
        }

        public async void PlayMusic(string musicName, bool loop, float fadeInDuration = 0)
        {
            if (configuration.IsBgmMuted)
            {
                currentBgmPath = musicName;
                isBgmLooping = loop;
                return;
            }

            await StopMusic(0.1f);

            var audioData = GetAudioData(musicName);
            if (audioData == null) return;

            WaveStream readerStream = new Mp3FileReader(new MemoryStream(audioData));
            if (loop)
            {
                readerStream = new LoopStream(readerStream);
            }

            // Assign the fade provider to our class field
            bgmFadeProvider = new FadeInOutSampleProvider(readerStream.ToSampleProvider(), true);
            // Then wrap it in the volume provider
            bgmVolumeProvider = new VolumeSampleProvider(bgmFadeProvider) { Volume = configuration.MusicVolume };

            bgmOutputDevice.Init(bgmVolumeProvider);
            bgmOutputDevice.Play();

            currentBgmPath = musicName;
            isBgmLooping = loop;

            if (fadeInDuration > 0)
            {
                bgmFadeProvider.BeginFadeIn(fadeInDuration * 1000);
            }
        }

        public async Task StopMusic(float fadeOutDurationSeconds = 0.25f)
        {
            if (bgmOutputDevice.PlaybackState == PlaybackState.Stopped) return;

            fadeTokenSource.Cancel();
            fadeTokenSource = new CancellationTokenSource();
            var token = fadeTokenSource.Token;

            if (fadeOutDurationSeconds > 0 && bgmFadeProvider != null)
            {
                // CORRECTED: Call BeginFadeOut on the stored bgmFadeProvider directly
                bgmFadeProvider.BeginFadeOut(fadeOutDurationSeconds * 1000);
                await Task.Delay((int)(fadeOutDurationSeconds * 1000), token).ContinueWith(_ => { });
            }

            if (!token.IsCancellationRequested)
            {
                bgmOutputDevice.Stop();
            }
        }

        public void PlaySfxAndInterruptMusic(string sfxName, Action? onFinished = null)
        {
            if (configuration.IsSfxMuted) return;

            previousBgmPath = currentBgmPath;
            wasBgmLooping = isBgmLooping;

            Task.Run(async () =>
            {
                await StopMusic(0.5f);

                var audioData = GetAudioData(sfxName);
                if (audioData == null)
                {
                    ResumePreviousMusic();
                    return;
                }

                interruptingSfxDevice = new WaveOutEvent();
                WaveStream readerStream = sfxName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                    ? new Mp3FileReader(new MemoryStream(audioData))
                    : new WaveFileReader(new MemoryStream(audioData));

                var volumeProvider = new VolumeSampleProvider(readerStream.ToSampleProvider()) { Volume = masterSfxVolumeProvider.Volume };
                interruptingSfxDevice.Init(volumeProvider);

                interruptingSfxDevice.PlaybackStopped += (s, a) => {
                    readerStream.Dispose();
                    interruptingSfxDevice?.Dispose();
                    ResumePreviousMusic();
                    onFinished?.Invoke();
                };

                interruptingSfxDevice.Play();
            });
        }

        private void ResumePreviousMusic()
        {
            if (!string.IsNullOrEmpty(previousBgmPath))
            {
                PlayMusic(previousBgmPath, wasBgmLooping, 1.0f);
                previousBgmPath = null;
            }
        }

        public void UpdateBgmState()
        {
            if (bgmOutputDevice == null) return;

            if (configuration.IsBgmMuted)
            {
                if (bgmOutputDevice.PlaybackState == PlaybackState.Playing)
                    bgmOutputDevice.Pause();
            }
            else
            {
                if (bgmOutputDevice.PlaybackState == PlaybackState.Paused)
                {
                    bgmOutputDevice.Play();
                }
                else if (bgmOutputDevice.PlaybackState == PlaybackState.Stopped && !string.IsNullOrEmpty(currentBgmPath))
                {
                    PlayMusic(currentBgmPath, isBgmLooping);
                }
            }
        }

        private void OnBgmPlaybackStopped(object? sender, StoppedEventArgs e)
        {
        }

        public void Dispose()
        {
            bgmOutputDevice.Dispose();
            sfxOutputDevice.Dispose();
            interruptingSfxDevice?.Dispose();
            fadeTokenSource.Dispose();
        }

        private class SoundFxStream : ISampleProvider
        {
            private readonly ISampleProvider source;
            private readonly IDisposable[] disposables;
            private bool isFinished;

            public WaveFormat WaveFormat => source.WaveFormat;

            public SoundFxStream(ISampleProvider source, params IDisposable[] disposables)
            {
                this.source = source;
                this.disposables = disposables;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                if (isFinished) return 0;
                int read = source.Read(buffer, offset, count);
                if (read == 0)
                {
                    isFinished = true;
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
                return read;
            }
        }

        public class LoopStream : WaveStream
        {
            private readonly WaveStream sourceStream;

            public LoopStream(WaveStream sourceStream)
            {
                this.sourceStream = sourceStream;
                this.EnableLooping = true;
            }

            public bool EnableLooping { get; set; }

            public override WaveFormat WaveFormat => sourceStream.WaveFormat;

            public override long Length => sourceStream.Length;

            public override long Position
            {
                get => sourceStream.Position;
                set => sourceStream.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int totalBytesRead = 0;
                while (totalBytesRead < count)
                {
                    int bytesRead = sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        if (sourceStream.Position == 0 || !EnableLooping)
                        {
                            break;
                        }
                        sourceStream.Position = 0;
                    }
                    totalBytesRead += bytesRead;
                }
                return totalBytesRead;
            }
        }
    }
}
