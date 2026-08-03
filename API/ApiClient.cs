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

        public ApiClient(string apiBaseUrl, string apiKey)
        {
            _apiBaseUrl = apiBaseUrl;
            _apiKey = apiKey;
            _config = ApiConfiguration.Default;
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

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/validate-score", content);

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

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/verify-achievement", content);

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

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/validate-session", content);

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

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/report-pattern", content);

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
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/achievement-status/{userId}/{achievementType}");

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

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/register", content);

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

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/login", content);

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
    }
}