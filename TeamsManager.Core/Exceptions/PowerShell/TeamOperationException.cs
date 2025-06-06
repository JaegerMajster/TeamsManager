using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Runtime.Serialization;

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

        protected TeamOperationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            TeamId = info.GetString(nameof(TeamId));
            TeamDisplayName = info.GetString(nameof(TeamDisplayName));
            OperationType = info.GetString(nameof(OperationType));
        }

        [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(TeamId), TeamId);
            info.AddValue(nameof(TeamDisplayName), TeamDisplayName);
            info.AddValue(nameof(OperationType), OperationType);
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