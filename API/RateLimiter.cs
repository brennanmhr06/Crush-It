using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CrushIt.API
{
    public class RateLimiter
    {
        private readonly ConcurrentDictionary<string, RateLimitEntry> _rateLimitEntries;
        private readonly int _maxRequests;
        private readonly TimeSpan _timeWindow;

        public RateLimiter(int maxRequests = 100, TimeSpan? timeWindow = null)
        {
            _maxRequests = maxRequests;
            _timeWindow = timeWindow ?? TimeSpan.FromMinutes(1);
            _rateLimitEntries = new ConcurrentDictionary<string, RateLimitEntry>();
        }

        public bool TryRequest(string identifier)
        {
            var now = DateTime.UtcNow;
            var entry = _rateLimitEntries.GetOrAdd(identifier, _ => new RateLimitEntry());

            lock (entry.Lock)
            {
                // Clean up old requests outside the time window
                entry.RequestTimestamps.RemoveAll(timestamp => 
                    now - timestamp > _timeWindow);

                // Check if under the limit
                if (entry.RequestTimestamps.Count < _maxRequests)
                {
                    entry.RequestTimestamps.Add(now);
                    return true;
                }

                return false;
            }
        }

        public int GetRemainingRequests(string identifier)
        {
            var now = DateTime.UtcNow;
            if (_rateLimitEntries.TryGetValue(identifier, out var entry))
            {
                lock (entry.Lock)
                {
                    entry.RequestTimestamps.RemoveAll(timestamp => 
                        now - timestamp > _timeWindow);
                    return _maxRequests - entry.RequestTimestamps.Count;
                }
            }
            return _maxRequests;
        }

        public TimeSpan GetResetTime(string identifier)
        {
            var now = DateTime.UtcNow;
            if (_rateLimitEntries.TryGetValue(identifier, out var entry))
            {
                lock (entry.Lock)
                {
                    if (entry.RequestTimestamps.Count > 0)
                    {
                        var oldestRequest = entry.RequestTimestamps[0];
                        var resetTime = oldestRequest + _timeWindow;
                        return resetTime - now;
                    }
                }
            }
            return TimeSpan.Zero;
        }

        public void Reset(string identifier)
        {
            _rateLimitEntries.TryRemove(identifier, out _);
        }

        public void ResetAll()
        {
            _rateLimitEntries.Clear();
        }

        private class RateLimitEntry
        {
            public List<DateTime> RequestTimestamps { get; } = new List<DateTime>();
            public object Lock { get; } = new object();
        }
    }

    public class RateLimitInfo
    {
        public bool IsAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public TimeSpan ResetTime { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
