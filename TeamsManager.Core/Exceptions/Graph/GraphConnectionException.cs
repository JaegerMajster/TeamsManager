using System;
using System.Collections.Generic;
using System.Net;

namespace TeamsManager.Core.Exceptions.Graph
{
    /// <summary>
    /// Wyjątek reprezentujący błędy połączenia z Microsoft Graph API.
    /// Implementuje ETAP 1.4.2 - GraphConnectionException.
    /// </summary>
    public class GraphConnectionException : Exception
    {
        /// <summary>
        /// Endpoint Graph API, na którym wystąpił błąd.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// Kod błędu Graph API.
        /// </summary>
        public string? GraphErrorCode { get; set; }

        /// <summary>
        /// Szczegóły błędu Graph API.
        /// </summary>
        public string? GraphErrorDetails { get; set; }

        /// <summary>
        /// ID żądania.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Czas oczekiwania przed ponowieniem żądania (w sekundach).
        /// </summary>
        public int? RetryAfter { get; set; }

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
        /// Konstruktor domyślny.
        /// </summary>
        public GraphConnectionException() : base("Błąd połączenia z Microsoft Graph API")
        {
        }

        /// <summary>
        /// Konstruktor z komunikatem.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        public GraphConnectionException(string message) : base(message)
        {
        }

        /// <summary>
        /// Konstruktor z komunikatem i wewnętrznym wyjątkiem.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="innerException">Wewnętrzny wyjątek</param>
        public GraphConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Sprawdza czy błąd można ponowić.
        /// </summary>
        /// <returns>True jeśli błąd można ponowić</returns>
        public bool CanRetry()
        {
            // Błędy uwierzytelnienia zwykle nie można ponowić
            if (IsAuthenticationError)
                return false;

            // Rate limiting można ponowić po czasie oczekiwania
            if (IsRateLimitError)
                return true;

            // Błędy serwera (5xx) można ponowić
            if (HttpStatusCode >= 500 && HttpStatusCode < 600)
                return true;

            // Błędy timeout można ponowić
            if (HttpStatusCode == 408 || GraphErrorCode?.Contains("Timeout") == true)
                return true;

            return false;
        }

        /// <summary>
        /// Pobiera zalecany czas oczekiwania przed ponowieniem żądania.
        /// </summary>
        /// <returns>Czas oczekiwania w sekundach</returns>
        public int GetRecommendedRetryDelay()
        {
            // Jeśli jest podany RetryAfter, użyj go
            if (RetryAfter.HasValue)
                return RetryAfter.Value;

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
        /// Pobiera szczegółowy komunikat błędu.
        /// </summary>
        /// <returns>Szczegółowy komunikat błędu</returns>
        public string GetDetailedErrorMessage()
        {
            var details = new List<string>
            {
                $"Błąd połączenia Graph API: {Message}"
            };

            if (!string.IsNullOrEmpty(Endpoint))
                details.Add($"Endpoint: {Endpoint}");

            if (HttpStatusCode.HasValue)
                details.Add($"Status HTTP: {HttpStatusCode}");

            if (!string.IsNullOrEmpty(GraphErrorCode))
                details.Add($"Kod błędu: {GraphErrorCode}");

            if (!string.IsNullOrEmpty(GraphErrorDetails))
                details.Add($"Szczegóły: {GraphErrorDetails}");

            if (!string.IsNullOrEmpty(RequestId))
                details.Add($"Request ID: {RequestId}");

            if (CanRetry())
            {
                var retryDelay = GetRecommendedRetryDelay();
                details.Add($"Można ponowić za: {retryDelay} sekund");
            }

            return string.Join(Environment.NewLine, details);
        }

        /// <summary>
        /// Tworzy wyjątek błędu uwierzytelnienia.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphConnectionException</returns>
        public static GraphConnectionException CreateAuthenticationError(string message, string? endpoint = null, string? requestId = null)
        {
            return new GraphConnectionException(message)
            {
                Endpoint = endpoint,
                HttpStatusCode = 401,
                GraphErrorCode = "Unauthorized",
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek błędu rate limiting.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="retryAfter">Czas oczekiwania</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphConnectionException</returns>
        public static GraphConnectionException CreateRateLimitError(string message, int retryAfter, string? endpoint = null, string? requestId = null)
        {
            return new GraphConnectionException(message)
            {
                Endpoint = endpoint,
                HttpStatusCode = 429,
                GraphErrorCode = "TooManyRequests",
                RequestId = requestId,
                RetryAfter = retryAfter
            };
        }

        /// <summary>
        /// Tworzy wyjątek błędu timeout.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphConnectionException</returns>
        public static GraphConnectionException CreateTimeoutError(string message, string? endpoint = null, string? requestId = null)
        {
            return new GraphConnectionException(message)
            {
                Endpoint = endpoint,
                HttpStatusCode = 408,
                GraphErrorCode = "RequestTimeout",
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek błędu sieciowego.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="innerException">Wewnętrzny wyjątek</param>
        /// <returns>Wyjątek GraphConnectionException</returns>
        public static GraphConnectionException CreateNetworkError(string message, string? endpoint = null, Exception? innerException = null)
        {
            return new GraphConnectionException(message, innerException)
            {
                Endpoint = endpoint,
                GraphErrorCode = "NetworkError"
            };
        }
    }
} 