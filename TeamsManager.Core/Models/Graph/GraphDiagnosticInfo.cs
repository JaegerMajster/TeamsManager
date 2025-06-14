using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Szczegółowe informacje diagnostyczne Microsoft Graph API.
    /// Zachowuje kompatybilność z PowerShellDiagnosticInfo.
    /// </summary>
    public class GraphDiagnosticInfo
    {
        /// <summary>
        /// Czy połączenie z Graph API jest aktywne.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Czy token dostępu jest ważny.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Czy uprawnienia są wystarczające.
        /// </summary>
        public bool HasRequiredPermissions { get; set; }

        /// <summary>
        /// Czy wszystkie testy przeszły pomyślnie.
        /// </summary>
        public bool AllTestsPassed { get; set; }

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
        /// Czas odpowiedzi Graph API w milisekundach.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Informacje o rate limiting.
        /// </summary>
        public GraphRateLimitInfo? RateLimitInfo { get; set; }

        /// <summary>
        /// Status zdrowia połączenia.
        /// </summary>
        public GraphHealthStatus Status { get; set; }

        /// <summary>
        /// Lista błędów diagnostycznych.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Lista ostrzeżeń diagnostycznych.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Dodatkowe informacje diagnostyczne.
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Czas ostatniego sprawdzenia.
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Pobiera szczegółowy raport diagnostyczny.
        /// </summary>
        /// <returns>Szczegółowy raport</returns>
        public string GetDetailedReport()
        {
            var report = new List<string>
            {
                "=== RAPORT DIAGNOSTYCZNY MICROSOFT GRAPH API ===",
                $"Data sprawdzenia: {LastChecked:yyyy-MM-dd HH:mm:ss} UTC",
                $"Status: {Status}",
                "",
                "=== PODSTAWOWE TESTY ===",
                $"✓ Połączenie z Graph API: {(IsConnected ? "OK" : "BŁĄD")}",
                $"✓ Uwierzytelnienie: {(IsAuthenticated ? "OK" : "BŁĄD")}",
                $"✓ Uprawnienia: {(HasRequiredPermissions ? "OK" : "BŁĄD")}",
                $"✓ Wszystkie testy: {(AllTestsPassed ? "OK" : "BŁĄD")}",
                "",
                "=== SZCZEGÓŁY TECHNICZNE ===",
                $"Wersja Graph API: {GraphApiVersion ?? "Nieznana"}",
                $"ID dzierżawy: {TenantId ?? "Nieznane"}",
                $"ID aplikacji: {ApplicationId ?? "Nieznane"}",
                $"Czas odpowiedzi: {ResponseTimeMs} ms",
                ""
            };

            if (RateLimitInfo != null)
            {
                report.Add("=== RATE LIMITING ===");
                report.Add($"Pozostałe żądania: {RateLimitInfo.RemainingRequests?.ToString() ?? "Nieznane"}");
                report.Add($"Maksymalne żądania: {RateLimitInfo.MaxRequests?.ToString() ?? "Nieznane"}");
                report.Add($"Reset limitu: {RateLimitInfo.ResetTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Nieznany"}");
                report.Add($"Wykorzystanie: {RateLimitInfo.UsagePercentage?.ToString("F1") ?? "Nieznane"}%");
                report.Add("");
            }

            if (Errors.Count > 0)
            {
                report.Add("=== BŁĘDY ===");
                foreach (var error in Errors)
                {
                    report.Add($"❌ {error}");
                }
                report.Add("");
            }

            if (Warnings.Count > 0)
            {
                report.Add("=== OSTRZEŻENIA ===");
                foreach (var warning in Warnings)
                {
                    report.Add($"⚠️ {warning}");
                }
                report.Add("");
            }

            if (AdditionalInfo.Count > 0)
            {
                report.Add("=== DODATKOWE INFORMACJE ===");
                foreach (var info in AdditionalInfo)
                {
                    report.Add($"{info.Key}: {info.Value}");
                }
                report.Add("");
            }

            report.Add("=== KONIEC RAPORTU ===");

            return string.Join(Environment.NewLine, report);
        }
    }
} 