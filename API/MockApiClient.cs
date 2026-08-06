using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrushIt.Data;
using CrushIt.API.Models;

namespace CrushIt.API
{
    public class MockApiClient : IApiClient
    {
        private readonly Random _random = new Random();
        private readonly bool _enableLogging;

        public MockApiClient(bool enableLogging = true)
        {
            _enableLogging = enableLogging;
        }

        public Task<bool> ValidateScoreAsync(string userId, int level, int score, int moves, TimeSpan playTime)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Validating score for user {userId}: {score} points on level {level}");

            // Simulate validation - always return true for development
            return Task.FromResult(true);
        }

        public Task<bool> VerifyAchievementAsync(string userId, AchievementType achievementType, object proofData)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Verifying achievement {achievementType} for user {userId}");

            // Simulate achievement verification - always return true for development
            return Task.FromResult(true);
        }

        public Task<bool> ValidateSessionAsync(string userId, string sessionId, DateTime clientTime)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Validating session {sessionId} for user {userId}");

            // Simulate session validation - always return true for development
            return Task.FromResult(true);
        }

        public Task<bool> ReportGameplayPatternAsync(string userId, GameplayPattern pattern)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Reporting gameplay pattern for user {userId}");

            // Simulate pattern reporting - always return true for development
            return Task.FromResult(true);
        }

        public Task<AchievementValidationResult> GetAchievementValidationStatusAsync(string userId, AchievementType achievementType)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting achievement validation status for {achievementType}");

            return Task.FromResult(new AchievementValidationResult
            {
                IsValid = true,
                Reason = "Mock validation successful",
                ValidatedAt = DateTime.UtcNow,
                RequiresManualReview = false
            });
        }

        public Task<UserRegistrationResult> RegisterUserAsync(string email, string password, string deviceFingerprint)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Registering user with email {email}");

            var userId = Guid.NewGuid().ToString();
            var username = email.Split('@')[0];

            return Task.FromResult(new UserRegistrationResult
            {
                Success = true,
                UserId = userId,
                Username = username,
                Message = "Mock registration successful",
                RequiresManualReview = false,
                RiskLevel = "LOW"
            });
        }

        public Task<UserLoginResult> LoginUserAsync(string email, string password, string deviceFingerprint)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Logging in user with email {email}");

            var userId = Guid.NewGuid().ToString();
            var username = email.Split('@')[0];

            return Task.FromResult(new UserLoginResult
            {
                Success = true,
                UserId = userId,
                Username = username,
                Message = "Mock login successful",
                HasCompletedTutorial = false,
                AccountFlagged = false
            });
        }

        public Task<ProgressSyncResponse> SyncProgressAsync(ProgressSyncRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Syncing progress for user {request.UserId}");

            return Task.FromResult(new ProgressSyncResponse
            {
                Success = true,
                Message = "Mock sync successful",
                ServerProgress = new ServerProgressData
                {
                    CompletedLevels = request.CompletedLevels,
                    Gold = request.Gold,
                    HighestScore = request.HighestScore,
                    TotalMatches = request.TotalMatches,
                    Achievements = request.Achievements,
                    Username = request.Username,
                    ServerTimestamp = DateTime.UtcNow
                },
                ConflictsResolved = new List<string>()
            });
        }

        public Task<ServerProgressData?> GetServerProgressAsync(string userId, string deviceFingerprint)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting server progress for user {userId}");

            return Task.FromResult<ServerProgressData?>(new ServerProgressData
            {
                CompletedLevels = new List<int>(),
                Gold = 0,
                HighestScore = 0,
                TotalMatches = 0,
                Achievements = new List<AchievementSyncData>(),
                Username = string.Empty,
                ServerTimestamp = DateTime.UtcNow
            });
        }

        public Task<EventLogResponse> LogEventsAsync(EventLogRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Logging {request.Events?.Count ?? 0} events");

            return Task.FromResult(new EventLogResponse
            {
                Success = true,
                Message = "Mock events logged",
                EventsProcessed = request.Events?.Count ?? 0,
                FailedEvents = new List<string>()
            });
        }

        public Task<ErrorReportResponse> ReportErrorAsync(ErrorReport errorReport)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Reporting error: {errorReport.ErrorMessage}");

            return Task.FromResult(new ErrorReportResponse
            {
                Success = true,
                Message = "Mock error report received",
                ReportId = Guid.NewGuid().ToString(),
                RequiresFollowUp = false
            });
        }

        public Task<UsageStatsResponse> SubmitUsageStatisticsAsync(UsageStatsRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Submitting usage statistics");

            return Task.FromResult(new UsageStatsResponse
            {
                Success = true,
                Message = "Mock usage statistics received",
                StatisticsProcessed = request.Statistics?.Count ?? 0
            });
        }

        public Task<HealthCheckResponse> CheckApiHealthAsync()
        {
            if (_enableLogging)
                Console.WriteLine("[Mock API] Health check");

            return Task.FromResult(new HealthCheckResponse
            {
                IsHealthy = true,
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Dependencies = new Dictionary<string, string>(),
                Version = "1.0.0-mock",
                Region = "local"
            });
        }

        // Guild management methods
        public Task<CreateGuildResponse> CreateGuildAsync(CreateGuildRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Creating guild '{request.Name}' for user {request.UserId}");

            var guildId = Guid.NewGuid().ToString();

            return Task.FromResult(new CreateGuildResponse
            {
                Success = true,
                Message = "Mock guild created successfully",
                Guild = new GuildDto
                {
                    Id = guildId,
                    Name = request.Name,
                    Description = request.Description,
                    LeaderId = request.UserId,
                    LeaderUsername = "MockUser",
                    Members = new List<GuildMemberDto>
                    {
                        new GuildMemberDto
                        {
                            UserId = request.UserId,
                            Username = "MockUser",
                            Role = GuildRole.Leader,
                            JoinedAt = DateTime.UtcNow,
                            TotalMatches = 0,
                            HighestScore = 0
                        }
                    },
                    JoinStatus = GuildJoinStatus.Open,
                    RequiredLevel = 1,
                    TotalMemberScore = 0,
                    TotalMemberMatches = 0,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                },
                RequiresManualReview = false
            });
        }

        public Task<JoinGuildResponse> JoinGuildAsync(JoinGuildRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] User {request.UserId} joining guild {request.GuildId}");

            return Task.FromResult(new JoinGuildResponse
            {
                Success = true,
                Message = "Mock guild joined successfully",
                Guild = new GuildDto
                {
                    Id = request.GuildId,
                    Name = "Mock Guild",
                    Description = "Mock description",
                    LeaderId = "mock-leader",
                    LeaderUsername = "MockLeader",
                    Members = new List<GuildMemberDto>(),
                    JoinStatus = GuildJoinStatus.Open,
                    RequiredLevel = 1,
                    TotalMemberScore = 1000,
                    TotalMemberMatches = 100,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                }
            });
        }

        public Task<LeaveGuildResponse> LeaveGuildAsync(LeaveGuildRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] User {request.UserId} leaving guild");

            return Task.FromResult(new LeaveGuildResponse
            {
                Success = true,
                Message = "Mock guild left successfully"
            });
        }

        public Task<GetGuildResponse> GetGuildAsync(GetGuildRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting guild {request.GuildId}");

            return Task.FromResult(new GetGuildResponse
            {
                Success = true,
                Message = "Mock guild retrieved successfully",
                Guild = new GuildDto
                {
                    Id = request.GuildId,
                    Name = "Mock Guild",
                    Description = "Mock description",
                    LeaderId = "mock-leader",
                    LeaderUsername = "MockLeader",
                    Members = new List<GuildMemberDto>
                    {
                        new GuildMemberDto
                        {
                            UserId = "mock-leader",
                            Username = "MockLeader",
                            Role = GuildRole.Leader,
                            JoinedAt = DateTime.UtcNow,
                            TotalMatches = 100,
                            HighestScore = 5000
                        }
                    },
                    JoinStatus = GuildJoinStatus.Open,
                    RequiredLevel = 1,
                    TotalMemberScore = 5000,
                    TotalMemberMatches = 100,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                }
            });
        }

        public Task<SearchGuildsResponse> SearchGuildsAsync(SearchGuildsRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Searching guilds with query '{request.Query}'");

            return Task.FromResult(new SearchGuildsResponse
            {
                Success = true,
                Message = "Mock guild search completed",
                Guilds = new List<GuildDto>
                {
                    new GuildDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Mock Guild 1",
                        Description = "First mock guild",
                        LeaderId = "leader1",
                        LeaderUsername = "Leader1",
                        Members = new List<GuildMemberDto>(),
                        JoinStatus = GuildJoinStatus.Open,
                        RequiredLevel = 1,
                        TotalMemberScore = 1000,
                        TotalMemberMatches = 50,
                        CreatedAt = DateTime.UtcNow,
                        MaxMembers = 20
                    },
                    new GuildDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Mock Guild 2",
                        Description = "Second mock guild",
                        LeaderId = "leader2",
                        LeaderUsername = "Leader2",
                        Members = new List<GuildMemberDto>(),
                        JoinStatus = GuildJoinStatus.InviteOnly,
                        RequiredLevel = 5,
                        TotalMemberScore = 2500,
                        TotalMemberMatches = 75,
                        CreatedAt = DateTime.UtcNow,
                        MaxMembers = 20
                    }
                },
                TotalCount = 2
            });
        }

        public Task<GetUserGuildResponse> GetUserGuildAsync(GetUserGuildRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting guild for user {request.UserId}");

            return Task.FromResult(new GetUserGuildResponse
            {
                Success = true,
                Message = "Mock user guild retrieved",
                Guild = null // User not in a guild by default
            });
        }

        public Task<UpdateGuildResponse> UpdateGuildAsync(UpdateGuildRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Updating guild {request.GuildId}");

            return Task.FromResult(new UpdateGuildResponse
            {
                Success = true,
                Message = "Mock guild updated successfully",
                Guild = new GuildDto
                {
                    Id = request.GuildId,
                    Name = "Updated Guild",
                    Description = request.Description ?? "Updated description",
                    LeaderId = "leader",
                    LeaderUsername = "Leader",
                    Members = new List<GuildMemberDto>(),
                    JoinStatus = request.JoinStatus ?? GuildJoinStatus.Open,
                    RequiredLevel = request.RequiredLevel ?? 1,
                    TotalMemberScore = 1000,
                    TotalMemberMatches = 50,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                }
            });
        }

        public Task<TransferLeadershipResponse> TransferLeadershipAsync(TransferLeadershipRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Transferring leadership in guild {request.GuildId} to {request.NewLeaderId}");

            return Task.FromResult(new TransferLeadershipResponse
            {
                Success = true,
                Message = "Mock leadership transferred successfully",
                Guild = new GuildDto
                {
                    Id = request.GuildId,
                    Name = "Mock Guild",
                    Description = "Mock description",
                    LeaderId = request.NewLeaderId,
                    LeaderUsername = "NewLeader",
                    Members = new List<GuildMemberDto>(),
                    JoinStatus = GuildJoinStatus.Open,
                    RequiredLevel = 1,
                    TotalMemberScore = 1000,
                    TotalMemberMatches = 50,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                }
            });
        }

        public Task<PromoteMemberResponse> PromoteMemberAsync(PromoteMemberRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Promoting member {request.MemberId} in guild {request.GuildId}");

            return Task.FromResult(new PromoteMemberResponse
            {
                Success = true,
                Message = "Mock member promoted successfully",
                Guild = new GuildDto
                {
                    Id = request.GuildId,
                    Name = "Mock Guild",
                    Description = "Mock description",
                    LeaderId = "leader",
                    LeaderUsername = "Leader",
                    Members = new List<GuildMemberDto>(),
                    JoinStatus = GuildJoinStatus.Open,
                    RequiredLevel = 1,
                    TotalMemberScore = 1000,
                    TotalMemberMatches = 50,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                }
            });
        }

        public Task<DemoteMemberResponse> DemoteMemberAsync(DemoteMemberRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Demoting member {request.MemberId} in guild {request.GuildId}");

            return Task.FromResult(new DemoteMemberResponse
            {
                Success = true,
                Message = "Mock member demoted successfully",
                Guild = new GuildDto
                {
                    Id = request.GuildId,
                    Name = "Mock Guild",
                    Description = "Mock description",
                    LeaderId = "leader",
                    LeaderUsername = "Leader",
                    Members = new List<GuildMemberDto>(),
                    JoinStatus = GuildJoinStatus.Open,
                    RequiredLevel = 1,
                    TotalMemberScore = 1000,
                    TotalMemberMatches = 50,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                }
            });
        }

        public Task<KickMemberResponse> KickMemberAsync(KickMemberRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Kicking member {request.MemberId} from guild {request.GuildId}");

            return Task.FromResult(new KickMemberResponse
            {
                Success = true,
                Message = "Mock member kicked successfully",
                Guild = new GuildDto
                {
                    Id = request.GuildId,
                    Name = "Mock Guild",
                    Description = "Mock description",
                    LeaderId = "leader",
                    LeaderUsername = "Leader",
                    Members = new List<GuildMemberDto>(),
                    JoinStatus = GuildJoinStatus.Open,
                    RequiredLevel = 1,
                    TotalMemberScore = 1000,
                    TotalMemberMatches = 50,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                }
            });
        }

        public Task<SendGuildInvitationResponse> SendGuildInvitationAsync(SendGuildInvitationRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Sending guild invitation to {request.InviteeId}");

            return Task.FromResult(new SendGuildInvitationResponse
            {
                Success = true,
                Message = "Mock guild invitation sent successfully",
                InvitationId = Guid.NewGuid().ToString()
            });
        }

        public Task<AcceptGuildInvitationResponse> AcceptGuildInvitationAsync(AcceptGuildInvitationRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Accepting guild invitation {request.InvitationId}");

            return Task.FromResult(new AcceptGuildInvitationResponse
            {
                Success = true,
                Message = "Mock guild invitation accepted successfully",
                Guild = new GuildDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Mock Guild",
                    Description = "Mock description",
                    LeaderId = "leader",
                    LeaderUsername = "Leader",
                    Members = new List<GuildMemberDto>(),
                    JoinStatus = GuildJoinStatus.Open,
                    RequiredLevel = 1,
                    TotalMemberScore = 1000,
                    TotalMemberMatches = 50,
                    CreatedAt = DateTime.UtcNow,
                    MaxMembers = 20
                }
            });
        }

        public Task<DeclineGuildInvitationResponse> DeclineGuildInvitationAsync(DeclineGuildInvitationRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Declining guild invitation {request.InvitationId}");

            return Task.FromResult(new DeclineGuildInvitationResponse
            {
                Success = true,
                Message = "Mock guild invitation declined successfully"
            });
        }

        public Task<GetGuildInvitationsResponse> GetGuildInvitationsAsync(GetGuildInvitationsRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting guild invitations for user {request.UserId}");

            return Task.FromResult(new GetGuildInvitationsResponse
            {
                Success = true,
                Message = "Mock guild invitations retrieved",
                Invitations = new List<GuildInvitationDto>()
            });
        }

        public Task<GetTopGuildsResponse> GetTopGuildsAsync(GetTopGuildsRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting top {request.Limit} guilds");

            return Task.FromResult(new GetTopGuildsResponse
            {
                Success = true,
                Message = "Mock top guilds retrieved",
                Guilds = new List<GuildDto>
                {
                    new GuildDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Champion Guild",
                        Description = "Top ranked guild",
                        LeaderId = "champion",
                        LeaderUsername = "Champion",
                        Members = new List<GuildMemberDto>(),
                        JoinStatus = GuildJoinStatus.Open,
                        RequiredLevel = 10,
                        TotalMemberScore = 10000,
                        TotalMemberMatches = 500,
                        CreatedAt = DateTime.UtcNow,
                        MaxMembers = 20
                    },
                    new GuildDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Elite Guild",
                        Description = "Second ranked guild",
                        LeaderId = "elite",
                        LeaderUsername = "Elite",
                        Members = new List<GuildMemberDto>(),
                        JoinStatus = GuildJoinStatus.InviteOnly,
                        RequiredLevel = 5,
                        TotalMemberScore = 7500,
                        TotalMemberMatches = 300,
                        CreatedAt = DateTime.UtcNow,
                        MaxMembers = 20
                    }
                }
            });
        }

        public Task<GetGuildRankResponse> GetGuildRankAsync(GetGuildRankRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting rank for guild {request.GuildId}");

            return Task.FromResult(new GetGuildRankResponse
            {
                Success = true,
                Message = "Mock guild rank retrieved",
                Rank = 1,
                TotalGuilds = 100
            });
        }
    }
}