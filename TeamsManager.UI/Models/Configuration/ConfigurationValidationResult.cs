using System.Collections.Generic;

namespace TeamsManager.UI.Models.Configuration
{
    /// <summary>
    /// Wynik walidacji konfiguracji
    /// </summary>
    public class ConfigurationValidationResult
    {
        /// <summary>
        /// Czy konfiguracja jest prawidłowa
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Lista błędów walidacji
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Status konfiguracji
        /// </summary>
        public ConfigurationStatus Status { get; set; }

        /// <summary>
        /// Dodatkowe informacje o błędzie
        /// </summary>
        public string DetailedMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Możliwe statusy konfiguracji
    /// </summary>
    public enum ConfigurationStatus
    {
        /// <summary>
        /// Konfiguracja jest prawidłowa
        /// </summary>
        Valid,

        /// <summary>
        /// Brak pliku konfiguracyjnego
        /// </summary>
        Missing,

        /// <summary>
        /// Konfiguracja jest nieprawidłowa (błędne dane)
        /// </summary>
        Invalid,

        /// <summary>
        /// Błąd połączenia z API/Azure AD
        /// </summary>
        ConnectionError,

        /// <summary>
        /// Brak uprawnień
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Nieznany błąd
        /// </summary>
        Unknown
    }
}