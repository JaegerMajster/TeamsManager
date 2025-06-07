using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.ViewModels.Import;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using TeamsManager.UI.Models.Import;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.ViewModels.Import
{
    /// <summary>
    /// Główny ViewModel dla Bulk Import Wizard - zarządza przepływem między krokami
    /// </summary>
    public class BulkImportWizardViewModel : BaseViewModel
    {
        private readonly IDataImportOrchestrator _importOrchestrator;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<BulkImportWizardViewModel> _logger;

        private int _currentStep = 0;
        private bool _isImporting = false;
        private string _dialogTitle = "Import masowy danych";

        // Sub-ViewModels dla kroków
        public ImportFileSelectionViewModel FileSelection { get; }
        public ImportColumnMappingViewModel ColumnMapping { get; }
        public ImportValidationViewModel Validation { get; }
        public ImportProgressViewModel Progress { get; }

        public BulkImportWizardViewModel(
            IDataImportOrchestrator importOrchestrator,
            ITokenManager tokenManager,
            ImportFileSelectionViewModel fileSelectionViewModel,
            ImportColumnMappingViewModel columnMappingViewModel,
            ImportValidationViewModel validationViewModel,
            ImportProgressViewModel progressViewModel,
            ILogger<BulkImportWizardViewModel> logger,
            IUIDialogService uiDialogService)
        {
            _importOrchestrator = importOrchestrator ?? throw new ArgumentNullException(nameof(importOrchestrator));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));

            FileSelection = fileSelectionViewModel ?? throw new ArgumentNullException(nameof(fileSelectionViewModel));
            ColumnMapping = columnMappingViewModel ?? throw new ArgumentNullException(nameof(columnMappingViewModel));
            Validation = validationViewModel ?? throw new ArgumentNullException(nameof(validationViewModel));
            Progress = progressViewModel ?? throw new ArgumentNullException(nameof(progressViewModel));

            // Komendy
            NextStepCommand = new AsyncRelayCommand(NextStepAsync, _ => CanGoNext && !IsImporting);
            PreviousStepCommand = new RelayCommand(PreviousStep, () => CanGoBack && !IsImporting);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            StartImportCommand = new AsyncRelayCommand(StartImportAsync, _ => CanStartImport);

            // Inicjalizuj kroki
            InitializeSteps();
            UpdateStepTitle();

            // Event handlers
            FileSelection.FileChanged += OnFileChanged;
            ColumnMapping.MappingChanged += OnMappingChanged;
            Validation.ValidationCompleted += OnValidationCompleted;
        }

        #region Properties

        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    UpdateStepTitle();
                    OnPropertyChanged(nameof(CanGoNext));
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(IsFileSelectionStep));
                    OnPropertyChanged(nameof(IsColumnMappingStep));
                    OnPropertyChanged(nameof(IsValidationStep));
                    OnPropertyChanged(nameof(IsProgressStep));
                }
            }
        }

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public bool IsImporting
        {
            get => _isImporting;
            set
            {
                if (SetProperty(ref _isImporting, value))
                {
                    OnPropertyChanged(nameof(CanGoNext));
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(CanStartImport));
                }
            }
        }

        public bool CanGoNext => ValidateCurrentStep() && !IsImporting;
        public bool CanGoBack => CurrentStep > 0 && !IsImporting;
        public bool CanStartImport => CurrentStep == 2 && Validation.IsValid && !IsImporting;

        // Właściwości dla widoczności kroków
        public bool IsFileSelectionStep => CurrentStep == 0;
        public bool IsColumnMappingStep => CurrentStep == 1;
        public bool IsValidationStep => CurrentStep == 2;
        public bool IsProgressStep => CurrentStep == 3;

        // Kolekcje
        public ObservableCollection<ImportDataTypeModel> ImportDataTypes { get; } = new();
        public ObservableCollection<string> StepTitles { get; } = new();

        #endregion

        #region Commands

        public ICommand NextStepCommand { get; }
        public ICommand PreviousStepCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand StartImportCommand { get; }
        public ICommand? CloseDialogCommand { get; set; }

        #endregion

        #region Private Methods

        private void InitializeSteps()
        {
            StepTitles.Clear();
            StepTitles.Add("1. Wybór pliku i typu danych");
            StepTitles.Add("2. Mapowanie kolumn");
            StepTitles.Add("3. Walidacja danych");
            StepTitles.Add("4. Import i postęp");

            // Inicjalizuj typy danych
            InitializeImportDataTypes();
        }

        private void InitializeImportDataTypes()
        {
            ImportDataTypes.Clear();
            ImportDataTypes.Add(new ImportDataTypeModel
            {
                Name = "Users",
                DisplayName = "Użytkownicy",
                Description = "Import użytkowników z pliku CSV lub Excel",
                IconKind = "Account",
                Type = Core.Abstractions.Services.ImportDataType.Users,
                SupportedFormats = new[] { ".csv", ".xlsx", ".xls" },
                RequiredColumns = new[] { "FirstName", "LastName", "UPN" },
                SampleFileName = "users_sample.csv"
            });

            ImportDataTypes.Add(new ImportDataTypeModel
            {
                Name = "Teams",
                DisplayName = "Zespoły",
                Description = "Import zespołów z pliku Excel",
                IconKind = "MicrosoftTeams",
                Type = Core.Abstractions.Services.ImportDataType.Teams,
                SupportedFormats = new[] { ".xlsx", ".xls", ".csv" },
                RequiredColumns = new[] { "Name", "Description", "TeamType" },
                SampleFileName = "teams_sample.xlsx"
            });

            ImportDataTypes.Add(new ImportDataTypeModel
            {
                Name = "SchoolStructure",
                DisplayName = "Struktura szkoły",
                Description = "Import działów i przedmiotów",
                IconKind = "Domain",
                Type = Core.Abstractions.Services.ImportDataType.SchoolStructure,
                SupportedFormats = new[] { ".csv", ".xlsx", ".json" },
                RequiredColumns = new[] { "Name", "Code", "Type" },
                SampleFileName = "structure_sample.csv"
            });

            ImportDataTypes.Add(new ImportDataTypeModel
            {
                Name = "Departments",
                DisplayName = "Działy",
                Description = "Import działów szkoły",
                IconKind = "OfficeBuildingOutline",
                Type = Core.Abstractions.Services.ImportDataType.Departments,
                SupportedFormats = new[] { ".csv", ".xlsx" },
                RequiredColumns = new[] { "Name", "Code" },
                SampleFileName = "departments_sample.csv"
            });

            ImportDataTypes.Add(new ImportDataTypeModel
            {
                Name = "Subjects",
                DisplayName = "Przedmioty",
                Description = "Import przedmiotów nauczania",
                IconKind = "BookEducationOutline",
                Type = Core.Abstractions.Services.ImportDataType.Subjects,
                SupportedFormats = new[] { ".csv", ".xlsx" },
                RequiredColumns = new[] { "Name", "Code", "Hours" },
                SampleFileName = "subjects_sample.csv"
            });
        }

        private void UpdateStepTitle()
        {
            var stepTitle = CurrentStep < StepTitles.Count ? StepTitles[CurrentStep] : "Import masowy";
            DialogTitle = $"Import masowy danych - {stepTitle}";
        }

        private bool ValidateCurrentStep()
        {
            return CurrentStep switch
            {
                0 => FileSelection.HasValidFile && FileSelection.SelectedDataType != null,
                1 => ColumnMapping.HasValidMapping,
                2 => Validation.IsValid,
                3 => true,
                _ => false
            };
        }

        private async Task NextStepAsync()
        {
            try
            {
                ShowLoadingOverlay("Przechodzę do następnego kroku...");

                switch (CurrentStep)
                {
                    case 0: // File Selection -> Column Mapping
                        await PrepareColumnMappingAsync();
                        break;
                    case 1: // Column Mapping -> Validation
                        await PrepareValidationAsync();
                        break;
                    case 2: // Validation -> Progress (start import)
                        CurrentStep = 3;
                        await StartImportAsync();
                        return;
                }

                CurrentStep++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przechodzenia do następnego kroku");
                await ShowErrorDialog("Błąd", $"Nie można przejść do następnego kroku: {ex.Message}");
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private void PreviousStep()
        {
            if (CanGoBack)
            {
                CurrentStep--;
            }
        }

        private async Task PrepareColumnMappingAsync()
        {
            if (FileSelection.FileStream != null && FileSelection.SelectedDataType != null)
            {
                ColumnMapping.LoadFileData(
                    FileSelection.FileStream,
                    FileSelection.SelectedDataType.Type,
                    FileSelection.ImportOptions);
            }
        }

        private async Task PrepareValidationAsync()
        {
            if (FileSelection.FileStream != null && FileSelection.SelectedDataType != null)
            {
                await Validation.ValidateDataAsync(
                    FileSelection.FileStream,
                    FileSelection.SelectedDataType.Type,
                    ColumnMapping.GetColumnMappings(),
                    FileSelection.ImportOptions);
            }
        }

        private async Task StartImportAsync()
        {
            try
            {
                IsImporting = true;
                _logger.LogInformation("Rozpoczynam import masowy typu {DataType}", FileSelection.SelectedDataType?.Name);

                if (FileSelection.FileStream == null || FileSelection.SelectedDataType == null)
                {
                    throw new InvalidOperationException("Brak pliku lub typu danych do importu");
                }

                // Pobierz token dostępu
                var accessToken = (string?)null; // await _tokenManager.GetTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException("Nie można uzyskać tokenu dostępu");
                }

                // Ustaw mapowanie kolumn w opcjach
                var options = FileSelection.ImportOptions;
                options.ColumnMapping = ColumnMapping.GetColumnMappings();

                // Przekaż dane do Progress ViewModel
                Progress.StartImport(
                    FileSelection.FileStream,
                    FileSelection.SelectedDataType.Type,
                    options,
                    accessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas rozpoczynania importu");
                await ShowErrorDialog("Błąd importu", ex.Message);
                IsImporting = false;
            }
        }

        private async Task CancelAsync()
        {
            var result = await ShowConfirmationDialog(
                "Anulować import?",
                "Czy na pewno chcesz anulować proces importu? Niezapisane zmiany zostaną utracone.");

            if (result)
            {
                if (IsImporting && Progress.CanCancelImport)
                {
                    await Progress.CancelImportAsync();
                }

                // Zamknij okno
                CloseDialogCommand?.Execute(false);
            }
        }

        #endregion

        #region Event Handlers

        private void OnFileChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanGoNext));
        }

        private void OnMappingChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanGoNext));
        }

        private void OnValidationCompleted(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanStartImport));
        }

        #endregion


    }
} 
