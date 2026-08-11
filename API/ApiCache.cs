using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CrushIt.API
{
    public class ApiCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly TimeSpan _defaultTtl;
        private readonly int _maxCacheSize;
        private int _currentCacheSize;

        public ApiCache(TimeSpan? defaultTtl = null, int maxCacheSize = 1000)
        {
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
            _maxCacheSize = maxCacheSize;
        }

        public T Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow < entry.ExpiresAt)
                {
                    entry.LastAccessed = DateTime.UtcNow;
                    if (entry.Value is T typedValue)
                    {
                        return typedValue;
                    }
                }
                else
                {
                    _cache.TryRemove(key, out _);
                    _currentCacheSize--;
                }
            }
            return default!;
        }

        public void Set<T>(string key, T value, TimeSpan? ttl = null)
        {
            var expiresAt = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
            var entry = new CacheEntry
            {
                Value = value!,
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                Size = EstimateSize(value)
            };

            // Check if we need to make space
            if (_currentCacheSize + entry.Size > _maxCacheSize)
            {
                EvictLeastRecentlyUsed();
            }

            _cache.AddOrUpdate(key, entry, (k, existing) => entry);
            _currentCacheSize = CalculateTotalCacheSize();
        }

        public bool Remove(string key)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                _currentCacheSize -= entry.Size;
                return true;
            }
            return false;
        }

        public bool Exists(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow < entry.ExpiresAt)
                {
                    return true;
                }
                else
                {
                    _cache.TryRemove(key, out _);
                    _currentCacheSize -= entry.Size;
                }
            }
            return false;
        }

        public void Clear()
        {
            _cache.Clear();
            _currentCacheSize = 0;
        }

        public void ClearExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _cache)
            {
                if (now >= kvp.Value.ExpiresAt)
                {
                    _cache.TryRemove(kvp.Key, out _);
                }
            }
            _currentCacheSize = CalculateTotalCacheSize();
        }

        public CacheStatistics GetStatistics()
        {
            ClearExpired();
            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                TotalSize = _currentCacheSize,
                HitRate = CalculateHitRate(),
                OldestEntry = GetOldestEntryAge(),
                NewestEntry = GetNewestEntryAge()
            };
        }

        private void EvictLeastRecentlyUsed()
        {
            string? oldestKey = null;
            DateTime oldestAccess = DateTime.UtcNow;

            foreach (var kvp in _cache)
            {
                if (kvp.Value.LastAccessed < oldestAccess)
                {
                    oldestAccess = kvp.Value.LastAccessed;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != null)
            {
                _cache.TryRemove(oldestKey, out var removed);
                if (removed != null)
                {
                    _currentCacheSize -= removed.Size;
                }
            }
        }

        private int CalculateTotalCacheSize()
        {
            int total = 0;
            foreach (var entry in _cache.Values)
            {
                total += entry.Size;
            }
            return total;
        }

        private int EstimateSize<T>(T value)
        {
            // Rough estimation of object size in bytes
            if (value == null) return 0;
            
            // This is a simplified estimation
            int size = System.Text.Json.JsonSerializer.Serialize(value).Length;
            return Math.Max(size, 1); // At least 1 byte
        }

        private double CalculateHitRate()
        {
            // Simplified hit rate calculation
            // In a real implementation, you'd track hits and misses
            return 0.0;
        }

        private TimeSpan GetOldestEntryAge()
        {
            DateTime oldest = DateTime.UtcNow;
            bool found = false;

            foreach (var entry in _cache.Values)
            {
                if (entry.CreatedAt < oldest)
                {
                    oldest = entry.CreatedAt;
                    found = true;
                }
            }

            return found ? DateTime.UtcNow - oldest : TimeSpan.Zero;
        }

        private TimeSpan GetNewestEntryAge()
        {
            DateTime newest = DateTime.MinValue;
            bool found = false;

            foreach (var entry in _cache.Values)
            {
                if (entry.CreatedAt > newest)
                {
                    newest = entry.CreatedAt;
                    found = true;
                }
            }

            return found ? DateTime.UtcNow - newest : TimeSpan.Zero;
        }

        private class CacheEntry
        {
            public object Value { get; set; } = null!;
            public DateTime CreatedAt { get; set; }
            public DateTime LastAccessed { get; set; }
            public DateTime ExpiresAt { get; set; }
            public int Size { get; set; }
        }
    }

    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public int TotalSize { get; set; }
        public double HitRate { get; set; }
        public TimeSpan OldestEntry { get; set; }
        public TimeSpan NewestEntry { get; set; }
    }

    public static class CacheKeys
    {
        public static class User
        {
            public static string Profile(string userId) => $"user:profile:{userId}";
            public static string Achievements(string userId) => $"user:achievements:{userId}";
            public static string Progress(string userId) => $"user:progress:{userId}";
        }

        public static class Game
        {
            public static string Leaderboard(int level) => $"game:leaderboard:{level}";
            public static string DailyChallenge() => $"game:daily_challenge:{DateTime.UtcNow:yyyy-MM-dd}";
            public static string Configuration() => $"game:configuration";
        }

        public static class System
        {
            public static string HealthCheck() => $"system:health";
            public static string ApiStatus() => $"system:status";
        }

        public static class Social
        {
            public static string Search(string query, int limit) => $"social:search:{query}:{limit}";
            public static string Friends(string userId) => $"social:friends:{userId}";
            public static string FriendRequests(string userId) => $"social:friend_requests:{userId}";
        }
    }
}
