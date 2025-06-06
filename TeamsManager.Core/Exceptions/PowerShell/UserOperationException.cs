using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.Json;

namespace TeamsManager.Core.Exceptions.PowerShell
{
    /// <summary>
    /// Wyjątek związany z operacjami na użytkownikach Microsoft 365
    /// </summary>
    [Serializable]
    public class UserOperationException : PowerShellException
    {
        /// <summary>
        /// UPN użytkownika, którego dotyczy błąd
        /// </summary>
        public string? UserUpn { get; }

        /// <summary>
        /// ID użytkownika, jeśli jest dostępne
        /// </summary>
        public string? UserId { get; }

        /// <summary>
        /// Nazwa wyświetlana użytkownika, jeśli jest dostępna
        /// </summary>
        public string? UserDisplayName { get; }

        /// <summary>
        /// Typ operacji, która się nie powiodła
        /// </summary>
        public string? OperationType { get; }

        public UserOperationException(string message)
            : base(message)
        {
        }

        public UserOperationException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        public UserOperationException(
            string message,
            string? userUpn = null,
            string? userId = null,
            string? userDisplayName = null,
            string? operationType = null,
            IEnumerable<ErrorRecord>? errorRecords = null,
            Exception? innerException = null)
            : base(message, errorRecords, innerException)
        {
            UserUpn = userUpn;
            UserId = userId;
            UserDisplayName = userDisplayName;
            OperationType = operationType;
        }

        public UserOperationException(
            string message,
            string? userUpn,
            string? userId,
            string? userDisplayName,
            string? operationType,
            IEnumerable<ErrorRecord>? errorRecords,
            IDictionary<string, object?>? contextData,
            Exception? innerException = null)
            : base(message, errorRecords, contextData, innerException)
        {
            UserUpn = userUpn;
            UserId = userId;
            UserDisplayName = userDisplayName;
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
                UserUpn,
                UserId,
                UserDisplayName,
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
        public static UserOperationException? FromJson(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Deserialized User exception";
                var userUpn = root.TryGetProperty("userUpn", out var upnProp) ? upnProp.GetString() : null;
                var userId = root.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetString() : null;
                var userDisplayName = root.TryGetProperty("userDisplayName", out var userNameProp) ? userNameProp.GetString() : null;
                var operationType = root.TryGetProperty("operationType", out var opTypeProp) ? opTypeProp.GetString() : null;

                return new UserOperationException(
                    message ?? "Deserialized User exception",
                    userUpn,
                    userId,
                    userDisplayName,
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
        /// Zwraca sformatowany opis błędu z kontekstem użytkownika
        /// </summary>
        public override string ToString()
        {
            var details = new List<string>
            {
                base.ToString()
            };

            if (!string.IsNullOrEmpty(UserUpn))
                details.Add($"User UPN: {UserUpn}");

            if (!string.IsNullOrEmpty(UserId))
                details.Add($"User ID: {UserId}");

            if (!string.IsNullOrEmpty(UserDisplayName))
                details.Add($"User Name: {UserDisplayName}");

            if (!string.IsNullOrEmpty(OperationType))
                details.Add($"Operation: {OperationType}");

            return string.Join(Environment.NewLine, details);
        }
    }
} 