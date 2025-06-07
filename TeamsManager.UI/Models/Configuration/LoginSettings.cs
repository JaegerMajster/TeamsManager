using System;

namespace TeamsManager.UI.Models.Configuration
{
    /// <summary>
    /// Ustawienia logowania użytkownika
    /// </summary>
    public class LoginSettings
    {
        /// <summary>
        /// Czy zapamiętać użytkownika
        /// </summary>
        public bool RememberMe { get; set; }

        /// <summary>
        /// Czy automatycznie logować przy starcie
        /// </summary>
        public bool AutoLogin { get; set; }

        /// <summary>
        /// Email ostatnio zalogowanego użytkownika
        /// </summary>
        public string? LastUserEmail { get; set; }

        /// <summary>
        /// Data ostatniego pomyślnego logowania
        /// </summary>
        public DateTime? LastLoginDate { get; set; }

        /// <summary>
        /// Zaszyfrowany refresh token (opcjonalnie)
        /// </summary>
        public string? EncryptedRefreshToken { get; set; }
    }
} 