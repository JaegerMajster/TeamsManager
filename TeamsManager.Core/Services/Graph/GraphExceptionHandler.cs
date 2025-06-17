using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Exceptions.Graph;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Uniwersalny helper do obsługi wyjątków Graph API z inteligentnym retry, rate limiting i circuit breaker.
    /// Używany przez wszystkie serwisy Graph API dla spójnej obsługi błędów.
    /// </summary>
    public static class GraphExceptionHandler
    {
        /// <summary>
        /// Uniwersalna obsługa GraphConnectionException z inteligentnym retry, rate limiting i circuit breaker.
        /// </summary>
        /// <typeparam name="T">Typ zwracanego wyniku</typeparam>
        /// <param name="ex">Wyjątek GraphConnectionException</param>
        /// <param name="logger">Logger</param>
        /// <param name="graphService">Serwis Graph API</param>
        /// <param name="notificationService">Serwis powiadomień</param>
        /// <param name="adminNotificationService">Serwis powiadomień administratorskich</param>
        /// <param name="operationHistoryService">Serwis historii operacji</param>
        /// <param name="currentUserService">Serwis bieżącego użytkownika</param>
        /// <param name="operation">Operacja historii (opcjonalna)</param>
        /// <param name="methodName">Nazwa metody</param>
        /// <param name="retryAction">Akcja retry (opcjonalna)</param>
        /// <returns>Wynik operacji lub wartość domyślna</returns>
        public static async Task<T> HandleGraphConnectionExceptionAsync<T>(
            GraphConnectionException ex,
            ILogger logger,
            IGraphService graphService,
            INotificationService notificationService,
            IAdminNotificationService adminNotificationService,
            IOperationHistoryService operationHistoryService,
            ICurrentUserService currentUserService,
            OperationHistory? operation = null,
            string? methodName = null,
            Func<Task<T>>? retryAction = null)
        {
            var currentUserUpn = currentUserService.GetCurrentUserUpn() ?? "system";
            var method = methodName ?? "UnknownMethod";
            
            // Zbierz metryki dla monitoringu
            var metrics = new Dictionary<string, object>
            {
                ["Method"] = method,
                ["Endpoint"] = ex.Endpoint ?? "Unknown",
                ["HttpStatusCode"] = ex.HttpStatusCode ?? 0,
                ["GraphErrorCode"] = ex.GraphErrorCode ?? "Unknown",
                ["RequestId"] = ex.RequestId ?? "Unknown",
                ["CanRetry"] = ex.CanRetry(),
                ["IsAuthenticationError"] = ex.IsAuthenticationError,
                ["IsRateLimitError"] = ex.IsRateLimitError
            };

            // Specjalna obsługa błędów uwierzytelnienia
            if (ex.IsAuthenticationError)
            {
                logger.LogError(ex, "Błąd uwierzytelnienia Graph API w {Method}: {Details}", method, ex.GetDetailedErrorMessage());
                
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Sesja wygasła. Wymagane ponowne logowanie do Microsoft Graph.",
                    "warning"
                );
                
                if (operation != null)
                {
                    await operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Błąd uwierzytelnienia Graph API w {method}: {ex.GraphErrorCode}"
                    );
                }
                
                // Wyślij metryki uwierzytelnienia
                await adminNotificationService.SendGraphApiErrorMetricsAsync(metrics);
                
                return default(T);
            }

            // Rate limiting - aktywuj circuit breaker jeśli potrzeba
            if (ex.IsRateLimitError)
            {
                var retryAfter = ex.GetRecommendedRetryDelay();
                logger.LogWarning(ex, "Rate limit Graph API w {Method} - oczekiwanie {RetryAfter}s. Szczegóły: {Details}", 
                    method, retryAfter, ex.GetDetailedErrorMessage());
                
                // Aktualizuj informacje o rate limiting w Graph Service
                await UpdateRateLimitInfoAsync(graphService, retryAfter);
                
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Microsoft Graph API jest przeciążony. Operacja zostanie ponowiona za {retryAfter} sekund.",
                    "info"
                );
            }

            // Circuit breaker dla błędów serwera
            if (ex.HttpStatusCode >= 500 && ex.HttpStatusCode < 600)
            {
                logger.LogError(ex, "Błąd serwera Graph API w {Method}: {Details}", method, ex.GetDetailedErrorMessage());
                
                // Jeśli to kolejny błąd serwera, rozważ circuit breaker
                await ReportServerErrorAsync(graphService);
                
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Microsoft Graph API ma problemy techniczne. Spróbuj ponownie za kilka minut.",
                    "error"
                );
            }

            // Spróbuj retry jeśli możliwe
            if (retryAction != null && ex.CanRetry() && !ex.IsAuthenticationError)
            {
                try
                {
                    var retryDelay = ex.GetRecommendedRetryDelay();
                    logger.LogInformation("Próba retry {Method} za {Delay}s", method, retryDelay);
                    
                    await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                    
                    var result = await retryAction();
                    if (result != null && !result.Equals(default(T)))
                    {
                        logger.LogInformation("Retry {Method} zakończony sukcesem", method);
                        
                        if (operation != null)
                        {
                            await operationHistoryService.UpdateOperationStatusAsync(
                                operation.Id,
                                OperationStatus.Completed,
                                $"Operacja {method} zakończona sukcesem po retry"
                            );
                        }
                        
                        return result;
                    }
                }
                catch (Exception retryEx)
                {
                    logger.LogError(retryEx, "Retry {Method} również zakończony błędem", method);
                }
            }

            // Finalna obsługa błędu
            if (operation != null)
            {
                await operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Błąd Graph API w {method}: {ex.GetDetailedErrorMessage()}"
                );
            }

            await notificationService.SendNotificationToUserAsync(
                currentUserUpn,
                $"Nie udało się wykonać operacji: {ex.Message}",
                "error"
            );

            // Wyślij szczegółowe metryki do administratorów
            _ = Task.Run(async () =>
            {
                try
                {
                    await adminNotificationService.SendGraphApiErrorMetricsAsync(metrics);
                }
                catch (Exception metricsEx)
                {
                    logger.LogError(metricsEx, "Błąd podczas wysyłania metryki Graph API");
                }
            });

            return default(T);
        }

        /// <summary>
        /// Uniwersalna obsługa GraphApiException z inteligentną analizą błędów.
        /// </summary>
        /// <typeparam name="T">Typ zwracanego wyniku</typeparam>
        /// <param name="ex">Wyjątek GraphApiException</param>
        /// <param name="logger">Logger</param>
        /// <param name="notificationService">Serwis powiadomień</param>
        /// <param name="adminNotificationService">Serwis powiadomień administratorskich</param>
        /// <param name="operationHistoryService">Serwis historii operacji</param>
        /// <param name="currentUserService">Serwis bieżącego użytkownika</param>
        /// <param name="operation">Operacja historii (opcjonalna)</param>
        /// <param name="methodName">Nazwa metody</param>
        /// <param name="retryAction">Akcja retry (opcjonalna)</param>
        /// <returns>Wynik operacji lub wartość domyślna</returns>
        public static async Task<T> HandleGraphApiExceptionAsync<T>(
            GraphApiException ex,
            ILogger logger,
            INotificationService notificationService,
            IAdminNotificationService adminNotificationService,
            IOperationHistoryService operationHistoryService,
            ICurrentUserService currentUserService,
            OperationHistory? operation = null,
            string? methodName = null,
            Func<Task<T>>? retryAction = null)
        {
            var currentUserUpn = currentUserService.GetCurrentUserUpn() ?? "system";
            var method = methodName ?? "UnknownMethod";
            
            // Zbierz metryki dla monitoringu
            var metrics = new Dictionary<string, object>
            {
                ["Method"] = method,
                ["Endpoint"] = ex.Endpoint ?? "Unknown",
                ["HttpStatusCode"] = ex.HttpStatusCode ?? 0,
                ["GraphErrorCode"] = ex.GraphErrorCode ?? "Unknown",
                ["RequestId"] = ex.RequestId ?? "Unknown",
                ["CanRetry"] = ex.CanRetry(),
                ["IsAuthenticationError"] = ex.IsAuthenticationError,
                ["IsPermissionError"] = ex.IsPermissionError,
                ["IsValidationError"] = ex.IsValidationError,
                ["IsNotFoundError"] = ex.IsNotFoundError,
                ["IsConflictError"] = ex.IsConflictError
            };

            // Specjalna obsługa różnych typów błędów
            if (ex.IsAuthenticationError)
            {
                logger.LogError(ex, "Błąd uwierzytelnienia Graph API w {Method}", method);
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Sesja wygasła. Wymagane ponowne logowanie.",
                    "warning"
                );
            }
            else if (ex.IsPermissionError)
            {
                logger.LogError(ex, "Błąd uprawnień Graph API w {Method}", method);
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Brak wymaganych uprawnień do wykonania operacji.",
                    "error"
                );
            }
            else if (ex.IsValidationError)
            {
                logger.LogWarning(ex, "Błąd walidacji Graph API w {Method}", method);
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Nieprawidłowe dane wejściowe. Sprawdź wprowadzone informacje.",
                    "warning"
                );
            }
            else if (ex.IsNotFoundError)
            {
                logger.LogWarning(ex, "Zasób nie znaleziony Graph API w {Method}", method);
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Żądany zasób nie został znaleziony.",
                    "info"
                );
            }
            else if (ex.IsConflictError)
            {
                logger.LogWarning(ex, "Konflikt zasobów Graph API w {Method}", method);
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Konflikt zasobów. Zasób może już istnieć.",
                    "warning"
                );
            }
            else
            {
                logger.LogError(ex, "Błąd Graph API w {Method}: {Details}", method, ex.GetDetailedErrorMessage());
            }

            // Spróbuj retry tylko dla błędów, które można ponowić
            if (retryAction != null && ex.CanRetry())
            {
                try
                {
                    var retryDelay = ex.GetRecommendedRetryDelay();
                    logger.LogInformation("Próba retry {Method} za {Delay}s", method, retryDelay);
                    
                    await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                    
                    var result = await retryAction();
                    if (result != null && !result.Equals(default(T)))
                    {
                        logger.LogInformation("Retry {Method} zakończony sukcesem", method);
                        
                        if (operation != null)
                        {
                            await operationHistoryService.UpdateOperationStatusAsync(
                                operation.Id,
                                OperationStatus.Completed,
                                $"Operacja {method} zakończona sukcesem po retry"
                            );
                        }
                        
                        return result;
                    }
                }
                catch (Exception retryEx)
                {
                    logger.LogError(retryEx, "Retry {Method} również zakończony błędem", method);
                }
            }

            // Finalna obsługa błędu
            if (operation != null)
            {
                await operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Błąd Graph API w {method}: {ex.GetDetailedErrorMessage()}"
                );
            }

            // Wyślij szczegółowe metryki do administratorów
            _ = Task.Run(async () =>
            {
                try
                {
                    await adminNotificationService.SendGraphApiErrorMetricsAsync(metrics);
                }
                catch (Exception metricsEx)
                {
                    logger.LogError(metricsEx, "Błąd podczas wysyłania metryki Graph API");
                }
            });

            return default(T);
        }

        /// <summary>
        /// Uniwersalna obsługa GraphRateLimitException z inteligentnym batch splitting.
        /// </summary>
        /// <typeparam name="T">Typ zwracanego wyniku</typeparam>
        /// <param name="ex">Wyjątek GraphRateLimitException</param>
        /// <param name="logger">Logger</param>
        /// <param name="graphService">Serwis Graph API</param>
        /// <param name="notificationService">Serwis powiadomień</param>
        /// <param name="currentUserService">Serwis bieżącego użytkownika</param>
        /// <param name="methodName">Nazwa metody</param>
        /// <param name="batchSplitAction">Akcja dzielenia na mniejsze batche (opcjonalna)</param>
        /// <param name="retryAction">Akcja retry (opcjonalna)</param>
        /// <returns>Wynik operacji lub wartość domyślna</returns>
        public static async Task<T> HandleGraphRateLimitExceptionAsync<T>(
            GraphRateLimitException ex,
            ILogger logger,
            IGraphService graphService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            string? methodName = null,
            Func<Task<T>>? batchSplitAction = null,
            Func<Task<T>>? retryAction = null)
        {
            var currentUserUpn = currentUserService.GetCurrentUserUpn() ?? "system";
            var method = methodName ?? "UnknownMethod";
            
            logger.LogWarning(ex, "Rate limit Graph API w {Method} - typ: {LimitType}, retry za {RetryAfter}s", 
                method, ex.LimitType, ex.RetryAfterSeconds);
            
            // Aktualizuj informacje o rate limiting w Graph Service
            // await graphService.Connection.UpdateRateLimitInfoAsync(ex.RetryAfterSeconds);
            // Tymczasowo wyłączone - metoda nie jest zaimplementowana
            
            // Powiadom użytkownika
            await notificationService.SendNotificationToUserAsync(
                currentUserUpn,
                $"Microsoft Graph API jest przeciążony ({ex.LimitType}). Operacja zostanie ponowiona za {ex.RetryAfterSeconds} sekund.",
                "info"
            );

            // Spróbuj batch splitting jeśli dostępne
            if (batchSplitAction != null)
            {
                try
                {
                    logger.LogInformation("Próba batch splitting dla {Method} z powodu rate limit", method);
                    var result = await batchSplitAction();
                    if (result != null && !result.Equals(default(T)))
                    {
                        logger.LogInformation("Batch splitting {Method} zakończony sukcesem", method);
                        return result;
                    }
                }
                catch (Exception splitEx)
                {
                    logger.LogError(splitEx, "Batch splitting {Method} również zakończony błędem", method);
                }
            }

            // Spróbuj retry po czasie oczekiwania
            if (retryAction != null)
            {
                try
                {
                    logger.LogInformation("Oczekiwanie {RetryAfter}s przed retry {Method}", ex.RetryAfterSeconds, method);
                    await Task.Delay(TimeSpan.FromSeconds(ex.RetryAfterSeconds));
                    
                    var result = await retryAction();
                    if (result != null && !result.Equals(default(T)))
                    {
                        logger.LogInformation("Retry {Method} po rate limit zakończony sukcesem", method);
                        return result;
                    }
                }
                catch (Exception retryEx)
                {
                    logger.LogError(retryEx, "Retry {Method} po rate limit również zakończony błędem", method);
                }
            }

            return default(T);
        }

        /// <summary>
        /// Uniwersalna obsługa standardowych wyjątków z konwersją na Graph exceptions.
        /// </summary>
        /// <typeparam name="T">Typ zwracanego wyniku</typeparam>
        /// <param name="ex">Standardowy wyjątek</param>
        /// <param name="logger">Logger</param>
        /// <param name="notificationService">Serwis powiadomień</param>
        /// <param name="currentUserService">Serwis bieżącego użytkownika</param>
        /// <param name="methodName">Nazwa metody</param>
        /// <param name="endpoint">Endpoint Graph API (opcjonalny)</param>
        /// <returns>Wynik operacji lub wartość domyślna</returns>
        public static async Task<T> HandleStandardExceptionAsync<T>(
            Exception ex,
            ILogger logger,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            string? methodName = null,
            string? endpoint = null)
        {
            var currentUserUpn = currentUserService.GetCurrentUserUpn() ?? "system";
            var method = methodName ?? "UnknownMethod";
            
            logger.LogError(ex, "Nieoczekiwany błąd w {Method}: {Message}", method, ex.Message);
            
            // Konwertuj na odpowiedni Graph exception jeśli możliwe
            if (ex is HttpRequestException httpEx)
            {
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Błąd komunikacji z Microsoft Graph API. Sprawdź połączenie internetowe.",
                    "error"
                );
            }
            else if (ex is TaskCanceledException timeoutEx)
            {
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Przekroczono limit czasu operacji. Spróbuj ponownie.",
                    "warning"
                );
            }
            else if (ex is UnauthorizedAccessException authEx)
            {
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Brak autoryzacji. Wymagane ponowne logowanie.",
                    "warning"
                );
            }
            else
            {
                await notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Wystąpił nieoczekiwany błąd: {ex.Message}",
                    "error"
                );
            }

            return default(T);
        }

        /// <summary>
        /// Uproszczona obsługa GraphConnectionException - kompatybilność wsteczna.
        /// </summary>
        /// <typeparam name="T">Typ zwracanego wyniku</typeparam>
        /// <param name="ex">Wyjątek GraphConnectionException</param>
        /// <param name="retryAction">Akcja retry</param>
        /// <param name="logger">Logger</param>
        /// <param name="methodName">Nazwa metody</param>
        /// <param name="defaultValue">Wartość domyślna zwracana w przypadku błędu</param>
        /// <returns>Wynik operacji lub wartość domyślna</returns>
        public static async Task<T> HandleGraphConnectionExceptionAsync<T>(
            GraphConnectionException ex,
            Func<Task<T>>? retryAction,
            ILogger logger,
            string? methodName = null,
            T? defaultValue = default(T))
        {
            var method = methodName ?? "UnknownMethod";
            
            logger.LogError(ex, "Błąd połączenia Graph API w {Method}: {Details}", method, ex.GetDetailedErrorMessage());

            // Spróbuj retry jeśli możliwe i nie jest to błąd uwierzytelnienia
            if (retryAction != null && ex.CanRetry() && !ex.IsAuthenticationError)
            {
                try
                {
                    var retryDelay = ex.GetRecommendedRetryDelay();
                    logger.LogInformation("Próba retry {Method} za {Delay}s", method, retryDelay);
                    
                    await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                    
                    var result = await retryAction();
                    if (result != null && !result.Equals(default(T)))
                    {
                        logger.LogInformation("Retry {Method} zakończony sukcesem", method);
                        return result;
                    }
                }
                catch (Exception retryEx)
                {
                    logger.LogError(retryEx, "Retry {Method} również zakończony błędem", method);
                }
            }

            // Zwróć wartość domyślną
            logger.LogWarning("Zwracanie wartości domyślnej dla {Method} z powodu błędu: {Error}", method, ex.Message);
            return defaultValue ?? default(T);
        }

        /// <summary>
        /// Uproszczona obsługa GraphConnectionException dla metod zwracających Task - kompatybilność wsteczna.
        /// </summary>
        /// <param name="ex">Wyjątek GraphConnectionException</param>
        /// <param name="retryAction">Akcja retry</param>
        /// <param name="logger">Logger</param>
        /// <param name="methodName">Nazwa metody</param>
        /// <param name="defaultValue">Wartość domyślna (ignorowana dla Task)</param>
        /// <returns>Task</returns>
        public static async Task HandleGraphConnectionExceptionAsync(
            GraphConnectionException ex,
            Func<Task>? retryAction,
            ILogger logger,
            string? methodName = null,
            Task? defaultValue = null)
        {
            var method = methodName ?? "UnknownMethod";
            
            logger.LogError(ex, "Błąd połączenia Graph API w {Method}: {Details}", method, ex.GetDetailedErrorMessage());

            // Spróbuj retry jeśli możliwe i nie jest to błąd uwierzytelnienia
            if (retryAction != null && ex.CanRetry() && !ex.IsAuthenticationError)
            {
                try
                {
                    var retryDelay = ex.GetRecommendedRetryDelay();
                    logger.LogInformation("Próba retry {Method} za {Delay}s", method, retryDelay);
                    
                    await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                    
                    await retryAction();
                    logger.LogInformation("Retry {Method} zakończony sukcesem", method);
                    return;
                }
                catch (Exception retryEx)
                {
                    logger.LogError(retryEx, "Retry {Method} również zakończony błędem", method);
                }
            }

            // Loguj błąd i zakończ
            logger.LogWarning("Operacja {Method} zakończona błędem: {Error}", method, ex.Message);
        }

        /// <summary>
        /// Aktualizuje informacje o rate limiting w Graph Service
        /// </summary>
        private static async Task UpdateRateLimitInfoAsync(IGraphService graphService, int retryAfterSeconds)
        {
            try
            {
                // Aktualizuj cache z informacjami o rate limiting
                var rateLimitInfo = new GraphRateLimitStatus
                {
                    IsLimitReached = true,
                    RemainingRequests = 0,
                    ResetTime = DateTime.UtcNow.AddSeconds(retryAfterSeconds),
                    RetryAfterSeconds = retryAfterSeconds
                };

                // Zapisz informacje w cache dla przyszłych żądań
                graphService.Cache.SetRateLimitInfo("global", rateLimitInfo);
                
                // Loguj informacje o rate limiting
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger("GraphExceptionHandler");
                logger.LogWarning("Rate limit reached. Retry after {RetryAfter} seconds", retryAfterSeconds);
            }
            catch (Exception ex)
            {
                // Nie rzucaj wyjątku - to metoda pomocnicza
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger("GraphExceptionHandler");
                logger.LogError(ex, "Error updating rate limit info");
            }
        }

        /// <summary>
        /// Raportuje błąd serwera do Graph Service.
        /// </summary>
        /// <param name="graphService">Serwis Graph API</param>
        private static async Task ReportServerErrorAsync(IGraphService graphService)
        {
            try
            {
                await graphService.ReportServerErrorAsync();
                
                // Loguj informacje o błędzie serwera
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger("GraphExceptionHandler");
                logger.LogWarning("Server error reported to Graph Service");
            }
            catch (Exception ex)
            {
                // Nie rzucaj wyjątku - to metoda pomocnicza
                var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
                var logger = loggerFactory.CreateLogger("GraphExceptionHandler");
                logger.LogError(ex, "Error reporting server error");
            }
        }
    }
} 
