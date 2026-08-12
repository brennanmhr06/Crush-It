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
            catch (FileNotFoundException ex)
            {
                Logger.LogWarning("Swipe sound file not found", ex);
            }
            catch (IOException ex)
            {
                Logger.LogWarning("Swipe sound file IO error", ex);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.LogWarning("Swipe sound audio device error", ex);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning("Swipe sound invalid operation", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to play swipe sound", ex);
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
            catch (FileNotFoundException ex)
            {
                Logger.LogWarning("Candy match sound file not found", ex);
            }
            catch (IOException ex)
            {
                Logger.LogWarning("Candy match sound file IO error", ex);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.LogWarning("Candy match sound audio device error", ex);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning("Candy match sound invalid operation", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to play candy match sound", ex);
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
            catch (FileNotFoundException ex)
            {
                Logger.LogWarning("Background music file not found", ex);
                StopBackgroundMusic();
            }
            catch (IOException ex)
            {
                Logger.LogWarning("Background music file IO error", ex);
                StopBackgroundMusic();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.LogWarning("Background music audio device error", ex);
                StopBackgroundMusic();
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning("Background music invalid operation", ex);
                StopBackgroundMusic();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start background music", ex);
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
            catch (FileNotFoundException ex)
            {
                Logger.LogWarning("Background music file not found", ex);
                StopBackgroundMusic();
            }
            catch (IOException ex)
            {
                Logger.LogWarning("Background music file IO error", ex);
                StopBackgroundMusic();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Logger.LogWarning("Background music audio device error", ex);
                StopBackgroundMusic();
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning("Background music invalid operation", ex);
                StopBackgroundMusic();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start background music", ex);
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
            catch (ObjectDisposedException ex)
            {
                Logger.LogWarning("Background music already disposed", ex);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning("Background music invalid operation on stop", ex);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to stop background music", ex);
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

