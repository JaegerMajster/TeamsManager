using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Status instalacji modułów PowerShell
    /// </summary>
    public class PowerShellModuleStatus
    {
        /// <summary>
        /// Lista modułów z ich statusem
        /// </summary>
        public List<ModuleInfo> Modules { get; set; } = new List<ModuleInfo>();

        /// <summary>
        /// Ogólny status modułów (Healthy, Warning, Degraded, Critical)
        /// </summary>
        public string OverallStatus { get; set; } = "Unknown";

        /// <summary>
        /// Liczba zainstalowanych modułów
        /// </summary>
        public int InstalledModulesCount { get; set; }

        /// <summary>
        /// Liczba zaimportowanych modułów
        /// </summary>
        public int ImportedModulesCount { get; set; }

        /// <summary>
        /// Liczba wymaganych modułów
        /// </summary>
        public int RequiredModulesCount { get; set; }

        /// <summary>
        /// Wersja PowerShell
        /// </summary>
        public string PowerShellVersion { get; set; } = string.Empty;

        /// <summary>
        /// Edycja PowerShell (Desktop/Core)
        /// </summary>
        public string PowerShellEdition { get; set; } = string.Empty;

        /// <summary>
        /// Polityka wykonywania
        /// </summary>
        public string ExecutionPolicy { get; set; } = string.Empty;

        /// <summary>
        /// Wersja systemu operacyjnego
        /// </summary>
        public string OSVersion { get; set; } = string.Empty;

        /// <summary>
        /// Komunikat błędu jeśli wystąpił
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Czas sprawdzenia
        /// </summary>
        public DateTime CheckTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Informacje o pojedynczym module PowerShell
    /// </summary>
    public class ModuleInfo
    {
        /// <summary>
        /// Nazwa modułu
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Czy moduł jest zainstalowany
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// Czy moduł jest zaimportowany
        /// </summary>
        public bool IsImported { get; set; }

        /// <summary>
        /// Wersja zainstalowanego modułu
        /// </summary>
        public string? InstalledVersion { get; set; }

        /// <summary>
        /// Wersja zaimportowanego modułu
        /// </summary>
        public string? ImportedVersion { get; set; }

        /// <summary>
        /// Ścieżka do modułu
        /// </summary>
        public string? ModulePath { get; set; }

        /// <summary>
        /// Autor modułu
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Opis modułu
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Komunikat błędu jeśli wystąpił
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Status modułu jako string
        /// </summary>
        public string Status => 
            !IsInstalled ? "Not Installed" :
            !IsImported ? "Installed" :
            "Active";

        /// <summary>
        /// Czy moduł jest w pełni funkcjonalny
        /// </summary>
        public bool IsHealthy => IsInstalled && IsImported && string.IsNullOrEmpty(ErrorMessage);
    }
} 