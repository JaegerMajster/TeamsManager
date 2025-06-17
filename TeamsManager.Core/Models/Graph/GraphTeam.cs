using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Reprezentuje zespół Microsoft Teams z Graph API z zachowaniem kompatybilności z lokalnym modelem Team.
    /// Dodaje Graph API specific properties i funkcjonalności.
    /// </summary>
    public class GraphTeam
    {
        /// <summary>
        /// Graph API Group ID (identyfikator zespołu w Graph API).
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Nazwa wyświetlana zespołu.
        /// Zachowuje kompatybilność z Team.DisplayName.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Opis zespołu.
        /// Zachowuje kompatybilność z Team.Description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Adres e-mail zespołu.
        /// </summary>
        public string? Mail { get; set; }

        /// <summary>
        /// Alias mail (krótka nazwa).
        /// </summary>
        public string? MailNickname { get; set; }

        /// <summary>
        /// URL zespołu w Teams.
        /// </summary>
        public string? WebUrl { get; set; }

        /// <summary>
        /// URL zdjęcia zespołu.
        /// </summary>
        public string? PhotoUrl { get; set; }

        /// <summary>
        /// Klasyfikacja zespołu (np. "Public", "Private").
        /// </summary>
        public string? Classification { get; set; }

        /// <summary>
        /// Widoczność zespołu (Public, Private, HiddenMembership).
        /// </summary>
        public string? Visibility { get; set; }

        /// <summary>
        /// Czy zespół jest zarchiwizowany.
        /// </summary>
        public bool IsArchived { get; set; }

        /// <summary>
        /// ETag dla cache validation.
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// Data utworzenia zespołu.
        /// </summary>
        public DateTime? CreatedDateTime { get; set; }

        /// <summary>
        /// ID dzierżawy.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Czy zespół jest aktywny.
        /// Zachowuje kompatybilność z Team.IsActive.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Liczba członków zespołu.
        /// Zachowuje kompatybilność z Team.MemberCount.
        /// </summary>
        public int MemberCount { get; set; }

        /// <summary>
        /// Liczba właścicieli zespołu.
        /// Zachowuje kompatybilność z Team.OwnerCount.
        /// </summary>
        public int OwnerCount { get; set; }

        /// <summary>
        /// Ustawienia zespołu.
        /// </summary>
        public GraphTeamSettings? Settings { get; set; }

        /// <summary>
        /// Ustawienia gości.
        /// </summary>
        public GraphTeamGuestSettings? GuestSettings { get; set; }

        /// <summary>
        /// Ustawienia członków.
        /// </summary>
        public GraphTeamMemberSettings? MemberSettings { get; set; }

        /// <summary>
        /// Ustawienia wiadomości.
        /// </summary>
        public GraphTeamMessagingSettings? MessagingSettings { get; set; }

        /// <summary>
        /// Ustawienia zabawy (Fun Settings).
        /// </summary>
        public GraphTeamFunSettings? FunSettings { get; set; }

        /// <summary>
        /// Ustawienia odkrywania zespołu.
        /// </summary>
        public GraphTeamDiscoverySettings? DiscoverySettings { get; set; }

        /// <summary>
        /// Lista członków zespołu.
        /// </summary>
        public List<GraphTeamMember> Members { get; set; } = new List<GraphTeamMember>();

        /// <summary>
        /// Lista kanałów zespołu.
        /// </summary>
        public List<GraphChannel> Channels { get; set; } = new List<GraphChannel>();

        /// <summary>
        /// Informacje o synchronizacji z Graph API.
        /// </summary>
        public GraphSyncInfo? SyncInfo { get; set; }

        /// <summary>
        /// Konwertuje GraphTeam na lokalny model Team.
        /// </summary>
        /// <returns>Lokalny model Team</returns>
        public Team ToLocalTeam()
        {
            return new Team
            {
                DisplayName = DisplayName ?? string.Empty,
                Description = Description ?? string.Empty,
                ExternalId = Id,
                Status = IsActive ? TeamStatus.Active : TeamStatus.Archived,
                CreatedDate = CreatedDateTime ?? DateTime.UtcNow,
                // Mapowanie innych właściwości według potrzeb
            };
        }

        /// <summary>
        /// Tworzy GraphTeam na podstawie lokalnego modelu Team.
        /// </summary>
        /// <param name="team">Lokalny model Team</param>
        /// <returns>GraphTeam</returns>
        public static GraphTeam FromLocalTeam(Team team)
        {
            return new GraphTeam
            {
                Id = team.ExternalId,
                DisplayName = team.DisplayName,
                Description = team.Description,
                IsActive = team.IsActive,
                CreatedDateTime = team.CreatedDate,
                // Mapowanie innych właściwości według potrzeb
            };
        }

        /// <summary>
        /// Sprawdza czy użytkownik jest członkiem zespołu.
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <returns>True jeśli jest członkiem</returns>
        public bool HasMember(string userId)
        {
            return Members.Any(m => m.UserId == userId);
        }

        /// <summary>
        /// Sprawdza czy użytkownik jest właścicielem zespołu.
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <returns>True jeśli jest właścicielem</returns>
        public bool HasOwner(string userId)
        {
            return Members.Any(m => m.UserId == userId && m.Role == "owner");
        }

        /// <summary>
        /// Pobiera członka zespołu.
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <returns>Członek zespołu lub null</returns>
        public GraphTeamMember? GetMember(string userId)
        {
            return Members.FirstOrDefault(m => m.UserId == userId);
        }

        /// <summary>
        /// Pobiera kanał zespołu.
        /// </summary>
        /// <param name="channelId">ID kanału</param>
        /// <returns>Kanał lub null</returns>
        public GraphChannel? GetChannel(string channelId)
        {
            return Channels.FirstOrDefault(c => c.Id == channelId);
        }

        /// <summary>
        /// Pobiera podsumowanie zespołu.
        /// </summary>
        /// <returns>Podsumowanie</returns>
        public string GetSummary()
        {
            var status = IsActive ? "Aktywny" : "Nieaktywny";
            var members = $"{MemberCount} członków ({OwnerCount} właścicieli)";
            var channels = $"{Channels.Count} kanałów";

            return $"{DisplayName}: {status}, {members}, {channels}";
        }
    }

    /// <summary>
    /// Ustawienia zespołu Graph API.
    /// </summary>
    public class GraphTeamSettings
    {
        /// <summary>
        /// Czy członkowie mogą tworzyć kanały.
        /// </summary>
        public bool? AllowCreateUpdateChannels { get; set; }

        /// <summary>
        /// Czy członkowie mogą usuwać kanały.
        /// </summary>
        public bool? AllowDeleteChannels { get; set; }

        /// <summary>
        /// Czy członkowie mogą dodawać i usuwać aplikacje.
        /// </summary>
        public bool? AllowAddRemoveApps { get; set; }

        /// <summary>
        /// Czy członkowie mogą tworzyć, aktualizować i usuwać karty.
        /// </summary>
        public bool? AllowCreateUpdateRemoveTabs { get; set; }

        /// <summary>
        /// Czy członkowie mogą tworzyć, aktualizować i usuwać konektory.
        /// </summary>
        public bool? AllowCreateUpdateRemoveConnectors { get; set; }
    }

    /// <summary>
    /// Ustawienia gości w zespole.
    /// </summary>
    public class GraphTeamGuestSettings
    {
        /// <summary>
        /// Czy goście mogą tworzyć kanały.
        /// </summary>
        public bool? AllowCreateUpdateChannels { get; set; }

        /// <summary>
        /// Czy goście mogą usuwać kanały.
        /// </summary>
        public bool? AllowDeleteChannels { get; set; }
    }

    /// <summary>
    /// Ustawienia członków zespołu.
    /// </summary>
    public class GraphTeamMemberSettings
    {
        /// <summary>
        /// Czy właściciele mogą dodawać aplikacje.
        /// </summary>
        public bool? AllowAddRemoveApps { get; set; }

        /// <summary>
        /// Czy właściciele mogą tworzyć, aktualizować i usuwać karty.
        /// </summary>
        public bool? AllowCreateUpdateRemoveTabs { get; set; }

        /// <summary>
        /// Czy właściciele mogą tworzyć, aktualizować i usuwać konektory.
        /// </summary>
        public bool? AllowCreateUpdateRemoveConnectors { get; set; }
    }

    /// <summary>
    /// Ustawienia wiadomości w zespole.
    /// </summary>
    public class GraphTeamMessagingSettings
    {
        /// <summary>
        /// Czy członkowie mogą edytować wiadomości.
        /// </summary>
        public bool? AllowUserEditMessages { get; set; }

        /// <summary>
        /// Czy członkowie mogą usuwać wiadomości.
        /// </summary>
        public bool? AllowUserDeleteMessages { get; set; }

        /// <summary>
        /// Czy właściciele mogą usuwać wiadomości.
        /// </summary>
        public bool? AllowOwnerDeleteMessages { get; set; }

        /// <summary>
        /// Czy team chat jest dozwolony.
        /// </summary>
        public bool? AllowTeamMentions { get; set; }

        /// <summary>
        /// Czy channel mentions są dozwolone.
        /// </summary>
        public bool? AllowChannelMentions { get; set; }
    }

    /// <summary>
    /// Ustawienia zabawy w zespole.
    /// </summary>
    public class GraphTeamFunSettings
    {
        /// <summary>
        /// Czy GIF-y są dozwolone.
        /// </summary>
        public bool? AllowGiphy { get; set; }

        /// <summary>
        /// Rating dla GIF-ów.
        /// </summary>
        public string? GiphyContentRating { get; set; }

        /// <summary>
        /// Czy stickers i memy są dozwolone.
        /// </summary>
        public bool? AllowStickersAndMemes { get; set; }

        /// <summary>
        /// Czy custom memy są dozwolone.
        /// </summary>
        public bool? AllowCustomMemes { get; set; }
    }

    /// <summary>
    /// Ustawienia odkrywania zespołu.
    /// </summary>
    public class GraphTeamDiscoverySettings
    {
        /// <summary>
        /// Czy zespół jest widoczny w wynikach wyszukiwania.
        /// </summary>
        public bool? ShowInTeamsSearchResults { get; set; }
    }

    /// <summary>
    /// Członek zespołu Graph API.
    /// </summary>
    public class GraphTeamMember
    {
        /// <summary>
        /// ID członka.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// ID użytkownika.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Adres e-mail użytkownika.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Nazwa wyświetlana użytkownika.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Rola w zespole (owner, member, guest).
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Lista ról w zespole.
        /// </summary>
        public List<string>? Roles { get; set; }

        /// <summary>
        /// Data dodania do zespołu.
        /// </summary>
        public DateTime? AddedDateTime { get; set; }

        /// <summary>
        /// Czy członek jest aktywny.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Dodatkowe informacje o użytkowniku.
        /// </summary>
        public GraphUser? User { get; set; }
    }

    /// <summary>
    /// Informacje o synchronizacji z Graph API.
    /// </summary>
    public class GraphSyncInfo
    {
        /// <summary>
        /// Data ostatniej synchronizacji.
        /// </summary>
        public DateTime? LastSyncDateTime { get; set; }

        /// <summary>
        /// Czy synchronizacja była pomyślna.
        /// </summary>
        public bool LastSyncSuccessful { get; set; } = true;

        /// <summary>
        /// Komunikat o błędzie synchronizacji.
        /// </summary>
        public string? LastSyncError { get; set; }

        /// <summary>
        /// Liczba prób synchronizacji.
        /// </summary>
        public int SyncAttempts { get; set; }

        /// <summary>
        /// Następna zaplanowana synchronizacja.
        /// </summary>
        public DateTime? NextSyncDateTime { get; set; }

        /// <summary>
        /// Czy synchronizacja jest wymagana.
        /// </summary>
        public bool SyncRequired { get; set; }

        /// <summary>
        /// Hash lokalnych danych dla porównania.
        /// </summary>
        public string? LocalDataHash { get; set; }

        /// <summary>
        /// Hash danych Graph API dla porównania.
        /// </summary>
        public string? GraphDataHash { get; set; }

        /// <summary>
        /// Czy dane są zsynchronizowane.
        /// </summary>
        public bool IsSynchronized => !SyncRequired && 
                                     LastSyncSuccessful && 
                                     LocalDataHash == GraphDataHash;
    }
} 