using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace CrushIt.Data
{
    public enum AchievementType
    {
        FirstMatch,
        Level1Complete,
        Level5Complete,
        Level10Complete,
        Score1000,
        Score5000,
        Score10000,
        Gold100,
        Gold500,
        Gold1000,
        Combo3,
        Combo5,
        SquareMatch,
        TotalMatches100,
        TotalMatches500,
        TotalMatches1000
    }

    public class Achievement
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("type")]
        public AchievementType Type { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("unlockedAt")]
        public DateTime? UnlockedAt { get; set; }

        [BsonElement("isUnlocked")]
        public bool IsUnlocked { get; set; } = false;

        [BsonElement("iconColor")]
        public string IconColor { get; set; } = "#FFD700";

        [BsonElement("goldReward")]
        public int GoldReward { get; set; } = 0;

        [BsonElement("isClaimed")]
        public bool IsClaimed { get; set; } = false;

        public Achievement(AchievementType type, string name, string description, string iconColor = "#FFD700", int goldReward = 0)
        {
            Type = type;
            Name = name;
            Description = description;
            IconColor = iconColor;
            GoldReward = goldReward;
        }
    }

    public static class AchievementDefinitions
    {
        public static readonly Achievement[] AllAchievements = new Achievement[]
        {
            new Achievement(AchievementType.FirstMatch, "First Crush", "Make your first match", "#FF6B6B", 10),
            new Achievement(AchievementType.Level1Complete, "Beginner", "Complete Level 1", "#4ECDC4", 25),
            new Achievement(AchievementType.Level5Complete, "Rising Star", "Complete Level 5", "#45B7D1", 100),
            new Achievement(AchievementType.Level10Complete, "Master Crusher", "Complete Level 10", "#96CEB4", 250),
            new Achievement(AchievementType.Score1000, "Scorer", "Score 1000 points in a level", "#FFEAA7", 15),
            new Achievement(AchievementType.Score5000, "High Scorer", "Score 5000 points in a level", "#DFE6E9", 50),
            new Achievement(AchievementType.Score10000, "Legendary Scorer", "Score 10000 points in a level", "#FD79A8", 150),
            new Achievement(AchievementType.Gold100, "Gold Collector", "Earn 100 gold total", "#FDCB6E", 20),
            new Achievement(AchievementType.Gold500, "Gold Hoarder", "Earn 500 gold total", "#E17055", 75),
            new Achievement(AchievementType.Gold1000, "Gold Tycoon", "Earn 1000 gold total", "#D63031", 200),
            new Achievement(AchievementType.Combo3, "Combo Starter", "Get a 3-match combo", "#74B9FF", 30),
            new Achievement(AchievementType.Combo5, "Combo Master", "Get a 5-match combo", "#A29BFE", 80),
            new Achievement(AchievementType.SquareMatch, "Square Crusher", "Create a square match", "#55E6C1", 60),
            new Achievement(AchievementType.TotalMatches100, "Match Maker", "Make 100 total matches", "#FAB1A0", 40),
            new Achievement(AchievementType.TotalMatches500, "Match Expert", "Make 500 total matches", "#00CEC9", 120),
            new Achievement(AchievementType.TotalMatches1000, "Match Legend", "Make 1000 total matches", "#E84393", 300)
        };

        public static Achievement GetAchievementByType(AchievementType type)
        {
            foreach (var achievement in AllAchievements)
            {
                if (achievement.Type == type)
                    return achievement;
            }
            return null!;
        }
    }
}

