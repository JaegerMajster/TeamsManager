using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TeamsManager.Core.Exceptions.PowerShell;

namespace TeamsManager.Core.Helpers.PowerShell
{
    /// <summary>
    /// Walidator i sanitizer parametrów dla poleceń PowerShell Microsoft.Graph
    /// </summary>
    public static class PSParameterValidator
    {
        // Maksymalne długości zgodne z Microsoft Graph API
        private const int MaxTeamDisplayNameLength = 256;
        private const int MaxTeamDescriptionLength = 1024;
        private const int MaxChannelDisplayNameLength = 50;
        private const int MaxChannelDescriptionLength = 1024;
        private const int MaxUserDisplayNameLength = 256;
        private const int MaxDepartmentLength = 64;
        
        // Regex patterns
        private static readonly Regex EmailRegex = new(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex GuidRegex = new(@"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$", RegexOptions.Compiled);
        private static readonly Regex SafeStringRegex = new(@"^[a-zA-Z0-9\s\-_.,!?@#&()\[\]{}:;'""]+$", RegexOptions.Compiled);
        
        // Znaki wymagające escape w PowerShell
        private static readonly Dictionary<char, string> PowerShellEscapeChars = new()
        {
            { '\'', "''" },      // Pojedynczy apostrof
            { '"', "`\"" },      // Cudzysłów
            { '`', "``" },       // Backtick
            { '$', "`$" },       // Dollar
            { '\r', "`r" },      // Carriage return
            { '\n', "`n" },      // New line
            { '\t', "`t" },      // Tab
            { '\0', "`0" },      // Null
            { '\\', "\\\\" }     // Backslash (dla ścieżek)
        };

        // Backward compatibility - stare nazwy dla istniejącego kodu
        private static readonly Regex SafeStringPattern = SafeStringRegex;
        private static readonly Regex EmailPattern = EmailRegex;
        private static readonly Regex GuidPattern = GuidRegex;

        /// <summary>
        /// Waliduje i sanityzuje string dla użycia w PowerShell
        /// </summary>
        public static string ValidateAndSanitizeString(
            string value, 
            string parameterName, 
            bool allowEmpty = false,
            int? maxLength = null,
            bool allowSpecialChars = true)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (allowEmpty)
                    return string.Empty;
                    
                throw new ArgumentException($"Parameter '{parameterName}' cannot be null or empty.", parameterName);
            }

            // Sprawdź maksymalną długość
            var effectiveMaxLength = maxLength ?? GetDefaultMaxLength(parameterName);
            if (value.Length > effectiveMaxLength)
            {
                throw new ArgumentException(
                    $"Parameter '{parameterName}' exceeds maximum length of {effectiveMaxLength} characters. Current length: {value.Length}",
                    parameterName);
            }

            // Sanityzacja - escape niebezpiecznych znaków
            var sanitized = value;
            foreach (var escapeChar in PowerShellEscapeChars)
            {
                sanitized = sanitized.Replace(escapeChar.Key.ToString(), escapeChar.Value);
            }

            // Dodatkowa walidacja dla niektórych parametrów
            if (!allowSpecialChars && !SafeStringRegex.IsMatch(value))
            {
                throw new ArgumentException(
                    $"Parameter '{parameterName}' contains invalid characters. Only alphanumeric and basic punctuation allowed.",
                    parameterName);
            }

            // Specjalna walidacja dla Microsoft Graph - niektóre znaki są zabronione w nazwach
            if (IsGraphNameParameter(parameterName))
            {
                var invalidChars = new[] { '/', '\\', '#', '?', '*' };
                if (invalidChars.Any(c => value.Contains(c)))
                {
                    throw new ArgumentException(
                        $"Parameter '{parameterName}' contains characters not allowed in Microsoft Graph names: {string.Join(", ", invalidChars.Where(c => value.Contains(c)))}",
                        parameterName);
                }
            }

            return sanitized;
        }

