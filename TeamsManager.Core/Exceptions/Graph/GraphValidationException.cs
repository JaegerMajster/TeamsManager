using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamsManager.Core.Exceptions.Graph
{
    /// <summary>
    /// Typy błędów walidacji.
    /// </summary>
    public enum ValidationType
    {
        Unknown,
        Required,
        Format,
        Length,
        Range,
        Unique,
        Reference,
        DataType,
        Pattern,
        BusinessRule,
        Multiple
    }

    /// <summary>
    /// Klasa reprezentująca szczegółowy błąd walidacji.
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// Nazwa pola, które nie przeszło walidacji.
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Typ błędu walidacji.
        /// </summary>
        public ValidationType Type { get; set; } = ValidationType.Unknown;

        /// <summary>
        /// Komunikat błędu walidacji.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Wartość która nie przeszła walidacji.
        /// </summary>
        public object? AttemptedValue { get; set; }

        /// <summary>
        /// Dodatkowe informacje o błędzie.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Konstruktor ValidationError.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <param name="type">Typ błędu</param>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="attemptedValue">Wartość która nie przeszła walidacji</param>
        public ValidationError(string fieldName, ValidationType type, string message, object? attemptedValue = null)
        {
            FieldName = fieldName;
            Type = type;
            Message = message;
            AttemptedValue = attemptedValue;
        }

        /// <summary>
        /// Dodaje metadane do błędu walidacji.
        /// </summary>
        /// <param name="key">Klucz metadanych</param>
        /// <param name="value">Wartość metadanych</param>
        /// <returns>Ta instancja ValidationError dla fluent interface</returns>
        public ValidationError WithMetadata(string key, object value)
        {
            Metadata[key] = value;
            return this;
        }

        /// <summary>
        /// Zwraca reprezentację tekstową błędu walidacji.
        /// </summary>
        /// <returns>String representation błędu</returns>
        public override string ToString()
        {
            return $"{FieldName}: {Message} (Type: {Type})";
        }
    }

    /// <summary>
    /// Wyjątek reprezentujący błędy walidacji Microsoft Graph API.
    /// Implementuje ETAP 1.4.5 - GraphValidationException.
    /// </summary>
    public class GraphValidationException : GraphApiException
    {
        /// <summary>
        /// Lista błędów walidacji.
        /// </summary>
        public List<ValidationError> ValidationErrors { get; set; } = new List<ValidationError>();

        /// <summary>
        /// Konstruktor domyślny.
        /// </summary>
        public GraphValidationException() : base("Błędy walidacji danych Graph API")
        {
            HttpStatusCode = 400;
            GraphErrorCode = "BadRequest";
        }

        /// <summary>
        /// Konstruktor z komunikatem.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        public GraphValidationException(string message) : base(message)
        {
            HttpStatusCode = 400;
            GraphErrorCode = "BadRequest";
        }

        /// <summary>
        /// Konstruktor z komunikatem i błędami walidacji.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="validationErrors">Lista błędów walidacji</param>
        public GraphValidationException(string message, IEnumerable<ValidationError> validationErrors) : base(message)
        {
            HttpStatusCode = 400;
            GraphErrorCode = "BadRequest";
            ValidationErrors = validationErrors?.ToList() ?? new List<ValidationError>();
        }

        /// <summary>
        /// Konstruktor z komunikatem i wewnętrznym wyjątkiem.
        /// </summary>
        /// <param name="message">Komunikat błędu</param>
        /// <param name="innerException">Wewnętrzny wyjątek</param>
        public GraphValidationException(string message, Exception innerException) : base(message, innerException)
        {
            HttpStatusCode = 400;
            GraphErrorCode = "BadRequest";
        }

        /// <summary>
        /// Sprawdza czy istnieje błąd walidacji dla określonego pola.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <returns>True jeśli istnieje błąd dla pola</returns>
        public bool HasErrorForField(string fieldName)
        {
            return ValidationErrors.Any(e => string.Equals(e.FieldName, fieldName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Pobiera błędy walidacji dla określonego pola.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <returns>Lista błędów dla pola</returns>
        public IEnumerable<ValidationError> GetErrorsForField(string fieldName)
        {
            return ValidationErrors.Where(e => string.Equals(e.FieldName, fieldName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Pobiera błędy walidacji według typu.
        /// </summary>
        /// <param name="type">Typ błędu walidacji</param>
        /// <returns>Lista błędów określonego typu</returns>
        public IEnumerable<ValidationError> GetErrorsByType(ValidationType type)
        {
            return ValidationErrors.Where(e => e.Type == type);
        }

        /// <summary>
        /// Pobiera szczegółowy komunikat błędu walidacji.
        /// </summary>
        /// <returns>Szczegółowy komunikat błędu</returns>
        public new string GetDetailedErrorMessage()
        {
            var baseMessage = base.GetDetailedErrorMessage();
            
            if (!ValidationErrors.Any())
                return baseMessage;

            var validationDetails = new List<string>
            {
                "",
                "=== SZCZEGÓŁY BŁĘDÓW WALIDACJI ===",
                $"Liczba błędów: {ValidationErrors.Count}"
            };

            var errorsByType = ValidationErrors.GroupBy(e => e.Type);
            foreach (var group in errorsByType)
            {
                validationDetails.Add($"{group.Key}: {group.Count()} błędów");
            }

            validationDetails.Add("");
            validationDetails.Add("BŁĘDY WALIDACJI:");

            foreach (var error in ValidationErrors)
            {
                validationDetails.Add($"• {error.FieldName}: {error.Message}");
                if (error.AttemptedValue != null)
                    validationDetails.Add($"  Wartość: {error.AttemptedValue}");
                if (error.Metadata.Any())
                {
                    validationDetails.Add("  Metadane:");
                    foreach (var kvp in error.Metadata)
                        validationDetails.Add($"    {kvp.Key}: {kvp.Value}");
                }
            }

            return baseMessage + Environment.NewLine + string.Join(Environment.NewLine, validationDetails);
        }

        /// <summary>
        /// Pobiera podsumowanie błędów walidacji.
        /// </summary>
        /// <returns>Podsumowanie błędów walidacji</returns>
        public string GetValidationSummary()
        {
            if (!ValidationErrors.Any())
                return "Brak błędów walidacji";

            var summary = new List<string>
            {
                $"Błędy walidacji ({ValidationErrors.Count}):"
            };

            var errorsByField = ValidationErrors.GroupBy(e => e.FieldName);
            foreach (var fieldGroup in errorsByField)
            {
                var fieldErrors = string.Join(", ", fieldGroup.Select(e => e.Type.ToString()));
                summary.Add($"  {fieldGroup.Key}: {fieldErrors}");
            }

            return string.Join(Environment.NewLine, summary);
        }

        /// <summary>
        /// Tworzy wyjątek dla wymaganego pola.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphValidationException</returns>
        public static GraphValidationException CreateRequiredFieldError(string fieldName, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            var validationError = new ValidationError(fieldName, ValidationType.Required, $"Pole '{fieldName}' jest wymagane");
            var message = $"Pole '{fieldName}' jest wymagane";

            return new GraphValidationException(message, new[] { validationError })
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu formatu.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <param name="attemptedValue">Wartość która nie przeszła walidacji</param>
        /// <param name="expectedFormat">Oczekiwany format</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphValidationException</returns>
        public static GraphValidationException CreateFormatError(string fieldName, object? attemptedValue, string expectedFormat, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            var validationError = new ValidationError(fieldName, ValidationType.Format, $"Pole '{fieldName}' ma nieprawidłowy format. Oczekiwany: {expectedFormat}", attemptedValue)
                .WithMetadata("ExpectedFormat", expectedFormat);
            var message = $"Pole '{fieldName}' ma nieprawidłowy format";

            return new GraphValidationException(message, new[] { validationError })
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu długości.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <param name="attemptedValue">Wartość która nie przeszła walidacji</param>
        /// <param name="minLength">Minimalna długość</param>
        /// <param name="maxLength">Maksymalna długość</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphValidationException</returns>
        public static GraphValidationException CreateLengthError(string fieldName, object? attemptedValue, int? minLength = null, int? maxLength = null, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            var lengthInfo = minLength.HasValue && maxLength.HasValue 
                ? $"między {minLength} a {maxLength}" 
                : minLength.HasValue 
                    ? $"minimum {minLength}" 
                    : $"maximum {maxLength}";

            var validationError = new ValidationError(fieldName, ValidationType.Length, $"Pole '{fieldName}' ma nieprawidłową długość. Wymagana długość: {lengthInfo}", attemptedValue);
            
            if (minLength.HasValue) validationError.WithMetadata("MinLength", minLength.Value);
            if (maxLength.HasValue) validationError.WithMetadata("MaxLength", maxLength.Value);

            var message = $"Pole '{fieldName}' ma nieprawidłową długość";

            return new GraphValidationException(message, new[] { validationError })
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu zakresu.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <param name="attemptedValue">Wartość która nie przeszła walidacji</param>
        /// <param name="minValue">Minimalna wartość</param>
        /// <param name="maxValue">Maksymalna wartość</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphValidationException</returns>
        public static GraphValidationException CreateRangeError(string fieldName, object? attemptedValue, object? minValue = null, object? maxValue = null, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            var rangeInfo = minValue != null && maxValue != null 
                ? $"między {minValue} a {maxValue}" 
                : minValue != null 
                    ? $"minimum {minValue}" 
                    : $"maximum {maxValue}";

            var validationError = new ValidationError(fieldName, ValidationType.Range, $"Pole '{fieldName}' ma wartość poza zakresem. Wymagana wartość: {rangeInfo}", attemptedValue);
            
            if (minValue != null) validationError.WithMetadata("MinValue", minValue);
            if (maxValue != null) validationError.WithMetadata("MaxValue", maxValue);

            var message = $"Pole '{fieldName}' ma wartość poza zakresem";

            return new GraphValidationException(message, new[] { validationError })
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu unikalności.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <param name="attemptedValue">Wartość która nie przeszła walidacji</param>
        /// <param name="conflictingResource">Konfliktowy zasób</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphValidationException</returns>
        public static GraphValidationException CreateUniqueError(string fieldName, object? attemptedValue, string? conflictingResource = null, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            var conflictInfo = !string.IsNullOrEmpty(conflictingResource) ? $" Konflikt z: {conflictingResource}" : "";
            var validationError = new ValidationError(fieldName, ValidationType.Unique, $"Pole '{fieldName}' musi być unikalne.{conflictInfo}", attemptedValue);
            
            if (!string.IsNullOrEmpty(conflictingResource)) 
                validationError.WithMetadata("ConflictingResource", conflictingResource);

            var message = $"Pole '{fieldName}' musi być unikalne";

            return new GraphValidationException(message, new[] { validationError })
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla błędu referencji.
        /// </summary>
        /// <param name="fieldName">Nazwa pola</param>
        /// <param name="attemptedValue">Wartość która nie przeszła walidacji</param>
        /// <param name="referencedResource">Referencyjny zasób</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphValidationException</returns>
        public static GraphValidationException CreateReferenceError(string fieldName, object? attemptedValue, string referencedResource, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            var validationError = new ValidationError(fieldName, ValidationType.Reference, $"Pole '{fieldName}' odnosi się do nieistniejącego zasobu: {referencedResource}", attemptedValue)
                .WithMetadata("ReferencedResource", referencedResource);

            var message = $"Pole '{fieldName}' odnosi się do nieistniejącego zasobu";

            return new GraphValidationException(message, new[] { validationError })
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Tworzy wyjątek dla wielu błędów walidacji.
        /// </summary>
        /// <param name="validationErrors">Lista błędów walidacji</param>
        /// <param name="endpoint">Endpoint</param>
        /// <param name="httpMethod">Metoda HTTP</param>
        /// <param name="requestId">ID żądania</param>
        /// <returns>Wyjątek GraphValidationException</returns>
        public static GraphValidationException CreateMultipleErrors(IEnumerable<ValidationError> validationErrors, string? endpoint = null, string? httpMethod = null, string? requestId = null)
        {
            var errors = validationErrors?.ToList() ?? new List<ValidationError>();
            var message = $"Znaleziono {errors.Count} błędów walidacji";

            return new GraphValidationException(message, errors)
            {
                Endpoint = endpoint,
                HttpMethod = httpMethod,
                RequestId = requestId
            };
        }
    }
} 