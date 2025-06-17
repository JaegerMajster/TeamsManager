using System;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Konfiguracja endpointów Microsoft Graph API.
    /// Centralizuje wszystkie URL-e i konfigurację Graph API.
    /// </summary>
    public class GraphApiConfiguration
    {
        /// <summary>
        /// Bazowy URL Microsoft Graph API
        /// </summary>
        public string BaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";

        /// <summary>
        /// Endpointy Graph API
        /// </summary>
        public GraphEndpoints Endpoints { get; set; } = new GraphEndpoints();

        /// <summary>
        /// Scope'y Graph API
        /// </summary>
        public GraphScopes Scopes { get; set; } = new GraphScopes();

        /// <summary>
        /// Timeout dla żądań Graph API w sekundach
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maksymalna liczba prób ponowienia żądania
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Czy respektować rate limiting Graph API
        /// </summary>
        public bool RespectRateLimit { get; set; } = true;
    }

    /// <summary>
    /// Definicje endpointów Microsoft Graph API
    /// </summary>
    public class GraphEndpoints
    {
        // User endpoints
        public string Me => "/me";
        public string Users => "/users";
        public string User(string userId) => $"/users/{userId}";
        public string UserByUpn(string upn) => $"/users/{upn}";
        public string UserLicenseDetails(string userId) => $"/users/{userId}/licenseDetails";
        public string UserMemberOf(string userId) => $"/users/{userId}/memberOf";
        public string UserRevokeSignInSessions(string userId) => $"/users/{userId}/revokeSignInSessions";
        public string UserJoinedTeams(string userUpn) => $"/users/{userUpn}/joinedTeams";

        // Team endpoints  
        public string Teams => "/teams";
        public string Team(string teamId) => $"/teams/{teamId}";
        public string TeamMembers(string teamId) => $"/teams/{teamId}/members";
        public string TeamMember(string teamId, string membershipId) => $"/teams/{teamId}/members/{membershipId}";
        public string TeamChannels(string teamId) => $"/teams/{teamId}/channels";
        public string TeamChannel(string teamId, string channelId) => $"/teams/{teamId}/channels/{channelId}";
        public string TeamArchive(string teamId) => $"/teams/{teamId}/archive";
        public string TeamUnarchive(string teamId) => $"/teams/{teamId}/unarchive";

        // Group endpoints
        public string Groups => "/groups";
        public string Group(string groupId) => $"/groups/{groupId}";
        public string GroupMembers(string groupId) => $"/groups/{groupId}/members";
        public string GroupOwners(string groupId) => $"/groups/{groupId}/owners";

        // License endpoints
        public string SubscribedSkus => "/subscribedSkus";
        public string UserAssignLicense(string userId) => $"/users/{userId}/assignLicense";

        // Batch endpoint
        public string Batch => "/$batch";

        // Organization endpoints
        public string Organization => "/organization";
        public string Applications => "/applications";
        public string Application(string appId) => $"/applications/{appId}";

        // Mail endpoints
        public string Mail => "/me/messages";
        public string SendMail => "/me/sendMail";
        public string SendMailOnBehalfOf(string userId) => $"/users/{userId}/sendMail";
        public string CreateDraftEmail => "/me/messages";
        public string MailMessage(string messageId) => $"/me/messages/{messageId}";
    }

    /// <summary>
    /// Definicje scope'ów Microsoft Graph API
    /// </summary>
    public class GraphScopes
    {
        /// <summary>
        /// Scope dla aplikacji (client credentials flow)
        /// </summary>
        public string[] ClientCredentials => new[] { "https://graph.microsoft.com/.default" };

        /// <summary>
        /// Scope'y dla delegated permissions (on-behalf-of flow)
        /// </summary>
        public string[] DelegatedPermissions => new[]
        {
            "https://graph.microsoft.com/User.Read",
            "https://graph.microsoft.com/Group.ReadWrite.All",
            "https://graph.microsoft.com/Team.ReadBasic.All",
            "https://graph.microsoft.com/TeamSettings.ReadWrite.All",
            "https://graph.microsoft.com/Channel.ReadBasic.All",
            "https://graph.microsoft.com/ChannelSettings.ReadWrite.All"
        };

        /// <summary>
        /// Scope'y tylko do odczytu
        /// </summary>
        public string[] ReadOnlyScopes => new[]
        {
            "https://graph.microsoft.com/User.Read",
            "https://graph.microsoft.com/User.ReadBasic.All",
            "https://graph.microsoft.com/Team.ReadBasic.All",
            "https://graph.microsoft.com/Channel.ReadBasic.All"
        };

        /// <summary>
        /// Scope'y do zarządzania użytkownikami
        /// </summary>
        public string[] UserManagementScopes => new[]
        {
            "https://graph.microsoft.com/User.ReadWrite.All",
            "https://graph.microsoft.com/Directory.ReadWrite.All"
        };

        /// <summary>
        /// Scope'y do zarządzania zespołami
        /// </summary>
        public string[] TeamManagementScopes => new[]
        {
            "https://graph.microsoft.com/Group.ReadWrite.All",
            "https://graph.microsoft.com/TeamSettings.ReadWrite.All",
            "https://graph.microsoft.com/ChannelSettings.ReadWrite.All"
        };
    }
} 