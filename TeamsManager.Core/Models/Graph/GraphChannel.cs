using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Reprezentuje kanał Microsoft Teams z Graph API z zachowaniem kompatybilności z lokalnym modelem Channel.
    /// Dodaje Graph API specific properties i funkcjonalności.
    /// </summary>
    public class GraphChannel
    {
        /// <summary>
        /// Graph API Channel ID (identyfikator kanału w Graph API).
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// ID zespołu do którego należy kanał.
        /// </summary>
        public string? TeamId { get; set; }

        /// <summary>
        /// Nazwa wyświetlana kanału.
        /// Zachowuje kompatybilność z Channel.DisplayName.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Opis kanału.
        /// Zachowuje kompatybilność z Channel.Description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Adres e-mail kanału (dla kanałów, które go posiadają).
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// URL kanału w Teams.
        /// </summary>
        public string? WebUrl { get; set; }

        /// <summary>
        /// ETag dla cache validation.
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// Data utworzenia kanału.
        /// </summary>
        public DateTime? CreatedDateTime { get; set; }

        /// <summary>
        /// ID dzierżawy.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Typ członkostwa (standard, private, unknownFutureValue).
        /// Zgodny z Graph API.
        /// </summary>
        public string? MembershipType { get; set; }

        /// <summary>
        /// Czy kanał jest aktywny.
        /// Zachowuje kompatybilność z Channel.IsActive.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Czy kanał jest prywatny.
        /// Zachowuje kompatybilność z Channel.IsPrivate.
        /// </summary>
        public bool IsPrivate => MembershipType == "private";

        /// <summary>
        /// Czy kanał jest kanałem ogólnym.
        /// Zachowuje kompatybilność z Channel.IsGeneral.
        /// </summary>
        public bool IsGeneral { get; set; }

        /// <summary>
        /// Czy kanał jest tylko do odczytu.
        /// Zachowuje kompatybilność z Channel.IsReadOnly.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Czy kanał jest domyślnie ulubiony dla nowych członków zespołu.
        /// </summary>
        public bool IsFavoriteByDefault { get; set; }

        /// <summary>
        /// Ustawienia kanału.
        /// </summary>
        public GraphChannelSettings? Settings { get; set; }

        /// <summary>
        /// Statystyki kanału.
        /// </summary>
        public GraphChannelStats? Stats { get; set; }

        /// <summary>
        /// Lista członków kanału prywatnego.
        /// Endpoint: GET /v1.0/teams/{team-id}/channels/{channel-id}/members
        /// </summary>
        public List<GraphChannelMember> Members { get; set; } = new List<GraphChannelMember>();

        /// <summary>
        /// Lista kart kanału.
        /// Endpoint: GET /v1.0/teams/{team-id}/channels/{channel-id}/tabs
        /// </summary>
        public List<GraphChannelTab> Tabs { get; set; } = new List<GraphChannelTab>();

        /// <summary>
        /// Konwertuje GraphChannel na lokalny model Channel.
        /// </summary>
        /// <returns>Lokalny model Channel</returns>
        public Channel ToLocalChannel()
        {
            return new Channel
            {
                DisplayName = DisplayName ?? string.Empty,
                Description = Description ?? string.Empty,
                TeamId = TeamId ?? string.Empty,
                IsGeneral = IsGeneral,
                IsPrivate = IsPrivate,
                IsReadOnly = IsReadOnly,
                Status = IsActive ? ChannelStatus.Active : ChannelStatus.Archived,
                CreatedDate = CreatedDateTime ?? DateTime.UtcNow,
                // Mapowanie statystyk jeśli dostępne
                MessageCount = Stats?.MessageCount ?? 0,
                FilesCount = Stats?.FilesCount ?? 0,
                FilesSize = Stats?.FilesSize ?? 0,
                LastActivityDate = Stats?.LastActivityDate,
                LastMessageDate = Stats?.LastMessageDate,
                // Mapowanie innych właściwości według potrzeb
            };
        }

        /// <summary>
        /// Tworzy GraphChannel na podstawie lokalnego modelu Channel.
        /// </summary>
        /// <param name="channel">Lokalny model Channel</param>
        /// <returns>GraphChannel</returns>
        public static GraphChannel FromLocalChannel(Channel channel)
        {
            return new GraphChannel
            {
                DisplayName = channel.DisplayName,
                Description = channel.Description,
                TeamId = channel.TeamId,
                IsGeneral = channel.IsGeneral,
                IsReadOnly = channel.IsReadOnly,
                IsActive = channel.IsActive,
                MembershipType = channel.IsPrivate ? "private" : "standard",
                CreatedDateTime = channel.CreatedDate,
                Stats = new GraphChannelStats
                {
                    MessageCount = channel.MessageCount,
                    FilesCount = channel.FilesCount,
                    FilesSize = channel.FilesSize,
                    LastActivityDate = channel.LastActivityDate,
                    LastMessageDate = channel.LastMessageDate
                }
                // Mapowanie innych właściwości według potrzeb
            };
        }

        /// <summary>
        /// Sprawdza czy użytkownik jest członkiem kanału.
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <returns>True jeśli jest członkiem</returns>
        public bool HasMember(string userId)
        {
            return Members.Any(m => m.UserId == userId);
        }

        /// <summary>
        /// Pobiera członka kanału.
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <returns>Członek kanału lub null</returns>
        public GraphChannelMember? GetMember(string userId)
        {
            return Members.FirstOrDefault(m => m.UserId == userId);
        }

        /// <summary>
        /// Pobiera kartę kanału.
        /// </summary>
        /// <param name="tabId">ID karty</param>
        /// <returns>Karta lub null</returns>
        public GraphChannelTab? GetTab(string tabId)
        {
            return Tabs.FirstOrDefault(t => t.Id == tabId);
        }

        /// <summary>
        /// Sprawdza czy kanał może być usunięty.
        /// </summary>
        /// <returns>True jeśli może być usunięty</returns>
        public bool CanBeDeleted()
        {
            // Kanał ogólny nie może być usunięty
            if (IsGeneral) return false;
            
            // Inne reguły biznesowe mogą być dodane tutaj
            return true;
        }

        /// <summary>
        /// Pobiera powód dlaczego kanał nie może być usunięty.
        /// </summary>
        /// <returns>Powód blokady usunięcia lub null</returns>
        public string? GetDeletionBlockReason()
        {
            if (IsGeneral) return "Kanał ogólny nie może być usunięty";
            return null;
        }

        /// <summary>
        /// Pobiera podsumowanie kanału.
        /// </summary>
        /// <returns>Podsumowanie</returns>
        public string GetSummary()
        {
            var type = IsPrivate ? "Prywatny" : "Publiczny";
            var status = IsActive ? "Aktywny" : "Nieaktywny";
            var members = IsPrivate ? $"{Members.Count} członków" : "Wszyscy członkowie zespołu";
            var tabs = $"{Tabs.Count} kart";

            return $"{DisplayName}: {type}, {status}, {members}, {tabs}";
        }

        /// <summary>
        /// Pobiera szczegółowe informacje o kanale.
        /// </summary>
        /// <returns>Szczegółowe informacje</returns>
        public string GetDetailedInfo()
        {
            var info = new List<string>
            {
                $"Nazwa: {DisplayName ?? "Brak"}",
                $"Opis: {Description ?? "Brak"}",
                $"ID: {Id ?? "Brak"}",
                $"ID zespołu: {TeamId ?? "Brak"}",
                $"Typ: {(IsPrivate ? "Prywatny" : "Publiczny")}",
                $"Status: {(IsActive ? "Aktywny" : "Nieaktywny")}",
                $"Kanał ogólny: {(IsGeneral ? "Tak" : "Nie")}",
                $"Tylko odczyt: {(IsReadOnly ? "Tak" : "Nie")}",
                $"Utworzony: {CreatedDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Nieznane"}",
                $"URL: {WebUrl ?? "Brak"}",
                $"Email: {Email ?? "Brak"}"
            };

            if (Stats != null)
            {
                info.Add("");
                info.Add("=== STATYSTYKI ===");
                info.Add($"Liczba wiadomości: {Stats.MessageCount}");
                info.Add($"Liczba plików: {Stats.FilesCount}");
                info.Add($"Rozmiar plików: {Stats.FilesSize} bajtów");
                info.Add($"Ostatnia aktywność: {Stats.LastActivityDate?.ToString("yyyy-MM-dd HH:mm") ?? "Nieznane"}");
                info.Add($"Ostatnia wiadomość: {Stats.LastMessageDate?.ToString("yyyy-MM-dd HH:mm") ?? "Nieznane"}");
            }

            if (Members.Count > 0)
            {
                info.Add("");
                info.Add($"=== CZŁONKOWIE ({Members.Count}) ===");
                foreach (var member in Members.Take(10))
                {
                    info.Add($"• {member.DisplayName} ({member.Role})");
                }
                if (Members.Count > 10)
                {
                    info.Add($"... i {Members.Count - 10} więcej");
                }
            }

            if (Tabs.Count > 0)
            {
                info.Add("");
                info.Add($"=== KARTY ({Tabs.Count}) ===");
                foreach (var tab in Tabs)
                {
                    info.Add($"• {tab.DisplayName} ({tab.TeamsAppId})");
                }
            }

            return string.Join(Environment.NewLine, info);
        }
    }

    /// <summary>
    /// Ustawienia kanału Graph API.
    /// </summary>
    public class GraphChannelSettings
    {
        /// <summary>
        /// Czy członkowie mogą odpowiadać na wiadomości.
        /// </summary>
        public bool? AllowNewMessageFromBots { get; set; }

        /// <summary>
        /// Czy członkowie mogą odpowiadać na wiadomości od konektorów.
        /// </summary>
        public bool? AllowNewMessageFromConnectors { get; set; }

        /// <summary>
        /// Czy członkowie mogą wspominać kanał.
        /// </summary>
        public bool? AllowChannelMentions { get; set; }

        /// <summary>
        /// Ustawienia powiadomień kanału.
        /// </summary>
        public object? NotificationSettings { get; set; }

        /// <summary>
        /// Czy moderacja jest włączona dla kanału.
        /// </summary>
        public bool? IsModerationEnabled { get; set; }

        /// <summary>
        /// Kategoria kanału.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Tagi kanału.
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Kolejność sortowania kanału.
        /// </summary>
        public int? SortOrder { get; set; }

        /// <summary>
        /// Dodatkowe ustawienia kanału.
        /// </summary>
        public Dictionary<string, object>? AdditionalSettings { get; set; }
    }

    /// <summary>
    /// Statystyki kanału Graph API.
    /// </summary>
    public class GraphChannelStats
    {
        /// <summary>
        /// Liczba wiadomości w kanale.
        /// </summary>
        public int MessageCount { get; set; }

        /// <summary>
        /// Liczba plików w kanale.
        /// </summary>
        public int FilesCount { get; set; }

        /// <summary>
        /// Rozmiar plików w bajtach.
        /// </summary>
        public long FilesSize { get; set; }

        /// <summary>
        /// Data ostatniej aktywności.
        /// </summary>
        public DateTime? LastActivityDate { get; set; }

        /// <summary>
        /// Data ostatniej wiadomości.
        /// </summary>
        public DateTime? LastMessageDate { get; set; }

        /// <summary>
        /// Liczba aktywnych członków.
        /// </summary>
        public int ActiveMembersCount { get; set; }

        /// <summary>
        /// Statystyki wykorzystania.
        /// </summary>
        public Dictionary<string, object>? UsageStats { get; set; }
    }

    /// <summary>
    /// Członek kanału prywatnego Graph API.
    /// </summary>
    public class GraphChannelMember
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
        /// Rola w kanale (owner, member, guest).
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Data dodania do kanału.
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
    /// Karta kanału Graph API.
    /// </summary>
    public class GraphChannelTab
    {
        /// <summary>
        /// ID karty.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Nazwa wyświetlana karty.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// URL karty.
        /// </summary>
        public string? WebUrl { get; set; }

        /// <summary>
        /// Konfiguracja karty.
        /// </summary>
        public string? Configuration { get; set; }

        /// <summary>
        /// ID aplikacji Teams.
        /// </summary>
        public string? TeamsAppId { get; set; }

        /// <summary>
        /// Pozycja sortowania karty.
        /// </summary>
        public int? SortOrderIndex { get; set; }

        /// <summary>
        /// Czy karta jest aktywna.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Data utworzenia karty.
        /// </summary>
        public DateTime? CreatedDateTime { get; set; }

        /// <summary>
        /// Dodatkowe metadane karty.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
} 