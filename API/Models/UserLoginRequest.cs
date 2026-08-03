using System;

namespace CrushIt.API.Models
{
    public class UserLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public DateTime ClientTimestamp { get; set; }
        public string UserAgent { get; set; } = string.Empty;
    }

    public class UserLoginResponse
    {
        public bool Success { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool HasCompletedTutorial { get; set; }
        public bool AccountFlagged { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTime LastLoginAt { get; set; }
    }
}

