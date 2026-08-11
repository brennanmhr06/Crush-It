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

        // Social management methods
        public Task<SendFriendRequestResponse> SendFriendRequestAsync(SendFriendRequestRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Sending friend request from {request.UserId} to {request.FriendId}");

            return Task.FromResult(new SendFriendRequestResponse
            {
                Success = true,
                Message = "Mock friend request sent successfully",
                RequestId = Guid.NewGuid().ToString()
            });
        }

        public Task<AcceptFriendRequestResponse> AcceptFriendRequestAsync(AcceptFriendRequestRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Accepting friend request {request.RequestId} for user {request.UserId}");

            return Task.FromResult(new AcceptFriendRequestResponse
            {
                Success = true,
                Message = "Mock friend request accepted successfully",
                Friend = new FriendDto
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = request.UserId,
                    FriendId = "mock-friend-id",
                    FriendUsername = "MockFriend",
                    Status = FriendStatus.Accepted,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    TotalMatches = 50,
                    HighestScore = 5000,
                    LastPlayed = DateTime.UtcNow
                }
            });
        }

        public Task<DeclineFriendRequestResponse> DeclineFriendRequestAsync(DeclineFriendRequestRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Declining friend request {request.RequestId} for user {request.UserId}");

            return Task.FromResult(new DeclineFriendRequestResponse
            {
                Success = true,
                Message = "Mock friend request declined successfully"
            });
        }

        public Task<RemoveFriendResponse> RemoveFriendAsync(RemoveFriendRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Removing friend {request.FriendId} for user {request.UserId}");

            return Task.FromResult(new RemoveFriendResponse
            {
                Success = true,
                Message = "Mock friend removed successfully"
            });
        }

        public Task<SearchUsersResponse> SearchUsersAsync(SearchUsersRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Searching users with query '{request.Query}'");

            return Task.FromResult(new SearchUsersResponse
            {
                Success = true,
                Message = "Mock user search completed",
                Users = new List<UserProfileDto>
                {
                    new UserProfileDto
                    {
                        UserId = Guid.NewGuid().ToString(),
                        Username = "MockUser1",
                        TotalMatches = 100,
                        HighestScore = 10000,
                        Gold = 5000,
                        AchievementsUnlocked = 15,
                        CompletedLevels = 50,
                        IsOnline = true,
                        LastPlayed = DateTime.UtcNow
                    },
                    new UserProfileDto
                    {
                        UserId = Guid.NewGuid().ToString(),
                        Username = "MockUser2",
                        TotalMatches = 75,
                        HighestScore = 7500,
                        Gold = 3000,
                        AchievementsUnlocked = 10,
                        CompletedLevels = 35,
                        IsOnline = false,
                        LastPlayed = DateTime.UtcNow.AddDays(-1)
                    }
                },
                TotalCount = 2
            });
        }

        public Task<GetFriendsResponse> GetFriendsAsync(GetFriendsRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting friends for user {request.UserId}");

            return Task.FromResult(new GetFriendsResponse
            {
                Success = true,
                Message = "Mock friends retrieved successfully",
                Friends = new List<FriendDto>
                {
                    new FriendDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = request.UserId,
                        FriendId = "friend1-id",
                        FriendUsername = "Friend1",
                        Status = FriendStatus.Accepted,
                        CreatedAt = DateTime.UtcNow.AddDays(-30),
                        UpdatedAt = DateTime.UtcNow,
                        TotalMatches = 25,
                        HighestScore = 3000,
                        LastPlayed = DateTime.UtcNow
                    },
                    new FriendDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = request.UserId,
                        FriendId = "friend2-id",
                        FriendUsername = "Friend2",
                        Status = FriendStatus.Accepted,
                        CreatedAt = DateTime.UtcNow.AddDays(-15),
                        UpdatedAt = DateTime.UtcNow,
                        TotalMatches = 10,
                        HighestScore = 1500,
                        LastPlayed = DateTime.UtcNow.AddDays(-2)
                    }
                }
            });
        }

        public Task<GetFriendRequestsResponse> GetFriendRequestsAsync(GetFriendRequestsRequest request)
        {
            if (_enableLogging)
                Console.WriteLine($"[Mock API] Getting friend requests for user {request.UserId}");

            return Task.FromResult(new GetFriendRequestsResponse
            {
                Success = true,
                Message = "Mock friend requests retrieved",
                Requests = new List<FriendRequestDto>
                {
                    new FriendRequestDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        FromUserId = "requester1-id",
                        FromUsername = "Requester1",
                        ToUserId = request.UserId,
                        ToUsername = "CurrentUser",
                        Status = FriendStatus.Pending,
                        CreatedAt = DateTime.UtcNow.AddHours(-2),
                        ExpiresAt = DateTime.UtcNow.AddDays(7),
                        Message = "Hey, want to be friends?"
                    },
                    new FriendRequestDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        FromUserId = "requester2-id",
                        FromUsername = "Requester2",
                        ToUserId = request.UserId,
                        ToUsername = "CurrentUser",
                        Status = FriendStatus.Pending,
                        CreatedAt = DateTime.UtcNow.AddHours(-5),
                        ExpiresAt = DateTime.UtcNow.AddDays(7),
                        Message = "Let's play together!"
                    }
                }
            });
        }
    }
}