using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Konfiguracja serwisu Graph API.
    /// Zawiera wszystkie ustawienia potrzebne do działania GraphService.
    /// </summary>
    public class GraphServiceConfiguration
    {
        /// <summary>
        /// Czy serwis jest włączony.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Timeout dla żądań Graph API w sekundach.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maksymalna liczba prób ponowienia żądania.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Czy włączyć szczegółowe logowanie.
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Czy włączyć zbieranie metryk wydajności.
        /// </summary>
        public bool EnablePerformanceMetrics { get; set; } = true;

        /// <summary>
        /// Czy respektować rate limiting Graph API.
        /// </summary>
        public bool RespectRateLimit { get; set; } = true;

        /// <summary>
        /// Maksymalna liczba równoczesnych żądań.
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 10;

        /// <summary>
        /// Rozmiar batch dla operacji masowych.
        /// </summary>
        public int BatchSize { get; set; } = 20;

        /// <summary>
        /// Konfiguracja cache.
        /// </summary>
        public GraphCacheConfiguration Cache { get; set; } = new GraphCacheConfiguration();

        /// <summary>
        /// Konfiguracja retry policy.
        /// </summary>
        public GraphRetryConfiguration Retry { get; set; } = new GraphRetryConfiguration();

        /// <summary>
        /// Konfiguracja rate limiting.
        /// </summary>
        public GraphRateLimitConfiguration RateLimit { get; set; } = new GraphRateLimitConfiguration();

        /// <summary>
        /// Sprawdza czy konfiguracja jest prawidłowa.
        /// </summary>
        /// <returns>True jeśli konfiguracja jest prawidłowa</returns>
        public bool IsValid()
        {
            return Enabled &&
                   RequestTimeoutSeconds > 0 &&
                   MaxRetryAttempts >= 0 &&
                   MaxConcurrentRequests > 0 &&
                   BatchSize > 0 && BatchSize <= 20 && // Graph API limit
                   Cache.IsValid() &&
                   Retry.IsValid() &&
                   RateLimit.IsValid();
        }

        /// <summary>
        /// Pobiera szczegółowy raport konfiguracji.
        /// </summary>
        /// <returns>Raport konfiguracji</returns>
        public string GetConfigurationReport()
        {
            var report = new List<string>
            {
                $"Graph Service Configuration:",
                $"  Enabled: {Enabled}",
                $"  Request Timeout: {RequestTimeoutSeconds}s",
                $"  Max Retry Attempts: {MaxRetryAttempts}",
                $"  Detailed Logging: {EnableDetailedLogging}",
                $"  Performance Metrics: {EnablePerformanceMetrics}",
                $"  Respect Rate Limit: {RespectRateLimit}",
                $"  Max Concurrent Requests: {MaxConcurrentRequests}",
                $"  Batch Size: {BatchSize}",
                "",
                "Cache Configuration:",
                $"  {Cache.GetConfigurationSummary()}",
                "",
                "Retry Configuration:",
                $"  {Retry.GetConfigurationSummary()}",
                "",
                "Rate Limit Configuration:",
                $"  {RateLimit.GetConfigurationSummary()}",
                "",
                $"Configuration Valid: {IsValid()}"
            };

            return string.Join(Environment.NewLine, report);
        }
    }

    /// <summary>
    /// Konfiguracja cache dla Graph API.
    /// </summary>
    public class GraphCacheConfiguration
    {
        /// <summary>
        /// Czy cache jest włączony.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Domyślny czas przechowywania w cache w minutach.
        /// </summary>
        public int DefaultCacheDurationMinutes { get; set; } = 15;

        /// <summary>
        /// Czas przechowywania krótkoterminowego w minutach.
        /// </summary>
        public int ShortTermCacheDurationMinutes { get; set; } = 5;

        /// <summary>
        /// Czas przechowywania długoterminowego w minutach.
        /// </summary>
        public int LongTermCacheDurationMinutes { get; set; } = 60;

        /// <summary>
        /// Maksymalny rozmiar cache w MB.
        /// </summary>
        public int MaxCacheSizeMB { get; set; } = 100;

        /// <summary>
        /// Czy włączyć cache warming.
        /// </summary>
        public bool EnableCacheWarming { get; set; } = true;

        /// <summary>
        /// Sprawdza czy konfiguracja cache jest prawidłowa.
        /// </summary>
        /// <returns>True jeśli konfiguracja jest prawidłowa</returns>
        public bool IsValid()
        {
            return DefaultCacheDurationMinutes > 0 &&
                   ShortTermCacheDurationMinutes > 0 &&
                   LongTermCacheDurationMinutes > 0 &&
                   MaxCacheSizeMB > 0 &&
                   ShortTermCacheDurationMinutes <= DefaultCacheDurationMinutes &&
                   DefaultCacheDurationMinutes <= LongTermCacheDurationMinutes;
        }

        /// <summary>
        /// Pobiera podsumowanie konfiguracji cache.
        /// </summary>
        /// <returns>Podsumowanie konfiguracji</returns>
        public string GetConfigurationSummary()
        {
            return $"Enabled: {Enabled}, Default: {DefaultCacheDurationMinutes}min, " +
                   $"Short: {ShortTermCacheDurationMinutes}min, Long: {LongTermCacheDurationMinutes}min, " +
                   $"Max Size: {MaxCacheSizeMB}MB, Warming: {EnableCacheWarming}";
        }
    }

    /// <summary>
    /// Konfiguracja retry policy dla Graph API.
    /// </summary>
    public class GraphRetryConfiguration
    {
        /// <summary>
        /// Czy retry jest włączony.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maksymalna liczba prób ponowienia.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Początkowe opóźnienie w milisekundach.
        /// </summary>
        public int InitialDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maksymalne opóźnienie w milisekundach.
        /// </summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// Mnożnik dla exponential backoff.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Czy dodać jitter do opóźnienia.
        /// </summary>
        public bool UseJitter { get; set; } = true;

        /// <summary>
        /// Sprawdza czy konfiguracja retry jest prawidłowa.
        /// </summary>
        /// <returns>True jeśli konfiguracja jest prawidłowa</returns>
        public bool IsValid()
        {
            return MaxAttempts >= 0 &&
                   InitialDelayMs > 0 &&
                   MaxDelayMs > 0 &&
                   BackoffMultiplier > 1.0 &&
                   InitialDelayMs <= MaxDelayMs;
        }

        /// <summary>
        /// Pobiera podsumowanie konfiguracji retry.
        /// </summary>
        /// <returns>Podsumowanie konfiguracji</returns>
        public string GetConfigurationSummary()
        {
            return $"Enabled: {Enabled}, Max Attempts: {MaxAttempts}, " +
                   $"Initial Delay: {InitialDelayMs}ms, Max Delay: {MaxDelayMs}ms, " +
                   $"Backoff: {BackoffMultiplier}x, Jitter: {UseJitter}";
        }
    }

    /// <summary>
    /// Konfiguracja rate limiting dla Graph API.
    /// </summary>
    public class GraphRateLimitConfiguration
    {
        /// <summary>
        /// Czy rate limiting jest włączony.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maksymalna liczba żądań na minutę.
        /// </summary>
        public int MaxRequestsPerMinute { get; set; } = 600; // 10,000 per 10 minutes = 1,000 per minute, ale bądźmy ostrożni

        /// <summary>
        /// Czy automatycznie czekać gdy osiągnięto limit.
        /// </summary>
        public bool AutoWaitOnLimit { get; set; } = true;

        /// <summary>
        /// Maksymalny czas oczekiwania na reset limitu w sekundach.
        /// </summary>
        public int MaxWaitTimeSeconds { get; set; } = 60;

        /// <summary>
        /// Czy monitorować rate limit headers z Graph API.
        /// </summary>
        public bool MonitorRateLimitHeaders { get; set; } = true;

        /// <summary>
        /// Próg ostrzeżenia o rate limiting (% wykorzystania).
        /// </summary>
        public int WarningThresholdPercent { get; set; } = 80;

        /// <summary>
        /// Sprawdza czy konfiguracja rate limiting jest prawidłowa.
        /// </summary>
        /// <returns>True jeśli konfiguracja jest prawidłowa</returns>
        public bool IsValid()
        {
            return MaxRequestsPerMinute > 0 &&
                   MaxWaitTimeSeconds > 0 &&
                   WarningThresholdPercent > 0 && WarningThresholdPercent <= 100;
        }

        /// <summary>
        /// Pobiera podsumowanie konfiguracji rate limiting.
        /// </summary>
        /// <returns>Podsumowanie konfiguracji</returns>
        public string GetConfigurationSummary()
        {
            return $"Enabled: {Enabled}, Max Requests/min: {MaxRequestsPerMinute}, " +
                   $"Auto Wait: {AutoWaitOnLimit}, Max Wait: {MaxWaitTimeSeconds}s, " +
                   $"Monitor Headers: {MonitorRateLimitHeaders}, Warning: {WarningThresholdPercent}%";
        }
    }
} 