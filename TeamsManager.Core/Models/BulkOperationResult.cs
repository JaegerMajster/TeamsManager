using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Wynik pojedynczej operacji w ramach operacji masowej PowerShell
    /// </summary>
    public class BulkOperationResult
    {
        /// <summary>
        /// Czy operacja zakończyła się sukcesem
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Komunikat błędu w przypadku niepowodzenia
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Timestamp przetworzenia operacji
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Dodatkowe dane specyficzne dla operacji
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
        
        /// <summary>
        /// Typ operacji PowerShell (dla debugowania)
        /// </summary>
        public string? OperationType { get; set; }
        
        /// <summary>
        /// Czas wykonania operacji w milisekundach
        /// </summary>
        public long? ExecutionTimeMs { get; set; }

        // ===== WŁAŚCIWOŚCI KOMPATYBILNOŚCI Z ORKIESTRATOREM =====
        
        /// <summary>
        /// Kompatybilność z orkiestratorem - settable success flag
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// Lista pomyślnych operacji
        /// </summary>
        public List<BulkOperationSuccess> SuccessfulOperations { get; set; } = new List<BulkOperationSuccess>();
        
        /// <summary>
        /// Lista błędów operacji
        /// </summary>
        public List<BulkOperationError> Errors { get; set; } = new List<BulkOperationError>();

        /// <summary>
        /// Konstruktor dla wyniku sukcesu
        /// </summary>
        public static BulkOperationResult CreateSuccess(string? operationType = null, long? executionTimeMs = null)
        {
            return new BulkOperationResult
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
        public static BulkOperationResult CreateError(string errorMessage, string? operationType = null, long? executionTimeMs = null)
        {
            return new BulkOperationResult
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
        public static implicit operator bool(BulkOperationResult result)
        {
            return result.Success;
        }
    }

    /// <summary>
    /// Reprezentuje pomyślną operację w ramach operacji masowej
    /// </summary>
    public class BulkOperationSuccess
    {
        /// <summary>
        /// Nazwa operacji
        /// </summary>
        public string Operation { get; set; } = string.Empty;
        
        /// <summary>
        /// ID encji której dotyczy operacja
        /// </summary>
        public string EntityId { get; set; } = string.Empty;
        
        /// <summary>
        /// Nazwa encji
        /// </summary>
        public string? EntityName { get; set; }
        
        /// <summary>
        /// Komunikat o sukcesie
        /// </summary>
        public string? Message { get; set; }
        
        /// <summary>
        /// Dodatkowe dane
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Reprezentuje błąd operacji w ramach operacji masowej
    /// </summary>
    public class BulkOperationError
    {
        /// <summary>
        /// Nazwa operacji która się nie powiodła
        /// </summary>
        public string Operation { get; set; } = string.Empty;
        
        /// <summary>
        /// ID encji której dotyczy błąd
        /// </summary>
        public string? EntityId { get; set; }
        
        /// <summary>
        /// Nazwa encji
        /// </summary>
        public string? EntityName { get; set; }
        
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
    }
} 