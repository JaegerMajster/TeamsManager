using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace TeamsManager.Core.Services.Logging
{
    /// <summary>
    /// Scentralizowany provider logowania do pliku dla całego systemu TeamsManager
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;
        private readonly string _applicationName;
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
        private bool _disposed;

        public FileLoggerProvider(string logDirectory, string applicationName = "TeamsManager")
        {
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            
            // Utwórz katalog jeśli nie istnieje
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _logDirectory, _applicationName));
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            foreach (var logger in _loggers.Values)
            {
                logger.Dispose();
            }
            
            _loggers.Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// Scentralizowany logger do pliku z rozszerzonymi funkcjami diagnostycznymi
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;
        private readonly string _applicationName;
        private readonly object _lock = new();
        private bool _disposed;

        public FileLogger(string categoryName, string logDirectory, string applicationName)
        {
            _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || _disposed)
                return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message))
                return;

            var logEntry = FormatLogEntry(logLevel, _categoryName, message, exception, eventId);
            WriteToFile(logEntry, logLevel);
        }

        private string FormatLogEntry(LogLevel logLevel, string categoryName, string message, Exception? exception, EventId eventId)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = GetLogLevelString(logLevel);
            var category = categoryName.Split('.').LastOrDefault() ?? categoryName;
            
            var entry = new StringBuilder();
            
            // Format: [timestamp] [level] [app] [category] [eventId] message
            var eventIdStr = eventId.Id != 0 ? $"[{eventId.Id}]" : "";
            entry.AppendLine($"[{timestamp}] [{level}] [{_applicationName}] [{category}] {eventIdStr} {message}");
            
            if (exception != null)
            {
                entry.AppendLine($"EXCEPTION: {exception.GetType().Name}: {exception.Message}");
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    entry.AppendLine($"STACK TRACE:");
                    entry.AppendLine(exception.StackTrace);
                }
                
                var innerEx = exception.InnerException;
                var innerLevel = 1;
                while (innerEx != null)
                {
                    entry.AppendLine($"INNER EXCEPTION [{innerLevel}]: {innerEx.GetType().Name}: {innerEx.Message}");
                    if (!string.IsNullOrEmpty(innerEx.StackTrace))
                    {
                        entry.AppendLine($"INNER STACK TRACE [{innerLevel}]:");
                        entry.AppendLine(innerEx.StackTrace);
                    }
                    innerEx = innerEx.InnerException;
                    innerLevel++;
                }
            }
            
            return entry.ToString();
        }

        private static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "FATAL",
                _ => "UNKN "
            };
        }

        private void WriteToFile(string logEntry, LogLevel logLevel)
        {
            if (_disposed) return;

            lock (_lock)
            {
                try
                {
                    // Różne pliki dla różnych poziomów logowania
                    var filePrefix = GetFilePrefix(logLevel);
                    var fileName = $"{_applicationName.ToLower()}-{filePrefix}-{DateTime.Now:yyyy-MM-dd}.log";
                    var filePath = Path.Combine(_logDirectory, fileName);
                    
                    // Zapisz z UTF-8 encoding
                    File.AppendAllText(filePath, logEntry, Encoding.UTF8);
                    
                    // Dla diagnostycznych logów dodaj także do głównego pliku
                    if (logEntry.Contains("[DIAGNOSTIC]") || logEntry.Contains("[API-DIAGNOSTIC]") || logEntry.Contains("[UI-DIAGNOSTIC]"))
                    {
                        var diagnosticFileName = $"{_applicationName.ToLower()}-diagnostic-{DateTime.Now:yyyy-MM-dd}.log";
                        var diagnosticFilePath = Path.Combine(_logDirectory, diagnosticFileName);
                        File.AppendAllText(diagnosticFilePath, logEntry, Encoding.UTF8);
                    }
                }
                catch
                {
                    // Nie loguj błędów logowania żeby uniknąć nieskończonej pętli
                }
            }
        }

        private string GetFilePrefix(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "trace",
                LogLevel.Debug => "debug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "error",
                LogLevel.Critical => "fatal",
                _ => "all"
            };
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
} 