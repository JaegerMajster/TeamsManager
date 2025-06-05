using System;

namespace TeamsManager.Core.Exceptions.PowerShell
{
    /// <summary>
    /// Wyjątek rzucany gdy walidacja parametrów PowerShell się nie powiedzie
    /// </summary>
    [Serializable]
    public class PowerShellValidationException : PowerShellException
    {
        public string ParameterName { get; }
        public object? InvalidValue { get; }
        public string? ValidationRule { get; }

        public PowerShellValidationException(
            string parameterName,
            object? invalidValue,
            string message,
            string? validationRule = null)
            : base($"Validation failed for parameter '{parameterName}': {message}")
        {
            ParameterName = parameterName;
            InvalidValue = invalidValue;
            ValidationRule = validationRule;
        }

        public static PowerShellValidationException ForInvalidEmail(string parameterName, string invalidEmail)
            => new(parameterName, invalidEmail, $"'{invalidEmail}' is not a valid email address", "EmailFormat");

        public static PowerShellValidationException ForInvalidGuid(string parameterName, string invalidGuid)
            => new(parameterName, invalidGuid, $"'{invalidGuid}' is not a valid GUID", "GuidFormat");

        public static PowerShellValidationException ForExceedsMaxLength(string parameterName, string value, int maxLength)
            => new(parameterName, value, $"Value exceeds maximum length of {maxLength} characters (actual: {value.Length})", "MaxLength");

        public static PowerShellValidationException ForInvalidCharacters(string parameterName, string value, string invalidChars)
            => new(parameterName, value, $"Value contains invalid characters: {invalidChars}", "InvalidCharacters");
    }
} 