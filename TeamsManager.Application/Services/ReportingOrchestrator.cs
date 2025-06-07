using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System.Text;

namespace TeamsManager.Application.Services
{
    /// <summary>
    /// Orkiestrator raportowania odpowiedzialny za generowanie raportów biznesowych,
    /// compliance i eksport danych systemu
    /// Następuje wzorce z SchoolYearProcessOrchestrator i innych orkiestratorów
    /// </summary>
    public class ReportingOrchestrator : IReportingOrchestrator
    {
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly ISchoolYearService _schoolYearService;
        private readonly ITeamService _teamService;
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ReportingOrchestrator> _logger;
        private readonly SemaphoreSlim _processSemaphore;

        // Thread-safe słowniki dla zarządzania aktywnymi procesami (wzorzec z innych orkiestratorów)
        private readonly ConcurrentDictionary<string, ReportingProcessStatus> _activeProcesses;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;

        public ReportingOrchestrator(
            IOperationHistoryService operationHistoryService,
            ISchoolYearService schoolYearService,
            ITeamService teamService,
            IUserService userService,
            IDepartmentService departmentService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<ReportingOrchestrator> logger)
        {
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _schoolYearService = schoolYearService ?? throw new ArgumentNullException(nameof(schoolYearService));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _processSemaphore = new SemaphoreSlim(2, 2); // Limit równoległych procesów raportowania
            _activeProcesses = new ConcurrentDictionary<string, ReportingProcessStatus>();
            _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        }

        public async Task<ReportOperationResult> GenerateSchoolYearReportAsync(string schoolYearId, ReportOptions options)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();

            _logger.LogInformation("[ReportingOrchestrator] Rozpoczynam generowanie raportu roku szkolnego {SchoolYearId}", schoolYearId);

            try
            {
                // Walidacja parametrów
                if (string.IsNullOrWhiteSpace(schoolYearId))
                {
                    _logger.LogWarning("[ReportingOrchestrator] Pusty ID roku szkolnego");
                    return ReportOperationResult.CreateError("ID roku szkolnego jest wymagane");
                }

                // Rejestracja procesu
                _cancellationTokens[processId] = cts;
                var processStatus = new ReportingProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "SchoolYearReport",
                    ReportType = "Raport roku szkolnego",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    CurrentOperation = "Pobieranie danych roku szkolnego",
                    StartedBy = _currentUserService.GetCurrentUserUpn() ?? "system"
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    // Sprawdź rok szkolny
                    processStatus.CurrentOperation = "Walidacja roku szkolnego";
                    processStatus.ProgressPercentage = 10;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var schoolYear = await _schoolYearService.GetByIdAsync(schoolYearId);
                    if (schoolYear == null)
                    {
                        throw new InvalidOperationException($"Rok szkolny o ID '{schoolYearId}' nie istnieje");
                    }

                    // Rozpocznij operację w historii
                    var operationHistory = await _operationHistoryService.CreateNewOperationEntryAsync(
                        OperationType.GenericOperation,
                        "Report",
                        processId,
                        $"Raport roku szkolnego {schoolYear.Name}",
                        $"GenerateSchoolYearReport_{schoolYear.Name}_{options.Format}",
                        null);

                    // Pobierz dane zespołów
                    processStatus.CurrentOperation = "Pobieranie danych zespołów";
                    processStatus.ProgressPercentage = 30;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var teams = await _teamService.GetTeamsBySchoolYearAsync(schoolYearId);
                    var filteredTeams = FilterTeamsByOptions(teams, options);

                    // Pobierz dane użytkowników
                    processStatus.CurrentOperation = "Pobieranie danych użytkowników";
                    processStatus.ProgressPercentage = 50;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var users = await _userService.GetAllActiveUsersAsync();
                    var activeUsers = users.ToList();

                    // Pobierz historię operacji dla roku
                    processStatus.CurrentOperation = "Pobieranie historii operacji";
                    processStatus.ProgressPercentage = 70;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var operationHistory_data = await _operationHistoryService.GetHistoryByFilterAsync(
                        schoolYear.StartDate,
                        schoolYear.EndDate,
                        null, // wszystkie typy operacji
                        null, // wszystkie statusy
                        null, // wszyscy użytkownicy
                        1,
                        1000);

                    // Generuj raport
                    processStatus.CurrentOperation = "Generowanie raportu";
                    processStatus.ProgressPercentage = 90;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var reportData = new SchoolYearReportData
                    {
                        SchoolYear = schoolYear,
                        Teams = filteredTeams.ToList(),
                        Users = activeUsers,
                        OperationHistory = operationHistory_data.ToList(),
                        GeneratedAt = DateTime.UtcNow,
                        GeneratedBy = _currentUserService.GetCurrentUserUpn() ?? "system"
                    };

                    var reportStream = await GenerateSchoolYearReportStreamAsync(reportData, options);
                    var fileName = $"Raport_RokSzkolny_{schoolYear.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{GetFileExtension(options.Format)}";

                    processStatus.CurrentOperation = "Finalizacja raportu";
                    processStatus.ProgressPercentage = 100;
                    processStatus.Status = "Completed";
                    processStatus.CompletedAt = DateTime.UtcNow;
                    processStatus.OutputFileName = fileName;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    // Zaktualizuj historię operacji
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Completed,
                        $"Raport wygenerowany: {fileName}");

