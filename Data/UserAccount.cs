using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace CrushIt.Data
{
    public class UserAccount
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("password")]
        public string Password { get; set; } = string.Empty;

        [BsonElement("hasCompletedTutorial")]
        public bool HasCompletedTutorial { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("completedLevels")]
        public List<int> CompletedLevels { get; set; } = new List<int>();

        [BsonElement("gold")]
        public int Gold { get; set; } = 0;

        [BsonElement("achievements")]
        public List<Achievement> Achievements { get; set; } = new List<Achievement>();

        [BsonElement("totalMatches")]
        public int TotalMatches { get; set; } = 0;

        [BsonElement("highestScore")]
        public int HighestScore { get; set; } = 0;

        [BsonElement("friendCount")]
        public int FriendCount { get; set; } = 0;
    }
}

