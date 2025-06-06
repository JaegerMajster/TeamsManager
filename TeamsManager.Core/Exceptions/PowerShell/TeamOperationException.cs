using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.Json;

namespace TeamsManager.Core.Exceptions.PowerShell
{
    /// <summary>
    /// Wyjątek związany z operacjami na zespołach Microsoft Teams
    /// </summary>
    [Serializable]
    public class TeamOperationException : PowerShellException
    {
        /// <summary>
        /// ID zespołu, którego dotyczy błąd
        /// </summary>
        public string? TeamId { get; }

        /// <summary>
        /// Nazwa zespołu, jeśli jest dostępna
        /// </summary>
        public string? TeamDisplayName { get; }

        /// <summary>
        /// Typ operacji, która się nie powiodła
        /// </summary>
        public string? OperationType { get; }

        public TeamOperationException(string message)
            : base(message)
        {
        }

        public TeamOperationException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        public TeamOperationException(
            string message,
            string? teamId = null,
            string? teamDisplayName = null,
            string? operationType = null,
            IEnumerable<ErrorRecord>? errorRecords = null,
            Exception? innerException = null)
            : base(message, errorRecords, innerException)
        {
            TeamId = teamId;
            TeamDisplayName = teamDisplayName;
            OperationType = operationType;
        }

        public TeamOperationException(
            string message,
            string? teamId,
            string? teamDisplayName,
            string? operationType,
            IEnumerable<ErrorRecord>? errorRecords,
            IDictionary<string, object?>? contextData,
            Exception? innerException = null)
            : base(message, errorRecords, contextData, innerException)
        {
            TeamId = teamId;
            TeamDisplayName = teamDisplayName;
            OperationType = operationType;
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
                TeamId,
                TeamDisplayName,
                OperationType,
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
        public static TeamOperationException? FromJson(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Deserialized Team exception";
                var teamId = root.TryGetProperty("teamId", out var teamIdProp) ? teamIdProp.GetString() : null;
                var teamDisplayName = root.TryGetProperty("teamDisplayName", out var teamNameProp) ? teamNameProp.GetString() : null;
                var operationType = root.TryGetProperty("operationType", out var opTypeProp) ? opTypeProp.GetString() : null;

                return new TeamOperationException(
                    message ?? "Deserialized Team exception",
                    teamId,
                    teamDisplayName,
                    operationType,
                    null, // ErrorRecords nie są deserializowane dla uproszczenia
                    null);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Zwraca sformatowany opis błędu z kontekstem zespołu
        /// </summary>
        public override string ToString()
        {
            var details = new List<string>
            {
                base.ToString()
            };

            if (!string.IsNullOrEmpty(TeamId))
                details.Add($"Team ID: {TeamId}");

            if (!string.IsNullOrEmpty(TeamDisplayName))
                details.Add($"Team Name: {TeamDisplayName}");

            if (!string.IsNullOrEmpty(OperationType))
                details.Add($"Operation: {OperationType}");

            return string.Join(Environment.NewLine, details);
        }
    }
} 