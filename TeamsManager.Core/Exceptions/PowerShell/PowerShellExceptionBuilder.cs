using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace TeamsManager.Core.Exceptions.PowerShell
{
    /// <summary>
    /// Klasa pomocnicza do budowania wyjątków PowerShell z ErrorRecord.
    /// </summary>
    public static class PowerShellExceptionBuilder
    {
        /// <summary>
        /// Tworzy odpowiedni wyjątek na podstawie kolekcji ErrorRecord.
        /// </summary>
        public static PowerShellException BuildFromErrorRecords(
            IEnumerable<ErrorRecord> errorRecords,
            string? command = null,
            string? defaultMessage = null)
        {
            var errors = errorRecords.ToList();
            if (!errors.Any())
            {
                return new PowerShellCommandExecutionException(
                    defaultMessage ?? "Nieznany błąd PowerShell");
            }

            var firstError = errors.First();
            var message = BuildErrorMessage(errors, defaultMessage);

            // Rozpoznaj typ błędu na podstawie kategorii
            if (IsConnectionError(firstError))
            {
                return new PowerShellConnectionException(message, null, innerException: firstError.Exception);
            }

            // Domyślnie zwróć błąd wykonania komendy
            return new PowerShellCommandExecutionException(
                message,
                command: command,
                parameters: null,
                executionTime: null,
                exitCode: null,
                errorRecords: errors,
                innerException: firstError.Exception);
        }

        private static bool IsConnectionError(ErrorRecord error)
        {
            // Sprawdź typowe kategorie błędów połączenia
            return error.CategoryInfo.Category == ErrorCategory.ConnectionError ||
                   error.CategoryInfo.Category == ErrorCategory.AuthenticationError ||
                   error.CategoryInfo.Category == ErrorCategory.SecurityError ||
                   (error.Exception?.GetType().Name.Contains("PSRemoting") ?? false) ||
                   (error.FullyQualifiedErrorId?.Contains("Connection") ?? false);
        }

        private static string BuildErrorMessage(List<ErrorRecord> errors, string? defaultMessage)
        {
            if (errors.Count == 1)
            {
                var error = errors[0];
                return error.Exception?.Message 
                    ?? error.ErrorDetails?.Message 
                    ?? defaultMessage 
                    ?? "Błąd wykonania komendy PowerShell";
            }

            var message = defaultMessage ?? $"Wystąpiło {errors.Count} błędów PowerShell";
            var firstError = errors[0];
            if (firstError.Exception != null)
            {
                message += $". Pierwszy błąd: {firstError.Exception.Message}";
            }

            return message;
        }

        /// <summary>
        /// Ekstraktuje szczegóły kontekstowe z ErrorRecord.
        /// </summary>
        public static Dictionary<string, object?> ExtractContextData(ErrorRecord error)
        {
            var context = new Dictionary<string, object?>
            {
                ["CategoryInfo.Category"] = error.CategoryInfo.Category.ToString(),
                ["CategoryInfo.Reason"] = error.CategoryInfo.Reason,
                ["CategoryInfo.TargetName"] = error.CategoryInfo.TargetName,
                ["CategoryInfo.TargetType"] = error.CategoryInfo.TargetType,
                ["FullyQualifiedErrorId"] = error.FullyQualifiedErrorId,
                ["ScriptStackTrace"] = error.ScriptStackTrace,
                ["PipelineIterationInfo"] = error.PipelineIterationInfo?.Any() == true 
                    ? string.Join(", ", error.PipelineIterationInfo) 
                    : null
            };

            // Usuń null wartości
            return context.Where(kvp => kvp.Value != null)
                         .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
} 