                    // Wyślij powiadomienie
                    if (options.SendNotifications)
                    {
                        await SendNotificationAsync($"Raport roku szkolnego {schoolYear.Name} został wygenerowany. Plik: {fileName}");
                    }

                    _logger.LogInformation("[ReportingOrchestrator] Raport roku szkolnego {SchoolYearId} wygenerowany pomyślnie", schoolYearId);

                    return ReportOperationResult.CreateSuccess(processId, fileName, reportStream);
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingOrchestrator] Błąd podczas generowania raportu roku szkolnego {SchoolYearId}", schoolYearId);

                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Failed";
                    status.CompletedAt = DateTime.UtcNow;
                    status.ErrorMessage = ex.Message;
                }

                return ReportOperationResult.CreateError(ex.Message);
            }
            finally
            {
                // Cleanup
                _cancellationTokens.TryRemove(processId, out var removedCts1);
                removedCts1?.Dispose();
                
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                {
                    _activeProcesses.TryRemove(processId, out var removedStatus1);
                });
            }
        }

        public async Task<ReportOperationResult> GenerateUserActivityReportAsync(DateTime fromDate, DateTime toDate)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();

            _logger.LogInformation("[ReportingOrchestrator] Rozpoczynam generowanie raportu aktywności użytkowników {FromDate} - {ToDate}", fromDate, toDate);

            try
            {
                // Walidacja dat
                if (fromDate >= toDate)
                {
                    return ReportOperationResult.CreateError("Data rozpoczęcia musi być wcześniejsza niż data zakończenia");
                }

                // Rejestracja procesu
                _cancellationTokens[processId] = cts;
                var processStatus = new ReportingProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "UserActivityReport",
                    ReportType = "Raport aktywności użytkowników",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    CurrentOperation = "Pobieranie danych aktywności",
                    StartedBy = _currentUserService.GetCurrentUserUpn() ?? "system"
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    // Rozpocznij operację w historii
                    var operationHistory = await _operationHistoryService.CreateNewOperationEntryAsync(
                        OperationType.GenericOperation,
                        "Report",
                        processId,
                        $"Raport aktywności {fromDate:yyyy-MM-dd} - {toDate:yyyy-MM-dd}",
                        $"GenerateUserActivityReport_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}",
                        null);

                    // Pobierz historię operacji w okresie
                    processStatus.CurrentOperation = "Pobieranie historii operacji";
                    processStatus.ProgressPercentage = 30;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var operationHistory_data = await _operationHistoryService.GetHistoryByFilterAsync(
                        fromDate,
                        toDate,
                        null,
                        null,
                        null,
                        1,
                        10000);

                    // Pobierz dane użytkowników
                    processStatus.CurrentOperation = "Pobieranie danych użytkowników";
                    processStatus.ProgressPercentage = 60;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var users = await _userService.GetAllActiveUsersAsync();

                    // Generuj raport
                    processStatus.CurrentOperation = "Generowanie raportu aktywności";
                    processStatus.ProgressPercentage = 90;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    var reportData = new UserActivityReportData
                    {
                        FromDate = fromDate,
                        ToDate = toDate,
                        OperationHistory = operationHistory_data.ToList(),
                        Users = users.ToList(),
                        GeneratedAt = DateTime.UtcNow,
                        GeneratedBy = _currentUserService.GetCurrentUserUpn() ?? "system"
                    };

                    var reportStream = await GenerateUserActivityReportStreamAsync(reportData);
                    var fileName = $"Raport_Aktywnosc_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

                    processStatus.CurrentOperation = "Finalizacja raportu";
                    processStatus.ProgressPercentage = 100;
                    processStatus.Status = "Completed";
                    processStatus.CompletedAt = DateTime.UtcNow;
                    processStatus.OutputFileName = fileName;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    // Zaktualizuj historię operacji
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Completed,
                        $"Raport aktywności wygenerowany: {fileName}");

                    _logger.LogInformation("[ReportingOrchestrator] Raport aktywności użytkowników wygenerowany pomyślnie");

                    return ReportOperationResult.CreateSuccess(processId, fileName, reportStream);
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingOrchestrator] Błąd podczas generowania raportu aktywności użytkowników");

                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Failed";
                    status.CompletedAt = DateTime.UtcNow;
                    status.ErrorMessage = ex.Message;
                }

                return ReportOperationResult.CreateError(ex.Message);
            }
            finally
            {
                // Cleanup
                _cancellationTokens.TryRemove(processId, out var removedCts1);
                removedCts1?.Dispose();
                
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                {
                    _activeProcesses.TryRemove(processId, out var removedStatus1);
                });
            }
        }

        public async Task<ReportOperationResult> GenerateComplianceReportAsync(ComplianceReportType type)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();

            _logger.LogInformation("[ReportingOrchestrator] Rozpoczynam generowanie raportu compliance {ComplianceType}", type);

            try
            {
                // Rejestracja procesu
                _cancellationTokens[processId] = cts;
                var processStatus = new ReportingProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "ComplianceReport",
                    ReportType = $"Raport compliance - {type}",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    CurrentOperation = "Inicjalizacja raportu compliance",
                    StartedBy = _currentUserService.GetCurrentUserUpn() ?? "system"
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    // Rozpocznij operację w historii
                    var operationHistory = await _operationHistoryService.CreateNewOperationEntryAsync(
                        OperationType.GenericOperation,
                        "Report",
                        processId,
                        $"Raport compliance {type}",
                        $"GenerateComplianceReport_{type}",
                        null);

                    var reportStream = type switch
                    {
                        ComplianceReportType.DataProtection => await GenerateDataProtectionReportAsync(processStatus, processId),
                        ComplianceReportType.UserAccess => await GenerateUserAccessReportAsync(processStatus, processId),
                        ComplianceReportType.SystemAudit => await GenerateSystemAuditReportAsync(processStatus, processId),
                        ComplianceReportType.ActivityLogs => await GenerateActivityLogsReportAsync(processStatus, processId),
                        ComplianceReportType.SecurityOverview => await GenerateSecurityOverviewReportAsync(processStatus, processId),
                        _ => throw new NotSupportedException($"Typ raportu compliance '{type}' nie jest obsługiwany")
                    };

                    var fileName = $"Raport_Compliance_{type}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                    processStatus.CurrentOperation = "Finalizacja raportu compliance";
                    processStatus.ProgressPercentage = 100;
                    processStatus.Status = "Completed";
                    processStatus.CompletedAt = DateTime.UtcNow;
                    processStatus.OutputFileName = fileName;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    // Zaktualizuj historię operacji
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Completed,
                        $"Raport compliance {type} wygenerowany: {fileName}");

                    await SendNotificationAsync($"Raport compliance {type} został wygenerowany. Plik: {fileName}");

                    _logger.LogInformation("[ReportingOrchestrator] Raport compliance {ComplianceType} wygenerowany pomyślnie", type);

                    return ReportOperationResult.CreateSuccess(processId, fileName, reportStream);
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingOrchestrator] Błąd podczas generowania raportu compliance {ComplianceType}", type);

                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Failed";
                    status.CompletedAt = DateTime.UtcNow;
                    status.ErrorMessage = ex.Message;
                }

                return ReportOperationResult.CreateError(ex.Message);
            }
            finally
            {
                // Cleanup
                _cancellationTokens.TryRemove(processId, out var removedCts2);
                removedCts2?.Dispose();
                
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                {
                    _activeProcesses.TryRemove(processId, out var removedStatus2);
                });
            }
        }

        public async Task<ReportOperationResult> ExportSystemDataAsync(ExportOptions options)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();

            _logger.LogInformation("[ReportingOrchestrator] Rozpoczynam eksport danych systemu {DataType} w formacie {Format}", options.DataType, options.Format);

            try
            {
                // Rejestracja procesu
                _cancellationTokens[processId] = cts;
                var processStatus = new ReportingProcessStatus
                {
                    ProcessId = processId,
                    ProcessType = "SystemDataExport",
                    ReportType = $"Eksport danych - {options.DataType}",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow,
                    CurrentOperation = "Inicjalizacja eksportu danych",
                    StartedBy = _currentUserService.GetCurrentUserUpn() ?? "system"
                };
                _activeProcesses[processId] = processStatus;

                await _processSemaphore.WaitAsync(cts.Token);

                try
                {
                    // Rozpocznij operację w historii
                    var operationHistory = await _operationHistoryService.CreateNewOperationEntryAsync(
                        OperationType.GenericOperation,
                        "Export",
                        processId,
                        $"Eksport danych {options.DataType}",
                        $"ExportSystemData_{options.DataType}_{options.Format}",
                        null);

                    var exportStream = options.DataType switch
                    {
                        ExportDataType.All => await ExportAllDataAsync(processStatus, processId, options),
                        ExportDataType.Users => await ExportUsersDataAsync(processStatus, processId, options),
                        ExportDataType.Teams => await ExportTeamsDataAsync(processStatus, processId, options),
                        ExportDataType.OperationHistory => await ExportOperationHistoryAsync(processStatus, processId, options),
                        ExportDataType.Configuration => await ExportConfigurationDataAsync(processStatus, processId, options),
                        ExportDataType.Reports => await ExportReportsDataAsync(processStatus, processId, options),
                        _ => throw new NotSupportedException($"Typ eksportu '{options.DataType}' nie jest obsługiwany")
                    };

                    var fileName = $"Eksport_{options.DataType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{GetExportFileExtension(options.Format)}";

                    processStatus.CurrentOperation = "Finalizacja eksportu";
                    processStatus.ProgressPercentage = 100;
                    processStatus.Status = "Completed";
                    processStatus.CompletedAt = DateTime.UtcNow;
                    processStatus.OutputFileName = fileName;
                    await UpdateProcessStatusAsync(processId, processStatus);

                    // Zaktualizuj historię operacji
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Completed,
                        $"Eksport danych {options.DataType} zakończony: {fileName}");

                    await SendNotificationAsync($"Eksport danych {options.DataType} został zakończony. Plik: {fileName}");

                    _logger.LogInformation("[ReportingOrchestrator] Eksport danych {DataType} zakończony pomyślnie", options.DataType);

                    return ReportOperationResult.CreateSuccess(processId, fileName, exportStream);
                }
                finally
                {
                    _processSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingOrchestrator] Błąd podczas eksportu danych {DataType}", options.DataType);

                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Failed";
                    status.CompletedAt = DateTime.UtcNow;
                    status.ErrorMessage = ex.Message;
                }

                return ReportOperationResult.CreateError(ex.Message);
            }
            finally
            {
                // Cleanup
                _cancellationTokens.TryRemove(processId, out var removedCts3);
                removedCts3?.Dispose();
                
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                {
                    _activeProcesses.TryRemove(processId, out var removedStatus3);
                });
            }
        }

        public async Task<IEnumerable<ReportingProcessStatus>> GetActiveProcessesStatusAsync()
        {
            return await Task.FromResult(_activeProcesses.Values.AsEnumerable());
        }

        public async Task<bool> CancelProcessAsync(string processId)
        {
            if (_cancellationTokens.TryGetValue(processId, out var cts))
            {
                cts.Cancel();
                
                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Cancelled";
                    status.CompletedAt = DateTime.UtcNow;
                }

                _logger.LogInformation("[ReportingOrchestrator] Anulowano proces {ProcessId}", processId);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        // ===== METODY POMOCNICZE =====

        private async Task UpdateProcessStatusAsync(string processId, ReportingProcessStatus status)
        {
            _activeProcesses[processId] = status;
            
            // Powiadomienie o postępie
            await SendNotificationAsync($"Proces raportowania {processId}: {status.CurrentOperation} - {status.ProgressPercentage:F1}%");
        }

        private async Task SendNotificationAsync(string message)
        {
            try
            {
                var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
                await _notificationService.SendNotificationToUserAsync(currentUserUpn, message, "info");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ReportingOrchestrator] Błąd wysyłania powiadomienia: {Message}", message);
            }
        }

        private IEnumerable<Team> FilterTeamsByOptions(IEnumerable<Team> teams, ReportOptions options)
        {
            var filteredTeams = teams;

            if (options.SchoolTypeIds?.Any() == true)
            {
                filteredTeams = filteredTeams.Where(t => options.SchoolTypeIds.Contains(t.SchoolTypeId));
            }

            // Uwaga: Team nie ma bezpośredniej właściwości DepartmentId
            // Filtrowanie po działach wymagałoby dodatkowej logiki przez właściciela zespołu
            if (options.DepartmentIds?.Any() == true)
            {
                // Tymczasowo pomijamy filtrowanie po działach - wymaga to dodatkowej implementacji
                // filteredTeams = filteredTeams.Where(t => options.DepartmentIds.Contains(t.DepartmentId));
            }

            return filteredTeams;
        }

        private string GetFileExtension(ReportFormat format)
        {
            return format switch
            {
                ReportFormat.PDF => "pdf",
                ReportFormat.Excel => "xlsx",
                ReportFormat.CSV => "csv",
                ReportFormat.JSON => "json",
                ReportFormat.HTML => "html",
                _ => "pdf"
            };
        }

        private string GetExportFileExtension(ExportFileFormat format)
        {
            return format switch
            {
                ExportFileFormat.Excel => "xlsx",
                ExportFileFormat.CSV => "csv",
                ExportFileFormat.JSON => "json",
                ExportFileFormat.XML => "xml",
                ExportFileFormat.ZIP => "zip",
                _ => "xlsx"
            };
        }

        // ===== GENEROWANIE RAPORTÓW =====

        private async Task<Stream> GenerateSchoolYearReportStreamAsync(SchoolYearReportData data, ReportOptions options)
        {
            // Symulacja generowania raportu roku szkolnego
            var content = $"RAPORT ROKU SZKOLNEGO\n" +
                         $"===================\n\n" +
                         $"Rok szkolny: {data.SchoolYear.Name}\n" +
                         $"Okres: {data.SchoolYear.StartDate:yyyy-MM-dd} - {data.SchoolYear.EndDate:yyyy-MM-dd}\n" +
                         $"Liczba zespołów: {data.Teams.Count}\n" +
                         $"Liczba użytkowników: {data.Users.Count}\n" +
                         $"Liczba operacji: {data.OperationHistory.Count}\n" +
                         $"Wygenerowano: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss}\n" +
                         $"Przez: {data.GeneratedBy}\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return await Task.FromResult(new MemoryStream(bytes));
        }

        private async Task<Stream> GenerateUserActivityReportStreamAsync(UserActivityReportData data)
        {
            // Symulacja generowania raportu aktywności użytkowników
            var content = $"RAPORT AKTYWNOŚCI UŻYTKOWNIKÓW\n" +
                         $"============================\n\n" +
                         $"Okres: {data.FromDate:yyyy-MM-dd} - {data.ToDate:yyyy-MM-dd}\n" +
                         $"Liczba użytkowników: {data.Users.Count}\n" +
                         $"Liczba operacji: {data.OperationHistory.Count}\n" +
                         $"Wygenerowano: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss}\n" +
                         $"Przez: {data.GeneratedBy}\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return await Task.FromResult(new MemoryStream(bytes));
        }

        // ===== RAPORTY COMPLIANCE =====

        private async Task<Stream> GenerateDataProtectionReportAsync(ReportingProcessStatus status, string processId)
        {
            status.CurrentOperation = "Generowanie raportu ochrony danych";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "RAPORT OCHRONY DANYCH (GDPR/RODO)\n" +
                         "================================\n\n" +
                         "Analiza zgodności z przepisami o ochronie danych osobowych...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> GenerateUserAccessReportAsync(ReportingProcessStatus status, string processId)
        {
            status.CurrentOperation = "Generowanie raportu kontroli dostępu";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "RAPORT KONTROLI DOSTĘPU\n" +
                         "=====================\n\n" +
                         "Analiza uprawnień użytkowników i kontroli dostępu...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> GenerateSystemAuditReportAsync(ReportingProcessStatus status, string processId)
        {
            status.CurrentOperation = "Generowanie raportu audytu systemu";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "RAPORT AUDYTU SYSTEMU\n" +
                         "==================\n\n" +
                         "Kompleksowy audyt systemu TeamsManager...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> GenerateActivityLogsReportAsync(ReportingProcessStatus status, string processId)
        {
            status.CurrentOperation = "Generowanie raportu logów aktywności";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "RAPORT LOGÓW AKTYWNOŚCI\n" +
                         "=====================\n\n" +
                         "Szczegółowe logi aktywności użytkowników...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> GenerateSecurityOverviewReportAsync(ReportingProcessStatus status, string processId)
        {
            status.CurrentOperation = "Generowanie przeglądu bezpieczeństwa";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "PRZEGLĄD BEZPIECZEŃSTWA\n" +
                         "=====================\n\n" +
                         "Analiza bezpieczeństwa systemu i infrastruktury...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        // ===== EKSPORT DANYCH =====

        private async Task<Stream> ExportAllDataAsync(ReportingProcessStatus status, string processId, ExportOptions options)
        {
            status.CurrentOperation = "Eksportowanie wszystkich danych";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "EKSPORT WSZYSTKICH DANYCH SYSTEMU\n" +
                         "==============================\n\n" +
                         "Kompletny eksport danych TeamsManager...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> ExportUsersDataAsync(ReportingProcessStatus status, string processId, ExportOptions options)
        {
            status.CurrentOperation = "Eksportowanie danych użytkowników";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var users = await _userService.GetAllActiveUsersAsync();
            var content = "EKSPORT DANYCH UŻYTKOWNIKÓW\n" +
                         "========================\n\n" +
                         $"Liczba użytkowników: {users.Count()}\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> ExportTeamsDataAsync(ReportingProcessStatus status, string processId, ExportOptions options)
        {
            status.CurrentOperation = "Eksportowanie danych zespołów";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "EKSPORT DANYCH ZESPOŁÓW\n" +
                         "====================\n\n" +
                         "Eksport wszystkich zespołów...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> ExportOperationHistoryAsync(ReportingProcessStatus status, string processId, ExportOptions options)
        {
            status.CurrentOperation = "Eksportowanie historii operacji";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "EKSPORT HISTORII OPERACJI\n" +
                         "=======================\n\n" +
                         "Eksport logów operacji systemu...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> ExportConfigurationDataAsync(ReportingProcessStatus status, string processId, ExportOptions options)
        {
            status.CurrentOperation = "Eksportowanie konfiguracji";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "EKSPORT KONFIGURACJI SYSTEMU\n" +
                         "==========================\n\n" +
                         "Eksport ustawień i konfiguracji...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }

        private async Task<Stream> ExportReportsDataAsync(ReportingProcessStatus status, string processId, ExportOptions options)
        {
            status.CurrentOperation = "Eksportowanie wygenerowanych raportów";
            status.ProgressPercentage = 80;
            await UpdateProcessStatusAsync(processId, status);

            var content = "EKSPORT WYGENEROWANYCH RAPORTÓW\n" +
                         "=============================\n\n" +
                         "Eksport wszystkich raportów...\n";

            var bytes = Encoding.UTF8.GetBytes(content);
            return new MemoryStream(bytes);
        }
    }

    // ===== MODELE DANYCH RAPORTÓW =====

    internal class SchoolYearReportData
    {
        public SchoolYear SchoolYear { get; set; } = new SchoolYear();
        public List<Team> Teams { get; set; } = new List<Team>();
        public List<User> Users { get; set; } = new List<User>();
        public List<OperationHistory> OperationHistory { get; set; } = new List<OperationHistory>();
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
    }

    internal class UserActivityReportData
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<OperationHistory> OperationHistory { get; set; } = new List<OperationHistory>();
        public List<User> Users { get; set; } = new List<User>();
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
    }
} 