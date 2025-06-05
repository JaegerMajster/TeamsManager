using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Runtime.Serialization;

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

        protected UserOperationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            UserUpn = info.GetString(nameof(UserUpn));
            UserId = info.GetString(nameof(UserId));
            UserDisplayName = info.GetString(nameof(UserDisplayName));
            OperationType = info.GetString(nameof(OperationType));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(UserUpn), UserUpn);
            info.AddValue(nameof(UserId), UserId);
            info.AddValue(nameof(UserDisplayName), UserDisplayName);
            info.AddValue(nameof(OperationType), OperationType);
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