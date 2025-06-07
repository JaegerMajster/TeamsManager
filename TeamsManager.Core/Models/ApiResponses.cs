using System;
using System.ComponentModel.DataAnnotations;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Wspólna odpowiedź dla operacji anulowania procesów orkiestratorów
    /// </summary>
    public class ProcessCancelResponse
    {
        /// <summary>
        /// Identyfikator procesu
        /// </summary>
        public string ProcessId { get; set; } = string.Empty;

        /// <summary>
        /// Czy anulowanie się powiodło
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Komunikat opisujący wynik operacji
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp operacji anulowania
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Typ procesu który został anulowany
        /// </summary>
        public string? ProcessType { get; set; }

        /// <summary>
        /// Tworzy odpowiedź sukcesu
        /// </summary>
        public static ProcessCancelResponse CreateSuccess(string processId, string message, string? processType = null)
        {
            return new ProcessCancelResponse
            {
                ProcessId = processId,
                Success = true,
                Message = message,
                ProcessType = processType,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Tworzy odpowiedź błędu
        /// </summary>
        public static ProcessCancelResponse CreateError(string processId, string message, string? processType = null)
        {
            return new ProcessCancelResponse
            {
                ProcessId = processId,
                Success = false,
                Message = message,
                ProcessType = processType,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Wspólna odpowiedź dla statusu procesów orkiestratorów
    /// </summary>
    public class ProcessStatusResponse<T> where T : class
    {
        /// <summary>
        /// Czy zapytanie się powiodło
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Komunikat wyniku
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Lista aktywnych procesów
        /// </summary>
        public T[] Processes { get; set; } = Array.Empty<T>();

        /// <summary>
        /// Liczba aktywnych procesów
        /// </summary>
        public int TotalCount => Processes.Length;

        /// <summary>
        /// Timestamp odpowiedzi
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tworzy odpowiedź sukcesu
        /// </summary>
        public static ProcessStatusResponse<T> CreateSuccess(T[] processes, string? message = null)
        {
            return new ProcessStatusResponse<T>
            {
                Success = true,
                Message = message ?? $"Znaleziono {processes.Length} aktywnych procesów",
                Processes = processes,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Tworzy odpowiedź błędu
        /// </summary>
        public static ProcessStatusResponse<T> CreateError(string message)
        {
            return new ProcessStatusResponse<T>
            {
                Success = false,
                Message = message,
                Processes = Array.Empty<T>(),
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Wspólny interfejs dla statusów procesów orkiestratorów
    /// </summary>
    public interface IProcessStatus
    {
        string ProcessId { get; set; }
        string ProcessType { get; set; }
        DateTime StartedAt { get; set; }
        DateTime? CompletedAt { get; set; }
        string Status { get; set; }
        int TotalItems { get; set; }
        int ProcessedItems { get; set; }
        int FailedItems { get; set; }
        double ProgressPercentage { get; }
        string? CurrentOperation { get; set; }
        string? ErrorMessage { get; set; }
        bool CanBeCancelled { get; set; }
    }
} 