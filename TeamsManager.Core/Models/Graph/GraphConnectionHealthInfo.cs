using System;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Informacje o zdrowiu połączenia z Microsoft Graph API.
    /// </summary>
    public class GraphConnectionHealthInfo
    {
        /// <summary>
        /// Czy połączenie z Graph API jest aktywne.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Czy token dostępu jest ważny.
        /// </summary>
        public bool IsTokenValid { get; set; }

        /// <summary>
        /// Data wygaśnięcia tokenu.
        /// </summary>
        public DateTime? TokenExpiresAt { get; set; }

        /// <summary>
        /// Czas odpowiedzi Graph API w milisekundach.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Wersja Graph API.
        /// </summary>
        public string? GraphApiVersion { get; set; }

        /// <summary>
        /// ID dzierżawy.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// ID aplikacji.
        /// </summary>
        public string? ApplicationId { get; set; }

        /// <summary>
        /// Ostatni błąd połączenia.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Czas ostatniego sprawdzenia.
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Status zdrowia połączenia.
        /// </summary>
        public GraphHealthStatus Status { get; set; }

        /// <summary>
        /// Informacje o rate limiting.
        /// </summary>
        public GraphRateLimitInfo? RateLimitInfo { get; set; }
    }

    /// <summary>
    /// Status zdrowia połączenia Graph API.
    /// </summary>
    public enum GraphHealthStatus
    {
        Unknown = 0,
        Healthy = 1,
        Warning = 2,
        Critical = 3,
        Disconnected = 4
    }

    /// <summary>
    /// Informacje o rate limiting Graph API.
    /// </summary>
    public class GraphRateLimitInfo
    {
        /// <summary>
        /// Pozostała liczba żądań.
        /// </summary>
        public int? RemainingRequests { get; set; }

        /// <summary>
        /// Maksymalna liczba żądań.
        /// </summary>
        public int? MaxRequests { get; set; }

        /// <summary>
        /// Czas resetowania limitu.
        /// </summary>
        public DateTime? ResetTime { get; set; }

        /// <summary>
        /// Czy osiągnięto limit.
        /// </summary>
        public bool IsLimitReached => RemainingRequests.HasValue && RemainingRequests.Value <= 0;

        /// <summary>
        /// Procent wykorzystania limitu.
        /// </summary>
        public double? UsagePercentage
        {
            get
            {
                if (!RemainingRequests.HasValue || !MaxRequests.HasValue || MaxRequests.Value == 0)
                    return null;

                return ((double)(MaxRequests.Value - RemainingRequests.Value) / MaxRequests.Value) * 100;
            }
        }
    }
} 