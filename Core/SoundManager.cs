using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using CrushIt.Data;

namespace CrushIt.Core
{
    public enum SoundType
    {
        Swipe,
        CandyMatch,
        LevelComplete,
        Achievement,
        SpecialMove,
        GameOver,
        ButtonClick,
        Navigation,
        Error
    }

    public class SoundSettings
    {
        public float MasterVolume { get; set; } = 1.0f;
        public float MusicVolume { get; set; } = 0.3f;
        public float SfxVolume { get; set; } = 1.0f;
        public bool MusicEnabled { get; set; } = true;
        public bool SfxEnabled { get; set; } = true;
    }

    public static class SoundManager
    {
        private static readonly object _lock = new();
        private static readonly List<IWavePlayer> _activePlayers = new();
        private static readonly List<AudioFileReader> _activeReaders = new();
        private static IWavePlayer? _backgroundPlayer;
        private static AudioFileReader? _backgroundReader;
        private static bool _isBackgroundMusicPlaying = false;
        private static bool _isCleaningUp = false;
        private static SoundSettings _settings = new();

        private static readonly Dictionary<SoundType, string> _soundPaths = new()
        {
            { SoundType.Swipe, "SwipeSound.mp3" },
            { SoundType.CandyMatch, "candymatchsquare.mp3" },
            { SoundType.LevelComplete, "levelcomplete.mp3" },
            { SoundType.Achievement, "achievement.mp3" },
            { SoundType.SpecialMove, "specialmove.mp3" },
            { SoundType.GameOver, "gameover.mp3" },
            { SoundType.ButtonClick, "buttonclick.mp3" },
            { SoundType.Navigation, "navigation.mp3" },
            { SoundType.Error, "error.mp3" }
        };

        public static SoundSettings Settings
        {
            get => _settings;
            set => _settings = value ?? new SoundSettings();
        }

        public static void Initialize()
        {
            lock (_lock)
            {
                _isCleaningUp = true;
                CleanupAllPlayers();
                _isCleaningUp = false;
            }
        }
        
        public static void LoadSettings(SoundSettings settings)
        {
            lock (_lock)
            {
                _settings = settings ?? new SoundSettings();
                
                // Update background music volume if playing
                if (_backgroundPlayer != null)
                {
                    _backgroundPlayer.Volume = _settings.MasterVolume * _settings.MusicVolume;
                }
            }
        }

        public static void PlaySound(SoundType soundType)
        {
            if (!_settings.SfxEnabled || _isCleaningUp)
                return;

            try
            {
                if (!_soundPaths.TryGetValue(soundType, out string? fileName))
                    return;

                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", fileName);
                if (!File.Exists(soundPath))
                    return;

                var reader = new AudioFileReader(soundPath);
                var player = new WaveOutEvent();

                player.PlaybackStopped += (_, _) =>
                {
                    lock (_lock)
                    {
                        if (!_isCleaningUp)
                        {
                            try
                            {
                                _activePlayers.Remove(player);
                                _activeReaders.Remove(reader);
                            }
                            catch { }
                        }
                    }
                    
                    try
                    {
                        player.Dispose();
                        reader.Dispose();
                    }
                    catch { }
                };

                lock (_lock)
                {
                    if (!_isCleaningUp)
                    {
                        _activePlayers.Add(player);
                        _activeReaders.Add(reader);
                        
                        player.Init(reader);
                        player.Volume = _settings.MasterVolume * _settings.SfxVolume;
                        player.Play();
                    }
                    else
                    {
                        player.Dispose();
                        reader.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to play sound {soundType}", ex);
            }
        }

        public static void StartBackgroundMusic()
        {
            StartBackgroundMusic(_settings.MusicVolume);
        }

        public static void StartBackgroundMusic(float volume)
        {
            if (!_settings.MusicEnabled)
                return;

            if (_isBackgroundMusicPlaying)
            {
                SetBackgroundMusicVolume(volume);
                return;
            }

            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "background.mp3");
                if (!File.Exists(soundPath))
                    return;

                _backgroundReader = new AudioFileReader(soundPath);
                _backgroundPlayer = new WaveOutEvent();
                
                _backgroundPlayer.PlaybackStopped += (_, _) =>
                {
                    if (_isBackgroundMusicPlaying && !_isCleaningUp && _backgroundReader != null && _backgroundPlayer != null)
                    {
                        try
                        {
                            _backgroundReader.Position = 0;
                            _backgroundPlayer.Volume = _settings.MasterVolume * _settings.MusicVolume;
                            _backgroundPlayer.Play();
                        }
                        catch
                        {
                            // If we can't restart, stop background music
                            _isBackgroundMusicPlaying = false;
                        }
                    }
                };

                _backgroundPlayer.Init(_backgroundReader);
                _backgroundPlayer.Volume = _settings.MasterVolume * _settings.MusicVolume;
                _backgroundPlayer.Play();
                _isBackgroundMusicPlaying = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start background music", ex);
                StopBackgroundMusic();
            }
        }

        public static void StopBackgroundMusic()
        {
            if (!_isBackgroundMusicPlaying)
                return;

            try
            {
                _backgroundPlayer?.Stop();
                _backgroundPlayer?.Dispose();
                _backgroundReader?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to stop background music", ex);
            }
            finally
            {
                _backgroundPlayer = null;
                _backgroundReader = null;
                _isBackgroundMusicPlaying = false;
            }
        }

        public static void SetBackgroundMusicVolume(float volume)
        {
            _settings.MusicVolume = Math.Clamp(volume, 0f, 1f);
            
            if (_backgroundPlayer != null)
            {
                _backgroundPlayer.Volume = _settings.MasterVolume * _settings.MusicVolume;
            }
        }

        public static void SetMasterVolume(float volume)
        {
            _settings.MasterVolume = Math.Clamp(volume, 0f, 1f);
            
            if (_backgroundPlayer != null)
            {
                _backgroundPlayer.Volume = _settings.MasterVolume * _settings.MusicVolume;
            }
        }

        public static void SetSfxVolume(float volume)
        {
            _settings.SfxVolume = Math.Clamp(volume, 0f, 1f);
        }

        public static void ToggleMusic()
        {
            _settings.MusicEnabled = !_settings.MusicEnabled;
            
            if (_settings.MusicEnabled)
            {
                StartBackgroundMusic();
            }
            else
            {
                StopBackgroundMusic();
            }
        }

        public static void ToggleSfx()
        {
            _settings.SfxEnabled = !_settings.SfxEnabled;
        }

        public static void Cleanup()
        {
            lock (_lock)
            {
                _isCleaningUp = true;
                StopBackgroundMusic();
                CleanupAllPlayers();
                _isCleaningUp = false;
            }
        }

        private static void CleanupAllPlayers()
        {
            foreach (var player in _activePlayers)
            {
                try
                {
                    player.Stop();
                    player.Dispose();
                }
                catch { }
            }
            _activePlayers.Clear();
            
            foreach (var reader in _activeReaders)
            {
                try
                {
                    reader.Dispose();
                }
                catch { }
            }
            _activeReaders.Clear();
        }

        public static bool IsBackgroundMusicPlaying => _isBackgroundMusicPlaying;
    }
}