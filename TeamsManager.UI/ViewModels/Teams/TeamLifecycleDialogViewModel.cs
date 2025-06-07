using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions;
using ArchiveOptions = TeamsManager.Core.Abstractions.Services.ArchiveOptions;
using RestoreOptions = TeamsManager.Core.Abstractions.Services.RestoreOptions;
using TeamMigrationPlan = TeamsManager.Core.Abstractions.Services.TeamMigrationPlan;
using ConsolidationOptions = TeamsManager.Core.Abstractions.Services.ConsolidationOptions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Teams
{
    public class TeamLifecycleDialogViewModel : INotifyPropertyChanged
    {
        private readonly ITeamLifecycleOrchestrator _lifecycleOrchestrator;
        private readonly ISchoolYearService _schoolYearService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TeamLifecycleDialogViewModel> _logger;
        
        private CancellationTokenSource? _cancellationTokenSource;
        private string _selectedOperation = string.Empty;
        private bool _isProcessing;
        private double _progressPercentage;
        private string _progressStatus = string.Empty;
        private string _progressTitle = string.Empty;
        private int _processedItems;
        private int _totalItems;
        private int _errorCount;

        // Operation options
        private string _archiveReason = "Koniec roku szkolnego";
        private bool _notifyOwners = true;
        private bool _removeInactiveMembers = false;
        private bool _cleanupChannels = false;
        private int _batchSize = 10;
        private bool _validateOwnerAvailability = true;
        private bool _restoreOriginalPermissions = true;
        private bool _notifyMembersOnRestore = false;
        private SchoolYear? _targetSchoolYear;
        private bool _archiveSourceTeams = true;
        private bool _copyMembersAndPermissions = true;
        private bool _updateNameTemplates = true;
        private int _minInactiveDays = 90;
        private int _maxMembersForConsolidation = 3;
        private bool _onlyInactiveTeams = true;
        private bool _excludeTeamsWithActiveTasks = true;
        private bool _dryRun = false;
        private bool _continueOnError = true;
        private double _acceptableErrorPercentage = 10;

        public TeamLifecycleDialogViewModel(
            ITeamLifecycleOrchestrator lifecycleOrchestrator,
            ISchoolYearService schoolYearService,
            ICurrentUserService currentUserService,
            ILogger<TeamLifecycleDialogViewModel> logger)
        {
            _lifecycleOrchestrator = lifecycleOrchestrator;
            _schoolYearService = schoolYearService;
            _currentUserService = currentUserService;
            _logger = logger;

            SelectedTeams = new ObservableCollection<Team>();
            AvailableSchoolYears = new ObservableCollection<SchoolYear>();

            // Initialize commands
            SelectOperationCommand = new RelayCommand<string>(SelectOperation);
            ExecuteOperationCommand = new AsyncRelayCommand(ExecuteOperationAsync, _ => CanExecuteOperation);
            CancelOperationCommand = new RelayCommand(CancelOperation);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke());

            LoadDataAsync();
        }

        #region Properties

        public ObservableCollection<Team> SelectedTeams { get; }
        public ObservableCollection<SchoolYear> AvailableSchoolYears { get; }

        public int SelectedTeamsCount => SelectedTeams?.Count ?? 0;

        public string SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                _selectedOperation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOperationSelected));
                OnPropertyChanged(nameof(IsArchiveSelected));
                OnPropertyChanged(nameof(IsRestoreSelected));
                OnPropertyChanged(nameof(IsMigrateSelected));
                OnPropertyChanged(nameof(IsConsolidateSelected));
                OnPropertyChanged(nameof(CanExecuteOperation));
            }
        }

        public bool IsOperationSelected => !string.IsNullOrEmpty(SelectedOperation);
        public bool IsArchiveSelected => SelectedOperation == "Archive";
        public bool IsRestoreSelected => SelectedOperation == "Restore";
        public bool IsMigrateSelected => SelectedOperation == "Migrate";
        public bool IsConsolidateSelected => SelectedOperation == "Consolidate";

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged();
            }
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                _progressPercentage = value;
                OnPropertyChanged();
            }
        }

        public string ProgressStatus
        {
            get => _progressStatus;
            set
            {
                _progressStatus = value;
                OnPropertyChanged();
            }
        }

        public string ProgressTitle
        {
            get => _progressTitle;
            set
            {
                _progressTitle = value;
                OnPropertyChanged();
            }
        }

        public int ProcessedItems
        {
            get => _processedItems;
            set
            {
                _processedItems = value;
                OnPropertyChanged();
            }
        }

        public int TotalItems
        {
            get => _totalItems;
            set
            {
                _totalItems = value;
                OnPropertyChanged();
            }
        }

        public int ErrorCount
        {
            get => _errorCount;
            set
            {
                _errorCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        public bool HasErrors => ErrorCount > 0;

        // Archive options
        public string ArchiveReason
        {
            get => _archiveReason;
            set
            {
                _archiveReason = value;
                OnPropertyChanged();
            }
        }

        public bool NotifyOwners
        {
            get => _notifyOwners;
            set
            {
                _notifyOwners = value;
                OnPropertyChanged();
            }
        }

        public bool RemoveInactiveMembers
        {
            get => _removeInactiveMembers;
            set
            {
                _removeInactiveMembers = value;
                OnPropertyChanged();
            }
        }

        public bool CleanupChannels
        {
            get => _cleanupChannels;
            set
            {
                _cleanupChannels = value;
                OnPropertyChanged();
            }
        }

        public int BatchSize
        {
            get => _batchSize;
            set
            {
                _batchSize = value;
                OnPropertyChanged();
            }
        }

        // Restore options
        public bool ValidateOwnerAvailability
        {
            get => _validateOwnerAvailability;
            set
            {
                _validateOwnerAvailability = value;
                OnPropertyChanged();
            }
        }

        public bool RestoreOriginalPermissions
        {
            get => _restoreOriginalPermissions;
            set
            {
                _restoreOriginalPermissions = value;
                OnPropertyChanged();
            }
        }

        public bool NotifyMembersOnRestore
        {
            get => _notifyMembersOnRestore;
            set
            {
                _notifyMembersOnRestore = value;
                OnPropertyChanged();
            }
        }

        // Migration options
        public SchoolYear? TargetSchoolYear
        {
            get => _targetSchoolYear;
            set
            {
                _targetSchoolYear = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanExecuteOperation));
            }
        }

        public bool ArchiveSourceTeams
        {
            get => _archiveSourceTeams;
            set
            {
                _archiveSourceTeams = value;
                OnPropertyChanged();
            }
        }

        public bool CopyMembersAndPermissions
        {
            get => _copyMembersAndPermissions;
            set
            {
                _copyMembersAndPermissions = value;
                OnPropertyChanged();
            }
        }

        public bool UpdateNameTemplates
        {
            get => _updateNameTemplates;
            set
            {
                _updateNameTemplates = value;
                OnPropertyChanged();
            }
        }

        // Consolidation options
        public int MinInactiveDays
        {
            get => _minInactiveDays;
            set
            {
                _minInactiveDays = value;
                OnPropertyChanged();
            }
        }

        public int MaxMembersForConsolidation
        {
            get => _maxMembersForConsolidation;
            set
            {
                _maxMembersForConsolidation = value;
                OnPropertyChanged();
            }
        }

        public bool OnlyInactiveTeams
        {
            get => _onlyInactiveTeams;
            set
            {
                _onlyInactiveTeams = value;
                OnPropertyChanged();
            }
        }

        public bool ExcludeTeamsWithActiveTasks
        {
            get => _excludeTeamsWithActiveTasks;
            set
            {
                _excludeTeamsWithActiveTasks = value;
                OnPropertyChanged();
            }
        }

        // Common options
        public bool DryRun
        {
            get => _dryRun;
            set
            {
                _dryRun = value;
                OnPropertyChanged();
            }
        }

        public bool ContinueOnError
        {
            get => _continueOnError;
            set
            {
                _continueOnError = value;
                OnPropertyChanged();
            }
        }

        public double AcceptableErrorPercentage
        {
            get => _acceptableErrorPercentage;
            set
            {
                _acceptableErrorPercentage = value;
                OnPropertyChanged();
            }
        }

        public bool CanExecuteOperation
        {
            get
            {
                if (IsProcessing || !IsOperationSelected)
                    return false;

                if (IsMigrateSelected && TargetSchoolYear == null)
                    return false;

                if (IsConsolidateSelected)
                    return true; // Consolidation nie wymaga wybranych zespołów

                return SelectedTeamsCount > 0;
            }
        }

        #endregion

        #region Commands

        public ICommand SelectOperationCommand { get; }
        public ICommand ExecuteOperationCommand { get; }
        public ICommand CancelOperationCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Methods

        private async void LoadDataAsync()
        {
            try
            {
                var schoolYears = await _schoolYearService.GetAllActiveSchoolYearsAsync();
                AvailableSchoolYears.Clear();
                foreach (var year in schoolYears.Where(y => y.IsActive))
                {
                    AvailableSchoolYears.Add(year);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd ładowania danych");
            }
        }

        private void SelectOperation(string? operation)
        {
            if (!string.IsNullOrEmpty(operation))
            {
                SelectedOperation = operation;
            }
        }

        private async Task ExecuteOperationAsync()
        {
            if (!CanExecuteOperation)
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            IsProcessing = true;
            ErrorCount = 0;
            ProcessedItems = 0;

            try
            {
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException("Brak tokenu dostępu");
                }

                BulkOperationResult? result = null;

                switch (SelectedOperation)
                {
                    case "Archive":
                        ProgressTitle = "Archiwizacja zespołów";
                        result = await ExecuteArchiveAsync(accessToken);
                        break;

                    case "Restore":
                        ProgressTitle = "Przywracanie zespołów";
                        result = await ExecuteRestoreAsync(accessToken);
                        break;

                    case "Migrate":
                        ProgressTitle = "Migracja zespołów";
                        result = await ExecuteMigrateAsync(accessToken);
                        break;

                    case "Consolidate":
                        ProgressTitle = "Konsolidacja zespołów";
                        result = await ExecuteConsolidateAsync(accessToken);
                        break;
                }

                if (result != null)
                {
                    await ShowResultsAsync(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wykonywania operacji {Operation}", SelectedOperation);
                ProgressStatus = $"Błąd: {ex.Message}";
                ErrorCount = TotalItems;
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task<BulkOperationResult> ExecuteArchiveAsync(string accessToken)
        {
            var options = new ArchiveOptions
            {
                Reason = ArchiveReason,
                NotifyOwners = NotifyOwners,
                RemoveInactiveMembers = RemoveInactiveMembers,
                CleanupChannels = CleanupChannels,
                BatchSize = BatchSize,
                DryRun = DryRun,
                ContinueOnError = ContinueOnError,
                AcceptableErrorPercentage = AcceptableErrorPercentage
            };

            var teamIds = SelectedTeams.Select(t => t.Id).ToArray();
            TotalItems = teamIds.Length;

            // Symulacja progress updates (w rzeczywistości byłby callback z orchestratora)
            _ = SimulateProgressAsync();

            return await _lifecycleOrchestrator.BulkArchiveTeamsWithCleanupAsync(
                teamIds, options, accessToken);
        }

        private async Task<BulkOperationResult> ExecuteRestoreAsync(string accessToken)
        {
            var options = new RestoreOptions
            {
                ValidateOwnerAvailability = ValidateOwnerAvailability,
                RestoreMembers = RestoreOriginalPermissions,
                RestoreChannels = true,
                BatchSize = BatchSize,
                ContinueOnError = ContinueOnError,
                AcceptableErrorPercentage = AcceptableErrorPercentage
            };

            var teamIds = SelectedTeams.Select(t => t.Id).ToArray();
            TotalItems = teamIds.Length;

            _ = SimulateProgressAsync();

            return await _lifecycleOrchestrator.BulkRestoreTeamsWithValidationAsync(
                teamIds, options, accessToken);
        }

        private async Task<BulkOperationResult> ExecuteMigrateAsync(string accessToken)
        {
            if (TargetSchoolYear == null)
                throw new InvalidOperationException("Docelowy rok szkolny nie został wybrany");

            var plan = new TeamMigrationPlan
            {
                TeamIds = SelectedTeams.Select(t => t.Id).ToArray(),
                FromSchoolYearId = SelectedTeams.First().SchoolYearId ?? string.Empty,
                ToSchoolYearId = TargetSchoolYear.Id,
                ArchiveSourceTeams = ArchiveSourceTeams,
                CopyMembers = CopyMembersAndPermissions,
                CopyChannels = false,
                BatchSize = BatchSize,
                ContinueOnError = ContinueOnError
            };

            TotalItems = plan.TeamIds.Length;
            _ = SimulateProgressAsync();

            return await _lifecycleOrchestrator.MigrateTeamsBetweenSchoolYearsAsync(
                plan, accessToken);
        }

        private async Task<BulkOperationResult> ExecuteConsolidateAsync(string accessToken)
        {
            var options = new ConsolidationOptions
            {
                MinInactiveDays = MinInactiveDays,
                MaxMembersCount = MaxMembersForConsolidation,
                OnlyTeamsWithoutActivity = OnlyInactiveTeams,
                BatchSize = BatchSize,
                DryRun = DryRun,
                ContinueOnError = ContinueOnError
            };

            ProgressStatus = "Wyszukiwanie nieaktywnych zespołów...";
            TotalItems = 1; // Będzie aktualizowane po znalezieniu zespołów

            return await _lifecycleOrchestrator.ConsolidateInactiveTeamsAsync(
                options, accessToken);
        }

        private async Task SimulateProgressAsync()
        {
            // W rzeczywistej implementacji byłby callback z orchestratora
            // To tylko symulacja dla demonstracji UI
            while (IsProcessing && ProcessedItems < TotalItems)
            {
                await Task.Delay(500);
                ProcessedItems = Math.Min(ProcessedItems + 1, TotalItems);
                ProgressPercentage = TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
                ProgressStatus = $"Przetwarzanie zespołu {ProcessedItems} z {TotalItems}...";

                // Symulacja błędów
                if (ProcessedItems % 7 == 0 && !DryRun)
                {
                    ErrorCount++;
                }
            }
        }

        private async Task ShowResultsAsync(BulkOperationResult result)
        {
            // Tu można pokazać dialog z wynikami lub zaktualizować UI
            var successCount = result.SuccessfulOperations?.Count ?? 0;
            var errorCount = result.Errors?.Count ?? 0;

            ProgressStatus = $"Zakończono: {successCount} sukces, {errorCount} błędów";
            await Task.Delay(3000); // Pokazanie wyniku przez 3 sekundy

            if (result.Success)
            {
                RequestClose?.Invoke();
            }
        }

        private void CancelOperation()
        {
            _cancellationTokenSource?.Cancel();
            ProgressStatus = "Anulowanie operacji...";
        }

        private async Task<string> GetAccessTokenAsync()
        {
            // TODO: Implementacja pobierania tokenu dostępu
            // Należy zaimplementować w oparciu o istniejący mechanizm w aplikacji
            return await Task.FromResult(string.Empty);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? RequestClose;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    // Helper classes for operation options
} 