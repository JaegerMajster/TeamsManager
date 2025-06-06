using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.Json;

namespace TeamsManager.Core.Exceptions.PowerShell
{
    /// <summary>
    /// Wyjątek związany z wykonywaniem komend PowerShell
    /// </summary>
    [Serializable]
    public class PowerShellCommandExecutionException : PowerShellException
    {
        /// <summary>
        /// Komenda PowerShell, która się nie powiodła
        /// </summary>
        public string? Command { get; }

        /// <summary>
        /// Parametry przekazane do komendy
        /// </summary>
        public IReadOnlyDictionary<string, object?>? Parameters { get; }

        /// <summary>
        /// Czas wykonywania komendy przed błędem
        /// </summary>
        public TimeSpan? ExecutionTime { get; }

        /// <summary>
        /// Kod wyjścia, jeśli dostępny
        /// </summary>
        public int? ExitCode { get; }

        public PowerShellCommandExecutionException(string message)
            : base(message)
        {
        }

        public PowerShellCommandExecutionException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        public PowerShellCommandExecutionException(
            string message,
            string? command = null,
            IDictionary<string, object?>? parameters = null,
            TimeSpan? executionTime = null,
            int? exitCode = null,
            IEnumerable<ErrorRecord>? errorRecords = null,
            Exception? innerException = null)
            : base(message, errorRecords, innerException)
        {
            Command = command;
            Parameters = parameters?.AsReadOnly();
            ExecutionTime = executionTime;
            ExitCode = exitCode;
        }

        public PowerShellCommandExecutionException(
            string message,
            string? command,
            IDictionary<string, object?>? parameters,
            TimeSpan? executionTime,
            int? exitCode,
            IEnumerable<ErrorRecord>? errorRecords,
            IDictionary<string, object?>? contextData,
            Exception? innerException = null)
            : base(message, errorRecords, contextData, innerException)
        {
            Command = command;
            Parameters = parameters?.AsReadOnly();
            ExecutionTime = executionTime;
            ExitCode = exitCode;
        }

        /// <summary>
        /// Serializes exception data to JSON string for modern .NET 9 compatibility
        /// Replaces obsolete binary serialization with JSON approach
        /// </summary>
        public string SerializeToJson()
        {
            var data = new
            {
                Message,
                Command,
                Parameters = Parameters?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString()),
                ExecutionTime = ExecutionTime?.TotalMilliseconds,
                ExitCode,
                InnerExceptionMessage = InnerException?.Message,
                StackTrace,
                ErrorRecords = ErrorRecords?.Take(5).Select(er => new 
                {
                    ErrorMessage = er.Exception?.Message,
                    CategoryInfo = er.CategoryInfo?.ToString()
                }).ToArray()
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// Creates exception from JSON data for modern deserialization
        /// </summary>
        public static PowerShellCommandExecutionException? FromJson(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Deserialized PowerShell exception";
                var command = root.TryGetProperty("command", out var cmdProp) ? cmdProp.GetString() : null;
                var exitCode = root.TryGetProperty("exitCode", out var exitProp) && exitProp.ValueKind == JsonValueKind.Number 
                    ? exitProp.GetInt32() : (int?)null;

                TimeSpan? executionTime = null;
                if (root.TryGetProperty("executionTime", out var timeProp) && timeProp.ValueKind == JsonValueKind.Number)
                {
                    executionTime = TimeSpan.FromMilliseconds(timeProp.GetDouble());
                }

                Dictionary<string, object?>? parameters = null;
                if (root.TryGetProperty("parameters", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
                {
                    parameters = new Dictionary<string, object?>();
                    foreach (var param in paramsProp.EnumerateObject())
                    {
                        parameters[param.Name] = param.Value.GetString();
                    }
                }

                return new PowerShellCommandExecutionException(
                    message ?? "Deserialized PowerShell exception",
                    command,
                    parameters,
                    executionTime,
                    exitCode,
                    null, // ErrorRecords nie są deserializowane dla uproszczenia
                    null);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Zwraca sformatowany opis błędu z kontekstem wykonania
        /// </summary>
        public override string ToString()
        {
            var details = new List<string>
            {
                base.ToString()
            };

            if (!string.IsNullOrEmpty(Command))
                details.Add($"Command: {Command}");

            if (Parameters != null && Parameters.Count > 0)
            {
                details.Add("Parameters:");
                foreach (var param in Parameters)
                {
                    details.Add($"  {param.Key}: {param.Value}");
                }
            }

            if (ExecutionTime.HasValue)
                details.Add($"Execution Time: {ExecutionTime.Value.TotalMilliseconds}ms");

            if (ExitCode.HasValue)
                details.Add($"Exit Code: {ExitCode.Value}");

            return string.Join(Environment.NewLine, details);
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu cmdlet.
        /// </summary>
        public static PowerShellCommandExecutionException ForCmdlet(
            string cmdletName,
            IEnumerable<ErrorRecord> errorRecords,
            IDictionary<string, object?>? parameters = null)
        {
            var message = $"Wykonanie cmdlet '{cmdletName}' zakończyło się błędem";
            if (errorRecords.Any())
            {
                var firstError = errorRecords.First();
                message += $": {firstError.Exception?.Message ?? firstError.ToString()}";
            }

            return new PowerShellCommandExecutionException(
                message,
                command: cmdletName,
                parameters: parameters,
                executionTime: null,
                exitCode: null,
                errorRecords: errorRecords,
                innerException: null);
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu skryptu.
        /// </summary>
        public static PowerShellCommandExecutionException ForScript(
            string script,
            IEnumerable<ErrorRecord> errorRecords,
            IDictionary<string, object?>? parameters = null)
        {
            var scriptPreview = script.Length > 100 
                ? script.Substring(0, 97) + "..." 
                : script;

            var message = $"Wykonanie skryptu zakończyło się błędem: {scriptPreview}";
            
            return new PowerShellCommandExecutionException(
                message,
                command: script,
                parameters: parameters,
                executionTime: null,
                exitCode: null,
                errorRecords: errorRecords,
                innerException: null);
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu pipeline.
        /// </summary>
        public static PowerShellCommandExecutionException ForPipeline(
            string command,
            IEnumerable<ErrorRecord> errorRecords,
            int position)
        {
            var message = $"Błąd w pipeline na pozycji {position}: {command}";
            
            return new PowerShellCommandExecutionException(
                message,
                command: command,
                parameters: null,
                executionTime: null,
                exitCode: null,
                errorRecords: errorRecords,
                innerException: null);
        }
    }

    /// <summary>
    /// Extension methods dla ułatwienia pracy z Parameters dictionary
    /// </summary>
    public static class DictionaryExtensions
    {
        public static IReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
            where TKey : notnull
        {
            return dictionary as IReadOnlyDictionary<TKey, TValue> ?? 
                   new Dictionary<TKey, TValue>(dictionary);
        }
    }
} 