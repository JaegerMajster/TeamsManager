using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs orkiestratora monitorowania zdrowia systemu
    /// Koordynuje kompleksowe operacje diagnostyczne, naprawy automatyczne i optymalizację
    /// Następuje wzorce z ISchoolYearProcessOrchestrator, ITeamLifecycleOrchestrator i IBulkUserManagementOrchestrator
    /// </summary>
    public interface IHealthMonitoringOrchestrator
    {
        /// <summary>
        /// Przeprowadza kompleksowe sprawdzenie zdrowia wszystkich komponentów systemu
        /// </summary>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji diagnostycznej z detalami</returns>
        Task<HealthOperationResult> RunComprehensiveHealthCheckAsync(string apiAccessToken);

        /// <summary>
        /// Automatyczne naprawianie typowych problemów systemowych
        /// </summary>
        /// <param name="options">Opcje procesu naprawy</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji naprawy z detalami</returns>
        Task<HealthOperationResult> AutoRepairCommonIssuesAsync(
            RepairOptions options,
            string apiAccessToken);

        /// <summary>
        /// Synchronizacja ze stanem Microsoft Graph
        /// </summary>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji synchronizacji z detalami</returns>
        Task<HealthOperationResult> SynchronizeWithMicrosoftGraphAsync(string apiAccessToken);

        /// <summary>
        /// Optymalizacja wydajności cache systemu
        /// </summary>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji optymalizacji z detalami</returns>
        Task<HealthOperationResult> OptimizeCachePerformanceAsync(string apiAccessToken);

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów monitorowania
        /// </summary>
        /// <returns>Status aktywnych procesów monitorowania</returns>
        Task<IEnumerable<HealthMonitoringProcessStatus>> GetActiveProcessesStatusAsync();

        /// <summary>
        /// Anuluje aktywny proces monitorowania (jeśli to możliwe)
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>True jeśli anulowanie powiodło się</returns>
        Task<bool> CancelProcessAsync(string processId);
    }

    /// <summary>
    /// Opcje konfiguracji procesu naprawy automatycznej
    /// </summary>
    public class RepairOptions
    {
        /// <summary>
        /// Czy naprawiać problemy z połączeniem PowerShell
        /// </summary>
        public bool RepairPowerShellConnection { get; set; } = true;

        /// <summary>
        /// Czy czyścić nieważne wpisy cache
        /// </summary>
        public bool ClearInvalidCache { get; set; } = true;

        /// <summary>
        /// Czy próbować restartować zawieszenie procesy
        /// </summary>
        public bool RestartStuckProcesses { get; set; } = true;

        /// <summary>
        /// Czy optymalizować bazę danych
        /// </summary>
        public bool OptimizeDatabase { get; set; } = false;

        /// <summary>
        /// Czy wysyłać powiadomienia administratorom
        /// </summary>
        public bool SendAdminNotifications { get; set; } = true;

        /// <summary>
        /// Czy symulować operacje (dry run) bez rzeczywistych zmian
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Maksymalny czas oczekiwania na operację (w minutach, domyślnie 30)
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Maksymalna liczba równoległych operacji naprawy
        /// </summary>
        public int MaxConcurrency { get; set; } = 2;
    }
} 