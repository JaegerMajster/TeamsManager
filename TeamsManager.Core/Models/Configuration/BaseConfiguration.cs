using System;

namespace TeamsManager.Core.Models.Configuration
{
    /// <summary>
    /// Bazowa klasa dla wszystkich typów konfiguracji w systemie
    /// Zapewnia wspólne właściwości i funkcjonalność zgodną z Clean Architecture
    /// </summary>
    public abstract class BaseConfiguration
    {
        /// <summary>
        /// Wersja konfiguracji
        /// </summary>
        public string Version { get; set; } = "2.0";

        /// <summary>
        /// Data ostatniej modyfikacji
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Typ konfiguracji (automatycznie ustawiany na podstawie nazwy klasy)
        /// </summary>
        public string ConfigType { get; set; } = string.Empty;

        /// <summary>
        /// Konstruktor bazowy
        /// </summary>
        protected BaseConfiguration()
        {
            ConfigType = GetType().Name;
        }

        /// <summary>
        /// Sprawdza czy konfiguracja jest prawidłowa
        /// </summary>
        /// <returns>True jeśli konfiguracja jest prawidłowa</returns>
        public virtual bool IsValid()
        {
            return !string.IsNullOrEmpty(Version) && !string.IsNullOrEmpty(ConfigType);
        }

        /// <summary>
        /// Aktualizuje timestamp ostatniej modyfikacji
        /// </summary>
        public void Touch()
        {
            LastModified = DateTime.UtcNow;
        }
    }
} 