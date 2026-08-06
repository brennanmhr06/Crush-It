using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace CrushIt.Data
{
    public enum GuildRole
    {
        Member,
        Officer,
        Leader
    }

    public enum GuildJoinStatus
    {
        Open,
        InviteOnly,
        Closed
    }

    [BsonIgnoreExtraElements]
    public class GuildMember
    {
        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("role")]
        public GuildRole Role { get; set; } = GuildRole.Member;

        [BsonElement("joinedAt")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("totalMatches")]
        public int TotalMatches { get; set; } = 0;

        [BsonElement("highestScore")]
        public int HighestScore { get; set; } = 0;
    }

    [BsonIgnoreExtraElements]
    public class Guild
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("leaderId")]
        public string LeaderId { get; set; } = string.Empty;

        [BsonElement("leaderUsername")]
        public string LeaderUsername { get; set; } = string.Empty;

        [BsonElement("members")]
        public List<GuildMember> Members { get; set; } = new List<GuildMember>();

        [BsonElement("joinStatus")]
        public GuildJoinStatus JoinStatus { get; set; } = GuildJoinStatus.Open;

        [BsonElement("requiredLevel")]
        public int RequiredLevel { get; set; } = 1;

        [BsonElement("totalMemberScore")]
        public int TotalMemberScore { get; set; } = 0;

        [BsonElement("totalMemberMatches")]
        public int TotalMemberMatches { get; set; } = 0;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("memberCount")]
        public int MemberCount => Members.Count;

        [BsonElement("maxMembers")]
        public int MaxMembers { get; set; } = 20;

        public bool IsFull => Members.Count >= MaxMembers;

        public bool CanJoin(UserAccount user)
        {
            if (IsFull) return false;
            if (JoinStatus == GuildJoinStatus.Closed) return false;
            if (JoinStatus == GuildJoinStatus.InviteOnly) return false;
            if (user.CompletedLevels == null || user.CompletedLevels.Count < RequiredLevel) return false;
            return true;
        }
    }

    [BsonIgnoreExtraElements]
    public class GuildInvitation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("guildId")]
        public string GuildId { get; set; } = string.Empty;

        [BsonElement("guildName")]
        public string GuildName { get; set; } = string.Empty;

        [BsonElement("inviterId")]
        public string InviterId { get; set; } = string.Empty;

        [BsonElement("inviterUsername")]
        public string InviterUsername { get; set; } = string.Empty;

        [BsonElement("inviteeId")]
        public string InviteeId { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    }
}
