using System;

namespace CrushIt.API.Models
{
    public class SessionValidationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime ClientTime { get; set; }
        public string DeviceFingerprint { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
    }

    public class SessionValidationResponse
    {
        public bool IsValid { get; set; }
        public DateTime ServerTime { get; set; }
        public TimeSpan TimeDifference { get; set; }
        public string Warning { get; set; } = string.Empty;
    }
}