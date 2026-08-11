using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CrushIt.Data
{
    public class SocialRepository
    {
        private readonly IMongoCollection<Friend> _friendsCollection;
        private readonly IMongoCollection<FriendRequest> _friendRequestsCollection;
        private readonly IMongoCollection<UserAccount> _usersCollection;

        public SocialRepository(IMongoDatabase database)
        {
            _friendsCollection = database.GetCollection<Friend>("friends");
            _friendRequestsCollection = database.GetCollection<FriendRequest>("friendRequests");
            _usersCollection = database.GetCollection<UserAccount>("users");
        }

        public async Task<Friend?> SendFriendRequestAsync(string fromUserId, string fromUsername, string toUserId, string toUsername, string? message = null)
        {
            if (string.IsNullOrEmpty(fromUserId) || string.IsNullOrEmpty(toUserId))
                throw new ArgumentException("User IDs cannot be empty.");

            if (fromUserId == toUserId)
                throw new InvalidOperationException("Cannot send friend request to yourself.");

            // Check if already friends
            var existingFriend = await _friendsCollection
                .Find(f => (f.UserId == fromUserId && f.FriendId == toUserId) || 
                           (f.UserId == toUserId && f.FriendId == fromUserId))
                .FirstOrDefaultAsync();

            if (existingFriend != null)
                throw new InvalidOperationException("Already friends with this user.");

            // Check if there's a pending request
            var existingRequest = await _friendRequestsCollection
                .Find(r => (r.FromUserId == fromUserId && r.ToUserId == toUserId) ||
                           (r.FromUserId == toUserId && r.ToUserId == fromUserId))
                .FirstOrDefaultAsync();

            if (existingRequest != null && existingRequest.Status == FriendStatus.Pending)
                throw new InvalidOperationException("Friend request already pending.");

            var friendRequest = new FriendRequest
            {
                FromUserId = fromUserId,
                FromUsername = fromUsername,
                ToUserId = toUserId,
                ToUsername = toUsername,
                Status = FriendStatus.Pending,
                Message = message
            };

            await _friendRequestsCollection.InsertOneAsync(friendRequest);

            // Create the friend relationship (pending status)
            var friend = new Friend
            {
                UserId = fromUserId,
                FriendId = toUserId,
                FriendUsername = toUsername,
                Status = FriendStatus.Pending
            };

            await _friendsCollection.InsertOneAsync(friend);

            return friend;
        }

        public async Task<bool> AcceptFriendRequestAsync(string userId, string friendId)
        {
            var friend = await _friendsCollection
                .Find(f => f.UserId == friendId && f.FriendId == userId && f.Status == FriendStatus.Pending)
                .FirstOrDefaultAsync();

            if (friend == null)
                throw new ArgumentException("Friend request not found.");

            // Update the friend relationship to accepted
            var filter = Builders<Friend>.Filter.Eq(f => f.Id, friend.Id);
            var update = Builders<Friend>.Update
                .Set(f => f.Status, FriendStatus.Accepted)
                .Set(f => f.UpdatedAt, DateTime.UtcNow);

            await _friendsCollection.UpdateOneAsync(filter, update);

            // Create reciprocal relationship
            var user = await _usersCollection.Find(u => u.UserId == userId).FirstOrDefaultAsync();
            var friendUser = await _usersCollection.Find(u => u.UserId == friendId).FirstOrDefaultAsync();

            if (user != null && friendUser != null)
            {
                var reciprocalFriend = new Friend
                {
                    UserId = userId,
                    FriendId = friendId,
                    FriendUsername = friendUser.Username,
                    Status = FriendStatus.Accepted,
                    TotalMatches = friendUser.TotalMatches,
                    HighestScore = friendUser.HighestScore
                };

                await _friendsCollection.InsertOneAsync(reciprocalFriend);
            }

            // Delete the friend request
            await _friendRequestsCollection.DeleteOneAsync(r => 
                r.FromUserId == friendId && r.ToUserId == userId);

            return true;
        }

        public async Task<bool> DeclineFriendRequestAsync(string userId, string friendId)
        {
            var friend = await _friendsCollection
                .Find(f => f.UserId == friendId && f.FriendId == userId && f.Status == FriendStatus.Pending)
                .FirstOrDefaultAsync();

            if (friend == null)
                throw new ArgumentException("Friend request not found.");

            await _friendsCollection.DeleteOneAsync(f => f.Id == friend.Id);
            await _friendRequestsCollection.DeleteOneAsync(r => 
                r.FromUserId == friendId && r.ToUserId == userId);

            return true;
        }

        public async Task<bool> RemoveFriendAsync(string userId, string friendId)
        {
            // Remove both directions of the friendship
            var filter = Builders<Friend>.Filter.Or(
                Builders<Friend>.Filter.And(
                    Builders<Friend>.Filter.Eq(f => f.UserId, userId),
                    Builders<Friend>.Filter.Eq(f => f.FriendId, friendId)
                ),
                Builders<Friend>.Filter.And(
                    Builders<Friend>.Filter.Eq(f => f.UserId, friendId),
                    Builders<Friend>.Filter.Eq(f => f.FriendId, userId)
                )
            );

            var result = await _friendsCollection.DeleteManyAsync(filter);
            return result.DeletedCount > 0;
        }

        public async Task<bool> BlockUserAsync(string userId, string friendId)
        {
            // Remove existing friendship if exists
            await RemoveFriendAsync(userId, friendId);

            // Create blocked relationship
            var blockedFriend = new Friend
            {
                UserId = userId,
                FriendId = friendId,
                Status = FriendStatus.Blocked
            };

            await _friendsCollection.InsertOneAsync(blockedFriend);
            return true;
        }

        public async Task<List<Friend>> GetFriendsAsync(string userId)
        {
            return await _friendsCollection
                .Find(f => f.UserId == userId && f.Status == FriendStatus.Accepted)
                .ToListAsync();
        }

        public async Task<List<FriendRequest>> GetPendingFriendRequestsAsync(string userId)
        {
            return await _friendRequestsCollection
                .Find(r => r.ToUserId == userId && r.Status == FriendStatus.Pending)
                .ToListAsync();
        }

        public async Task<List<FriendRequest>> GetSentFriendRequestsAsync(string userId)
        {
            return await _friendRequestsCollection
                .Find(r => r.FromUserId == userId && r.Status == FriendStatus.Pending)
                .ToListAsync();
        }

        public async Task<List<UserProfile>> SearchUsersAsync(string query, string currentUserId, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<UserProfile>();

            var filter = Builders<UserAccount>.Filter.And(
                Builders<UserAccount>.Filter.Ne(u => u.UserId, currentUserId),
                Builders<UserAccount>.Filter.Or(
                    Builders<UserAccount>.Filter.Regex(u => u.Username, new MongoDB.Bson.BsonRegularExpression(query, "i")),
                    Builders<UserAccount>.Filter.Regex(u => u.Email, new MongoDB.Bson.BsonRegularExpression(query, "i"))
                )
            );

            return await _usersCollection
                .Find(filter)
                .Limit(limit)
                .Project(u => new UserProfile
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    TotalMatches = u.TotalMatches,
                    HighestScore = u.HighestScore,
                    Gold = u.Gold,
                    AchievementsUnlocked = u.Achievements != null ? u.Achievements.Count(a => a.IsUnlocked) : 0,
                    CompletedLevels = u.CompletedLevels != null ? u.CompletedLevels.Count : 0
                })
                .ToListAsync();
        }

        public async Task<UserProfile?> GetUserProfileAsync(string userId)
        {
            var user = await _usersCollection
                .Find(u => u.UserId == userId)
                .FirstOrDefaultAsync();

            if (user == null)
                return null;

            return new UserProfile
            {
                UserId = user.UserId,
                Username = user.Username,
                TotalMatches = user.TotalMatches,
                HighestScore = user.HighestScore,
                Gold = user.Gold,
                AchievementsUnlocked = user.Achievements?.Count(a => a.IsUnlocked) ?? 0,
                CompletedLevels = user.CompletedLevels?.Count ?? 0,
                LastPlayed = user.CreatedAt // Using CreatedAt as proxy since LastPlayed doesn't exist
            };
        }

        public async Task UpdateFriendStatsAsync(string userId, int totalMatches, int highestScore)
        {
            var filter = Builders<Friend>.Filter.Eq(f => f.FriendId, userId);
            var update = Builders<Friend>.Update
                .Set(f => f.TotalMatches, totalMatches)
                .Set(f => f.HighestScore, highestScore)
                .Set(f => f.LastPlayed, DateTime.UtcNow);

            await _friendsCollection.UpdateManyAsync(filter, update);
        }
    }
}