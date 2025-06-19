using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace TeamsManager.UI.Services.Configuration
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
        private bool _disposed;

        public FileLoggerProvider(string logDirectory)
        {
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            
            // Utwórz katalog jeśli nie istnieje
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _logDirectory));
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

    public class FileLogger : ILogger, IDisposable
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;
        private readonly object _lock = new();
        private bool _disposed;

        public FileLogger(string categoryName, string logDirectory)
        {
            _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
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

            var logEntry = FormatLogEntry(logLevel, _categoryName, message, exception);
            WriteToFile(logEntry);
        }

        private string FormatLogEntry(LogLevel logLevel, string categoryName, string message, Exception? exception)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = GetLogLevelString(logLevel);
            var category = categoryName.Split('.').LastOrDefault() ?? categoryName;
            
            var entry = new StringBuilder();
            entry.AppendLine($"[{timestamp}] [{level}] [{category}] {message}");
            
            if (exception != null)
            {
                entry.AppendLine($"Exception: {exception.GetType().Name}: {exception.Message}");
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    entry.AppendLine($"StackTrace: {exception.StackTrace}");
                }
                
                var innerEx = exception.InnerException;
                while (innerEx != null)
                {
                    entry.AppendLine($"Inner Exception: {innerEx.GetType().Name}: {innerEx.Message}");
                    innerEx = innerEx.InnerException;
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

        private void WriteToFile(string logEntry)
        {
            if (_disposed) return;

            lock (_lock)
            {
                try
                {
                    var fileName = $"teamsmanager-{DateTime.Now:yyyy-MM-dd}.log";
                    var filePath = Path.Combine(_logDirectory, fileName);
                    
                    // Zapisz z UTF-8 encoding
                    File.AppendAllText(filePath, logEntry, Encoding.UTF8);
                }
                catch
                {
                    // Nie loguj błędów logowania żeby uniknąć nieskończonej pętli
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
} 