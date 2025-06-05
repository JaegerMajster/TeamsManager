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

        /// <summary>
        /// Konstruktor dla wyniku sukcesu
        /// </summary>
        public static BulkOperationResult CreateSuccess(string? operationType = null, long? executionTimeMs = null)
        {
            return new BulkOperationResult
            {
                Success = true,
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
} 