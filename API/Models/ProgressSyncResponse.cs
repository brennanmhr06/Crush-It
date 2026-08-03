using System;
using System.Collections.Generic;

namespace CrushIt.API.Models
{
    public class ProgressSyncResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        
        // Server's latest progress data
        public ServerProgressData? ServerProgress { get; set; }
        
        // Conflict information
        public List<string>? ConflictsResolved { get; set; }
    }

    public class ServerProgressData
    {
        public List<int>? CompletedLevels { get; set; }
        public int Gold { get; set; }
        public int HighestScore { get; set; }
        public int TotalMatches { get; set; }
        public List<AchievementSyncData>? Achievements { get; set; }
        public string Username { get; set; } = string.Empty;
        public DateTime ServerTimestamp { get; set; }
    }
}
