using System;

namespace CrushIt.API.Models
{
    public class ScoreValidationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Score { get; set; }
        public int Moves { get; set; }
        public TimeSpan PlayTime { get; set; }
        public DateTime ClientTimestamp { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
    }

    public class ScoreValidationResponse
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int AdjustedScore { get; set; }
        public bool FlaggedForReview { get; set; }
        public string ServerTimestamp { get; set; } = string.Empty;
    }
}