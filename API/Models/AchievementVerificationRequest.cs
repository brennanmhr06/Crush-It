using CrushIt.Data;

namespace CrushIt.API.Models
{
    public class AchievementVerificationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public AchievementType AchievementType { get; set; }
        public DateTime UnlockTime { get; set; }
        public object ProofData { get; set; } = new object();
        public string SessionId { get; set; } = string.Empty;
        public int LevelContext { get; set; }
        public string GamestateHash { get; set; } = string.Empty;
    }

    public class AchievementVerificationResponse
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public bool RequiresManualReview { get; set; }
        public DateTime ValidatedAt { get; set; }
    }
}