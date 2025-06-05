using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TeamsManager.Core.Exceptions.PowerShell;

namespace TeamsManager.Core.Helpers.PowerShell
{
    /// <summary>
    /// Walidator i sanitizer parametrów przed przekazaniem do PowerShell
    /// </summary>
    public static class PSParameterValidator
    {
        // Wzorce walidacji
        private static readonly Regex SafeStringPattern = new(@"^[a-zA-Z0-9\s\-_\.@]+$");
        private static readonly Regex EmailPattern = new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
        private static readonly Regex GuidPattern = new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

        /// <summary>
        /// Waliduje i sanituje string dla PowerShell
        /// </summary>
        public static string ValidateAndSanitizeString(string? value, string parameterName, 
            bool allowEmpty = false, int? maxLength = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!allowEmpty)
                {
                    throw new ArgumentException($"Parametr '{parameterName}' nie może być pusty.");
                }
                return string.Empty;
            }

            // Trim i normalizacja
            value = value.Trim();

            // Sprawdź długość
            if (maxLength.HasValue && value.Length > maxLength.Value)
            {
                throw new ArgumentException(
                    $"Parametr '{parameterName}' przekracza maksymalną długość {maxLength.Value} znaków.");
            }

            // Escape potencjalnie niebezpiecznych znaków dla PowerShell
            value = value.Replace("'", "''");  // Escape single quotes
            value = value.Replace("`", "``");  // Escape backticks
            value = value.Replace("$", "`$");  // Escape dollar signs

            return value;
        }

        /// <summary>
        /// Waliduje adres email
        /// </summary>
        public static string ValidateEmail(string? email, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException($"Parametr '{parameterName}' (email) nie może być pusty.");
            }

            email = email.Trim().ToLowerInvariant();

            if (!EmailPattern.IsMatch(email))
            {
                throw new ArgumentException($"Parametr '{parameterName}' zawiera nieprawidłowy adres email: {email}");
            }

            return email;
        }

        /// <summary>
        /// Waliduje GUID
        /// </summary>
        public static string ValidateGuid(string? guid, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new ArgumentException($"Parametr '{parameterName}' (GUID) nie może być pusty.");
            }

            guid = guid.Trim();

            if (!GuidPattern.IsMatch(guid))
            {
                throw new ArgumentException($"Parametr '{parameterName}' zawiera nieprawidłowy GUID: {guid}");
            }

            return guid;
        }

        /// <summary>
        /// Waliduje kolekcję identyfikatorów
        /// </summary>
        public static List<string> ValidateIdCollection(IEnumerable<string>? ids, string parameterName, 
            int? maxCount = null)
        {
            if (ids == null || !ids.Any())
            {
                throw new ArgumentException($"Parametr '{parameterName}' nie może być pustą kolekcją.");
            }

            var validatedIds = new List<string>();
            var duplicates = new HashSet<string>();

            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException($"Parametr '{parameterName}' zawiera pusty identyfikator.");
                }

                var trimmedId = id.Trim();
                
                // Sprawdź duplikaty
                if (!duplicates.Add(trimmedId))
                {
                    throw new ArgumentException($"Parametr '{parameterName}' zawiera duplikat: {trimmedId}");
                }

                validatedIds.Add(trimmedId);
            }

            // Sprawdź maksymalną liczbę
            if (maxCount.HasValue && validatedIds.Count > maxCount.Value)
            {
                throw new ArgumentException(
                    $"Parametr '{parameterName}' zawiera więcej elementów ({validatedIds.Count}) " +
                    $"niż dozwolone maksimum ({maxCount.Value}).");
            }

            return validatedIds;
        }

        /// <summary>
        /// Tworzy bezpieczny słownik parametrów dla PowerShell
        /// </summary>
        public static Dictionary<string, object> CreateSafeParameters(params (string key, object? value)[] parameters)
        {
            var safeParams = new Dictionary<string, object>();

            foreach (var (key, value) in parameters)
            {
                if (value == null) continue;

                // Sanityzuj wartości string
                if (value is string strValue)
                {
                    safeParams[key] = ValidateAndSanitizeString(strValue, key, allowEmpty: true);
                }
                else
                {
                    safeParams[key] = value;
                }
            }

            return safeParams;
        }
    }
} 