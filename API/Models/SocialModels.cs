using System;
using System.Collections.Generic;
using CrushIt.Data;

namespace CrushIt.API.Models
{
    public class SendFriendRequestRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class SendFriendRequestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
    }

    public class AcceptFriendRequestRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class AcceptFriendRequestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public FriendDto? Friend { get; set; }
    }

    public class DeclineFriendRequestRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class DeclineFriendRequestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RemoveFriendRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class RemoveFriendResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SearchUsersRequest
    {
        public string Query { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int Limit { get; set; } = 20;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class SearchUsersResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<UserProfileDto> Users { get; set; } = new List<UserProfileDto>();
        public int TotalCount { get; set; }
    }

    public class GetFriendsRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class GetFriendsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<FriendDto> Friends { get; set; } = new List<FriendDto>();
    }

    public class GetFriendRequestsRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class GetFriendRequestsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<FriendRequestDto> Requests { get; set; } = new List<FriendRequestDto>();
    }

    // DTOs for transferring social data over API
    public class FriendDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
        public string FriendUsername { get; set; } = string.Empty;
        public FriendStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int TotalMatches { get; set; }
        public int HighestScore { get; set; }
        public DateTime? LastPlayed { get; set; }
    }

    public class FriendRequestDto
    {
        public string Id { get; set; } = string.Empty;
        public string FromUserId { get; set; } = string.Empty;
        public string FromUsername { get; set; } = string.Empty;
        public string ToUserId { get; set; } = string.Empty;
        public string ToUsername { get; set; } = string.Empty;
        public FriendStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? Message { get; set; }
    }

    public class UserProfileDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int TotalMatches { get; set; }
        public int HighestScore { get; set; }
        public int Gold { get; set; }
        public int AchievementsUnlocked { get; set; }
        public int CompletedLevels { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastPlayed { get; set; }
    }
}