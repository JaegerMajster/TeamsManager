using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Informacje o rate limiting Microsoft Graph API.
    /// </summary>
    public class GraphRateLimitInfo
    {
        /// <summary>
        /// Czy osiągnięto limit żądań.
        /// </summary>
        public bool IsLimitReached { get; set; }

        /// <summary>
        /// Pozostała liczba żądań w bieżącym oknie czasowym.
        /// </summary>
        public int? RemainingRequests { get; set; }

        /// <summary>
        /// Maksymalna liczba żądań w oknie czasowym.
        /// </summary>
        public int? MaxRequests { get; set; }

        /// <summary>
        /// Czas resetowania limitu.
        /// </summary>
        public DateTime? ResetTime { get; set; }

        /// <summary>
        /// Czas oczekiwania przed ponowieniem żądania (w sekundach).
        /// </summary>
        public int? RetryAfterSeconds { get; set; }

        /// <summary>
        /// Typ limitu (np. "Application", "User", "Tenant").
        /// </summary>
        public string? LimitType { get; set; }

        /// <summary>
        /// Procent wykorzystania limitu (0-100%).
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

        /// <summary>
        /// Czy wykorzystanie limitu jest wysokie (>80%).
        /// </summary>
        public bool IsHighUsage => UsagePercentage.HasValue && UsagePercentage.Value > 80;

        /// <summary>
        /// Czy wykorzystanie limitu jest krytyczne (>95%).
        /// </summary>
        public bool IsCriticalUsage => UsagePercentage.HasValue && UsagePercentage.Value > 95;

        /// <summary>
        /// Czas do następnego resetu w sekundach.
        /// </summary>
        public int? SecondsToReset
        {
            get
            {
                if (!ResetTime.HasValue)
                    return null;

                var timeToReset = ResetTime.Value - DateTime.UtcNow;
                return timeToReset.TotalSeconds > 0 ? (int)timeToReset.TotalSeconds : 0;
            }
        }

        /// <summary>
        /// Pobiera szczegółowy raport rate limiting.
        /// </summary>
        /// <returns>Szczegółowy raport</returns>
        public string GetDetailedReport()
        {
            var report = new List<string>
            {
                "=== RAPORT RATE LIMITING MICROSOFT GRAPH API ==="
            };

            if (IsLimitReached)
            {
                report.Add("❌ LIMIT ŻĄDAŃ OSIĄGNIĘTY");
                if (RetryAfterSeconds.HasValue)
                    report.Add($"   Spróbuj ponownie za: {RetryAfterSeconds} sekund");
            }
            else
            {
                report.Add("✓ Limit żądań nie został osiągnięty");
            }

            report.Add("");

            if (RemainingRequests.HasValue && MaxRequests.HasValue)
            {
                report.Add($"Pozostałe żądania: {RemainingRequests}/{MaxRequests}");
                if (UsagePercentage.HasValue)
                {
                    var status = IsCriticalUsage ? "KRYTYCZNE" : IsHighUsage ? "WYSOKIE" : "NORMALNE";
                    report.Add($"Wykorzystanie: {UsagePercentage:F1}% ({status})");
                }
            }

            if (ResetTime.HasValue)
            {
                report.Add($"Reset limitu: {ResetTime:yyyy-MM-dd HH:mm:ss} UTC");
                if (SecondsToReset.HasValue)
                    report.Add($"Czas do resetu: {SecondsToReset} sekund");
            }

            if (!string.IsNullOrEmpty(LimitType))
                report.Add($"Typ limitu: {LimitType}");

            return string.Join(Environment.NewLine, report);
        }

        /// <summary>
        /// Tworzy informacje o rate limiting na podstawie nagłówków HTTP.
        /// </summary>
        /// <param name="remainingRequests">Pozostałe żądania</param>
        /// <param name="maxRequests">Maksymalne żądania</param>
        /// <param name="resetTime">Czas resetu</param>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="limitType">Typ limitu</param>
        /// <returns>Informacje o rate limiting</returns>
        public static GraphRateLimitInfo CreateFromHeaders(
            int? remainingRequests = null,
            int? maxRequests = null,
            DateTime? resetTime = null,
            int? retryAfterSeconds = null,
            string? limitType = null)
        {
            return new GraphRateLimitInfo
            {
                RemainingRequests = remainingRequests,
                MaxRequests = maxRequests,
                ResetTime = resetTime,
                RetryAfterSeconds = retryAfterSeconds,
                LimitType = limitType,
                IsLimitReached = remainingRequests.HasValue && remainingRequests.Value <= 0
            };
        }
    }
} 