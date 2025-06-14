using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Exceptions.Graph
{
    /// <summary>
    /// Typy limitów rate limiting.
    /// </summary>
    public enum RateLimitType
    {
        Unknown,
        Standard,
        ServiceSpecific,
        ResourceSpecific,
        TenantLevel,
        ApplicationLevel,
        UserLevel
    }

    /// <summary>
    /// Wyjątek reprezentujący błędy rate limiting Microsoft Graph API.
    /// Implementuje ETAP 1.4.4 - GraphRateLimitException.
    /// </summary>
    public class GraphRateLimitException : GraphApiException
    {
        /// <summary>
        /// Czas oczekiwania przed ponowieniem żądania (w sekundach).
        /// </summary>
        public int RetryAfterSeconds { get; set; }

        /// <summary>
        /// Timestamp kiedy można ponowić żądanie.
        /// </summary>
        public DateTime? RetryAfterTimestamp { get; set; }

        /// <summary>
        /// Typ limitu rate limiting.
        /// </summary>
        public RateLimitType LimitType { get; set; } = RateLimitType.Unknown;

        /// <summary>
        /// Obecna liczba żądań w oknie czasowym.
        /// </summary>
        public int? CurrentRequestCount { get; set; }

        /// <summary>
        /// Maksymalna liczba żądań w oknie czasowym.
        /// </summary>
        public int? MaxRequestCount { get; set; }

        /// <summary>
        /// Rozmiar okna czasowego w sekundach.
        /// </summary>
        public int? WindowSizeSeconds { get; set; }

        /// <summary>
        /// Czas do resetu okna w sekundach.
        /// </summary>
        public int? WindowResetSeconds { get; set; }

        /// <summary>
        /// Pozostała liczba żądań w bieżącym oknie czasowym.
        /// </summary>
        public int? RemainingRequests
        {
            get
            {
                if (!CurrentRequestCount.HasValue || !MaxRequestCount.HasValue)
                    return null;
                return Math.Max(0, MaxRequestCount.Value - CurrentRequestCount.Value);
            }
        }

        /// <summary>
        /// Maksymalna liczba żądań w oknie czasowym (alias dla MaxRequestCount).
        /// </summary>
        public int? MaxRequests => MaxRequestCount;

        /// <summary>
        /// Czas resetowania limitu.
        /// </summary>
        public DateTime? ResetTime
        {
            get
            {
                if (!WindowResetSeconds.HasValue)
                    return null;
                return DateTime.UtcNow.AddSeconds(WindowResetSeconds.Value);
            }
        }

        /// <summary>
        /// Procent wykorzystania limitu.
        /// </summary>
        public double? UsagePercentage
        {
            get
            {
                if (!CurrentRequestCount.HasValue || !MaxRequestCount.HasValue || MaxRequestCount.Value == 0)
                    return null;

                return ((double)CurrentRequestCount.Value / MaxRequestCount.Value) * 100;
            }
        }

        /// <summary>
        /// Konstruktor domyślny.
        /// </summary>
        public GraphRateLimitException() : base("Przekroczono limit żądań Microsoft Graph API")
        {
            HttpStatusCode = 429;
            GraphErrorCode = "TooManyRequests";
            RetryAfterSeconds = 60; // Domyślnie 60 sekund
        }

        /// <summary>
        /// Konstruktor z komunikatem.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        public GraphRateLimitException(string message) : base(message)
        {
            HttpStatusCode = 429;
            GraphErrorCode = "TooManyRequests";
            RetryAfterSeconds = 60;
        }

        /// <summary>
        /// Konstruktor z komunikatem i czasem oczekiwania.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="retryAfterSeconds">Czas oczekiwania w sekundach</param>
        public GraphRateLimitException(string message, int retryAfterSeconds) : base(message)
        {
            HttpStatusCode = 429;
            GraphErrorCode = "TooManyRequests";
            RetryAfterSeconds = retryAfterSeconds;
            RetryAfterTimestamp = DateTime.UtcNow.AddSeconds(retryAfterSeconds);
        }

        /// <summary>
        /// Konstruktor z komunikatem i wewnętrznym wyjątkiem.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="innerException">Wewnętrzny wyjątek</param>
        public GraphRateLimitException(string message, Exception innerException) : base(message, innerException)
        {
            HttpStatusCode = 429;
            GraphErrorCode = "TooManyRequests";
            RetryAfterSeconds = 60;
        }

        /// <summary>
        /// Sprawdza czy można ponowić żądanie teraz.
        /// </summary>
        /// <returns>True jeśli można ponowić żądanie teraz</returns>
        public bool CanRetryNow()
        {
            if (!RetryAfterTimestamp.HasValue)
                return true;
            
            return DateTime.UtcNow >= RetryAfterTimestamp.Value;
        }

        /// <summary>
        /// Pobiera czas do ponowienia żądania.
        /// </summary>
        /// <returns>Czas do ponowienia żądania lub TimeSpan.Zero jeśli można ponowić teraz</returns>
        public TimeSpan GetTimeUntilRetry()
        {
            if (!RetryAfterTimestamp.HasValue)
                return TimeSpan.Zero;

            var timeUntilRetry = RetryAfterTimestamp.Value - DateTime.UtcNow;
            return timeUntilRetry > TimeSpan.Zero ? timeUntilRetry : TimeSpan.Zero;
        }

        /// <summary>
        /// Pobiera szczegółowy komunikat błędu rate limiting.
        /// </summary>
        /// <returns>Szczegółowy komunikat błędu</returns>
        public new string GetDetailedErrorMessage()
        {
            var baseMessage = base.GetDetailedErrorMessage();
            var rateLimitDetails = new List<string>
            {
                "",
                "=== SZCZEGÓŁY RATE LIMITING ===",
                $"Czas oczekiwania: {RetryAfterSeconds} sekund"
            };

            if (RetryAfterTimestamp.HasValue)
                rateLimitDetails.Add($"Możliwość ponowienia: {RetryAfterTimestamp:yyyy-MM-dd HH:mm:ss} UTC");

            if (LimitType != RateLimitType.Unknown)
                rateLimitDetails.Add($"Typ limitu: {LimitType}");

            if (CurrentRequestCount.HasValue)
                rateLimitDetails.Add($"Obecne żądania: {CurrentRequestCount}");

            if (MaxRequestCount.HasValue)
                rateLimitDetails.Add($"Maksymalne żądania: {MaxRequestCount}");

            if (RemainingRequests.HasValue)
                rateLimitDetails.Add($"Pozostałe żądania: {RemainingRequests}");

            if (WindowSizeSeconds.HasValue)
                rateLimitDetails.Add($"Rozmiar okna: {WindowSizeSeconds} sekund");

            if (WindowResetSeconds.HasValue)
                rateLimitDetails.Add($"Reset okna za: {WindowResetSeconds} sekund");

            if (ResetTime.HasValue)
                rateLimitDetails.Add($"Reset limitu: {ResetTime:yyyy-MM-dd HH:mm:ss} UTC");

            if (UsagePercentage.HasValue)
                rateLimitDetails.Add($"Wykorzystanie: {UsagePercentage:F1}%");

            if (CanRetryNow())
                rateLimitDetails.Add("Status: Można ponowić teraz");
            else
                rateLimitDetails.Add($"Status: Oczekiwanie {GetTimeUntilRetry().TotalSeconds:F0} sekund");

            return baseMessage + Environment.NewLine + string.Join(Environment.NewLine, rateLimitDetails);
        }

        /// <summary>
        /// Sprawdza czy błąd można ponowić (zawsze true dla rate limiting).
        /// </summary>
        /// <returns>Zawsze true</returns>
        public new bool CanRetry()
        {
            return true;
        }

        /// <summary>
        /// Pobiera zalecany czas oczekiwania z uwzględnieniem exponential backoff.
        /// </summary>
        /// <param name="attemptNumber">Numer próby (dla exponential backoff)</param>
        /// <returns>Czas oczekiwania w sekundach</returns>
        public new int GetRecommendedRetryDelay(int attemptNumber = 1)
        {
            // Bazowy czas oczekiwania z nagłówka Retry-After
            var baseDelay = RetryAfterSeconds;

            // Dla kolejnych prób zastosuj exponential backoff
            if (attemptNumber > 1)
            {
                var exponentialDelay = (int)Math.Pow(2, attemptNumber - 1) * baseDelay;
                // Maksymalnie 15 minut
                return Math.Min(exponentialDelay, 900);
            }

            return baseDelay;
        }

        /// <summary>
        /// Tworzy wyjątek standardowego rate limiting.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateStandardRateLimit(int retryAfterSeconds, string? endpoint = null, string? requestId = null)
        {
            return new GraphRateLimitException($"Przekroczono standardowy limit żądań. Spróbuj ponownie za {retryAfterSeconds} sekund.", retryAfterSeconds)
            {
                LimitType = RateLimitType.Standard,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek service-specific rate limiting.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="serviceName">Nazwa usługi</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateServiceSpecificRateLimit(int retryAfterSeconds, string serviceName, string? endpoint = null, string? requestId = null)
        {
            return new GraphRateLimitException($"Przekroczono limit żądań dla usługi {serviceName}. Spróbuj ponownie za {retryAfterSeconds} sekund.", retryAfterSeconds)
            {
                LimitType = RateLimitType.ServiceSpecific,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek resource-specific rate limiting.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="resourceType">Typ zasobu</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateResourceSpecificRateLimit(int retryAfterSeconds, string resourceType, string? endpoint = null, string? requestId = null)
        {
            return new GraphRateLimitException($"Przekroczono limit żądań dla zasobu {resourceType}. Spróbuj ponownie za {retryAfterSeconds} sekund.", retryAfterSeconds)
            {
                LimitType = RateLimitType.ResourceSpecific,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek tenant-level rate limiting.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateTenantLevelRateLimit(int retryAfterSeconds, string? endpoint = null, string? requestId = null)
        {
            return new GraphRateLimitException($"Przekroczono limit żądań dzierżawy. Spróbuj ponownie za {retryAfterSeconds} sekund.", retryAfterSeconds)
            {
                LimitType = RateLimitType.TenantLevel,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek application-level rate limiting.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateApplicationLevelRateLimit(int retryAfterSeconds, string? endpoint = null, string? requestId = null)
        {
            return new GraphRateLimitException($"Przekroczono limit żądań aplikacji. Spróbuj ponownie za {retryAfterSeconds} sekund.", retryAfterSeconds)
            {
                LimitType = RateLimitType.ApplicationLevel,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek rate limiting na podstawie nagłówków HTTP.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="currentRequestCount">Obecna liczba żądań</param>
        /// <param name="maxRequestCount">Maksymalna liczba żądań</param>
        /// <param name="windowSizeSeconds">Rozmiar okna w sekundach</param>
        /// <param name="windowResetSeconds">Czas do resetu okna</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateFromHeaders(
            int retryAfterSeconds,
            int? currentRequestCount = null,
            int? maxRequestCount = null,
            int? windowSizeSeconds = null,
            int? windowResetSeconds = null,
            string? endpoint = null,
            string? requestId = null)
        {
            var remainingRequests = currentRequestCount.HasValue && maxRequestCount.HasValue
                ? Math.Max(0, maxRequestCount.Value - currentRequestCount.Value)
                : (int?)null;

            var message = currentRequestCount.HasValue && maxRequestCount.HasValue
                ? $"Przekroczono limit żądań ({currentRequestCount}/{maxRequestCount}). Spróbuj ponownie za {retryAfterSeconds} sekund."
                : $"Przekroczono limit żądań. Spróbuj ponownie za {retryAfterSeconds} sekund.";

            return new GraphRateLimitException(message, retryAfterSeconds)
            {
                CurrentRequestCount = currentRequestCount,
                MaxRequestCount = maxRequestCount,
                WindowSizeSeconds = windowSizeSeconds,
                WindowResetSeconds = windowResetSeconds,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek rate limiting dla aplikacji.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateApplicationLimitError(int retryAfterSeconds, string? endpoint = null, string? requestId = null)
        {
            return new GraphRateLimitException($"Przekroczono limit żądań aplikacji. Spróbuj ponownie za {retryAfterSeconds} sekund.", retryAfterSeconds)
            {
                LimitType = RateLimitType.ApplicationLevel,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek rate limiting dla użytkownika.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateUserLimitError(int retryAfterSeconds, string? endpoint = null, string? requestId = null)
        {
            return new GraphRateLimitException($"Przekroczono limit żądań użytkownika. Spróbuj ponownie za {retryAfterSeconds} sekund.", retryAfterSeconds)
            {
                LimitType = RateLimitType.UserLevel,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek rate limiting dla dzierżawy.
        /// </summary>
        /// <param name="retryAfterSeconds">Czas oczekiwania</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphRateLimitException</returns>
        public static GraphRateLimitException CreateTenantLimitError(int retryAfterSeconds, string? endpoint = null, string? requestId = null)
        {
            return new GraphRateLimitException($"Przekroczono limit żądań dzierżawy. Spróbuj ponownie za {retryAfterSeconds} sekund.", retryAfterSeconds)
            {
                LimitType = RateLimitType.TenantLevel,
                Endpoint = endpoint,
                RequestId = requestId
            };
        }
    }
} 