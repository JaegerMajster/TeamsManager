using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Informacje diagnostyczne o stanie połączenia PowerShell/Graph
    /// </summary>
    public class PowerShellDiagnosticInfo
    {
        /// <summary>
        /// UPN użytkownika
        /// </summary>
        public string? UserUpn { get; set; }

        /// <summary>
        /// Czy ma token API
        /// </summary>
        public bool HasApiToken { get; set; }

        /// <summary>
        /// Długość tokenu API
        /// </summary>
        public int ApiTokenLength { get; set; }

        /// <summary>
        /// Czy ma token Graph
        /// </summary>
        public bool HasGraphToken { get; set; }

        /// <summary>
        /// Długość tokenu Graph
        /// </summary>
        public int GraphTokenLength { get; set; }

        /// <summary>
        /// Czy ma uprawnienia do tworzenia użytkowników
        /// </summary>
        public bool HasUserCreationPermissions { get; set; }

        /// <summary>
        /// Czy system jest w pełni sprawny
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Stan runspace PowerShell
        /// </summary>
        public string RunspaceState { get; set; } = "";

        /// <summary>
        /// Czy jest połączony z Graph
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Stan Circuit Breaker
        /// </summary>
        public string CircuitBreakerState { get; set; } = "";

        /// <summary>
        /// Ostatnia próba połączenia
        /// </summary>
        public DateTime? LastConnectionAttempt { get; set; }

        /// <summary>
        /// Ostatnie udane połączenie
        /// </summary>
        public DateTime? LastSuccessfulConnection { get; set; }

        /// <summary>
        /// Czy runspace jest gotowy
        /// </summary>
        public bool RunspaceReady { get; set; }

        /// <summary>
        /// Test podstawowej komendy PowerShell
        /// </summary>
        public bool BasicCommandTest { get; set; }

        /// <summary>
        /// Test połączenia Graph
        /// </summary>
        public bool GraphConnectionTest { get; set; }

        /// <summary>
        /// Czy ma wymagane uprawnienia
        /// </summary>
        public bool? HasRequiredPermissions { get; set; }

        /// <summary>
        /// Dostępne zakresy uprawnień
        /// </summary>
        public List<string> AvailableScopes { get; set; } = new List<string>();

        /// <summary>
        /// Wyniki testów dodatkowych komend
        /// </summary>
        public Dictionary<string, bool> TestResults { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Lista błędów diagnostycznych
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Ogólny stan zdrowia systemu
        /// </summary>
        public PowerShellHealthStatus OverallHealth { get; set; } = PowerShellHealthStatus.Unknown;

        /// <summary>
        /// Status połączenia (dla kompatybilności)
        /// </summary>
        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        /// <summary>
        /// Status Graph API (dla kompatybilności)
        /// </summary>
        public string GraphApiStatus => GraphConnectionTest ? "Healthy" : "Unhealthy";

        /// <summary>
        /// Ostatnia udana operacja (dla kompatybilności)
        /// </summary>
        public DateTime? LastSuccessfulOperation => LastSuccessfulConnection;

        /// <summary>
        /// Czas trwania ostatniej operacji (dla kompatybilności)
        /// </summary>
        public TimeSpan? LastOperationDuration { get; set; }

        /// <summary>
        /// Liczba błędów (dla kompatybilności)
        /// </summary>
        public int ErrorCount => Errors.Count;

        /// <summary>
        /// Zwraca podsumowanie diagnostyki
        /// </summary>
        public string GetSummary()
        {
            if (IsConnected)
            {
                return "System PowerShell/Graph jest w pełni sprawny";
            }

            return $"Wykryto {Errors.Count} problemów: {string.Join(", ", Errors)}";
        }
    }

    /// <summary>
    /// Informacje o uprawnieniach PowerShell/Graph
    /// </summary>
    public class PowerShellPermissionInfo
    {
        /// <summary>
        /// Czy uprawnienia są prawidłowe
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Czy ma wszystkie wymagane uprawnienia (alias dla IsValid)
        /// </summary>
        public bool HasAllRequiredPermissions => IsValid;

        /// <summary>
        /// Czy jest połączony z Graph
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Konto użytkownika
        /// </summary>
        public string? Account { get; set; }

        /// <summary>
        /// ID dzierżawy
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Nazwa aplikacji
        /// </summary>
        public string? AppName { get; set; }

        /// <summary>
        /// Dostępne zakresy uprawnień
        /// </summary>
        public List<string>? AvailableScopes { get; set; }

        /// <summary>
        /// Wyniki sprawdzenia konkretnych uprawnień
        /// </summary>
        public Dictionary<string, bool> PermissionResults { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Lista brakujących uprawnień
        /// </summary>
        public List<string> MissingPermissions => PermissionResults
            .Where(kvp => !kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();

        /// <summary>
        /// Komunikat błędu jeśli wystąpił
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Status zdrowia systemu PowerShell
    /// </summary>
    public enum PowerShellHealthStatus
    {
        /// <summary>
        /// Stan nieznany
        /// </summary>
        Unknown,

        /// <summary>
        /// System działa prawidłowo
        /// </summary>
        Healthy,

        /// <summary>
        /// System działa z ostrzeżeniami
        /// </summary>
        Warning,

        /// <summary>
        /// System działa w trybie ograniczonym
        /// </summary>
        Degraded,

        /// <summary>
        /// System nie działa
        /// </summary>
        Critical
    }
} 