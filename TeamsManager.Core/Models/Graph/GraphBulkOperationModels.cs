using System;
using System.Collections.Generic;
using System.Globalization;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Progress reporting dla operacji masowych Graph API
    /// </summary>
    public class BulkOperationProgress
    {
        /// <summary>
        /// Całkowita liczba operacji do wykonania
        /// </summary>
        public int TotalOperations { get; set; }

        /// <summary>
        /// Liczba ukończonych operacji
        /// </summary>
        public int CompletedOperations { get; set; }

        /// <summary>
        /// Liczba udanych operacji
        /// </summary>
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// Liczba nieudanych operacji
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Procent ukończenia (0-100)
        /// </summary>
        public double PercentageComplete => TotalOperations != 0 ? 
            (double)CompletedOperations / TotalOperations * 100.0 : 0.0;

        /// <summary>
        /// Wskaźnik sukcesu (0-100%)
        /// </summary>
        public double SuccessRate => CompletedOperations != 0 ? 
            (double)SuccessfulOperations / CompletedOperations * 100.0 : 0.0;

        /// <summary>
        /// Aktualnie przetwarzana operacja
        /// </summary>
        public string? CurrentOperation { get; set; }

        /// <summary>
        /// Czas rozpoczęcia operacji
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Szacowany czas zakończenia
        /// </summary>
        public DateTime? EstimatedEndTime
        {
            get
            {
                if (CompletedOperations == 0 || TotalOperations == 0)
                    return null;

                var elapsed = DateTime.UtcNow - StartTime;
                var avgTimePerOperation = elapsed.TotalMilliseconds / CompletedOperations;
                var remainingOperations = TotalOperations - CompletedOperations;
                var estimatedRemainingTime = TimeSpan.FromMilliseconds(avgTimePerOperation * remainingOperations);

                return DateTime.UtcNow.Add(estimatedRemainingTime);
            }
        }

        /// <summary>
        /// Dodatkowe informacje o postępie
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Czy operacja jest ukończona
        /// </summary>
        public bool IsCompleted => CompletedOperations >= TotalOperations;

        /// <summary>
        /// Czy operacja jest w trakcie
        /// </summary>
        public bool IsInProgress => CompletedOperations > 0 && !IsCompleted;

        /// <summary>
        /// Komunikat statusu
        /// </summary>
        public string StatusMessage
        {
            get
            {
                if (TotalOperations == 0)
                    return "Oczekuje: 0 operacji do wykonania";
                
                if (IsCompleted)
                    return $"Ukończono: {SuccessfulOperations}/{TotalOperations} operacji zakończonych sukcesem";
                
                if (IsInProgress)
                    return $"W trakcie: {CompletedOperations}/{TotalOperations} ({PercentageComplete.ToString("F1", CultureInfo.InvariantCulture)}%)";
                    
                return $"Oczekuje: {TotalOperations} operacji do wykonania";
            }
        }
    }

    /// <summary>
    /// Operacja batch Graph API
    /// </summary>
    public class GraphBatchOperation
    {
        /// <summary>
        /// Unikalny ID operacji w batch
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Metoda HTTP (GET, POST, PATCH, DELETE)
        /// </summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>
        /// URL endpointu Graph API (bez domeny, np. /v1.0/users)
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Nagłówki żądania
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Treść żądania (dla POST, PATCH)
        /// </summary>
        public object? Body { get; set; }

        /// <summary>
        /// Typ operacji (dla logowania i raportowania)
        /// </summary>
        public string? OperationType { get; set; }

        /// <summary>
        /// ID encji, na której wykonywana jest operacja
        /// </summary>
        public string? EntityId { get; set; }

        /// <summary>
        /// Nazwa encji (dla raportowania)
        /// </summary>
        public string? EntityName { get; set; }

        /// <summary>
        /// Czy operacja jest krytyczna (wpływa na ogólny wynik batch)
        /// </summary>
        public bool IsCritical { get; set; } = true;

        /// <summary>
        /// Maksymalna liczba powtórzeń dla tej operacji
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Dodatkowe metadane operacji
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Tworzy operację GET
        /// </summary>
        /// <param name="url">URL endpointu</param>
        /// <param name="operationType">Typ operacji</param>
        /// <param name="entityId">ID encji</param>
        /// <returns>Operacja GET</returns>
        public static GraphBatchOperation CreateGet(string url, string? operationType = null, string? entityId = null)
        {
            return new GraphBatchOperation
            {
                Method = "GET",
                Url = url,
                OperationType = operationType,
                EntityId = entityId
            };
        }

        /// <summary>
        /// Tworzy operację POST
        /// </summary>
        /// <param name="url">URL endpointu</param>
        /// <param name="body">Treść żądania</param>
        /// <param name="operationType">Typ operacji</param>
        /// <param name="entityId">ID encji</param>
        /// <returns>Operacja POST</returns>
        public static GraphBatchOperation CreatePost(string url, object body, string? operationType = null, string? entityId = null)
        {
            return new GraphBatchOperation
            {
                Method = "POST",
                Url = url,
                Body = body,
                OperationType = operationType,
                EntityId = entityId
            };
        }

        /// <summary>
        /// Tworzy operację PATCH
        /// </summary>
        /// <param name="url">URL endpointu</param>
        /// <param name="body">Treść żądania</param>
        /// <param name="operationType">Typ operacji</param>
        /// <param name="entityId">ID encji</param>
        /// <returns>Operacja PATCH</returns>
        public static GraphBatchOperation CreatePatch(string url, object body, string? operationType = null, string? entityId = null)
        {
            return new GraphBatchOperation
            {
                Method = "PATCH",
                Url = url,
                Body = body,
                OperationType = operationType,
                EntityId = entityId
            };
        }

        /// <summary>
        /// Tworzy operację DELETE
        /// </summary>
        /// <param name="url">URL endpointu</param>
        /// <param name="operationType">Typ operacji</param>
        /// <param name="entityId">ID encji</param>
        /// <returns>Operacja DELETE</returns>
        public static GraphBatchOperation CreateDelete(string url, string? operationType = null, string? entityId = null)
        {
            return new GraphBatchOperation
            {
                Method = "DELETE",
                Url = url,
                OperationType = operationType,
                EntityId = entityId
            };
        }
    }
} 