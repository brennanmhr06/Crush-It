using System;
using System.Threading.Tasks;
using CrushIt.Data;
using CrushIt.API.Models;

namespace CrushIt.API
{
    public interface IApiClient
    {
        Task<bool> ValidateScoreAsync(string userId, int level, int score, int moves, TimeSpan playTime);
        Task<bool> VerifyAchievementAsync(string userId, AchievementType achievementType, object proofData);
        Task<bool> ValidateSessionAsync(string userId, string sessionId, DateTime clientTime);
        Task<bool> ReportGameplayPatternAsync(string userId, GameplayPattern pattern);
        Task<AchievementValidationResult> GetAchievementValidationStatusAsync(string userId, AchievementType achievementType);
        Task<UserRegistrationResult> RegisterUserAsync(string email, string password, string deviceFingerprint);
        Task<UserLoginResult> LoginUserAsync(string email, string password, string deviceFingerprint);
        Task<ProgressSyncResponse> SyncProgressAsync(ProgressSyncRequest request);
        Task<ServerProgressData?> GetServerProgressAsync(string userId, string deviceFingerprint);

        // Analytics methods
        Task<EventLogResponse> LogEventsAsync(EventLogRequest request);
        Task<ErrorReportResponse> ReportErrorAsync(ErrorReport errorReport);
        Task<UsageStatsResponse> SubmitUsageStatisticsAsync(UsageStatsRequest request);
        Task<HealthCheckResponse> CheckApiHealthAsync();

        // Guild management methods
        Task<CreateGuildResponse> CreateGuildAsync(CreateGuildRequest request);
        Task<JoinGuildResponse> JoinGuildAsync(JoinGuildRequest request);
        Task<LeaveGuildResponse> LeaveGuildAsync(LeaveGuildRequest request);
        Task<GetGuildResponse> GetGuildAsync(GetGuildRequest request);
        Task<SearchGuildsResponse> SearchGuildsAsync(SearchGuildsRequest request);
        Task<GetUserGuildResponse> GetUserGuildAsync(GetUserGuildRequest request);
        Task<UpdateGuildResponse> UpdateGuildAsync(UpdateGuildRequest request);
        Task<TransferLeadershipResponse> TransferLeadershipAsync(TransferLeadershipRequest request);
        Task<PromoteMemberResponse> PromoteMemberAsync(PromoteMemberRequest request);
        Task<DemoteMemberResponse> DemoteMemberAsync(DemoteMemberRequest request);
        Task<KickMemberResponse> KickMemberAsync(KickMemberRequest request);
        Task<SendGuildInvitationResponse> SendGuildInvitationAsync(SendGuildInvitationRequest request);
        Task<AcceptGuildInvitationResponse> AcceptGuildInvitationAsync(AcceptGuildInvitationRequest request);
        Task<DeclineGuildInvitationResponse> DeclineGuildInvitationAsync(DeclineGuildInvitationRequest request);
        Task<GetGuildInvitationsResponse> GetGuildInvitationsAsync(GetGuildInvitationsRequest request);
        Task<GetTopGuildsResponse> GetTopGuildsAsync(GetTopGuildsRequest request);
        Task<GetGuildRankResponse> GetGuildRankAsync(GetGuildRankRequest request);
    }

    public class GameplayPattern
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
    }

    public class AchievementValidationResult
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime ValidatedAt { get; set; }
        public bool RequiresManualReview { get; set; }
    }

    public class ScoreValidationResult
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int AdjustedScore { get; set; }
        public bool FlaggedForReview { get; set; }
    }

    public class UserRegistrationResult
    {
        public bool Success { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool RequiresManualReview { get; set; }
        public string RiskLevel { get; set; } = "LOW";
    }

    public class UserLoginResult
    {
        public bool Success { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool HasCompletedTutorial { get; set; }
        public bool AccountFlagged { get; set; }
    }
}