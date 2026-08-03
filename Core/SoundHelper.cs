using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace CrushIt.Core
{
    public static class SoundHelper
    {
        private static readonly object Lock = new();
        private static readonly List<IWavePlayer> ActivePlayers = new();
        private static IWavePlayer? backgroundPlayer;
        private static AudioFileReader? backgroundReader;
        private static bool isBackgroundMusicPlaying = false;
        private static float currentBackgroundVolume = 0.3f;

        public static void PlaySwipeSound()
        {
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "SwipeSound.mp3");
                if (!File.Exists(soundPath))
                    return;

                var reader = new AudioFileReader(soundPath);
                var player = new WaveOutEvent();

                player.PlaybackStopped += (_, _) =>
                {
                    try
                    {
                        player.Dispose();
                        reader.Dispose();
                    }
                    catch { }
                    lock (Lock)
                    {
                        ActivePlayers.Remove(player);
                    }
                };

                lock (Lock)
                {
                    ActivePlayers.Add(player);
                }

                player.Init(reader);
                player.Volume = 1.0f;
                player.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play swipe sound: {ex.Message}");
            }
        }

        public static void PlayCandyMatchSound()
        {
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "candymatchsquare.mp3");
                if (!File.Exists(soundPath))
                    return;

                var reader = new AudioFileReader(soundPath);
                var player = new WaveOutEvent();

                player.PlaybackStopped += (_, _) =>
                {
                    try
                    {
                        player.Dispose();
                        reader.Dispose();
                    }
                    catch { }
                    lock (Lock)
                    {
                        ActivePlayers.Remove(player);
                    }
                };

                lock (Lock)
                {
                    ActivePlayers.Add(player);
                }

                player.Init(reader);
                player.Volume = 1.0f;
                player.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play candy match sound: {ex.Message}");
            }
        }

        public static void StartBackgroundMusic()
        {
            if (isBackgroundMusicPlaying)
                return;

            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "background.mp3");
                if (!File.Exists(soundPath))
                    return;

                backgroundReader = new AudioFileReader(soundPath);

                backgroundPlayer = new WaveOutEvent();
                backgroundPlayer.PlaybackStopped += (_, _) =>
                {

                    if (isBackgroundMusicPlaying && backgroundReader != null && backgroundPlayer != null)
                    {
                        backgroundReader.Position = 0;
                        backgroundPlayer.Volume = currentBackgroundVolume;
                        backgroundPlayer.Play();
                    }
                };

                backgroundPlayer.Init(backgroundReader);
                backgroundPlayer.Volume = currentBackgroundVolume;
                backgroundPlayer.Play();
                isBackgroundMusicPlaying = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start background music: {ex.Message}");
                StopBackgroundMusic();
            }
        }

        public static void StartBackgroundMusic(float volume)
        {
            currentBackgroundVolume = volume;

            if (isBackgroundMusicPlaying)
            {
                SetBackgroundMusicVolume(volume);
                return;
            }

            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "background.mp3");
                if (!File.Exists(soundPath))
                    return;

                backgroundReader = new AudioFileReader(soundPath);

                backgroundPlayer = new WaveOutEvent();
                backgroundPlayer.PlaybackStopped += (_, _) =>
                {

                    if (isBackgroundMusicPlaying && backgroundReader != null && backgroundPlayer != null)
                    {
                        backgroundReader.Position = 0;
                        backgroundPlayer.Volume = currentBackgroundVolume;
                        backgroundPlayer.Play();
                    }
                };

                backgroundPlayer.Init(backgroundReader);
                backgroundPlayer.Volume = volume;
                backgroundPlayer.Play();
                isBackgroundMusicPlaying = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start background music: {ex.Message}");
                StopBackgroundMusic();
            }
        }

        public static void StopBackgroundMusic()
        {
            if (!isBackgroundMusicPlaying)
                return;

            try
            {
                backgroundPlayer?.Stop();
                backgroundPlayer?.Dispose();
                backgroundReader?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to stop background music: {ex.Message}");
            }
            finally
            {
                backgroundPlayer = null;
                backgroundReader = null;
                isBackgroundMusicPlaying = false;
            }
        }

        public static void SetBackgroundMusicVolume(float volume)
        {
            if (backgroundPlayer != null && volume >= 0f && volume <= 1f)
            {
                backgroundPlayer.Volume = volume;
            }
        }
    }
}

