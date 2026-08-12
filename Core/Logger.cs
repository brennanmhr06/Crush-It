using System;
using System.IO;
using System.Text;

namespace CrushIt.Core
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath = string.Empty;
        private static bool _initialized = false;
        private static LogLevel _minLogLevel = LogLevel.Debug;

        public static void Initialize(string logDirectory = "Logs", LogLevel minLogLevel = LogLevel.Debug)
        {
            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    _minLogLevel = minLogLevel;

                    // Create logs directory if it doesn't exist
                    if (!Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }

                    // Create log file with timestamp
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _logFilePath = Path.Combine(logDirectory, $"CrushIt_{timestamp}.log");

                    // Write initialization message
                    File.WriteAllText(_logFilePath, $"=== Crush It! Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                    
                    _initialized = true;
                    
                    LogInfo("Logger initialized successfully");
                }
                catch (Exception ex)
                {
                    // If logging initialization fails, fall back to console
                    Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                }
            }
        }

        public static void LogDebug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public static void LogInfo(string message)
        {
            Log(LogLevel.Info, message);
        }

        public static void LogWarning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public static void LogWarning(string message, Exception ex)
        {
            Log(LogLevel.Warning, $"{message}: {ex.GetType().Name} - {ex.Message}");
        }

        public static void LogError(string message)
        {
            Log(LogLevel.Error, message);
        }

        public static void LogError(string message, Exception ex)
        {
            Log(LogLevel.Error, $"{message}: {ex.GetType().Name} - {ex.Message}");
            if (ex.StackTrace != null)
            {
                Log(LogLevel.Error, $"Stack Trace: {ex.StackTrace}");
            }
        }

        public static void LogCritical(string message)
        {
            Log(LogLevel.Critical, message);
        }

        public static void LogCritical(string message, Exception ex)
        {
            Log(LogLevel.Critical, $"{message}: {ex.GetType().Name} - {ex.Message}");
            if (ex.StackTrace != null)
            {
                Log(LogLevel.Critical, $"Stack Trace: {ex.StackTrace}");
            }
        }

        private static void Log(LogLevel level, string message)
        {
            if (level < _minLogLevel) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] [{level}] {message}";

            lock (_lock)
            {
                // Always write to console
                Console.WriteLine(logMessage);

                // Write to file if initialized
                if (_initialized && !string.IsNullOrEmpty(_logFilePath))
                {
                    try
                    {
                        File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                    }
                    catch
                    {
                        // If file writing fails, just continue with console output
                        Console.WriteLine($"Failed to write to log file: {_logFilePath}");
                    }
                }
            }
        }

        public static void LogPerformance(string operation, TimeSpan duration)
        {
            LogInfo($"[PERF] {operation} completed in {duration.TotalMilliseconds:F2}ms");
        }

        public static void LogApiCall(string endpoint, bool success, string? additionalInfo = null)
        {
            string status = success ? "SUCCESS" : "FAILED";
            string message = $"[API] {endpoint} - {status}";
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                message += $" - {additionalInfo}";
            }
            
            if (success)
            {
                LogInfo(message);
            }
            else
            {
                LogWarning(message);
            }
        }

        public static void LogGameState(string state, string? additionalInfo = null)
        {
            string message = $"[GAME] State: {state}";
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                message += $" - {additionalInfo}";
            }
            LogInfo(message);
        }

        public static void LogUserAction(string action, string? details = null)
        {
            string message = $"[USER] {action}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            LogInfo(message);
        }

        public static string? GetLogFilePath()
        {
            return _logFilePath;
        }

        public static void SetMinLogLevel(LogLevel level)
        {
            _minLogLevel = level;
        }
    }
}