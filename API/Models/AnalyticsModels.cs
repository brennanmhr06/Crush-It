using System;
using System.Collections.Generic;

namespace CrushIt.API.Models
{
    public class GameEvent
    {
        public string UserId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object>? EventData { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class EventLogRequest
    {
        public List<GameEvent> Events { get; set; } = new List<GameEvent>();
        public string BatchId { get; set; } = Guid.NewGuid().ToString();
        public DateTime ClientTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class EventLogResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int EventsProcessed { get; set; }
        public List<string>? FailedEvents { get; set; }
    }

    public class ErrorReport
    {
        public string UserId { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string SessionId { get; set; } = string.Empty;
        public Dictionary<string, object>? Context { get; set; }
        public string DeviceFingerprint { get; set; } = string.Empty;
        public string AppVersion { get; set; } = "1.0";
    }

    public class ErrorReportResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ReportId { get; set; } = string.Empty;
        public bool RequiresFollowUp { get; set; }
    }

    public class UsageStatistics
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan TotalPlayTime { get; set; }
        public int LevelsPlayed { get; set; }
        public int TotalMatches { get; set; }
        public int TotalScore { get; set; }
        public Dictionary<string, int> EventsCount { get; set; } = new Dictionary<string, int>();
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class UsageStatsRequest
    {
        public List<UsageStatistics> Statistics { get; set; } = new List<UsageStatistics>();
        public DateTime ClientTimestamp { get; set; } = DateTime.UtcNow;
    }

    public class UsageStatsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatisticsProcessed { get; set; }
    }

    public class HealthCheckResponse
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Dependencies { get; set; } = new Dictionary<string, string>();
        public string Version { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }
}
