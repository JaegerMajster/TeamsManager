using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Wynik testu połączenia Microsoft Graph PowerShell
    /// </summary>
    public class PowerShellConnectionTestResult
    {
        /// <summary>
        /// Czas rozpoczęcia testu
        /// </summary>
        public DateTime TestStartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Czas zakończenia testu
        /// </summary>
        public DateTime TestEndTime { get; set; }

        /// <summary>
        /// Czas trwania testu
        /// </summary>
        public TimeSpan TestDuration { get; set; }

        /// <summary>
        /// Ogólny wynik testu (Passed, Partial, Failed)
        /// </summary>
        public string OverallResult { get; set; } = "Unknown";

        /// <summary>
        /// Test runspace PowerShell
        /// </summary>
        public bool RunspaceTest { get; set; }

        /// <summary>
        /// Test kontekstu Microsoft Graph
        /// </summary>
        public bool GraphContextTest { get; set; }

        /// <summary>
        /// Test odczytu użytkowników
        /// </summary>
        public bool UserReadTest { get; set; }

        /// <summary>
        /// Test odczytu grup
        /// </summary>
        public bool GroupReadTest { get; set; }

        /// <summary>
        /// Konto połączone z Graph
        /// </summary>
        public string? ConnectedAccount { get; set; }

        /// <summary>
        /// ID tenanta
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Dostępne zakresy uprawnień
        /// </summary>
        public List<string> AvailableScopes { get; set; } = new List<string>();

        /// <summary>
        /// Nazwa testowego użytkownika (z testu odczytu)
        /// </summary>
        public string? TestUserName { get; set; }

        /// <summary>
        /// Nazwa testowej grupy (z testu odczytu)
        /// </summary>
        public string? TestGroupName { get; set; }

        /// <summary>
        /// Lista komunikatów błędów
        /// </summary>
        public List<string> ErrorMessages { get; set; } = new List<string>();

        /// <summary>
        /// Liczba testów które przeszły pomyślnie
        /// </summary>
        public int PassedTestsCount => 
            (RunspaceTest ? 1 : 0) +
            (GraphContextTest ? 1 : 0) +
            (UserReadTest ? 1 : 0) +
            (GroupReadTest ? 1 : 0);

        /// <summary>
        /// Całkowita liczba testów
        /// </summary>
        public int TotalTestsCount => 4;

        /// <summary>
        /// Procent pomyślnych testów
        /// </summary>
        public double SuccessPercentage => TotalTestsCount > 0 ? (double)PassedTestsCount / TotalTestsCount * 100 : 0;

        /// <summary>
        /// Czy test zakończył się sukcesem
        /// </summary>
        public bool IsSuccess => OverallResult == "Passed";

        /// <summary>
        /// Czy test zakończył się częściowym sukcesem
        /// </summary>
        public bool IsPartialSuccess => OverallResult == "Partial";

        /// <summary>
        /// Czy test zakończył się niepowodzeniem
        /// </summary>
        public bool IsFailure => OverallResult == "Failed";

        /// <summary>
        /// Szczegółowy opis wyniku testu
        /// </summary>
        public string GetDetailedResult()
        {
            var result = $"Test połączenia Microsoft Graph - {OverallResult}\n";
            result += $"Testy przeszły: {PassedTestsCount}/{TotalTestsCount} ({SuccessPercentage:F1}%)\n";
            result += $"Czas trwania: {TestDuration.TotalMilliseconds:F0}ms\n\n";

            result += $"Szczegóły testów:\n";
            result += $"✓ PowerShell Runspace: {(RunspaceTest ? "PASS" : "FAIL")}\n";
            result += $"✓ Microsoft Graph Context: {(GraphContextTest ? "PASS" : "FAIL")}\n";
            result += $"✓ User Read Test: {(UserReadTest ? "PASS" : "FAIL")}\n";
            result += $"✓ Group Read Test: {(GroupReadTest ? "PASS" : "FAIL")}\n";

            if (!string.IsNullOrEmpty(ConnectedAccount))
            {
                result += $"\nKonto: {ConnectedAccount}\n";
            }

            if (!string.IsNullOrEmpty(TenantId))
            {
                result += $"Tenant: {TenantId}\n";
            }

            if (AvailableScopes.Any())
            {
                result += $"Uprawnienia: {string.Join(", ", AvailableScopes)}\n";
            }

            if (ErrorMessages.Any())
            {
                result += $"\nBłędy:\n";
                foreach (var error in ErrorMessages)
                {
                    result += $"• {error}\n";
                }
            }

            return result;
        }
    }
} 