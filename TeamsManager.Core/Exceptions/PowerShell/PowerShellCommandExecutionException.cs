using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Runtime.Serialization;
using System.Text;

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

        protected PowerShellCommandExecutionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Command = info.GetString(nameof(Command));
            ExecutionTime = (TimeSpan?)info.GetValue(nameof(ExecutionTime), typeof(TimeSpan?));
            ExitCode = (int?)info.GetValue(nameof(ExitCode), typeof(int?));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(Command), Command);
            info.AddValue(nameof(ExecutionTime), ExecutionTime);
            info.AddValue(nameof(ExitCode), ExitCode);
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