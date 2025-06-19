using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace TeamsManager.UI.Services
{
    public class SimpleFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _filePath;
        private readonly object _lock = new object();

        public SimpleFileLogger(string categoryName, string filePath)
        {
            _categoryName = categoryName;
            _filePath = filePath;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";
            
            if (exception != null)
            {
                logEntry += Environment.NewLine + exception.ToString();
            }

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_filePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Ignoruj błędy zapisu do pliku żeby nie crashować aplikacji
                }
            }
        }
    }

    public class SimpleFileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;

        public SimpleFileLoggerProvider(string filePath)
        {
            _filePath = filePath;
            
            // Upewnij się, że katalog istnieje
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SimpleFileLogger(categoryName, _filePath);
        }

        public void Dispose()
        {
        }
    }
} 