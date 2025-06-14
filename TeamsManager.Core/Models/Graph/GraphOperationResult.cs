using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Wynik operacji Microsoft Graph API z zaawansowaną funkcjonalnością.
    /// Zachowuje kompatybilność z BulkOperationResult.
    /// </summary>
    /// <typeparam name="T">Typ danych zwracanych przez operację</typeparam>
    public class GraphOperationResult<T>
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
        /// Dane zwrócone przez operację.
        /// </summary>
        public T? Data { get; set; }

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
        /// ID żądania Graph API.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Kod błędu Graph API.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Szczegóły błędu Graph API.
        /// </summary>
        public string? ErrorDetails { get; set; }

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
        public List<GraphOperationSuccess> SuccessfulOperations { get; set; } = new List<GraphOperationSuccess>();

        /// <summary>
        /// Lista błędów operacji.
        /// </summary>
        public List<GraphOperationError> Errors { get; set; } = new List<GraphOperationError>();

        /// <summary>
        /// Informacje o rate limiting.
        /// </summary>
        public GraphRateLimitInfo? RateLimitInfo { get; set; }

        /// <summary>
        /// Dodatkowe metadane operacji.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Czy występują problemy z wydajnością.
        /// </summary>
        public bool HasPerformanceIssues => ExecutionTimeMs > 2000; // > 2 sekundy

        /// <summary>
        /// Czy występują problemy z rate limiting.
        /// </summary>
        public bool HasRateLimitIssues => RateLimitInfo?.IsLimitReached == true || 
                                          RateLimitInfo?.UsagePercentage > 90;

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
        /// Tworzy wynik sukcesu.
        /// </summary>
        /// <param name="data">Dane zwrócone przez operację</param>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="method">Metoda HTTP</param>
        /// <param name="executionTimeMs">Czas wykonania</param>
        /// <returns>Wynik sukcesu</returns>
        public static GraphOperationResult<T> CreateSuccess(T? data = default, string? endpoint = null, 
            string? method = null, long executionTimeMs = 0)
        {
            return new GraphOperationResult<T>
            {
                Success = true,
                IsSuccess = true,
                Data = data,
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
        /// <param name="errorCode">Kod błędu Graph API</param>
        /// <param name="executionTimeMs">Czas wykonania</param>
        /// <returns>Wynik błędu</returns>
        public static GraphOperationResult<T> CreateError(string errorMessage, string? endpoint = null,
            string? method = null, HttpStatusCode? statusCode = null, string? errorCode = null, 
            long executionTimeMs = 0)
        {
            return new GraphOperationResult<T>
            {
                Success = false,
                IsSuccess = false,
                ErrorMessage = errorMessage,
                GraphEndpoint = endpoint,
                HttpMethod = method,
                HttpStatusCode = statusCode,
                ErrorCode = errorCode,
                ExecutionTimeMs = executionTimeMs
            };
        }

        /// <summary>
        /// Tworzy wynik z cache.
        /// </summary>
        /// <param name="data">Dane z cache</param>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="etag">ETag</param>
        /// <returns>Wynik z cache</returns>
        public static GraphOperationResult<T> CreateFromCache(T? data, string? endpoint = null, string? etag = null)
        {
            return new GraphOperationResult<T>
            {
                Success = true,
                IsSuccess = true,
                Data = data,
                GraphEndpoint = endpoint,
                FromCache = true,
                ETag = etag,
                ExecutionTimeMs = 0
            };
        }

        /// <summary>
        /// Tworzy wynik operacji batch.
        /// </summary>
        /// <param name="successfulOperations">Pomyślne operacje</param>
        /// <param name="errors">Błędy operacji</param>
        /// <param name="batchId">ID batcha</param>
        /// <returns>Wynik batch</returns>
        public static GraphOperationResult<T> CreateBatchResult(List<GraphOperationSuccess> successfulOperations,
            List<GraphOperationError> errors, string? batchId = null)
        {
            var result = new GraphOperationResult<T>
            {
                Success = errors.Count == 0,
                IsSuccess = errors.Count == 0,
                SuccessfulOperations = successfulOperations,
                Errors = errors,
                GraphEndpoint = "/v1.0/$batch",
                HttpMethod = "POST"
            };

            if (!string.IsNullOrEmpty(batchId))
            {
                result.AddMetadata("BatchId", batchId);
            }

            return result;
        }

        /// <summary>
        /// Pobiera szczegółowy raport wyniku.
        /// </summary>
        /// <returns>Szczegółowy raport</returns>
        public string GetDetailedResult()
        {
            var report = new List<string>
            {
                "=== WYNIK OPERACJI MICROSOFT GRAPH API ===",
                $"Status: {(Success ? "SUKCES" : "BŁĄD")}",
                $"Endpoint: {GraphEndpoint ?? "Nieznany"}",
                $"Metoda HTTP: {HttpMethod ?? "Nieznana"}",
                $"Kod statusu: {HttpStatusCode?.ToString() ?? "Nieznany"}",
                $"Czas wykonania: {ExecutionTimeMs} ms",
                $"Data: {ProcessedAt:yyyy-MM-dd HH:mm:ss} UTC",
                ""
            };

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

            if (!string.IsNullOrEmpty(RequestId))
            {
                report.Add($"Request ID: {RequestId}");
                report.Add("");
            }

            if (!Success)
            {
                report.Add("=== BŁĄD ===");
                report.Add($"Komunikat: {ErrorMessage ?? "Nieznany"}");
                if (!string.IsNullOrEmpty(ErrorCode))
                {
                    report.Add($"Kod błędu: {ErrorCode}");
                }
                if (!string.IsNullOrEmpty(ErrorDetails))
                {
                    report.Add($"Szczegóły: {ErrorDetails}");
                }
                report.Add("");
            }

            if (SuccessfulOperations.Count > 0)
            {
                report.Add("=== POMYŚLNE OPERACJE ===");
                foreach (var op in SuccessfulOperations)
                {
                    report.Add($"✓ {op.Operation}: {op.EntityName ?? op.EntityId}");
                }
                report.Add("");
            }

            if (Errors.Count > 0)
            {
                report.Add("=== BŁĘDY OPERACJI ===");
                foreach (var error in Errors)
                {
                    report.Add($"❌ {error.Operation}: {error.Message}");
                }
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

            if (Metadata.Count > 0)
            {
                report.Add("=== METADANE ===");
                foreach (var meta in Metadata)
                {
                    report.Add($"{meta.Key}: {meta.Value}");
                }
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
            var time = ExecutionTimeMs;
            var cache = FromCache ? " (z cache)" : "";

            return $"{status}: {endpoint} - {time}ms{cache}";
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
        /// <typeparam name="TMetadata">Typ metadanych</typeparam>
        /// <param name="key">Klucz</param>
        /// <returns>Metadane lub default</returns>
        public TMetadata? GetMetadata<TMetadata>(string key)
        {
            if (Metadata.TryGetValue(key, out var value) && value is TMetadata metadata)
            {
                return metadata;
            }
            return default;
        }

        /// <summary>
        /// Kompatybilność z istniejącym API - konwersja do bool.
        /// </summary>
        public static implicit operator bool(GraphOperationResult<T> result)
        {
            return result.Success;
        }
    }

    /// <summary>
    /// Reprezentuje pomyślną operację Graph API.
    /// </summary>
    public class GraphOperationSuccess
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
    /// Reprezentuje błąd operacji Graph API.
    /// </summary>
    public class GraphOperationError
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
    /// Metryki serwisu Graph API.
    /// </summary>
    public class GraphServiceMetrics
    {
        /// <summary>
        /// Całkowita liczba żądań.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Liczba pomyślnych żądań.
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// Liczba nieudanych żądań.
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// Średni czas odpowiedzi w milisekundach.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Wskaźnik sukcesu (0-100%).
        /// </summary>
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100.0 : 0.0;

        /// <summary>
        /// Ostatnia aktualizacja metryk.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Opcje cache warming dla Graph API.
    /// </summary>
    public class GraphCacheWarmupOptions
    {
        /// <summary>
        /// Czy warming jest włączony.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Timeout dla operacji warming w sekundach.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maksymalna liczba równoczesnych operacji.
        /// </summary>
        public int MaxConcurrency { get; set; } = 5;

        /// <summary>
        /// Lista endpointów do warming.
        /// </summary>
        public List<string> Endpoints { get; set; } = new List<string>();
    }

    /// <summary>
    /// Wynik operacji cache warming.
    /// </summary>
    public class GraphCacheWarmupResult
    {
        /// <summary>
        /// Czy warming zakończył się sukcesem.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Liczba endpointów, które zostały przygotowane.
        /// </summary>
        public int WarmedEndpoints { get; set; }

        /// <summary>
        /// Całkowita liczba endpointów.
        /// </summary>
        public int TotalEndpoints { get; set; }

        /// <summary>
        /// Czas trwania warming w milisekundach.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Lista błędów podczas warming.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Wskaźnik sukcesu (0-100%).
        /// </summary>
        public double SuccessRate => TotalEndpoints > 0 ? (double)WarmedEndpoints / TotalEndpoints * 100.0 : 0.0;
    }
} 