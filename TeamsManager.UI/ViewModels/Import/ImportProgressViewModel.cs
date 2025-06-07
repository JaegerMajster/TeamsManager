using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using System.Timers;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.ViewModels.Import
{
    /// <summary>
    /// ViewModel dla kroku postępu importu w wizardzie
    /// </summary>
    public class ImportProgressViewModel : BaseViewModel, IDisposable
    {
        private readonly IDataImportOrchestrator _importOrchestrator;
        private readonly ILogger<ImportProgressViewModel> _logger;

        private ImportProcessStatus? _processStatus;
        private bool _isImportRunning = false;
        private bool _canCancel = false;
        private bool _isImportComplete = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private System.Timers.Timer? _pollingTimer;
        private string? _currentProcessId;

        public ImportProgressViewModel(
            IDataImportOrchestrator importOrchestrator,
            ILogger<ImportProgressViewModel> logger,
            IUIDialogService uiDialogService)
        {
            _importOrchestrator = importOrchestrator ?? throw new ArgumentNullException(nameof(importOrchestrator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));

            // Komendy
            CancelImportCommand = new AsyncRelayCommand(CancelImportAsync, _ => CanCancel);
            SaveReportCommand = new AsyncRelayCommand(SaveReportAsync, _ => IsImportComplete);
            NewImportCommand = new RelayCommand(StartNewImport, () => IsImportComplete);
        }

        #region Properties

        public ImportProcessStatus? ProcessStatus
        {
            get => _processStatus;
            private set
            {
                if (SetProperty(ref _processStatus, value))
                {
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(CurrentOperation));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(ProcessedRecords));
                    OnPropertyChanged(nameof(SuccessfulRecords));
                    OnPropertyChanged(nameof(FailedRecords));
                    OnPropertyChanged(nameof(TotalRecords));
                }
            }
        }

        public bool IsImportRunning
        {
            get => _isImportRunning;
            private set
            {
                if (SetProperty(ref _isImportRunning, value))
                {
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        public bool CanCancel
        {
            get => _canCancel && IsImportRunning;
            private set => SetProperty(ref _canCancel, value);
        }

        // Alias dla kompatybilności
        public bool CanCancelImport => CanCancel;

        public bool IsImportComplete
        {
            get => _isImportComplete;
            private set
            {
                if (SetProperty(ref _isImportComplete, value))
                {
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        // Właściwości obliczane z ProcessStatus
        public double ProgressPercentage => ProcessStatus?.ProgressPercentage ?? 0;
        public string CurrentOperation => ProcessStatus?.CurrentOperation ?? "Oczekiwanie...";
        public string StatusText => ProcessStatus?.Status ?? "Nieznany";
        public int ProcessedRecords => ProcessStatus?.ProcessedRecords ?? 0;
        public int SuccessfulRecords => ProcessStatus?.SuccessfulRecords ?? 0;
        public int FailedRecords => ProcessStatus?.FailedRecords ?? 0;
        public int TotalRecords => ProcessStatus?.TotalRecords ?? 0;

        // Kolekcje
        public ObservableCollection<string> OperationLogs { get; } = new();

        #endregion

        #region Commands

        public ICommand CancelImportCommand { get; }
        public ICommand SaveReportCommand { get; }
        public ICommand NewImportCommand { get; }

        #endregion

        #region Public Methods

        public void StartImport(
            Stream fileStream,
            ImportDataType dataType,
            ImportOptions importOptions,
            string accessToken)
        {
            _ = StartImportAsync(fileStream, dataType, importOptions, accessToken);
        }

        public async Task CancelImportAsync()
        {
            try
            {
                if (!CanCancel || string.IsNullOrEmpty(_currentProcessId))
                    return;

                var result = await ShowConfirmationDialog(
                    "Anulować import?",
                    "Czy na pewno chcesz anulować import? Proces zostanie zatrzymany, ale już zaimportowane dane pozostaną w systemie.");

                if (result)
                {
                    AddLog("Anulowanie importu...");
                    
                    _cancellationTokenSource?.Cancel();
                    
                    var cancelled = await _importOrchestrator.CancelImportProcessAsync(_currentProcessId);
                    
                    if (cancelled)
                    {
                        AddLog("Import został anulowany przez użytkownika");
                        await HandleImportComplete("Cancelled");
                    }
                    else
                    {
                        AddLog("Nie można anulować importu - proces prawdopodobnie się już zakończył");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas anulowania importu");
                AddLog($"Błąd anulowania: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        private async Task StartImportAsync(
            Stream fileStream,
            ImportDataType dataType,
            ImportOptions importOptions,
            string accessToken)
        {
            try
            {
                IsImportRunning = true;
                CanCancel = true;
                IsImportComplete = false;
                _cancellationTokenSource = new CancellationTokenSource();

                AddLog($"Rozpoczynam import danych typu: {dataType}");
                AddLog($"Rozmiar pliku: {FormatFileSize(fileStream.Length)}");
                AddLog($"Opcje: DryRun={importOptions.DryRun}, UpdateExisting={importOptions.UpdateExisting}");

                // Inicjalizuj status procesu
                ProcessStatus = new ImportProcessStatus
                {
                    ProcessId = Guid.NewGuid().ToString(),
                    DataType = dataType,
                    FileName = "imported_file",
                    StartedAt = DateTime.UtcNow,
                    Status = "Starting",
                    TotalRecords = 0,
                    ProcessedRecords = 0,
                    SuccessfulRecords = 0,
                    FailedRecords = 0,
                    CurrentOperation = "Inicjalizacja importu...",
                    CanBeCancelled = true,
                    FileSizeBytes = fileStream.Length,
                    StartedBy = "current-user"
                };

                _currentProcessId = ProcessStatus.ProcessId;

                // Rozpocznij import w tle
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteImportAsync(fileStream, dataType, importOptions, accessToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd podczas wykonywania importu");
                        AddLog($"Błąd importu: {ex.Message}");
                        await HandleImportComplete("Failed");
                    }
                }, _cancellationTokenSource.Token);

                // Rozpocznij monitorowanie postępu
                StartProgressMonitoring();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas inicjalizacji importu");
                AddLog($"Błąd inicjalizacji: {ex.Message}");
                await HandleImportComplete("Failed");
            }
        }

        private async Task ExecuteImportAsync(
            Stream fileStream,
            ImportDataType dataType,
            ImportOptions importOptions,
            string accessToken)
        {
            BulkOperationResult result;

            try
            {
                // Aktualizuj status
                UpdateStatus("Running", "Wykonywanie importu...");

                // Wywołaj odpowiednią metodę importu
                result = dataType switch
                {
                    ImportDataType.Users => await _importOrchestrator.ImportUsersFromCsvAsync(fileStream, importOptions, accessToken),
                    ImportDataType.Teams => await _importOrchestrator.ImportTeamsFromExcelAsync(fileStream, importOptions, accessToken),
                    ImportDataType.SchoolStructure => await _importOrchestrator.ImportSchoolStructureAsync(fileStream, importOptions, accessToken),
                    _ => throw new NotSupportedException($"Typ danych {dataType} nie jest obsługiwany")
                };

                // Aktualizuj status na podstawie wyniku
                if (result.Success)
                {
                    AddLog($"Import zakończony pomyślnie: {result.SuccessfulOperations.Count} operacji");
                    if (result.Errors.Any())
                    {
                        AddLog($"Ostrzeżenia: {result.Errors.Count} błędów");
                        await HandleImportComplete("Completed with warnings");
                    }
                    else
                    {
                        await HandleImportComplete("Completed");
                    }
                }
                else
                {
                    AddLog($"Import zakończony z błędami: {result.ErrorMessage}");
                    await HandleImportComplete("Failed");
                }

                // Loguj szczegóły operacji
                foreach (var success in result.SuccessfulOperations)
                {
                    AddLog($"✓ {success.Operation}: {success.EntityName} - {success.Message}");
                }

                foreach (var error in result.Errors)
                {
                    AddLog($"✗ {error.Operation}: {error.EntityId} - {error.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                AddLog("Import został anulowany");
                await HandleImportComplete("Cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykonywania importu");
                AddLog($"Błąd krytyczny: {ex.Message}");
                await HandleImportComplete("Failed");
            }
        }

        private void StartProgressMonitoring()
        {
            _pollingTimer = new System.Timers.Timer(1000); // Co sekundę
            _pollingTimer.Elapsed += async (s, e) => await PollImportStatusAsync();
            _pollingTimer.Start();
        }

        private async Task PollImportStatusAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentProcessId))
                    return;

                // W rzeczywistej implementacji można by pobierać status z API
                // var processes = await _importOrchestrator.GetActiveImportProcessesStatusAsync();
                // var currentProcess = processes.FirstOrDefault(p => p.ProcessId == _currentProcessId);

                // Na potrzeby demo symulujemy postęp
                if (ProcessStatus != null && IsImportRunning)
                {
                    SimulateProgress();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas monitorowania postępu importu");
            }
        }

        private void SimulateProgress()
        {
            if (ProcessStatus == null) return;

            // Symulacja postępu (w rzeczywistej implementacji dane pochodziłyby z API)
            if (ProcessStatus.TotalRecords == 0)
            {
                ProcessStatus.TotalRecords = 100; // Przykładowa liczba rekordów
            }

            if (ProcessStatus.ProcessedRecords < ProcessStatus.TotalRecords)
            {
                ProcessStatus.ProcessedRecords = Math.Min(
                    ProcessStatus.ProcessedRecords + new Random().Next(1, 5),
                    ProcessStatus.TotalRecords);

                ProcessStatus.SuccessfulRecords = (int)(ProcessStatus.ProcessedRecords * 0.9);
                ProcessStatus.FailedRecords = ProcessStatus.ProcessedRecords - ProcessStatus.SuccessfulRecords;

                ProcessStatus.CurrentOperation = $"Przetwarzanie rekordu {ProcessStatus.ProcessedRecords}/{ProcessStatus.TotalRecords}";

                // Aktualizuj UI w głównym wątku
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(CurrentOperation));
                    OnPropertyChanged(nameof(ProcessedRecords));
                    OnPropertyChanged(nameof(SuccessfulRecords));
                    OnPropertyChanged(nameof(FailedRecords));
                });
            }
        }

        private void UpdateStatus(string status, string currentOperation)
        {
            if (ProcessStatus != null)
            {
                ProcessStatus.Status = status;
                ProcessStatus.CurrentOperation = currentOperation;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CurrentOperation));
                });
            }
        }

        private async Task HandleImportComplete(string finalStatus)
        {
            try
            {
                _pollingTimer?.Stop();
                _pollingTimer?.Dispose();
                _pollingTimer = null;

                if (ProcessStatus != null)
                {
                    ProcessStatus.Status = finalStatus;
                    ProcessStatus.CompletedAt = DateTime.UtcNow;
                    ProcessStatus.CanBeCancelled = false;
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsImportRunning = false;
                    CanCancel = false;
                    IsImportComplete = true;
                });

                AddLog($"Import zakończony ze statusem: {finalStatus}");

                // Pokaż powiadomienie
                var message = finalStatus switch
                {
                    "Completed" => "Import został zakończony pomyślnie!",
                    "Completed with warnings" => "Import zakończony z ostrzeżeniami. Sprawdź logi.",
                    "Failed" => "Import zakończony niepowodzeniem. Sprawdź błędy.",
                    "Cancelled" => "Import został anulowany przez użytkownika.",
                    _ => $"Import zakończony ze statusem: {finalStatus}"
                };

                await ShowInfoDialog("Import zakończony", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas finalizacji importu");
            }
        }

        private void AddLog(string message)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                OperationLogs.Add(logEntry);
                
                // Ogranicz liczbę logów do 100
                while (OperationLogs.Count > 100)
                {
                    OperationLogs.RemoveAt(0);
                }
            });

            _logger.LogInformation(message);
        }

        private async Task SaveReportAsync()
        {
            try
            {
                ShowLoadingOverlay("Generowanie raportu...");

                // TODO: Implementuj zapisywanie raportu
                await Task.Delay(1000);

                await ShowInfoDialog("Raport zapisany", 
                    "Raport z importu został zapisany.\n" +
                    "Funkcja zostanie w pełni zaimplementowana w przyszłych wersjach.");

                _logger.LogInformation("Zapisano raport z importu dla procesu {ProcessId}", _currentProcessId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania raportu");
                await ShowErrorDialog("Błąd", $"Nie można zapisać raportu: {ex.Message}");
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private void StartNewImport()
        {
            // Reset stanu dla nowego importu
            ProcessStatus = null;
            OperationLogs.Clear();
            IsImportComplete = false;
            _currentProcessId = null;

            // Powróć do pierwszego kroku wizarda (można to zaimplementować poprzez event)
            // NewImportRequested?.Invoke(this, EventArgs.Empty);
        }

        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _pollingTimer?.Stop();
            _pollingTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        #endregion
    }
} 
