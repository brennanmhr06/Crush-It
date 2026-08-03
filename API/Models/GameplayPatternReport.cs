namespace CrushIt.API.Models
{
    public class GameplayPatternReport
    {
        public string UserId { get; set; } = string.Empty;
        public int Level { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalMoves { get; set; }
        public int TotalMatches { get; set; }
        public double AverageMoveTime { get; set; }
        public int MaxCombo { get; set; }
        public int RapidMovesCount { get; set; }
        public int ImpossibleMovesCount { get; set; }
        public double PatternScore { get; set; }
        public string RiskLevel { get; set; } = "LOW";
    }

    public class PatternAnalysisResult
    {
        public bool IsSuspicious { get; set; }
        public string SuspiciousActivity { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public bool FlaggedForReview { get; set; }
    }
}