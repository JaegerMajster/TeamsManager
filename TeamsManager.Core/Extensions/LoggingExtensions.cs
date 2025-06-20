using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using TeamsManager.Core.Services.Logging;

namespace TeamsManager.Core.Extensions
{
    /// <summary>
    /// Extension methods dla konfiguracji scentralizowanego systemu logowania TeamsManager
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Dodaje scentralizowany system logowania TeamsManager do IServiceCollection
        /// </summary>
        /// <param name="services">Kolekcja serwisów</param>
        /// <param name="applicationName">Nazwa aplikacji (UI, API, etc.)</param>
        /// <param name="logDirectory">Opcjonalny katalog logów (domyślnie AppData/TeamsManager/logs)</param>
        /// <returns>IServiceCollection dla fluent API</returns>
        public static IServiceCollection AddTeamsManagerLogging(this IServiceCollection services, 
            string applicationName, 
            string? logDirectory = null)
        {
            // Domyślny katalog logów w AppData
            logDirectory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TeamsManager", "logs");

            // Dodaj scentralizowany FileLoggerProvider
            services.AddLogging(builder =>
            {
                builder.AddProvider(new FileLoggerProvider(logDirectory, applicationName));
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            return services;
        }

        /// <summary>
        /// Dodaje scentralizowany system logowania TeamsManager do ILoggingBuilder
        /// </summary>
        /// <param name="builder">ILoggingBuilder</param>
        /// <param name="applicationName">Nazwa aplikacji (UI, API, etc.)</param>
        /// <param name="logDirectory">Opcjonalny katalog logów (domyślnie AppData/TeamsManager/logs)</param>
        /// <returns>ILoggingBuilder dla fluent API</returns>
        public static ILoggingBuilder AddTeamsManagerFileLogging(this ILoggingBuilder builder, 
            string applicationName, 
            string? logDirectory = null)
        {
            // Domyślny katalog logów w AppData
            logDirectory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TeamsManager", "logs");

            // Dodaj scentralizowany FileLoggerProvider
            builder.AddProvider(new FileLoggerProvider(logDirectory, applicationName));

            return builder;
        }

        /// <summary>
        /// Konfiguruje pełny system logowania TeamsManager z diagnostyką
        /// </summary>
        /// <param name="builder">ILoggingBuilder</param>
        /// <param name="applicationName">Nazwa aplikacji (UI, API, etc.)</param>
        /// <param name="enableDiagnostics">Czy włączyć rozszerzoną diagnostykę</param>
        /// <param name="logDirectory">Opcjonalny katalog logów</param>
        /// <returns>ILoggingBuilder dla fluent API</returns>
        public static ILoggingBuilder AddTeamsManagerDiagnosticLogging(this ILoggingBuilder builder, 
            string applicationName, 
            bool enableDiagnostics = true,
            string? logDirectory = null)
        {
            // Domyślny katalog logów w AppData
            logDirectory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TeamsManager", "logs");

            // Dodaj scentralizowany FileLoggerProvider
            builder.AddProvider(new FileLoggerProvider(logDirectory, applicationName));

            // Ustaw poziom logowania w zależności od diagnostyki
            if (enableDiagnostics)
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                
                // Dodaj filtry dla kategorii diagnostycznych
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
                builder.AddFilter("TeamsManager", LogLevel.Debug);
            }
            else
            {
                builder.SetMinimumLevel(LogLevel.Information);
            }

            return builder;
        }

        /// <summary>
        /// Tworzy scentralizowany logger factory dla TeamsManager
        /// </summary>
        /// <param name="applicationName">Nazwa aplikacji</param>
        /// <param name="enableDiagnostics">Czy włączyć diagnostykę</param>
        /// <param name="logDirectory">Opcjonalny katalog logów</param>
        /// <returns>ILoggerFactory</returns>
        public static ILoggerFactory CreateTeamsManagerLoggerFactory(string applicationName, 
            bool enableDiagnostics = true, 
            string? logDirectory = null)
        {
            // Domyślny katalog logów w AppData
            logDirectory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TeamsManager", "logs");

            return LoggerFactory.Create(builder =>
            {
                builder.AddTeamsManagerDiagnosticLogging(applicationName, enableDiagnostics, logDirectory);
            });
        }
    }
} 