using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MongoDB.Driver;
using CrushIt.Data;
using CrushIt.API;
using CrushIt.API.Models;

namespace CrushIt.Core
{
    public static class ProgressSyncService
    {
        private static readonly object SyncLock = new object();
        private static bool isSyncing = false;
        private static DateTime lastSyncTime = DateTime.MinValue;
        private static DateTime lastErrorTime = DateTime.MinValue;
        private static string lastErrorMessage = "";
        private static int consecutiveErrors = 0;
        private const int MAX_CONSECUTIVE_ERRORS = 5;

        /// <summary>
        /// Sync progress on app launch - pulls latest data from server
        /// </summary>
        public static async Task<bool> SyncOnLaunchAsync(UserAccount currentUser, IMongoDatabase database, IApiClient? apiClient)
        {
            if (apiClient == null || string.IsNullOrEmpty(currentUser.UserId))
            {
                LogError("Sync on launch skipped: API client unavailable or no user ID");
                return false;
            }

            // Check if we've had too many consecutive errors
            if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
            {
                TimeSpan timeSinceLastError = DateTime.UtcNow - lastErrorTime;
                if (timeSinceLastError.TotalMinutes < 5)
                {
                    LogError($"Sync on launch skipped: Too many consecutive errors ({consecutiveErrors}). Waiting {5 - timeSinceLastError.TotalMinutes:F1} minutes.");
                    return false;
                }
                else
                {
                    // Reset error counter after cooldown period
                    consecutiveErrors = 0;
                }
            }

            lock (SyncLock)
            {
                if (isSyncing)
                    return false;
                isSyncing = true;
            }

            try
            {
                string deviceFingerprint = GenerateDeviceFingerprint();
                System.Diagnostics.Debug.WriteLine($"[Sync] Starting sync on launch for user {currentUser.UserId}");
                
                var serverProgress = await apiClient.GetServerProgressAsync(currentUser.UserId, deviceFingerprint);

                if (serverProgress == null)
                {
                    LogError("Sync on launch failed: Server returned null progress data");
                    return false;
                }

                // Merge server data with local data (server takes precedence)
                await MergeServerProgressAsync(currentUser, database, serverProgress);

                lastSyncTime = DateTime.UtcNow;
                consecutiveErrors = 0; // Reset error counter on success
                System.Diagnostics.Debug.WriteLine($"[Sync] Sync on launch completed successfully for user {currentUser.UserId}");
                return true;
            }
            catch (HttpRequestException ex)
            {
                LogError($"Sync on launch failed: Network error - {ex.Message}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                LogError($"Sync on launch failed: Request timeout - {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"Sync on launch failed: Authentication error - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Sync on launch failed: Unexpected error - {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            finally
            {
                lock (SyncLock)
                {
                    isSyncing = false;
                }
            }
        }

        /// <summary>
        /// Sync progress after level completion - uploads local changes to server
        /// </summary>
        public static async Task<bool> SyncAfterLevelAsync(UserAccount currentUser, IMongoDatabase database, IApiClient? apiClient)
        {
            if (apiClient == null || string.IsNullOrEmpty(currentUser.UserId))
            {
                LogError("Sync after level skipped: API client unavailable or no user ID");
                return false;
            }

            // Check if we've had too many consecutive errors
            if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
            {
                TimeSpan timeSinceLastError = DateTime.UtcNow - lastErrorTime;
                if (timeSinceLastError.TotalMinutes < 5)
                {
                    LogError($"Sync after level skipped: Too many consecutive errors ({consecutiveErrors}). Waiting {5 - timeSinceLastError.TotalMinutes:F1} minutes.");
                    return false;
                }
                else
                {
                    consecutiveErrors = 0;
                }
            }

            // Don't sync if we synced recently (within 30 seconds)
            if ((DateTime.UtcNow - lastSyncTime).TotalSeconds < 30)
                return true;

            lock (SyncLock)
            {
                if (isSyncing)
                    return false;
                isSyncing = true;
            }

            try
            {
                // Reload user from database to get latest local data
                var usersCollection = database.GetCollection<UserAccount>("users");
                var latestUser = await usersCollection.Find(u => u.Id == currentUser.Id).FirstOrDefaultAsync();
                
                if (latestUser == null)
                {
                    LogError("Sync after level failed: User not found in database");
                    return false;
                }

                var syncRequest = BuildSyncRequest(latestUser);
                System.Diagnostics.Debug.WriteLine($"[Sync] Starting sync after level for user {currentUser.UserId}");
                
                var syncResponse = await apiClient.SyncProgressAsync(syncRequest);

                if (syncResponse.Success && syncResponse.ServerProgress != null)
                {
                    // Merge any server changes back to local
                    await MergeServerProgressAsync(latestUser, database, syncResponse.ServerProgress);
                    
                    // Update current user reference
                    currentUser.CompletedLevels = latestUser.CompletedLevels;
                    currentUser.Gold = latestUser.Gold;
                    currentUser.HighestScore = latestUser.HighestScore;
                    currentUser.TotalMatches = latestUser.TotalMatches;
                    currentUser.Achievements = latestUser.Achievements;
                    currentUser.Username = latestUser.Username;
                }

                lastSyncTime = DateTime.UtcNow;
                consecutiveErrors = 0; // Reset error counter on success
                System.Diagnostics.Debug.WriteLine($"[Sync] Sync after level completed successfully for user {currentUser.UserId}");
                return syncResponse.Success;
            }
            catch (HttpRequestException ex)
            {
                LogError($"Sync after level failed: Network error - {ex.Message}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                LogError($"Sync after level failed: Request timeout - {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"Sync after level failed: Authentication error - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Sync after level failed: Unexpected error - {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            finally
            {
                lock (SyncLock)
                {
                    isSyncing = false;
                }
            }
        }

        /// <summary>
        /// Sync progress on app close - ensures all changes are uploaded
        /// </summary>
        public static async Task<bool> SyncOnCloseAsync(UserAccount currentUser, IMongoDatabase database, IApiClient? apiClient)
        {
            if (apiClient == null || string.IsNullOrEmpty(currentUser.UserId))
            {
                LogError("Sync on close skipped: API client unavailable or no user ID");
                return false;
            }

            // For close sync, we always try regardless of consecutive errors
            // This is a best-effort attempt to save data before closing

            lock (SyncLock)
            {
                if (isSyncing)
                    return false;
                isSyncing = true;
            }

            try
            {
                // Reload user from database to get latest local data
                var usersCollection = database.GetCollection<UserAccount>("users");
                var latestUser = await usersCollection.Find(u => u.Id == currentUser.Id).FirstOrDefaultAsync();
                
                if (latestUser == null)
                {
                    LogError("Sync on close failed: User not found in database");
                    return false;
                }

                var syncRequest = BuildSyncRequest(latestUser);
                System.Diagnostics.Debug.WriteLine($"[Sync] Starting sync on close for user {currentUser.UserId}");
                
                var syncResponse = await apiClient.SyncProgressAsync(syncRequest);

                lastSyncTime = DateTime.UtcNow;
                consecutiveErrors = 0; // Reset error counter on success
                System.Diagnostics.Debug.WriteLine($"[Sync] Sync on close completed successfully for user {currentUser.UserId}");
                return syncResponse.Success;
            }
            catch (HttpRequestException ex)
            {
                LogError($"Sync on close failed: Network error - {ex.Message}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                LogError($"Sync on close failed: Request timeout - {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogError($"Sync on close failed: Authentication error - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Sync on close failed: Unexpected error - {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            finally
            {
                lock (SyncLock)
                {
                    isSyncing = false;
                }
            }
        }

        private static ProgressSyncRequest BuildSyncRequest(UserAccount user)
        {
            return new ProgressSyncRequest
            {
                UserId = user.UserId,
                Email = user.Email,
                DeviceFingerprint = GenerateDeviceFingerprint(),
                CompletedLevels = user.CompletedLevels,
                Gold = user.Gold,
                HighestScore = user.HighestScore,
                TotalMatches = user.TotalMatches,
                Achievements = user.Achievements?.Select(a => new AchievementSyncData
                {
                    Type = a.Type.ToString(),
                    IsUnlocked = a.IsUnlocked,
                    UnlockedAt = a.UnlockedAt
                }).ToList(),
                Username = user.Username,
                ClientTimestamp = DateTime.UtcNow
            };
        }

        private static async Task MergeServerProgressAsync(UserAccount currentUser, IMongoDatabase database, ServerProgressData serverProgress)
        {
            var usersCollection = database.GetCollection<UserAccount>("users");
            
            bool needsUpdate = false;

            // Merge completed levels (union of both)
            if (serverProgress.CompletedLevels != null && serverProgress.CompletedLevels.Count > 0)
            {
                if (currentUser.CompletedLevels == null)
                    currentUser.CompletedLevels = new List<int>();
                
                foreach (var level in serverProgress.CompletedLevels)
                {
                    if (!currentUser.CompletedLevels.Contains(level))
                    {
                        currentUser.CompletedLevels.Add(level);
                        needsUpdate = true;
                    }
                }
            }

            // Take max values for numeric fields
            if (serverProgress.Gold > currentUser.Gold)
            {
                currentUser.Gold = serverProgress.Gold;
                needsUpdate = true;
            }

            if (serverProgress.HighestScore > currentUser.HighestScore)
            {
                currentUser.HighestScore = serverProgress.HighestScore;
                needsUpdate = true;
            }

            if (serverProgress.TotalMatches > currentUser.TotalMatches)
            {
                currentUser.TotalMatches = serverProgress.TotalMatches;
                needsUpdate = true;
            }

            // Merge achievements
            if (serverProgress.Achievements != null && serverProgress.Achievements.Count > 0)
            {
                if (currentUser.Achievements == null)
                    currentUser.Achievements = new List<Achievement>();

                foreach (var serverAchievement in serverProgress.Achievements)
                {
                    if (Enum.TryParse<AchievementType>(serverAchievement.Type, out var achievementType))
                    {
                        var localAchievement = currentUser.Achievements.FirstOrDefault(a => a.Type == achievementType);
                        
                        if (localAchievement == null && serverAchievement.IsUnlocked)
                        {
                            // Server has an achievement we don't have
                            var definition = AchievementDefinitions.GetAchievementByType(achievementType);
                            if (definition != null)
                            {
                                var newAchievement = new Achievement(achievementType, definition.Name, definition.Description, definition.IconColor, definition.GoldReward);
                                newAchievement.IsUnlocked = true;
                                newAchievement.UnlockedAt = serverAchievement.UnlockedAt ?? DateTime.UtcNow;
                                currentUser.Achievements.Add(newAchievement);
                                needsUpdate = true;
                            }
                        }
                        else if (localAchievement != null && serverAchievement.IsUnlocked && !localAchievement.IsUnlocked)
                        {
                            // Server has achievement unlocked, we don't
                            localAchievement.IsUnlocked = true;
                            localAchievement.UnlockedAt = serverAchievement.UnlockedAt ?? DateTime.UtcNow;
                            needsUpdate = true;
                        }
                    }
                }
            }

            // Update username if server has a newer one
            if (!string.IsNullOrEmpty(serverProgress.Username) && serverProgress.Username != currentUser.Username)
            {
                currentUser.Username = serverProgress.Username;
                needsUpdate = true;
            }

            // Save to database if there were changes
            if (needsUpdate)
            {
                var filter = Builders<UserAccount>.Filter.Eq(u => u.Id, currentUser.Id);
                var update = Builders<UserAccount>.Update
                    .Set(u => u.CompletedLevels, currentUser.CompletedLevels)
                    .Set(u => u.Gold, currentUser.Gold)
                    .Set(u => u.HighestScore, currentUser.HighestScore)
                    .Set(u => u.TotalMatches, currentUser.TotalMatches)
                    .Set(u => u.Achievements, currentUser.Achievements)
                    .Set(u => u.Username, currentUser.Username);
                
                await usersCollection.UpdateOneAsync(filter, update);
            }
        }

        private static string GenerateDeviceFingerprint()
        {
            return Environment.MachineName + "|" + Environment.OSVersion.VersionString;
        }

        private static void LogError(string message)
        {
            lastErrorTime = DateTime.UtcNow;
            lastErrorMessage = message;
            consecutiveErrors++;
            
            System.Diagnostics.Debug.WriteLine($"[Sync Error] {message}");
            System.Diagnostics.Debug.WriteLine($"[Sync Error] Consecutive errors: {consecutiveErrors}");
            
            // TODO: In production, you might want to:
            // 1. Log to a file
            // 2. Send to a monitoring service
            // 3. Show user notification if errors persist
        }

        public static string GetLastError()
        {
            return lastErrorMessage;
        }

        public static int GetConsecutiveErrorCount()
        {
            return consecutiveErrors;
        }

        public static void ResetErrorTracking()
        {
            consecutiveErrors = 0;
            lastErrorMessage = "";
            lastErrorTime = DateTime.MinValue;
        }
    }
}