        /// <summary>
        /// Waliduje adres email
        /// </summary>
        public static string ValidateEmail(string email, string parameterName = "email")
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException($"Parameter '{parameterName}' cannot be null or empty.", parameterName);
            }

            if (!EmailRegex.IsMatch(email))
            {
                throw new ArgumentException($"Parameter '{parameterName}' is not a valid email address: {email}", parameterName);
            }

            return email.ToLowerInvariant(); // Graph API preferuje lowercase emails
        }

        /// <summary>
        /// Waliduje GUID
        /// </summary>
        public static string ValidateGuid(string guid, string parameterName = "id")
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new ArgumentException($"Parameter '{parameterName}' cannot be null or empty.", parameterName);
            }

            if (!GuidRegex.IsMatch(guid))
            {
                throw new ArgumentException($"Parameter '{parameterName}' is not a valid GUID: {guid}", parameterName);
            }

            return guid.ToLowerInvariant(); // Normalizacja dla spójności
        }

        /// <summary>
        /// Waliduje wartość enum
        /// </summary>
        public static TEnum ValidateEnum<TEnum>(string value, string parameterName) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Parameter '{parameterName}' cannot be null or empty.", parameterName);
            }

            if (!Enum.TryParse<TEnum>(value, true, out var result))
            {
                var validValues = Enum.GetNames(typeof(TEnum));
                throw new ArgumentException(
                    $"Parameter '{parameterName}' has invalid value '{value}'. Valid values are: {string.Join(", ", validValues)}",
                    parameterName);
            }

            return result;
        }

        /// <summary>
        /// Waliduje tablicę stringów
        /// </summary>
        public static string[] ValidateStringArray(
            string[]? values, 
            string parameterName,
            bool allowEmpty = false,
            int? maxCount = null)
        {
            if (values == null || values.Length == 0)
            {
                if (allowEmpty)
                    return Array.Empty<string>();
                    
                throw new ArgumentException($"Parameter '{parameterName}' cannot be null or empty.", parameterName);
            }

            if (maxCount.HasValue && values.Length > maxCount.Value)
            {
                throw new ArgumentException(
                    $"Parameter '{parameterName}' contains too many items. Maximum: {maxCount.Value}, Actual: {values.Length}",
                    parameterName);
            }

            // Waliduj każdy element
            var validated = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]))
                {
                    throw new ArgumentException(
                        $"Parameter '{parameterName}[{i}]' cannot be null or empty.",
                        parameterName);
                }
                validated[i] = values[i].Trim();
            }

            return validated;
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
                
                // Specjalne traktowanie dla różnych typów
                switch (value)
                {
                    case string stringValue:
                        // Stringi są już przetworzone przez ValidateAndSanitizeString
                        safeParams[key] = stringValue;
                        break;
                        
                    case bool boolValue:
                        safeParams[key] = boolValue;
                        break;
                        
                    case int intValue:
                    case long longValue:
                    case double doubleValue:
                        safeParams[key] = value;
                        break;
                        
                    case string[] arrayValue:
                        safeParams[key] = arrayValue;
                        break;
                        
                    case Enum enumValue:
                        safeParams[key] = enumValue.ToString();
                        break;
                        
                    default:
                        // Dla innych typów używamy ToString z escape
                        var stringRep = value.ToString() ?? string.Empty;
                        safeParams[key] = ValidateAndSanitizeString(stringRep, key, allowEmpty: true);
                        break;
                }
            }
            
            return safeParams;
        }

        /// <summary>
        /// Waliduje parametry połączenia Microsoft Graph
        /// </summary>
        public static (string[] scopes, string tenantId) ValidateGraphConnectionParams(
            string[]? scopes,
            string? tenantId)
        {
            // Domyślne scopes dla Microsoft Graph jeśli nie podano
            var validatedScopes = scopes?.Length > 0 
                ? ValidateStringArray(scopes, "scopes", maxCount: 50)
                : new[] { "https://graph.microsoft.com/.default" };

            // Walidacja tenant ID jeśli podany
            var validatedTenantId = tenantId;
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                // Tenant ID może być GUID lub domena
                if (!GuidRegex.IsMatch(tenantId) && !tenantId.Contains('.'))
                {
                    throw new ArgumentException(
                        $"Tenant ID must be a valid GUID or domain name: {tenantId}",
                        nameof(tenantId));
                }
                validatedTenantId = tenantId.ToLowerInvariant();
            }

            return (validatedScopes, validatedTenantId ?? "common");
        }

        // Pomocnicze metody prywatne

        private static int GetDefaultMaxLength(string parameterName)
        {
            return parameterName.ToLowerInvariant() switch
            {
                var name when name.Contains("displayname") && name.Contains("team") => MaxTeamDisplayNameLength,
                var name when name.Contains("displayname") && name.Contains("channel") => MaxChannelDisplayNameLength,
                var name when name.Contains("displayname") => MaxUserDisplayNameLength,
                var name when name.Contains("description") && name.Contains("team") => MaxTeamDescriptionLength,
                var name when name.Contains("description") && name.Contains("channel") => MaxChannelDescriptionLength,
                var name when name.Contains("department") => MaxDepartmentLength,
                _ => 1024 // Domyślna bezpieczna wartość
            };
        }

        private static bool IsGraphNameParameter(string parameterName)
        {
            var nameParams = new[] { "displayname", "name", "mailnickname", "alias" };
            var lowerParam = parameterName.ToLowerInvariant();
            return nameParams.Any(np => lowerParam.Contains(np));
        }
    }
} 