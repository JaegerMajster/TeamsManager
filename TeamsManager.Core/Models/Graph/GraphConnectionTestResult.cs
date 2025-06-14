using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Wyniki testów połączenia z Microsoft Graph API.
    /// Zachowuje kompatybilność z PowerShellConnectionTestResult.
    /// </summary>
    public class GraphConnectionTestResult
    {
        /// <summary>
        /// Czy wszystkie testy przeszły pomyślnie.
        /// </summary>
        public bool AllTestsPassed { get; set; }

        /// <summary>
        /// Liczba testów, które przeszły pomyślnie.
        /// </summary>
        public int PassedTests { get; set; }

        /// <summary>
        /// Całkowita liczba testów.
        /// </summary>
        public int TotalTests { get; set; }

        /// <summary>
        /// Wyniki testów poszczególnych endpointów.
        /// </summary>
        public List<GraphEndpointTestResult> EndpointTestResults { get; set; } = new List<GraphEndpointTestResult>();

        /// <summary>
        /// Informacje o rate limiting.
        /// </summary>
        public GraphRateLimitInfo? RateLimitInfo { get; set; }

        /// <summary>
        /// Średni czas odpowiedzi w milisekundach.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Lista ostrzeżeń.
        /// </summary>
        public List<string> WarningMessages { get; set; } = new List<string>();

        /// <summary>
        /// Lista błędów.
        /// </summary>
        public List<string> ErrorMessages { get; set; } = new List<string>();

        /// <summary>
        /// Czas wykonania testów.
        /// </summary>
        public DateTime TestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Czy występują problemy z wydajnością.
        /// </summary>
        public bool HasPerformanceIssues => AverageResponseTimeMs > 2000; // > 2 sekundy

        /// <summary>
        /// Czy występują problemy z rate limiting.
        /// </summary>
        public bool HasRateLimitIssues => RateLimitInfo?.IsLimitReached == true || 
                                          RateLimitInfo?.UsagePercentage > 90;

        /// <summary>
        /// Procent testów, które przeszły pomyślnie.
        /// </summary>
        public double SuccessRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100.0 : 0.0;

        /// <summary>
        /// Pobiera szczegółowy raport wyników testów.
        /// </summary>
        /// <returns>Szczegółowy raport</returns>
        public string GetDetailedResult()
        {
            var report = new List<string>
            {
                "=== RAPORT TESTÓW POŁĄCZENIA MICROSOFT GRAPH API ===",
                $"Data testów: {TestedAt:yyyy-MM-dd HH:mm:ss} UTC",
                $"Wynik ogólny: {(AllTestsPassed ? "SUKCES" : "BŁĄD")}",
                $"Testy przeszły: {PassedTests}/{TotalTests} ({SuccessRate:F1}%)",
                $"Średni czas odpowiedzi: {AverageResponseTimeMs:F0} ms",
                ""
            };

            // Sekcja wydajności
            if (HasPerformanceIssues)
            {
                report.Add("⚠️ OSTRZEŻENIE: Wykryto problemy z wydajnością");
                report.Add($"   Średni czas odpowiedzi ({AverageResponseTimeMs:F0} ms) przekracza zalecane 2000 ms");
                report.Add("");
            }

            // Sekcja rate limiting
            if (RateLimitInfo != null)
            {
                report.Add("=== RATE LIMITING ===");
                report.Add($"Pozostałe żądania: {RateLimitInfo.RemainingRequests?.ToString() ?? "Nieznane"}");
                report.Add($"Wykorzystanie: {RateLimitInfo.UsagePercentage?.ToString("F1") ?? "Nieznane"}%");
                
                if (HasRateLimitIssues)
                {
                    report.Add("⚠️ OSTRZEŻENIE: Wysokie wykorzystanie rate limiting");
                }
                report.Add("");
            }

            // Wyniki testów endpointów
            if (EndpointTestResults.Count > 0)
            {
                report.Add("=== WYNIKI TESTÓW ENDPOINTÓW ===");
                foreach (var test in EndpointTestResults.OrderBy(t => t.TestName))
                {
                    var status = test.Success ? "✓" : "❌";
                    report.Add($"{status} {test.TestName}: {test.ResponseTimeMs} ms");
                    
                    if (!test.Success && !string.IsNullOrEmpty(test.ErrorMessage))
                    {
                        report.Add($"   Błąd: {test.ErrorMessage}");
                    }
                }
                report.Add("");
            }

            // Ostrzeżenia
            if (WarningMessages.Count > 0)
            {
                report.Add("=== OSTRZEŻENIA ===");
                foreach (var warning in WarningMessages)
                {
                    report.Add($"⚠️ {warning}");
                }
                report.Add("");
            }

            // Błędy
            if (ErrorMessages.Count > 0)
            {
                report.Add("=== BŁĘDY ===");
                foreach (var error in ErrorMessages)
                {
                    report.Add($"❌ {error}");
                }
                report.Add("");
            }

            // Rekomendacje
            report.Add("=== REKOMENDACJE ===");
            if (!AllTestsPassed)
            {
                report.Add("• Sprawdź uprawnienia aplikacji w Azure AD");
                report.Add("• Zweryfikuj konfigurację połączenia");
            }
            if (HasPerformanceIssues)
            {
                report.Add("• Rozważ optymalizację zapytań Graph API");
                report.Add("• Sprawdź połączenie sieciowe");
            }
            if (HasRateLimitIssues)
            {
                report.Add("• Zaimplementuj exponential backoff");
                report.Add("• Rozważ zmniejszenie częstotliwości zapytań");
            }

            report.Add("");
            report.Add("=== KONIEC RAPORTU ===");

            return string.Join(Environment.NewLine, report);
        }
    }

    /// <summary>
    /// Wynik testu konkretnego endpointu Graph API.
    /// </summary>
    public class GraphEndpointTestResult
    {
        /// <summary>
        /// Nazwa testu.
        /// </summary>
        public string TestName { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint Graph API.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Czy test przeszedł pomyślnie.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Czas odpowiedzi w milisekundach.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// Komunikat błędu (jeśli wystąpił).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Szczegóły błędu.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Czas wykonania testu.
        /// </summary>
        public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    }
} 