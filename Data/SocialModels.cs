using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace CrushIt.Data
{
    public enum FriendStatus
    {
        Pending,
        Accepted,
        Blocked
    }

    [BsonIgnoreExtraElements]
    public class Friend
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("friendId")]
        public string FriendId { get; set; } = string.Empty;

        [BsonElement("friendUsername")]
        public string FriendUsername { get; set; } = string.Empty;

        [BsonElement("status")]
        public FriendStatus Status { get; set; } = FriendStatus.Pending;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("totalMatches")]
        public int TotalMatches { get; set; } = 0;

        [BsonElement("highestScore")]
        public int HighestScore { get; set; } = 0;

        [BsonElement("lastPlayed")]
        public DateTime? LastPlayed { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class FriendRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("fromUserId")]
        public string FromUserId { get; set; } = string.Empty;

        [BsonElement("fromUsername")]
        public string FromUsername { get; set; } = string.Empty;

        [BsonElement("toUserId")]
        public string ToUserId { get; set; } = string.Empty;

        [BsonElement("toUsername")]
        public string ToUsername { get; set; } = string.Empty;

        [BsonElement("status")]
        public FriendStatus Status { get; set; } = FriendStatus.Pending;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

        [BsonElement("message")]
        public string? Message { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class UserProfile
    {
        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("totalMatches")]
        public int TotalMatches { get; set; } = 0;

        [BsonElement("highestScore")]
        public int HighestScore { get; set; } = 0;

        [BsonElement("gold")]
        public int Gold { get; set; } = 0;

        [BsonElement("achievementsUnlocked")]
        public int AchievementsUnlocked { get; set; } = 0;

        [BsonElement("completedLevels")]
        public int CompletedLevels { get; set; } = 0;

        [BsonElement("isOnline")]
        public bool IsOnline { get; set; } = false;

        [BsonElement("lastPlayed")]
        public DateTime? LastPlayed { get; set; }
    }
}