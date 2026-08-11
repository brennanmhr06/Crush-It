using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CrushIt.Data;
using CrushIt.API.Models;

namespace CrushIt.API
{
    public class ApiClient : IApiClient
    {
        private readonly string _apiBaseUrl;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly ApiConfiguration _config;
        private readonly RetryPolicy _retryPolicy;
        private readonly RateLimiter _rateLimiter;
        private readonly ApiCache _cache;

        public ApiClient(string apiBaseUrl, string apiKey)
        {
            _apiBaseUrl = apiBaseUrl;
            _apiKey = apiKey;
            _config = ApiConfiguration.Default;
            _retryPolicy = new RetryPolicy(
                maxRetries: _config.MaxRetries,
                backoffMultiplier: _config.RetryBackoffMultiplier);
            _rateLimiter = new RateLimiter(
                maxRequests: _config.RateLimitMaxRequests, 
                TimeSpan.FromMinutes(_config.RateLimitTimeWindowMinutes));
            _cache = new ApiCache(
                TimeSpan.FromMinutes(_config.CacheTtlMinutes), 
                _config.MaxCacheSize);
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds)
            };
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CrushItGame/1.0");
        }

        public async Task<bool> ValidateScoreAsync(string userId, int level, int score, int moves, TimeSpan playTime)
        {
            try
            {
                // Apply rate limiting
                if (!_rateLimiter.TryRequest(userId))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for user {userId}");
                    return true; // Fail-open
                }

                var request = new ScoreValidationRequest
                {
                    UserId = userId,
                    Level = level,
                    Score = score,
                    Moves = moves,
                    PlayTime = playTime,
                    ClientTimestamp = DateTime.UtcNow,
                    SessionId = GenerateSessionId(),
                    Checksum = GenerateChecksum(userId, level, score, moves)
                };

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/validate-score", content));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Score validation failed: {response.StatusCode}");
                    return false;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<ScoreValidationResponse>(responseJson);

                return result?.IsValid ?? false;
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Score validation error: {ex.Message}");
                return true;
            }
        }

        public async Task<bool> VerifyAchievementAsync(string userId, AchievementType achievementType, object proofData)
        {
            try
            {
                if (!_rateLimiter.TryRequest(userId))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for user {userId}");
                    return true; // Fail-open
                }

                var request = new AchievementVerificationRequest
                {
                    UserId = userId,
                    AchievementType = achievementType,
                    UnlockTime = DateTime.UtcNow,
                    ProofData = proofData,
                    SessionId = GenerateSessionId(),
                    LevelContext = GetCurrentLevel(),
                    GamestateHash = GenerateGameStateHash()
                };

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/verify-achievement", content));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Achievement verification failed: {response.StatusCode}");
                    return false;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<AchievementVerificationResponse>(responseJson);

                return result?.IsValid ?? false;
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Achievement verification error: {ex.Message}");
                return true;
            }
        }

        public async Task<bool> ValidateSessionAsync(string userId, string sessionId, DateTime clientTime)
        {
            try
            {
                if (!_rateLimiter.TryRequest(userId))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for user {userId}");
                    return true; // Fail-open
                }

                var request = new SessionValidationRequest
                {
                    UserId = userId,
                    SessionId = sessionId,
                    ClientTime = clientTime,
                    DeviceFingerprint = GetDeviceFingerprint(),
                    IpAddress = GetLocalIpAddress()
                };

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/validate-session", content));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Session validation failed: {response.StatusCode}");
                    return false;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<SessionValidationResponse>(responseJson);


                if (result != null && Math.Abs(result.TimeDifference.TotalMinutes) > 5)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Time manipulation detected: {result.TimeDifference}");
                    return false;
                }

                return result?.IsValid ?? false;
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Session validation error: {ex.Message}");
                return true;
            }
        }

        public async Task<bool> ReportGameplayPatternAsync(string userId, GameplayPattern pattern)
        {
            try
            {
                if (!_rateLimiter.TryRequest(userId))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for user {userId}");
                    return true; // Fail-open
                }

                var report = new GameplayPatternReport
                {
                    UserId = userId,
                    Level = pattern.Level,
                    StartTime = pattern.StartTime,
                    EndTime = pattern.EndTime,
                    TotalMoves = pattern.TotalMoves,
                    TotalMatches = pattern.TotalMatches,
                    AverageMoveTime = pattern.AverageMoveTime,
                    MaxCombo = pattern.MaxCombo,
                    RapidMovesCount = pattern.RapidMovesCount,
                    ImpossibleMovesCount = pattern.ImpossibleMovesCount,
                    PatternScore = CalculatePatternScore(pattern),
                    RiskLevel = DetermineRiskLevel(pattern)
                };

                var json = System.Text.Json.JsonSerializer.Serialize(report);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/report-pattern", content));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Pattern reporting failed: {response.StatusCode}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Pattern reporting error: {ex.Message}");
                return true;
            }
        }

        public async Task<AchievementValidationResult> GetAchievementValidationStatusAsync(string userId, AchievementType achievementType)
        {
            try
            {
                if (!_rateLimiter.TryRequest(userId))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for user {userId}");
                    return new AchievementValidationResult { IsValid = true, Reason = "Rate limit exceeded - client accepted" };
                }

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.GetAsync($"{_apiBaseUrl}/achievement-status/{userId}/{achievementType}"));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Achievement status check failed: {response.StatusCode}");
                    return new AchievementValidationResult
                    {
                        IsValid = true,
                        Reason = "API unavailable - client accepted",
                        ValidatedAt = DateTime.UtcNow,
                        RequiresManualReview = false
                    };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<AchievementVerificationResponse>(responseJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return new AchievementValidationResult
                {
                    IsValid = result?.IsValid ?? true,
                    Reason = result?.Reason ?? "No validation data",
                    ValidatedAt = result?.ValidatedAt ?? DateTime.UtcNow,
                    RequiresManualReview = result?.RequiresManualReview ?? false
                };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Achievement status check error: {ex.Message}");
                return new AchievementValidationResult
                {
                    IsValid = true,
                    Reason = "API error - client accepted",
                    ValidatedAt = DateTime.UtcNow,
                    RequiresManualReview = false
                };
            }
        }

        public async Task<UserRegistrationResult> RegisterUserAsync(string email, string password, string deviceFingerprint)
        {
            try
            {
                // Rate limit by email
                if (!_rateLimiter.TryRequest(email))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for email {email}");
                    return new UserRegistrationResult { Success = false, Message = "Rate limit exceeded" };
                }

                var request = new UserRegistrationRequest
                {
                    Email = email,
                    Password = password,
                    DeviceFingerprint = deviceFingerprint,
                    IpAddress = GetLocalIpAddress(),
                    ClientTimestamp = DateTime.UtcNow,
                    UserAgent = "CrushItGame/1.0"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/register", content));

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<UserRegistrationResponse>(responseJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null || !result.Success)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Registration failed: {result?.Message ?? "Unknown error"}");
                    return new UserRegistrationResult
                    {
                        Success = false,
                        Message = result?.Message ?? "Registration failed",
                        RiskLevel = "LOW"
                    };
                }

                return new UserRegistrationResult
                {
                    Success = result.Success,
                    UserId = result.UserId,
                    Username = result.Username,
                    Message = result.Message,
                    RequiresManualReview = result.RequiresManualReview,
                    RiskLevel = result.RiskLevel
                };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Registration	error: {ex.Message}");
                return new UserRegistrationResult
                {
                    Success = false,
                    Message = "API unavailable - try offline registration",
                    RiskLevel = "LOW"
                };
            }
        }

        public async Task<UserLoginResult> LoginUserAsync(string email, string password, string deviceFingerprint)
        {
            try
            {
                // Rate limit by email
                if (!_rateLimiter.TryRequest(email))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for email {email}");
                    return new UserLoginResult { Success = false, Message = "Rate limit exceeded" };
                }

                var request = new UserLoginRequest
                {
                    Email = email,
                    Password = password,
                    DeviceFingerprint = deviceFingerprint,
                    IpAddress = GetLocalIpAddress(),
                    ClientTimestamp = DateTime.UtcNow,
                    UserAgent = "CrushItGame/1.0"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/login", content));

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<UserLoginResponse>(responseJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null || !result.Success)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Login failed: {result?.Message ?? "Unknown error"}");
                    return new UserLoginResult
                    {
                        Success = false,
                        Message = result?.Message ?? "Login failed"
                    };
                }

                return new UserLoginResult
                {
                    Success = result.Success,
                    UserId = result.UserId,
                    Username = result.Username,
                    Message = result.Message,
                    HasCompletedTutorial = result.HasCompletedTutorial,
                    AccountFlagged = result.AccountFlagged
                };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Login error: {ex.Message}");
                return new UserLoginResult
                {
                    Success = false,
                    Message = "API unavailable - try offline login"
                };
            }
        }

        public async Task<ProgressSyncResponse> SyncProgressAsync(ProgressSyncRequest request)
        {
            try
            {
                if (!_rateLimiter.TryRequest(request.UserId))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for user {request.UserId}");
                    return new ProgressSyncResponse { Success = false, Message = "Rate limit exceeded" };
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/sync/progress", content));

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<ProgressSyncResponse>(responseJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine("Sync failed: null response");
                    return new ProgressSyncResponse
                    {
                        Success = false,
                        Message = "Sync failed - null response"
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Sync error: {ex.Message}");
                return new ProgressSyncResponse
                {
                    Success = false,
                    Message = "API unavailable - sync failed"
                };
            }
        }

        public async Task<ServerProgressData?> GetServerProgressAsync(string userId, string deviceFingerprint)
        {
            try
            {
                if (!_rateLimiter.TryRequest(userId))
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Rate limit exceeded for user {userId}");
                    return null;
                }

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.GetAsync($"{_apiBaseUrl}/user/progress?userId={userId}&deviceFingerprint={deviceFingerprint}"));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Get server progress failed: {response.StatusCode}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<ServerProgressData>(responseJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return result;
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Get server progress error: {ex.Message}");
                return null;
            }
        }

        private string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private string GenerateChecksum(string userId, int level, int score, int moves)
        {

            var data = $"{userId}|{level}|{score}|{moves}|{_apiKey}";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash).Substring(0, 16);
            }
        }

        private int GetCurrentLevel()
        {

            return 1;
        }

        private string GenerateGameStateHash()
        {

            return Guid.NewGuid().ToString("N");
        }

        private string GetDeviceFingerprint()
        {

            return Environment.MachineName + "|" + Environment.OSVersion.VersionString;
        }

        private string GetLocalIpAddress()
        {

            return "127.0.0.1";
        }

        private double CalculatePatternScore(GameplayPattern pattern)
        {
            double score = 100;


            score -= pattern.RapidMovesCount * 2;


            score -= pattern.ImpossibleMovesCount * 10;


            if (pattern.AverageMoveTime > 500 && pattern.AverageMoveTime < 2000)
                score += 10;


            if (pattern.MaxCombo > 20 && pattern.AverageMoveTime < 300)
                score -= 15;

            return Math.Max(0, Math.Min(100, score));
        }

        private string DetermineRiskLevel(GameplayPattern pattern)
        {
            double patternScore = CalculatePatternScore(pattern);

            if (patternScore < 30) return "HIGH";
            if (patternScore < 60) return "MEDIUM";
            return "LOW";
        }

        public async Task<EventLogResponse> LogEventsAsync(EventLogRequest request)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/analytics/events", content));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Event logging failed: {response.StatusCode}");
                    return new EventLogResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<EventLogResponse>(responseJson);

                return result ?? new EventLogResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Event logging error: {ex.Message}");
                return new EventLogResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<ErrorReportResponse> ReportErrorAsync(ErrorReport errorReport)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(errorReport);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/analytics/error", content));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Error reporting failed: {response.StatusCode}");
                    return new ErrorReportResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<ErrorReportResponse>(responseJson);

                return result ?? new ErrorReportResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Error reporting error: {ex.Message}");
                return new ErrorReportResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<UsageStatsResponse> SubmitUsageStatisticsAsync(UsageStatsRequest request)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.PostAsync($"{_apiBaseUrl}/analytics/usage", content));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Usage statistics submission failed: {response.StatusCode}");
                    return new UsageStatsResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<UsageStatsResponse>(responseJson);

                return result ?? new UsageStatsResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Usage statistics error: {ex.Message}");
                return new UsageStatsResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<HealthCheckResponse> CheckApiHealthAsync()
        {
            try
            {
                // Check cache first
                var cacheKey = CacheKeys.System.HealthCheck();
                var cached = _cache.Get<HealthCheckResponse>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                var response = await _retryPolicy.ExecuteWithRetryAsync(() => 
                    _httpClient.GetAsync($"{_apiBaseUrl}/health"));

                if (!response.IsSuccessStatusCode)
                {
                    if (_config.EnableLogging)
                        Console.WriteLine($"Health check failed: {response.StatusCode}");
                    return new HealthCheckResponse { IsHealthy = false, Status = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<HealthCheckResponse>(responseJson);

                if (result != null)
                {
                    // Cache the result for 30 seconds
                    _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));
                }

                return result ?? new HealthCheckResponse { IsHealthy = false, Status = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Health check error: {ex.Message}");
                return new HealthCheckResponse { IsHealthy = false, Status = ex.Message };
            }
        }

        public ApiCache GetCache()
        {
            return _cache;
        }

        public RateLimiter GetRateLimiter()
        {
            return _rateLimiter;
        }

        // Social management methods
        public async Task<SendFriendRequestResponse> SendFriendRequestAsync(SendFriendRequestRequest request)
        {
            try
            {
                if (!_rateLimiter.TryRequest(request.UserId))
                {
                    return new SendFriendRequestResponse { Success = false, Message = "Rate limit exceeded" };
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() =>
                    _httpClient.PostAsync($"{_apiBaseUrl}/social/send-friend-request", content));

                if (!response.IsSuccessStatusCode)
                {
                    return new SendFriendRequestResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<SendFriendRequestResponse>(responseJson);

                return result ?? new SendFriendRequestResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Send friend request error: {ex.Message}");
                return new SendFriendRequestResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<AcceptFriendRequestResponse> AcceptFriendRequestAsync(AcceptFriendRequestRequest request)
        {
            try
            {
                if (!_rateLimiter.TryRequest(request.UserId))
                {
                    return new AcceptFriendRequestResponse { Success = false, Message = "Rate limit exceeded" };
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() =>
                    _httpClient.PostAsync($"{_apiBaseUrl}/social/accept-friend-request", content));

                if (!response.IsSuccessStatusCode)
                {
                    return new AcceptFriendRequestResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<AcceptFriendRequestResponse>(responseJson);

                return result ?? new AcceptFriendRequestResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Accept friend request error: {ex.Message}");
                return new AcceptFriendRequestResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<DeclineFriendRequestResponse> DeclineFriendRequestAsync(DeclineFriendRequestRequest request)
        {
            try
            {
                if (!_rateLimiter.TryRequest(request.UserId))
                {
                    return new DeclineFriendRequestResponse { Success = false, Message = "Rate limit exceeded" };
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() =>
                    _httpClient.PostAsync($"{_apiBaseUrl}/social/decline-friend-request", content));

                if (!response.IsSuccessStatusCode)
                {
                    return new DeclineFriendRequestResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<DeclineFriendRequestResponse>(responseJson);

                return result ?? new DeclineFriendRequestResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Decline friend request error: {ex.Message}");
                return new DeclineFriendRequestResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<RemoveFriendResponse> RemoveFriendAsync(RemoveFriendRequest request)
        {
            try
            {
                if (!_rateLimiter.TryRequest(request.UserId))
                {
                    return new RemoveFriendResponse { Success = false, Message = "Rate limit exceeded" };
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() =>
                    _httpClient.PostAsync($"{_apiBaseUrl}/social/remove-friend", content));

                if (!response.IsSuccessStatusCode)
                {
                    return new RemoveFriendResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<RemoveFriendResponse>(responseJson);

                return result ?? new RemoveFriendResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Remove friend error: {ex.Message}");
                return new RemoveFriendResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<SearchUsersResponse> SearchUsersAsync(SearchUsersRequest request)
        {
            try
            {
                var cacheKey = CacheKeys.Social.Search(request.Query, request.Limit);
                var cached = _cache.Get<SearchUsersResponse>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() =>
                    _httpClient.PostAsync($"{_apiBaseUrl}/social/search-users", content));

                if (!response.IsSuccessStatusCode)
                {
                    return new SearchUsersResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<SearchUsersResponse>(responseJson);

                if (result != null)
                {
                    _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
                }

                return result ?? new SearchUsersResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Search users error: {ex.Message}");
                return new SearchUsersResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<GetFriendsResponse> GetFriendsAsync(GetFriendsRequest request)
        {
            try
            {
                var cacheKey = CacheKeys.Social.Friends(request.UserId);
                var cached = _cache.Get<GetFriendsResponse>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() =>
                    _httpClient.PostAsync($"{_apiBaseUrl}/social/get-friends", content));

                if (!response.IsSuccessStatusCode)
                {
                    return new GetFriendsResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<GetFriendsResponse>(responseJson);

                if (result != null)
                {
                    _cache.Set(cacheKey, result, TimeSpan.FromMinutes(3));
                }

                return result ?? new GetFriendsResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Get friends error: {ex.Message}");
                return new GetFriendsResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task<GetFriendRequestsResponse> GetFriendRequestsAsync(GetFriendRequestsRequest request)
        {
            try
            {
                var cacheKey = CacheKeys.Social.FriendRequests(request.UserId);
                var cached = _cache.Get<GetFriendRequestsResponse>(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                var json = System.Text.Json.JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _retryPolicy.ExecuteWithRetryAsync(() =>
                    _httpClient.PostAsync($"{_apiBaseUrl}/social/get-friend-requests", content));

                if (!response.IsSuccessStatusCode)
                {
                    return new GetFriendRequestsResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = System.Text.Json.JsonSerializer.Deserialize<GetFriendRequestsResponse>(responseJson);

                if (result != null)
                {
                    _cache.Set(cacheKey, result, TimeSpan.FromMinutes(3));
                }

                return result ?? new GetFriendRequestsResponse { Success = false, Message = "Failed to parse response" };
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                    Console.WriteLine($"Get friend requests error: {ex.Message}");
                return new GetFriendRequestsResponse { Success = false, Message = ex.Message };
            }
        }
    }
}
