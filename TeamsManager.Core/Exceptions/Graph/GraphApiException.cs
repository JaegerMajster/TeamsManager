using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Exceptions.Graph
{
    /// <summary>
    /// Wyjątek reprezentujący błędy Microsoft Graph API.
    /// Implementuje ETAP 1.4.3 - GraphApiException.
    /// </summary>
    public class GraphApiException : Exception
    {
        /// <summary>
        /// Endpoint Graph API, na którym wystąpił błąd.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Metoda HTTP użyta w żądaniu.
        /// </summary>
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// Kod błędu Graph API.
        /// </summary>
        public string? GraphErrorCode { get; set; }

        /// <summary>
        /// Komunikat błędu Graph API.
        /// </summary>
        public string? GraphErrorMessage { get; set; }

        /// <summary>
        /// Szczegóły błędu Graph API.
        /// </summary>
        public string? GraphErrorDetails { get; set; }

        /// <summary>
        /// ID żądania.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// ID korelacji żądania.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Czas wystąpienia błędu.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// System metadanych dla dodatkowych informacji.
        /// </summary>
        private readonly Dictionary<string, object> _metadata = new Dictionary<string, object>();

        /// <summary>
        /// Czy błąd jest związany z uprawnieniami.
        /// </summary>
        public bool IsPermissionError => HttpStatusCode == 403 || 
                                        GraphErrorCode?.Contains("Forbidden") == true ||
                                        GraphErrorCode?.Contains("InsufficientPermissions") == true;

        /// <summary>
        /// Czy błąd jest związany z uwierzytelnieniem.
        /// </summary>
        public bool IsAuthenticationError => HttpStatusCode == 401 || 
                                           GraphErrorCode?.Contains("Unauthorized") == true ||
                                           GraphErrorCode?.Contains("InvalidAuthenticationToken") == true;

        /// <summary>
        /// Czy błąd jest związany z rate limiting.
        /// </summary>
        public bool IsRateLimitError => HttpStatusCode == 429 || 
                                       GraphErrorCode?.Contains("TooManyRequests") == true;

        /// <summary>
        /// Czy błąd jest związany z walidacją danych.
        /// </summary>
        public bool IsValidationError => HttpStatusCode == 400 || 
                                        GraphErrorCode?.Contains("BadRequest") == true ||
                                        GraphErrorCode?.Contains("InvalidRequest") == true;

        /// <summary>
        /// Czy błąd jest związany z nieznalezionym zasobem.
        /// </summary>
        public bool IsNotFoundError => HttpStatusCode == 404 || 
                                      GraphErrorCode?.Contains("NotFound") == true ||
                                      GraphErrorCode?.Contains("ItemNotFound") == true;

        /// <summary>
        /// Czy błąd jest związany z konfliktem zasobów.
        /// </summary>
        public bool IsConflictError => HttpStatusCode == 409 || 
                                      GraphErrorCode?.Contains("Conflict") == true ||
                                      GraphErrorCode?.Contains("ResourceExists") == true;

        /// <summary>
        /// Konstruktor domyślny.
        /// </summary>
        public GraphApiException() : base("Błąd Microsoft Graph API")
        {
        }

        /// <summary>
        /// Konstruktor z komunikatem.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        public GraphApiException(string message) : base(message)
        {
        }

        /// <summary>
        /// Konstruktor z komunikatem i wewnętrznym wyjątkiem.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="innerException">Wewnętrzny wyjątek</param>
        public GraphApiException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Sprawdza czy błąd można ponowić.
        /// </summary>
        /// <returns>True jeśli błąd można ponowić</returns>
        public bool CanRetry()
        {
            // Błędy uwierzytelnienia i uprawnień zwykle nie można ponowić
            if (IsAuthenticationError || IsPermissionError)
                return false;

            // Błędy walidacji nie można ponowić
            if (IsValidationError)
                return false;

            // Błędy not found i conflict nie można ponowić
            if (IsNotFoundError || IsConflictError)
                return false;

            // Rate limiting można ponowić po czasie oczekiwania
            if (IsRateLimitError)
                return true;

            // Błędy serwera (5xx) można ponowić
            if (HttpStatusCode >= 500 && HttpStatusCode < 600)
                return true;

            // Błędy timeout można ponowić
            if (HttpStatusCode == 408)
                return true;

            return false;
        }

        /// <summary>
        /// Pobiera zalecany czas oczekiwania przed ponowieniem żądania.
        /// </summary>
        /// <returns>Czas oczekiwania w sekundach</returns>
        public int GetRecommendedRetryDelay()
        {
            // Dla rate limiting domyślnie 60 sekund
            if (IsRateLimitError)
                return 60;

            // Dla błędów serwera exponential backoff
            if (HttpStatusCode >= 500 && HttpStatusCode < 600)
                return 30;

            // Dla timeout krótsze oczekiwanie
            if (HttpStatusCode == 408)
                return 10;

            return 5; // Domyślnie 5 sekund
        }

        /// <summary>
        /// Dodaje metadane do wyjątku.
        /// </summary>
        /// <param name="key">Klucz metadanych</param>
        /// <param name="value">Wartość metadanych</param>
        public void AddMetadata(string key, object value)
        {
            _metadata[key] = value;
        }

        /// <summary>
        /// Pobiera metadane z wyjątku.
        /// </summary>
        /// <typeparam name="T">Typ metadanych</typeparam>
        /// <param name="key">Klucz metadanych</param>
        /// <returns>Wartość metadanych lub default(T)</returns>
        public T? GetMetadata<T>(string key)
        {
            if (_metadata.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return default;
        }

        /// <summary>
        /// Pobiera szczegółowy komunikat błędu.
        /// </summary>
        /// <returns>Szczegółowy komunikat błędu</returns>
        public string GetDetailedErrorMessage()
        {
            var details = new List<string>
            {
                $"Błąd Graph API: {GraphErrorMessage ?? Message}"
            };

            if (!string.IsNullOrEmpty(Endpoint))
                details.Add($"Endpoint: {HttpMethod} {Endpoint}");

            if (HttpStatusCode.HasValue)
                details.Add($"Status HTTP: {HttpStatusCode}");

            if (!string.IsNullOrEmpty(GraphErrorCode))
                details.Add($"Kod błędu: {GraphErrorCode}");

            if (!string.IsNullOrEmpty(GraphErrorDetails))
                details.Add($"Szczegóły: {GraphErrorDetails}");

            if (!string.IsNullOrEmpty(RequestId))
                details.Add($"Request ID: {RequestId}");

            if (!string.IsNullOrEmpty(CorrelationId))
                details.Add($"Correlation ID: {CorrelationId}");

            details.Add($"Czas: {Timestamp:yyyy-MM-dd HH:mm:ss} UTC");

            if (CanRetry())
            {
                var retryDelay = GetRecommendedRetryDelay();
                details.Add($"Można ponowić za: {retryDelay} sekund");
            }

            if (_metadata.Count > 0)
            {
                details.Add("Metadane:");
                foreach (var kvp in _metadata)
                {
                    details.Add($"  {kvp.Key}: {kvp.Value}");
                }
            }

            return string.Join(Environment.NewLine, details);
        }

        /// <summary>
        /// Tworzy wyjątek błędu uprawnień.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphApiException</returns>
        public static GraphApiException CreatePermissionError(string message, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            return new GraphApiException(message)
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                HttpStatusCode = 403,
                GraphErrorCode = "Forbidden",
                GraphErrorMessage = message,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek błędu walidacji.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="details">Szczegóły błędu</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphApiException</returns>
        public static GraphApiException CreateValidationError(string message, string? details = null, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            return new GraphApiException(message)
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                HttpStatusCode = 400,
                GraphErrorCode = "BadRequest",
                GraphErrorMessage = message,
                GraphErrorDetails = details,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek błędu nieznalezionego zasobu.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphApiException</returns>
        public static GraphApiException CreateNotFoundError(string message, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            return new GraphApiException(message)
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                HttpStatusCode = 404,
                GraphErrorCode = "NotFound",
                GraphErrorMessage = message,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek błędu konfliktu zasobów.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphApiException</returns>
        public static GraphApiException CreateConflictError(string message, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            return new GraphApiException(message)
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                HttpStatusCode = 409,
                GraphErrorCode = "Conflict",
                GraphErrorMessage = message,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek błędu operacji bulk.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="totalOperations">Całkowita liczba operacji</param>
        /// <param name="failedOperations">Liczba nieudanych operacji</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphApiException</returns>
        public static GraphApiException CreateBulkOperationError(string message, int totalOperations, int failedOperations, string? endpoint = null, string? requestId = null)
        {
            var exception = new GraphApiException(message)
            {
                Endpoint = endpoint,
                HttpMethod = "POST",
                HttpStatusCode = 207, // Multi-Status
                GraphErrorCode = "BulkOperationFailed",
                GraphErrorMessage = message,
                RequestId = requestId
            };

            exception.AddMetadata("TotalOperations", totalOperations);
            exception.AddMetadata("FailedOperations", failedOperations);
            exception.AddMetadata("SuccessfulOperations", totalOperations - failedOperations);
            exception.AddMetadata("FailureRate", (double)failedOperations / totalOperations);

            return exception;
        }

        /// <summary>
        /// Tworzy wyjątek błędu serwera.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="httpStatusCode">Kod statusu HTTP</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphApiException</returns>
        public static GraphApiException CreateServerError(string message, int httpStatusCode, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            return new GraphApiException(message)
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                HttpStatusCode = httpStatusCode,
                GraphErrorCode = "InternalServerError",
                GraphErrorMessage = message,
                RequestId = requestId
            };
        }
    }
} 