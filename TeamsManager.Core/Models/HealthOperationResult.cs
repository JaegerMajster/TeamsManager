using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Wynik operacji monitorowania zdrowia systemu
    /// Następuje wzorce z BulkOperationResult
    /// </summary>
    public class HealthOperationResult
    {
        /// <summary>
        /// Czy operacja zakończyła się sukcesem
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Kompatybilność z wzorcami orkiestratora - settable success flag
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// Komunikat błędu w przypadku niepowodzenia
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Timestamp przetworzenia operacji
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Typ operacji monitorowania
        /// </summary>
        public string? OperationType { get; set; }
        
        /// <summary>
        /// Czas wykonania operacji w milisekundach
        /// </summary>
        public long? ExecutionTimeMs { get; set; }

        /// <summary>
        /// Lista pomyślnych operacji zdrowia
        /// </summary>
        public List<HealthOperationSuccess> SuccessfulOperations { get; set; } = new List<HealthOperationSuccess>();
        
        /// <summary>
        /// Lista błędów operacji zdrowia
        /// </summary>
        public List<HealthOperationError> Errors { get; set; } = new List<HealthOperationError>();

        /// <summary>
        /// Szczegółowe wyniki sprawdzeń zdrowia
        /// </summary>
        public List<HealthCheckDetail> HealthChecks { get; set; } = new List<HealthCheckDetail>();

        /// <summary>
        /// Metryki wydajności wykryte podczas operacji
        /// </summary>
        public HealthMetrics? Metrics { get; set; }

        /// <summary>
        /// Rekomendacje do poprawy stanu systemu
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();

        /// <summary>
        /// Konstruktor dla wyniku sukcesu
        /// </summary>
        public static HealthOperationResult CreateSuccess(string? operationType = null, long? executionTimeMs = null)
        {
            return new HealthOperationResult
            {
                Success = true,
                IsSuccess = true,
                OperationType = operationType,
                ExecutionTimeMs = executionTimeMs
            };
        }

        /// <summary>
        /// Konstruktor dla wyniku błędu
        /// </summary>
        public static HealthOperationResult CreateError(string errorMessage, string? operationType = null, long? executionTimeMs = null)
        {
            return new HealthOperationResult
            {
                Success = false,
                IsSuccess = false,
                ErrorMessage = errorMessage,
                OperationType = operationType,
                ExecutionTimeMs = executionTimeMs
            };
        }

        /// <summary>
        /// Kompatybilność z istniejącym API - konwersja do bool
        /// </summary>
        public static implicit operator bool(HealthOperationResult result)
        {
            return result.Success;
        }
    }

    /// <summary>
    /// Reprezentuje pomyślną operację monitorowania zdrowia
    /// </summary>
    public class HealthOperationSuccess
    {
        /// <summary>
        /// Nazwa operacji
        /// </summary>
        public string Operation { get; set; } = string.Empty;
        
        /// <summary>
        /// Komponent systemu którego dotyczy operacja
        /// </summary>
        public string Component { get; set; } = string.Empty;
        
        /// <summary>
        /// Nazwa komponentu
        /// </summary>
        public string? ComponentName { get; set; }
        
        /// <summary>
        /// Komunikat o sukcesie
        /// </summary>
        public string? Message { get; set; }
        
        /// <summary>
        /// Dodatkowe dane diagnostyczne
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Reprezentuje błąd operacji monitorowania zdrowia
    /// </summary>
    public class HealthOperationError
    {
        /// <summary>
        /// Nazwa operacji która się nie powiodła
        /// </summary>
        public string Operation { get; set; } = string.Empty;
        
        /// <summary>
        /// Komponent który spowodował błąd
        /// </summary>
        public string? Component { get; set; }
        
        /// <summary>
        /// Nazwa komponentu
        /// </summary>
        public string? ComponentName { get; set; }
        
        /// <summary>
        /// Komunikat błędu
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Wyjątek który spowodował błąd
        /// </summary>
        public Exception? Exception { get; set; }
        
        /// <summary>
        /// Dodatkowe dane o błędzie
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }

        /// <summary>
        /// Poziom krytyczności błędu
        /// </summary>
        public HealthErrorSeverity Severity { get; set; } = HealthErrorSeverity.Warning;
    }

    /// <summary>
    /// Szczegółowy wynik sprawdzenia zdrowia komponentu
    /// </summary>
    public class HealthCheckDetail
    {
        /// <summary>
        /// Nazwa komponentu
        /// </summary>
        public string ComponentName { get; set; } = string.Empty;

        /// <summary>
        /// Status zdrowia komponentu
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Opis stanu
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Czas wykonania sprawdzenia w milisekundach
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Dodatkowe dane diagnostyczne
        /// </summary>
        public Dictionary<string, object>? Data { get; set; }

        /// <summary>
        /// Timestamp sprawdzenia
        /// </summary>
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Metryki wydajności systemu
    /// </summary>
    public class HealthMetrics
    {
        /// <summary>
        /// Metryki cache
        /// </summary>
        public CacheMetrics? CacheMetrics { get; set; }

        /// <summary>
        /// Średni czas odpowiedzi API w milisekundach
        /// </summary>
        public double AverageApiResponseTimeMs { get; set; }

        /// <summary>
        /// Liczba aktywnych połączeń
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Użycie pamięci w bajtach
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Obciążenie CPU w procentach
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Liczba błędów w ostatniej godzinie
        /// </summary>
        public int ErrorsLastHour { get; set; }

        /// <summary>
        /// Status połączenia PowerShell
        /// </summary>
        public string? PowerShellConnectionStatus { get; set; }
    }

    /// <summary>
    /// Status procesu monitorowania zdrowia
    /// </summary>
    public class HealthMonitoringProcessStatus
    {
        /// <summary>
        /// Identyfikator procesu
        /// </summary>
        public string ProcessId { get; set; } = string.Empty;

        /// <summary>
        /// Typ operacji monitorowania
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Status procesu
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Aktualna operacja
        /// </summary>
        public string CurrentOperation { get; set; } = string.Empty;

        /// <summary>
        /// Procent ukończenia
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Liczba sprawdzonych komponentów
        /// </summary>
        public int ComponentsChecked { get; set; }

        /// <summary>
        /// Całkowita liczba komponentów do sprawdzenia
        /// </summary>
        public int TotalComponents { get; set; }

        /// <summary>
        /// Liczba wykrytych problemów
        /// </summary>
        public int IssuesFound { get; set; }

        /// <summary>
        /// Liczba naprawionych problemów
        /// </summary>
        public int IssuesRepaired { get; set; }

        /// <summary>
        /// Data rozpoczęcia
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Data zakończenia
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Czy proces może być anulowany
        /// </summary>
        public bool CanBeCancelled { get; set; } = true;

        /// <summary>
        /// Dodatkowe dane o procesie
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Status zdrowia komponentu
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// Komponent jest w pełni sprawny
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// Komponent działa z ograniczeniami
        /// </summary>
        Degraded = 1,

        /// <summary>
        /// Komponent nie działa
        /// </summary>
        Unhealthy = 2
    }

    /// <summary>
    /// Poziom krytyczności błędu zdrowia
    /// </summary>
    public enum HealthErrorSeverity
    {
        /// <summary>
        /// Informacja
        /// </summary>
        Info = 0,

        /// <summary>
        /// Ostrzeżenie
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Błąd
        /// </summary>
        Error = 2,

        /// <summary>
        /// Krytyczny błąd
        /// </summary>
        Critical = 3
    }
} 