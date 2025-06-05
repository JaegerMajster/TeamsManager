using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Runtime.Serialization;

namespace TeamsManager.Core.Exceptions.PowerShell
{
    /// <summary>
    /// Bazowa klasa wyjątku dla wszystkich błędów związanych z PowerShell.
    /// Dostarcza wspólną funkcjonalność dla hierarchii wyjątków PowerShell.
    /// </summary>
    [Serializable]
    public abstract class PowerShellException : Exception
    {
        /// <summary>
        /// Kolekcja ErrorRecord z sesji PowerShell.
        /// </summary>
        public IReadOnlyList<ErrorRecord> ErrorRecords { get; }

        /// <summary>
        /// Stan sesji PowerShell w momencie wystąpienia błędu.
        /// </summary>
        public PSInvocationState? SessionState { get; init; }

        /// <summary>
        /// Identyfikator runspace gdzie wystąpił błąd.
        /// </summary>
        public Guid? RunspaceId { get; init; }

        /// <summary>
        /// Czas wystąpienia błędu.
        /// </summary>
        public DateTimeOffset OccurredAt { get; }

        /// <summary>
        /// Dodatkowe właściwości kontekstowe.
        /// </summary>
        public IReadOnlyDictionary<string, object?> ContextData { get; }

        protected PowerShellException(string message) 
            : base(message)
        {
            ErrorRecords = new List<ErrorRecord>().AsReadOnly();
            ContextData = new Dictionary<string, object?>().AsReadOnly();
            OccurredAt = DateTimeOffset.UtcNow;
        }

        protected PowerShellException(string message, Exception? innerException) 
            : base(message, innerException)
        {
            ErrorRecords = new List<ErrorRecord>().AsReadOnly();
            ContextData = new Dictionary<string, object?>().AsReadOnly();
            OccurredAt = DateTimeOffset.UtcNow;
        }

        protected PowerShellException(
            string message, 
            IEnumerable<ErrorRecord>? errorRecords = null,
            Exception? innerException = null) 
            : base(message, innerException)
        {
            ErrorRecords = (errorRecords?.ToList() ?? new List<ErrorRecord>()).AsReadOnly();
            ContextData = new Dictionary<string, object?>().AsReadOnly();
            OccurredAt = DateTimeOffset.UtcNow;
        }

        protected PowerShellException(
            string message,
            IEnumerable<ErrorRecord>? errorRecords,
            IDictionary<string, object?>? contextData,
            Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorRecords = (errorRecords?.ToList() ?? new List<ErrorRecord>()).AsReadOnly();
            ContextData = new Dictionary<string, object?>(contextData ?? new Dictionary<string, object?>()).AsReadOnly();
            OccurredAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Konstruktor dla deserializacji.
        /// </summary>
        protected PowerShellException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorRecords = new List<ErrorRecord>().AsReadOnly();
            ContextData = new Dictionary<string, object?>().AsReadOnly();
            OccurredAt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Zwraca sformatowany opis wszystkich ErrorRecord.
        /// </summary>
        public string GetFormattedErrorDetails()
        {
            if (!ErrorRecords.Any())
                return "Brak szczegółowych informacji o błędzie PowerShell.";

            var details = new List<string>();
            for (int i = 0; i < ErrorRecords.Count; i++)
            {
                var error = ErrorRecords[i];
                details.Add($"Błąd {i + 1}:");
                details.Add($"  Kategoria: {error.CategoryInfo.Category}");
                details.Add($"  Przyczyna: {error.CategoryInfo.Reason}");
                
                if (!string.IsNullOrEmpty(error.FullyQualifiedErrorId))
                    details.Add($"  ID błędu: {error.FullyQualifiedErrorId}");
                
                if (error.TargetObject != null)
                    details.Add($"  Obiekt docelowy: {error.TargetObject}");
                
                if (error.ErrorDetails != null && !string.IsNullOrEmpty(error.ErrorDetails.Message))
                    details.Add($"  Szczegóły: {error.ErrorDetails.Message}");
            }

            return string.Join(Environment.NewLine, details);
        }
    }
} 