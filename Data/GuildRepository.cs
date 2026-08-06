using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CrushIt.Data
{
    public class GuildRepository
    {
        private readonly IMongoCollection<Guild> _guildsCollection;
        private readonly IMongoCollection<UserAccount> _usersCollection;
        private readonly IMongoCollection<GuildInvitation> _invitationsCollection;

        public GuildRepository(IMongoDatabase database)
        {
            _guildsCollection = database.GetCollection<Guild>("guilds");
            _usersCollection = database.GetCollection<UserAccount>("users");
            _invitationsCollection = database.GetCollection<GuildInvitation>("guildInvitations");
        }

        public async Task<Guild?> CreateGuildAsync(string name, string description, UserAccount leader)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 30)
                throw new ArgumentException("Guild name must be between 3 and 30 characters.");

            if (string.IsNullOrEmpty(description) || description.Length > 150)
                throw new ArgumentException("Description must be less than 150 characters.");

            if (!string.IsNullOrEmpty(leader.GuildId))
                throw new InvalidOperationException("User is already in a guild.");

            var existingGuild = await _guildsCollection.Find(g => g.Name == name).FirstOrDefaultAsync();
            if (existingGuild != null)
                throw new InvalidOperationException("A guild with this name already exists.");

            var guild = new Guild
            {
                Name = name,
                Description = description,
                LeaderId = leader.UserId,
                LeaderUsername = leader.Username,
                Members = new List<GuildMember>
                {
                    new GuildMember
                    {
                        UserId = leader.UserId,
                        Username = leader.Username,
                        Role = GuildRole.Leader,
                        JoinedAt = DateTime.UtcNow,
                        TotalMatches = leader.TotalMatches,
                        HighestScore = leader.HighestScore
                    }
                },
                JoinStatus = GuildJoinStatus.Open,
                RequiredLevel = 1,
                TotalMemberScore = leader.HighestScore,
                TotalMemberMatches = leader.TotalMatches
            };

            await _guildsCollection.InsertOneAsync(guild);

            var userFilter = Builders<UserAccount>.Filter.Eq(u => u.Id, leader.Id);
            var userUpdate = Builders<UserAccount>.Update
                .Set(u => u.GuildId, guild.Id)
                .Set(u => u.GuildName, guild.Name)
                .Set(u => u.GuildRole, GuildRole.Leader);

            await _usersCollection.UpdateOneAsync(userFilter, userUpdate);

            return guild;
        }

        public async Task<bool> JoinGuildAsync(string guildId, UserAccount user)
        {
            if (!string.IsNullOrEmpty(user.GuildId))
                throw new InvalidOperationException("User is already in a guild.");

            var guild = await _guildsCollection.Find(g => g.Id == guildId).FirstOrDefaultAsync();
            if (guild == null)
                throw new ArgumentException("Guild not found.");

            if (!guild.CanJoin(user))
                throw new InvalidOperationException("Cannot join this guild.");

            if (guild.Members.Any(m => m.UserId == user.UserId))
                throw new InvalidOperationException("User is already a member of this guild.");

            var newMember = new GuildMember
            {
                UserId = user.UserId,
                Username = user.Username,
                Role = GuildRole.Member,
                JoinedAt = DateTime.UtcNow,
                TotalMatches = user.TotalMatches,
                HighestScore = user.HighestScore
            };

            var guildFilter = Builders<Guild>.Filter.Eq(g => g.Id, guildId);
            var guildUpdate = Builders<Guild>.Update
                .Push(g => g.Members, newMember)
                .Inc(g => g.TotalMemberScore, user.HighestScore)
                .Inc(g => g.TotalMemberMatches, user.TotalMatches);

            await _guildsCollection.UpdateOneAsync(guildFilter, guildUpdate);

            var userFilter = Builders<UserAccount>.Filter.Eq(u => u.Id, user.Id);
            var userUpdate = Builders<UserAccount>.Update
                .Set(u => u.GuildId, guild.Id)
                .Set(u => u.GuildName, guild.Name)
                .Set(u => u.GuildRole, GuildRole.Member);

            await _usersCollection.UpdateOneAsync(userFilter, userUpdate);

            return true;
        }

        public async Task<bool> LeaveGuildAsync(UserAccount user)
        {
            if (string.IsNullOrEmpty(user.GuildId))
                throw new InvalidOperationException("User is not in a guild.");

            var guild = await _guildsCollection.Find(g => g.Id == user.GuildId).FirstOrDefaultAsync();
            if (guild == null)
                throw new ArgumentException("Guild not found.");

            if (user.GuildRole == GuildRole.Leader)
                throw new InvalidOperationException("Guild leader cannot leave. Transfer leadership first.");

            var guildFilter = Builders<Guild>.Filter.Eq(g => g.Id, user.GuildId);
            var guildUpdate = Builders<Guild>.Update.PullFilter(g => g.Members, m => m.UserId == user.UserId);

            await _guildsCollection.UpdateOneAsync(guildFilter, guildUpdate);

            var userFilter = Builders<UserAccount>.Filter.Eq(u => u.Id, user.Id);
            var userUpdate = Builders<UserAccount>.Update
                .Set(u => u.GuildId, null)
                .Set(u => u.GuildName, null)
                .Set(u => u.GuildRole, GuildRole.Member);

            await _usersCollection.UpdateOneAsync(userFilter, userUpdate);

            return true;
        }

        public async Task<bool> TransferLeadershipAsync(string guildId, string currentLeaderId, string newLeaderId)
        {
            var guild = await _guildsCollection.Find(g => g.Id == guildId).FirstOrDefaultAsync();
            if (guild == null)
                throw new ArgumentException("Guild not found.");

            if (guild.LeaderId != currentLeaderId)
                throw new InvalidOperationException("Only the current leader can transfer leadership.");

            var newMember = guild.Members.FirstOrDefault(m => m.UserId == newLeaderId);
            if (newMember == null)
                throw new ArgumentException("New leader must be a guild member.");

            var guildFilter = Builders<Guild>.Filter.Eq(g => g.Id, guildId);
            var guildUpdate = Builders<Guild>.Update
                .Set(g => g.LeaderId, newLeaderId)
                .Set(g => g.LeaderUsername, newMember.Username);

            await _guildsCollection.UpdateOneAsync(guildFilter, guildUpdate);

            var oldLeaderFilter = Builders<UserAccount>.Filter.Eq(u => u.UserId, currentLeaderId);
            var oldLeaderUpdate = Builders<UserAccount>.Update.Set(u => u.GuildRole, GuildRole.Officer);
            await _usersCollection.UpdateOneAsync(oldLeaderFilter, oldLeaderUpdate);

            var newLeaderFilter = Builders<UserAccount>.Filter.Eq(u => u.UserId, newLeaderId);
            var newLeaderUpdate = Builders<UserAccount>.Update.Set(u => u.GuildRole, GuildRole.Leader);
            await _usersCollection.UpdateOneAsync(newLeaderFilter, newLeaderUpdate);

            return true;
        }

        public async Task<bool> PromoteMemberAsync(string guildId, string leaderId, string memberId)
        {
            var guild = await _guildsCollection.Find(g => g.Id == guildId).FirstOrDefaultAsync();
            if (guild == null)
                throw new ArgumentException("Guild not found.");

            if (guild.LeaderId != leaderId)
                throw new InvalidOperationException("Only the leader can promote members.");

            var member = guild.Members.FirstOrDefault(m => m.UserId == memberId);
            if (member == null)
                throw new ArgumentException("Member not found.");

            if (member.Role == GuildRole.Leader)
                throw new InvalidOperationException("Cannot promote the leader.");

            if (member.Role == GuildRole.Officer)
                throw new InvalidOperationException("Member is already an officer.");

            // Update member role in guild
            var updatedMembers = guild.Members.Select(m => 
                m.UserId == memberId ? new GuildMember
                {
                    UserId = m.UserId,
                    Username = m.Username,
                    Role = GuildRole.Officer,
                    JoinedAt = m.JoinedAt,
                    TotalMatches = m.TotalMatches,
                    HighestScore = m.HighestScore
                } : m).ToList();

            var guildFilter = Builders<Guild>.Filter.Eq(g => g.Id, guildId);
            var guildUpdate = Builders<Guild>.Update.Set(g => g.Members, updatedMembers);
            await _guildsCollection.UpdateOneAsync(guildFilter, guildUpdate);

            var userFilter = Builders<UserAccount>.Filter.Eq(u => u.UserId, memberId);
            var userUpdate = Builders<UserAccount>.Update.Set(u => u.GuildRole, GuildRole.Officer);
            await _usersCollection.UpdateOneAsync(userFilter, userUpdate);

            return true;
        }

        public async Task<bool> KickMemberAsync(string guildId, string leaderId, string memberId)
        {
            var guild = await _guildsCollection.Find(g => g.Id == guildId).FirstOrDefaultAsync();
            if (guild == null)
                throw new ArgumentException("Guild not found.");

            if (guild.LeaderId != leaderId)
                throw new InvalidOperationException("Only the leader can kick members.");

            if (memberId == leaderId)
                throw new InvalidOperationException("Cannot kick yourself.");

            var member = guild.Members.FirstOrDefault(m => m.UserId == memberId);
            if (member == null)
                throw new ArgumentException("Member not found.");

            var guildFilter = Builders<Guild>.Filter.Eq(g => g.Id, guildId);
            var guildUpdate = Builders<Guild>.Update.PullFilter(g => g.Members, m => m.UserId == memberId);
            await _guildsCollection.UpdateOneAsync(guildFilter, guildUpdate);

            var userFilter = Builders<UserAccount>.Filter.Eq(u => u.UserId, memberId);
            var userUpdate = Builders<UserAccount>.Update
                .Set(u => u.GuildId, null)
                .Set(u => u.GuildName, null)
                .Set(u => u.GuildRole, GuildRole.Member);

            await _usersCollection.UpdateOneAsync(userFilter, userUpdate);

            return true;
        }

        public async Task<Guild?> GetGuildByIdAsync(string guildId)
        {
            return await _guildsCollection.Find(g => g.Id == guildId).FirstOrDefaultAsync();
        }

        public async Task<Guild?> GetGuildByNameAsync(string name)
        {
            return await _guildsCollection.Find(g => g.Name == name).FirstOrDefaultAsync();
        }

        public async Task<List<Guild>> GetSearchableGuildsAsync(UserAccount user)
        {
            var userLevel = user.CompletedLevels?.Count ?? 0;
            return await _guildsCollection
                .Find(g => (g.JoinStatus == GuildJoinStatus.Open || g.JoinStatus == GuildJoinStatus.InviteOnly))
                .Limit(50)
                .ToListAsync();
        }

        public async Task<List<Guild>> GetTopGuildsByScoreAsync(int limit = 10)
        {
            return await _guildsCollection
                .Find(_ => true)
                .SortByDescending(g => g.TotalMemberScore)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<Guild>> GetTopGuildsByMembersAsync(int limit = 10)
        {
            return await _guildsCollection
                .Find(_ => true)
                .SortByDescending(g => g.MemberCount)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> UpdateGuildSettingsAsync(string guildId, string leaderId, string? description = null, GuildJoinStatus? joinStatus = null, int? requiredLevel = null)
        {
            var guild = await _guildsCollection.Find(g => g.Id == guildId).FirstOrDefaultAsync();
            if (guild == null)
                throw new ArgumentException("Guild not found.");

            if (guild.LeaderId != leaderId)
                throw new InvalidOperationException("Only the leader can update guild settings.");

            var updates = new List<UpdateDefinition<Guild>>();

            if (description != null && description.Length <= 150)
                updates.Add(Builders<Guild>.Update.Set(g => g.Description, description));

            if (joinStatus.HasValue)
                updates.Add(Builders<Guild>.Update.Set(g => g.JoinStatus, joinStatus.Value));

            if (requiredLevel.HasValue && requiredLevel.Value >= 1 && requiredLevel.Value <= 40)
                updates.Add(Builders<Guild>.Update.Set(g => g.RequiredLevel, requiredLevel.Value));

            if (updates.Count == 0)
                return false;

            var combinedUpdate = Builders<Guild>.Update.Combine(updates);
            var filter = Builders<Guild>.Filter.Eq(g => g.Id, guildId);
            await _guildsCollection.UpdateOneAsync(filter, combinedUpdate);

            return true;
        }

    }
}
