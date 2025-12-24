using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AetherialArena.Audio
{
    public class AudioManager : IDisposable
    {
        private readonly Plugin plugin;
        private readonly Configuration configuration;

        private WaveOutEvent? bgmOutputDevice;
        private IDisposable? currentBgmStream;
        private VolumeSampleProvider? currentBgmVolumeProvider;

        private readonly WaveOutEvent sfxOutputDevice;
        private readonly MixingSampleProvider sfxMixer;
        private readonly VolumeSampleProvider masterSfxVolumeProvider;
        private IWavePlayer? interruptingSfxDevice;

        private string? currentBgmPath;
        private bool isBgmLooping;
        private string? previousBgmPath;
        private bool wasBgmLooping;

        private readonly ConcurrentDictionary<string, byte[]> audioCache = new();

        private readonly List<string> allMusicTracks = new();
        private readonly List<string> bgmPlaylist = new();
        private int currentTrackIndex = -1;
        private bool isBgmPlaying = false;
        private readonly Random random = new();

        public AudioManager(Plugin plugin)
        {
            this.plugin = plugin;
            this.configuration = plugin.Configuration;

            var mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            sfxMixer = new MixingSampleProvider(mixerFormat) { ReadFully = true };
            masterSfxVolumeProvider = new VolumeSampleProvider(sfxMixer) { Volume = configuration.SfxVolume };

            sfxOutputDevice = new WaveOutEvent();
            sfxOutputDevice.Init(masterSfxVolumeProvider);
            sfxOutputDevice.Play();

            DiscoverMusicTracks();
        }

        private void DiscoverMusicTracks()
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string resourcePrefix = "AetherialArena.Assets.Music.";
            this.allMusicTracks.AddRange(
                assembly.GetManifestResourceNames()
                    .Where(r => r.StartsWith(resourcePrefix) && r.EndsWith(".mp3"))
                    .Select(r => r.Substring(resourcePrefix.Length))
            );
        }

        public void StartBattlePlaylist()
        {
            isBgmPlaying = true;
            bgmPlaylist.Clear();
            var battleTracks = allMusicTracks.Where(t => t.StartsWith("fightmusic", StringComparison.OrdinalIgnoreCase)).ToList();

            if (!battleTracks.Any())
            {
                PlayMusic("titlemusic.mp3", true);
                return;
            }

            bgmPlaylist.AddRange(battleTracks.OrderBy(x => random.Next()));

            if (configuration.IsBgmMuted) return;

            currentTrackIndex = 0;
            PlayTrack(currentTrackIndex);
        }

        public void EndPlaylist()
        {
            isBgmPlaying = false;
            StopMusic();
        }

        private void PlayTrack(int trackIndex)
        {
            StopMusic();
            if (trackIndex < 0 || trackIndex >= bgmPlaylist.Count) return;

            currentTrackIndex = trackIndex;
            var bgmName = bgmPlaylist[trackIndex];
            var audioData = GetAudioData(bgmName);
            if (audioData == null)
            {
                OnBgmPlaybackStopped(this, EventArgs.Empty);
                return;
            }

            var readerStream = new Mp3FileReader(new MemoryStream(audioData));
            currentBgmStream = readerStream;
            currentBgmVolumeProvider = new VolumeSampleProvider(readerStream.ToSampleProvider()) { Volume = configuration.MusicVolume };

            bgmOutputDevice = new WaveOutEvent();
            bgmOutputDevice.PlaybackStopped += OnBgmPlaybackStopped;
            bgmOutputDevice.Init(currentBgmVolumeProvider);
            bgmOutputDevice.Play();

            currentBgmPath = bgmName;
            isBgmLooping = false;
        }

        private void OnBgmPlaybackStopped(object? sender, EventArgs e)
        {
            if (isBgmPlaying)
            {
                currentTrackIndex++;
                if (currentTrackIndex >= bgmPlaylist.Count)
                {
                    currentTrackIndex = 0;
                }
                PlayTrack(currentTrackIndex);
            }
        }

        public void PlayMusic(string musicName, bool loop)
        {
            isBgmPlaying = false;
            StopMusic();

            if (configuration.IsBgmMuted)
            {
                currentBgmPath = musicName;
                isBgmLooping = loop;
                return;
            }

            var audioData = GetAudioData(musicName);
            if (audioData == null) return;

            WaveStream readerStream = new Mp3FileReader(new MemoryStream(audioData));
            if (loop)
            {
                readerStream = new LoopStream(readerStream);
            }

            currentBgmStream = readerStream;
            currentBgmVolumeProvider = new VolumeSampleProvider(readerStream.ToSampleProvider()) { Volume = configuration.MusicVolume };

            bgmOutputDevice = new WaveOutEvent();
            if (loop) bgmOutputDevice.PlaybackStopped += OnBgmPlaybackStopped;
            bgmOutputDevice.Init(currentBgmVolumeProvider);
            bgmOutputDevice.Play();

            currentBgmPath = musicName;
            isBgmLooping = loop;
        }

        public void StopMusic()
        {
            if (bgmOutputDevice != null)
            {
                bgmOutputDevice.PlaybackStopped -= OnBgmPlaybackStopped;
                bgmOutputDevice.Stop();
                bgmOutputDevice.Dispose();
                bgmOutputDevice = null;
            }
            currentBgmStream?.Dispose();
            currentBgmStream = null;
        }

        public void PlaySfxAndInterruptMusic(string sfxName, Action? onFinished = null)
        {
            if (configuration.IsSfxMuted) return;

            previousBgmPath = currentBgmPath;
            wasBgmLooping = isBgmLooping;
            StopMusic();

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

            interruptingSfxDevice.PlaybackStopped += (s, a) =>
            {
                readerStream.Dispose();
                interruptingSfxDevice?.Dispose();
                ResumePreviousMusic();
                onFinished?.Invoke();
            };

            interruptingSfxDevice.Play();
        }

        private void ResumePreviousMusic()
        {
            if (isBgmPlaying) StartBattlePlaylist();
            else if (!string.IsNullOrEmpty(previousBgmPath)) PlayMusic(previousBgmPath, wasBgmLooping);
            previousBgmPath = null;
        }

        public void PlaySfx(string sfxName)
        {
            if (configuration.IsSfxMuted) return;
            var audioData = GetAudioData(sfxName);
            if (audioData == null) return;
            var memoryStream = new MemoryStream(audioData);
            WaveStream readerStream;
            if (sfxName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                readerStream = new Mp3FileReader(memoryStream);
            }
            else
            {
                readerStream = new WaveFileReader(memoryStream);
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

        private byte[]? GetAudioData(string resourceName)
        {
            if (audioCache.TryGetValue(resourceName, out var cachedData)) return cachedData;

            var assembly = Assembly.GetExecutingAssembly();
            string resourcePrefix;

            var musicFiles = new HashSet<string> { "titlemusic.mp3", "credits.mp3", "allcapmusic.mp3", "victory.mp3", "capture.mp3", "ko.wav" };
            if (musicFiles.Contains(resourceName) || resourceName.StartsWith("fightmusic") || resourceName.StartsWith("bossmusic"))
            {
                resourcePrefix = "AetherialArena.Assets.Music.";
            }
            else
            {
                resourcePrefix = "AetherialArena.Assets.Sfx.";
            }


            var fullResourceName = resourcePrefix + resourceName;
            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            if (stream == null) return null;

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var data = memoryStream.ToArray();
            audioCache[resourceName] = data;
            return data;
        }

        public void UpdateBgmState()
        {
            if (configuration.IsBgmMuted)
            {
                if (bgmOutputDevice?.PlaybackState == PlaybackState.Playing) StopMusic();
            }
            else
            {
                if (bgmOutputDevice == null && !string.IsNullOrEmpty(currentBgmPath))
                {
                    if (isBgmPlaying) StartBattlePlaylist();
                    else PlayMusic(currentBgmPath, isBgmLooping);
                }
            }
        }

        public void SetBgmVolume(float volume)
        {
            if (currentBgmVolumeProvider != null)
            {
                currentBgmVolumeProvider.Volume = volume;
            }
        }

        public void SetSfxVolume(float volume)
        {
            masterSfxVolumeProvider.Volume = volume;
        }

        public void Dispose()
        {
            StopMusic();
            sfxOutputDevice.Dispose();
            interruptingSfxDevice?.Dispose();
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
                    foreach (var disposable in disposables) disposable.Dispose();
                }
                return read;
            }
        }

        public class LoopStream : WaveStream
        {
            private readonly WaveStream sourceStream;
            public LoopStream(WaveStream sourceStream) { this.sourceStream = sourceStream; EnableLooping = true; }
            public bool EnableLooping { get; set; }
            public override WaveFormat WaveFormat => sourceStream.WaveFormat;
            public override long Length => sourceStream.Length;
            public override long Position { get => sourceStream.Position; set => sourceStream.Position = value; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int totalBytesRead = 0;
                while (totalBytesRead < count)
                {
                    int bytesRead = sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        if (sourceStream.Position == 0 || !EnableLooping) break;
                        sourceStream.Position = 0;
                    }
                    totalBytesRead += bytesRead;
                }
                return totalBytesRead;
            }
        }
    }
}
