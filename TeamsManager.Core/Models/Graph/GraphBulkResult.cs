using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Wynik operacji masowej Microsoft Graph API z zaawansowaną funkcjonalnością.
    /// Zachowuje kompatybilność z BulkOperationResult.
    /// </summary>
    public class GraphBulkResult
    {
        /// <summary>
        /// Czy operacja zakończyła się sukcesem.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Kompatybilność z orkiestratorem - settable success flag.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Komunikat błędu w przypadku niepowodzenia.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// ID żądania Graph API.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// ID batcha Graph API.
        /// </summary>
        public string? BatchId { get; set; }

        /// <summary>
        /// Endpoint Graph API.
        /// </summary>
        public string? GraphEndpoint { get; set; }

        /// <summary>
        /// Metoda HTTP używana w żądaniu.
        /// </summary>
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public HttpStatusCode? HttpStatusCode { get; set; }

        /// <summary>
        /// Czy dane pochodzą z cache.
        /// </summary>
        public bool FromCache { get; set; }

        /// <summary>
        /// ETag dla cache validation.
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// Czy operacja była powtarzana.
        /// </summary>
        public bool WasRetried { get; set; }

        /// <summary>
        /// Liczba powtórzeń.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Czas wykonania operacji w milisekundach.
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Timestamp przetworzenia operacji.
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Lista pomyślnych operacji.
        /// </summary>
        public List<GraphBulkOperationSuccess> SuccessfulOperations { get; set; } = new List<GraphBulkOperationSuccess>();

        /// <summary>
        /// Lista błędów operacji.
        /// </summary>
        public List<GraphBulkOperationError> Errors { get; set; } = new List<GraphBulkOperationError>();

        /// <summary>
        /// Wyniki operacji batch (dla Graph Batch API).
        /// </summary>
        public List<GraphBatchOperationResult> BatchResults { get; set; } = new List<GraphBatchOperationResult>();

        /// <summary>
        /// Informacje o rate limiting.
        /// </summary>
        public GraphRateLimitStatus? RateLimitInfo { get; set; }

        /// <summary>
        /// Dodatkowe metadane operacji.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Czy występują problemy z wydajnością.
        /// </summary>
        public bool HasPerformanceIssues => ExecutionTimeMs > 5000; // > 5 sekund dla bulk

        /// <summary>
        /// Czy występują problemy z rate limiting.
        /// </summary>
        public bool HasRateLimitIssues => RateLimitInfo?.IsLimitReached == true || 
                                          RateLimitInfo?.UsagePercentage > 90;

        /// <summary>
        /// Kompatybilność z API - liczba dodanych elementów.
        /// </summary>
        public int AddedCount => SuccessfulOperations.Count(s => s.Operation?.Contains("Add") == true || s.Operation?.Contains("Create") == true);

        /// <summary>
        /// Kompatybilność z API - liczba usuniętych elementów.
        /// </summary>
        public int RemovedCount => SuccessfulOperations.Count(s => s.Operation?.Contains("Remove") == true || s.Operation?.Contains("Delete") == true);

        /// <summary>
        /// Czy operacja powinna być powtórzona.
        /// </summary>
        public bool ShouldRetry => !Success && RetryCount < 3 && 
                                   (HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                                    HttpStatusCode == System.Net.HttpStatusCode.InternalServerError ||
                                    HttpStatusCode == System.Net.HttpStatusCode.BadGateway ||
                                    HttpStatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                                    HttpStatusCode == System.Net.HttpStatusCode.GatewayTimeout);

        /// <summary>
        /// Całkowita liczba operacji.
        /// </summary>
        public int TotalOperations => SuccessfulOperations.Count + Errors.Count;

        /// <summary>
        /// Wskaźnik sukcesu (0-100%).
        /// </summary>
        public double SuccessRate => TotalOperations > 0 ? 
            (double)SuccessfulOperations.Count / TotalOperations * 100.0 : 0.0;

        /// <summary>
        /// Tworzy wynik sukcesu.
        /// </summary>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="method">Metoda HTTP</param>
        /// <param name="executionTimeMs">Czas wykonania</param>
        /// <returns>Wynik sukcesu</returns>
        public static GraphBulkResult CreateSuccess(string? endpoint = null, string? method = null, 
            long executionTimeMs = 0)
        {
            return new GraphBulkResult
            {
                Success = true,
                IsSuccess = true,
                GraphEndpoint = endpoint,
                HttpMethod = method,
                ExecutionTimeMs = executionTimeMs,
                HttpStatusCode = System.Net.HttpStatusCode.OK
            };
        }

        /// <summary>
        /// Tworzy wynik błędu.
        /// </summary>
        /// <param name="errorMessage">Komunikat błędu</param>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="method">Metoda HTTP</param>
        /// <param name="statusCode">Kod statusu HTTP</param>
        /// <param name="executionTimeMs">Czas wykonania</param>
        /// <returns>Wynik błędu</returns>
        public static GraphBulkResult CreateError(string errorMessage, string? endpoint = null,
            string? method = null, HttpStatusCode? statusCode = null, long executionTimeMs = 0)
        {
            return new GraphBulkResult
            {
                Success = false,
                IsSuccess = false,
                ErrorMessage = errorMessage,
                GraphEndpoint = endpoint,
                HttpMethod = method,
                HttpStatusCode = statusCode,
                ExecutionTimeMs = executionTimeMs
            };
        }

        /// <summary>
        /// Tworzy wynik z cache.
        /// </summary>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="etag">ETag</param>
        /// <returns>Wynik z cache</returns>
        public static GraphBulkResult CreateFromCache(string? endpoint = null, string? etag = null)
        {
            return new GraphBulkResult
            {
                Success = true,
                IsSuccess = true,
                GraphEndpoint = endpoint,
                FromCache = true,
                ETag = etag,
                ExecutionTimeMs = 0
            };
        }

        /// <summary>
        /// Tworzy wynik operacji batch.
        /// </summary>
        /// <param name="batchResults">Wyniki batch</param>
        /// <param name="batchId">ID batcha</param>
        /// <returns>Wynik batch</returns>
        public static GraphBulkResult CreateBatchResult(List<GraphBatchOperationResult> batchResults, 
            string? batchId = null)
        {
            var successCount = batchResults.Count(r => r.IsSuccessful);
            var result = new GraphBulkResult
            {
                Success = successCount == batchResults.Count,
                IsSuccess = successCount == batchResults.Count,
                BatchResults = batchResults,
                GraphEndpoint = "/v1.0/$batch",
                HttpMethod = "POST"
            };

            if (!string.IsNullOrEmpty(batchId))
            {
                result.BatchId = batchId;
                result.AddMetadata("BatchId", batchId);
            }

            return result;
        }

        /// <summary>
        /// Dodaje pomyślną operację.
        /// </summary>
        /// <param name="operation">Operacja</param>
        public void AddSuccess(GraphBulkOperationSuccess operation)
        {
            SuccessfulOperations.Add(operation);
            UpdateSuccessStatus();
        }

        /// <summary>
        /// Dodaje błąd operacji.
        /// </summary>
        /// <param name="error">Błąd</param>
        public void AddError(GraphBulkOperationError error)
        {
            Errors.Add(error);
            UpdateSuccessStatus();
        }

        /// <summary>
        /// Dodaje metadane do wyniku.
        /// </summary>
        /// <param name="key">Klucz</param>
        /// <param name="value">Wartość</param>
        public void AddMetadata(string key, object value)
        {
            Metadata[key] = value;
        }

        /// <summary>
        /// Pobiera metadane o określonym kluczu.
        /// </summary>
        /// <typeparam name="T">Typ metadanych</typeparam>
        /// <param name="key">Klucz</param>
        /// <returns>Metadane lub default</returns>
        public T? GetMetadata<T>(string key)
        {
            if (Metadata.TryGetValue(key, out var value) && value is T metadata)
            {
                return metadata;
            }
            return default;
        }

        /// <summary>
        /// Pobiera szczegółowy raport wyniku.
        /// </summary>
        /// <returns>Szczegółowy raport</returns>
        public string GetDetailedResult()
        {
            var report = new List<string>
            {
                "=== WYNIK OPERACJI MASOWEJ MICROSOFT GRAPH API ===",
                $"Status: {(Success ? "SUKCES" : "BŁĄD")}",
                $"Endpoint: {GraphEndpoint ?? "Nieznany"}",
                $"Metoda HTTP: {HttpMethod ?? "Nieznana"}",
                $"Czas wykonania: {ExecutionTimeMs} ms",
                $"Data: {ProcessedAt:yyyy-MM-dd HH:mm:ss} UTC",
                $"Operacje: {SuccessfulOperations.Count} sukces / {Errors.Count} błąd / {TotalOperations} razem",
                $"Wskaźnik sukcesu: {SuccessRate:F1}%",
                ""
            };

            if (!string.IsNullOrEmpty(BatchId))
            {
                report.Add($"Batch ID: {BatchId}");
                report.Add("");
            }

            if (FromCache)
            {
                report.Add("✓ Dane pobrane z cache");
                report.Add($"ETag: {ETag ?? "Brak"}");
                report.Add("");
            }

            if (WasRetried)
            {
                report.Add($"🔄 Operacja powtarzana {RetryCount} razy");
                report.Add("");
            }

            if (HasPerformanceIssues)
            {
                report.Add("⚠️ OSTRZEŻENIE: Wykryto problemy z wydajnością");
                report.Add("");
            }

            if (HasRateLimitIssues)
            {
                report.Add("⚠️ OSTRZEŻENIE: Wykryto problemy z rate limiting");
                report.Add("");
            }

            report.Add("=== KONIEC RAPORTU ===");
            return string.Join(Environment.NewLine, report);
        }

        /// <summary>
        /// Pobiera podsumowanie wyniku.
        /// </summary>
        /// <returns>Podsumowanie</returns>
        public string GetSummary()
        {
            var status = Success ? "SUKCES" : "BŁĄD";
            var endpoint = GraphEndpoint ?? "Nieznany";
            var operations = $"{SuccessfulOperations.Count}/{TotalOperations}";
            var time = ExecutionTimeMs;
            var cache = FromCache ? " (z cache)" : "";

            return $"{status}: {endpoint} - {operations} operacji - {time}ms{cache}";
        }

        /// <summary>
        /// Kompatybilność z istniejącym API - konwersja do bool.
        /// </summary>
        public static implicit operator bool(GraphBulkResult result)
        {
            return result.Success;
        }

        /// <summary>
        /// Aktualizuje status sukcesu na podstawie operacji.
        /// </summary>
        private void UpdateSuccessStatus()
        {
            Success = Errors.Count == 0 && TotalOperations > 0;
            IsSuccess = Success;
        }
    }

    /// <summary>
    /// Reprezentuje pomyślną operację bulk Graph API.
    /// </summary>
    public class GraphBulkOperationSuccess
    {
        /// <summary>
        /// Nazwa operacji.
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// ID encji której dotyczy operacja.
        /// </summary>
        public string EntityId { get; set; } = string.Empty;

        /// <summary>
        /// Nazwa encji.
        /// </summary>
        public string? EntityName { get; set; }

        /// <summary>
        /// Komunikat o sukcesie.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Endpoint Graph API.
        /// </summary>
        public string? GraphEndpoint { get; set; }

        /// <summary>
        /// Metoda HTTP.
        /// </summary>
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public HttpStatusCode? HttpStatusCode { get; set; }

        /// <summary>
        /// Czas wykonania w milisekundach.
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Dodatkowe dane.
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Reprezentuje błąd operacji bulk Graph API.
    /// </summary>
    public class GraphBulkOperationError
    {
        /// <summary>
        /// Nazwa operacji która się nie powiodła.
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// ID encji której dotyczy błąd.
        /// </summary>
        public string? EntityId { get; set; }

        /// <summary>
        /// Nazwa encji.
        /// </summary>
        public string? EntityName { get; set; }

        /// <summary>
        /// Komunikat błędu.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Kod błędu Graph API.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Szczegóły błędu Graph API.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Endpoint Graph API.
        /// </summary>
        public string? GraphEndpoint { get; set; }

        /// <summary>
        /// Metoda HTTP.
        /// </summary>
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public HttpStatusCode? HttpStatusCode { get; set; }

        /// <summary>
        /// ID żądania Graph API.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Wyjątek który spowodował błąd.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Dodatkowe dane o błędzie.
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Wynik pojedynczej operacji w ramach Graph Batch API.
    /// Pełne wsparcie dla POST /v1.0/$batch.
    /// </summary>
    public class GraphBatchOperationResult
    {
        /// <summary>
        /// ID operacji w batch.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// URL żądania.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Metoda HTTP.
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public int? Status { get; set; }

        /// <summary>
        /// Nagłówki odpowiedzi.
        /// </summary>
        public Dictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// Treść odpowiedzi.
        /// </summary>
        public object? Body { get; set; }

        /// <summary>
        /// Kod błędu (jeśli wystąpił).
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Komunikat błędu.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Szczegóły błędu.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Czy operacja zakończyła się sukcesem.
        /// </summary>
        public bool IsSuccessful => Status >= 200 && Status < 300;

        /// <summary>
        /// Czy wystąpił błąd.
        /// </summary>
        public bool HasError => !IsSuccessful;
    }
} 