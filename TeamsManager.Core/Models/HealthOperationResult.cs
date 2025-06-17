using System;
using System.Collections.Generic;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Wynik operacji monitorowania zdrowia systemu
    /// Rozszerzenie BulkOperationResult o specyficzne funkcjonalności health check
    /// </summary>
    public class HealthOperationResult
    {
        /// <summary>
        /// Czy operacja zakończyła się sukcesem
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Kompatybilność z orkiestratorem - settable success flag
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
        /// Typ operacji health check
        /// </summary>
        public string? OperationType { get; set; }
        
        /// <summary>
        /// Czas wykonania operacji w milisekundach
        /// </summary>
        public long? ExecutionTimeMs { get; set; }

        /// <summary>
        /// Lista pomyślnych operacji health check
        /// </summary>
        public List<HealthOperationSuccess> SuccessfulOperations { get; set; } = new List<HealthOperationSuccess>();
        
        /// <summary>
        /// Lista błędów operacji health check
        /// </summary>
        public List<HealthOperationError> Errors { get; set; } = new List<HealthOperationError>();

        /// <summary>
        /// Szczegółowe wyniki sprawdzenia zdrowia komponentów
        /// </summary>
        public List<HealthCheckDetail> HealthChecks { get; set; } = new List<HealthCheckDetail>();

        /// <summary>
        /// Metryki wydajności systemu
        /// </summary>
        public HealthMetrics? Metrics { get; set; }

        /// <summary>
        /// Rekomendacje naprawcze
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
    /// Pomyślna operacja health check
    /// </summary>
    public class HealthOperationSuccess
    {
        /// <summary>
        /// Nazwa operacji
        /// </summary>
        public string Operation { get; set; } = string.Empty;
        
        /// <summary>
        /// Komponent systemu
        /// </summary>
        public string Component { get; set; } = string.Empty;
        
        /// <summary>
        /// Nazwa komponentu (opcjonalna)
        /// </summary>
        public string? ComponentName { get; set; }
        
        /// <summary>
        /// Komunikat sukcesu
        /// </summary>
        public string? Message { get; set; }
        
        /// <summary>
        /// Dodatkowe dane operacji
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Błąd operacji health check
    /// </summary>
    public class HealthOperationError
    {
        /// <summary>
        /// Nazwa operacji
        /// </summary>
        public string Operation { get; set; } = string.Empty;
        
        /// <summary>
        /// Komponent systemu
        /// </summary>
        public string? Component { get; set; }
        
        /// <summary>
        /// Nazwa komponentu (opcjonalna)
        /// </summary>
        public string? ComponentName { get; set; }
        
        /// <summary>
        /// Komunikat błędu
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Wyjątek (jeśli wystąpił)
        /// </summary>
        public Exception? Exception { get; set; }
        
        /// <summary>
        /// Dodatkowe dane błędu
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
        /// Metryki cache - MIGRACJA ZAKOŃCZONA: używa GraphCacheMetrics
        /// </summary>
        public GraphCacheMetrics? CacheMetrics { get; set; }

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
        /// Status połączenia Graph API
        /// </summary>
        public string? GraphConnectionStatus { get; set; }

        

        /// <summary>
        /// Metryki specyficzne dla TeamsManager
        /// </summary>
        public Dictionary<string, object> TeamsManagerSpecificMetrics { get; set; } = new();
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
} 