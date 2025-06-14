using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Wynik instalacji modułów PowerShell
    /// </summary>
    public class PowerShellModuleInstallationResult
    {
        /// <summary>
        /// Czy instalacja zakończyła się sukcesem
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Komunikat o wyniku instalacji
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Komunikat błędu jeśli wystąpił
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Lista wyników instalacji dla poszczególnych modułów
        /// </summary>
        public List<ModuleInstallationResult> ModuleResults { get; set; } = new List<ModuleInstallationResult>();

        /// <summary>
        /// Liczba pomyślnie zainstalowanych modułów
        /// </summary>
        public int InstalledCount { get; set; }

        /// <summary>
        /// Liczba pominiętych modułów (już zainstalowane)
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Liczba modułów z błędami instalacji
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Czas rozpoczęcia instalacji
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Czas zakończenia instalacji
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Czas trwania instalacji
        /// </summary>
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
    }

    /// <summary>
    /// Wynik instalacji pojedynczego modułu
    /// </summary>
    public class ModuleInstallationResult
    {
        /// <summary>
        /// Nazwa modułu
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;

        /// <summary>
        /// Czy instalacja się powiodła
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Czy moduł był już zainstalowany
        /// </summary>
        public bool AlreadyInstalled { get; set; }

        /// <summary>
        /// Wersja zainstalowanego modułu
        /// </summary>
        public string? InstalledVersion { get; set; }

        /// <summary>
        /// Komunikat błędu jeśli wystąpił
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Akcja wykonana (Installed, Reinstalled, Skipped)
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Status jako string
        /// </summary>
        public string Status => 
            Success ? "Success" :
            !string.IsNullOrEmpty(ErrorMessage) ? "Error" :
            "Unknown";
    }
} 