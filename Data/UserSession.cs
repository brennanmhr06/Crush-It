using System;
using System.IO;
using System.Text.Json;

namespace CrushIt.Data
{
    public static class UserSession
    {
        private static readonly string SessionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrushIt",
            "session.json"
        );

        static UserSession()
        {

            var directory = Path.GetDirectoryName(SessionFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public static void SaveLastUser(string email)
        {
            try
            {
                var sessionData = new SessionData { Email = email, LastLogin = DateTime.UtcNow };
                var json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SessionFilePath, json);
            }
            catch (Exception ex)
            {

                System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
            }
        }

        public static string? GetLastUserEmail()
        {
            try
            {
                if (!File.Exists(SessionFilePath))
                    return null;

                var json = File.ReadAllText(SessionFilePath);
                var sessionData = JsonSerializer.Deserialize<SessionData>(json);
                return sessionData?.Email;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load session: {ex.Message}");
                return null;
            }
        }

        public static void ClearSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    File.Delete(SessionFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear session: {ex.Message}");
            }
        }

        private class SessionData
        {
            public string Email { get; set; } = string.Empty;
            public DateTime LastLogin { get; set; }
        }
    }
}


