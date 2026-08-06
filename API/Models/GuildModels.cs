using System;
using System.Collections.Generic;
using CrushIt.Data;

namespace CrushIt.API.Models
{
    public class CreateGuildRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class CreateGuildResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
        public bool RequiresManualReview { get; set; }
    }

    public class JoinGuildRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class JoinGuildResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class LeaveGuildRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class LeaveGuildResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class GetGuildRequest
    {
        public string GuildId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class GetGuildResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class SearchGuildsRequest
    {
        public string Query { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int Limit { get; set; } = 50;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class SearchGuildsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<GuildDto> Guilds { get; set; } = new List<GuildDto>();
        public int TotalCount { get; set; }
    }

    public class GetUserGuildRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class GetUserGuildResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class UpdateGuildRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string? Description { get; set; }
        public GuildJoinStatus? JoinStatus { get; set; }
        public int? RequiredLevel { get; set; }
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class UpdateGuildResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class TransferLeadershipRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string NewLeaderId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class TransferLeadershipResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class PromoteMemberRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string MemberId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class PromoteMemberResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class DemoteMemberRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string MemberId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class DemoteMemberResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class KickMemberRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string MemberId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class KickMemberResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class SendGuildInvitationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string InviteeId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class SendGuildInvitationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string InvitationId { get; set; } = string.Empty;
    }

    public class AcceptGuildInvitationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string InvitationId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class AcceptGuildInvitationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GuildDto? Guild { get; set; }
    }

    public class DeclineGuildInvitationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string InvitationId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class DeclineGuildInvitationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class GetGuildInvitationsRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class GetGuildInvitationsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<GuildInvitationDto> Invitations { get; set; } = new List<GuildInvitationDto>();
    }

    public class GetTopGuildsRequest
    {
        public int Limit { get; set; } = 10;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class GetTopGuildsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<GuildDto> Guilds { get; set; } = new List<GuildDto>();
    }

    public class GetGuildRankRequest
    {
        public string GuildId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    public class GetGuildRankResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Rank { get; set; }
        public int TotalGuilds { get; set; }
    }

    // DTOs for transferring guild data over API
    public class GuildDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LeaderId { get; set; } = string.Empty;
        public string LeaderUsername { get; set; } = string.Empty;
        public List<GuildMemberDto> Members { get; set; } = new List<GuildMemberDto>();
        public GuildJoinStatus JoinStatus { get; set; }
        public int RequiredLevel { get; set; }
        public int TotalMemberScore { get; set; }
        public int TotalMemberMatches { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MaxMembers { get; set; }
    }

    public class GuildMemberDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public GuildRole Role { get; set; }
        public DateTime JoinedAt { get; set; }
        public int TotalMatches { get; set; }
        public int HighestScore { get; set; }
    }

    public class GuildInvitationDto
    {
        public string Id { get; set; } = string.Empty;
        public string GuildId { get; set; } = string.Empty;
        public string GuildName { get; set; } = string.Empty;
        public string InviterId { get; set; } = string.Empty;
        public string InviterUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}