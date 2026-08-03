using System;
using System.Collections.Generic;

namespace CrushIt.API.Models
{
    public class ProgressSyncRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
        
        // Progress data to sync
        public List<int>? CompletedLevels { get; set; }
        public int Gold { get; set; }
        public int HighestScore { get; set; }
        public int TotalMatches { get; set; }
        public List<AchievementSyncData>? Achievements { get; set; }
        public string Username { get; set; } = string.Empty;
        
        // Timestamp for conflict resolution
        public DateTime ClientTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class AchievementSyncData
    {
        public string Type { get; set; } = string.Empty;
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedAt { get; set; }
    }
}
