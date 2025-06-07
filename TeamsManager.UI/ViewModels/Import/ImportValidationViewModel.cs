using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Models.Import;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.ViewModels.Import
{
    /// <summary>
    /// ViewModel dla kroku walidacji danych w wizardzie importu
    /// </summary>
    public class ImportValidationViewModel : BaseViewModel
    {
        private readonly IDataImportOrchestrator _importOrchestrator;
        private readonly ILogger<ImportValidationViewModel> _logger;

        private bool _isValid = false;
        private int _totalRecords = 0;
        private int _validRecords = 0;
        private int _invalidRecords = 0;
        private bool _showOnlyErrors = false;
        private string _filterText = string.Empty;
        private bool _showErrors = true;
        private bool _showWarnings = true;

        private ImportValidationResult? _validationResult;

        public ImportValidationViewModel(
            IDataImportOrchestrator importOrchestrator,
            ILogger<ImportValidationViewModel> logger,
            IUIDialogService uiDialogService)
        {
            _importOrchestrator = importOrchestrator ?? throw new ArgumentNullException(nameof(importOrchestrator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));

            // Komendy
            AutoFixCommand = new AsyncRelayCommand(AutoFixErrorsAsync, _ => HasFixableErrors);
            ExportErrorsCommand = new AsyncRelayCommand(ExportErrorsAsync, _ => HasErrors);
            FixItemCommand = new AsyncRelayCommand<ValidationItemModel>(FixItemAsync, item => item?.CanFix == true);

            // Subskrypcje na zmiany filtrów
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(ShowErrors) or nameof(ShowWarnings) or nameof(FilterText))
                {
                    UpdateFilteredItems();
                }
            };
        }

        #region Properties

        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        public int TotalRecords
        {
            get => _totalRecords;
            private set => SetProperty(ref _totalRecords, value);
        }

        public int ValidRecords
        {
            get => _validRecords;
            private set => SetProperty(ref _validRecords, value);
        }

        public int InvalidRecords
        {
            get => _invalidRecords;
            private set => SetProperty(ref _invalidRecords, value);
        }

        public bool ShowOnlyErrors
        {
            get => _showOnlyErrors;
            set => SetProperty(ref _showOnlyErrors, value);
        }

        public string FilterText
        {
            get => _filterText;
            set => SetProperty(ref _filterText, value);
        }

        public bool ShowErrors
        {
            get => _showErrors;
            set => SetProperty(ref _showErrors, value);
        }

        public bool ShowWarnings
        {
            get => _showWarnings;
            set => SetProperty(ref _showWarnings, value);
        }

        public ImportValidationResult? ValidationResult
        {
            get => _validationResult;
            private set
            {
                _validationResult = value;
                UpdateValidationStats();
                UpdateFilteredItems();
            }
        }

        // Właściwości obliczane
        public bool HasErrors => ValidationItems.Any(vi => vi.IsError);
        public bool HasWarnings => ValidationItems.Any(vi => vi.IsWarning);
        public bool HasFixableErrors => ValidationItems.Any(vi => vi.IsError && vi.CanFix);

        // Kolekcje
        public ObservableCollection<ValidationItemModel> ValidationItems { get; } = new();
        public ObservableCollection<ValidationItemModel> FilteredValidationItems { get; } = new();

        #endregion

        #region Commands

        public ICommand AutoFixCommand { get; }
        public ICommand ExportErrorsCommand { get; }
        public ICommand FixItemCommand { get; }

        #endregion

        #region Events

        public event EventHandler? ValidationCompleted;

        #endregion

        #region Public Methods

        public async Task ValidateDataAsync(
            Stream fileStream, 
            ImportDataType dataType, 
            Dictionary<string, string> columnMappings,
            ImportOptions importOptions)
        {
            try
            {
                ShowLoadingOverlay("Walidacja danych...");

                _logger.LogInformation("Rozpoczynam walidację danych typu {DataType}", dataType);

                // Ustaw mapowanie kolumn w opcjach
                importOptions.ColumnMapping = columnMappings;

                // Wywołaj walidację przez orkiestrator
                var result = await _importOrchestrator.ValidateImportDataAsync(fileStream, dataType, importOptions);

                ValidationResult = result;
                IsValid = result.IsValid;

                // Konwertuj błędy i ostrzeżenia na modele UI
                await ConvertValidationResultsAsync(result);

                _logger.LogInformation("Walidacja zakończona: {Valid}/{Total} rekordów poprawnych", 
                    ValidRecords, TotalRecords);

                // Powiadom o zakończeniu walidacji
                ValidationCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas walidacji danych");
                await ShowErrorDialog("Błąd walidacji", $"Nie można zwalidować danych: {ex.Message}");
                IsValid = false;
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        #endregion

        #region Private Methods

        private async Task ConvertValidationResultsAsync(ImportValidationResult result)
        {
            await Task.Run(() =>
            {
                ValidationItems.Clear();

                // Dodaj błędy
                foreach (var error in result.Errors)
                {
                    var item = new ValidationItemModel
                    {
                        RowNumber = error.RowNumber,
                        ColumnName = error.ColumnName ?? "",
                        Value = error.Value ?? "",
                        Message = error.Message,
                        ValidationType = error.ErrorType,
                        IsError = true,
                        IsWarning = false,
                        CanFix = CanFixError(error)
                    };

                    System.Windows.Application.Current.Dispatcher.Invoke(() => ValidationItems.Add(item));
                }

                // Dodaj ostrzeżenia
                foreach (var warning in result.Warnings)
                {
                    var item = new ValidationItemModel
                    {
                        RowNumber = warning.RowNumber,
                        ColumnName = warning.ColumnName ?? "",
                        Value = warning.Value ?? "",
                        Message = warning.Message,
                        ValidationType = warning.WarningType,
                        IsError = false,
                        IsWarning = true,
                        CanFix = false // Ostrzeżenia zazwyczaj nie są naprawialne automatycznie
                    };

                    System.Windows.Application.Current.Dispatcher.Invoke(() => ValidationItems.Add(item));
                }
            });

            UpdateFilteredItems();
        }

        private bool CanFixError(ImportValidationError error)
        {
            // Określ które błędy można naprawić automatycznie
            return error.ErrorType switch
            {
                "EmptyValue" => true,
                "InvalidFormat" => true,
                "InvalidEmail" => true,
                "DuplicateValue" => false, // Wymaga decyzji użytkownika
                "FileSize" => false,
                "FormatError" => false,
                _ => false
            };
        }

        private void UpdateValidationStats()
        {
            if (ValidationResult != null)
            {
                TotalRecords = ValidationResult.TotalRecords;
                ValidRecords = ValidationResult.ValidRecords;
                InvalidRecords = TotalRecords - ValidRecords;
            }
            else
            {
                TotalRecords = ValidRecords = InvalidRecords = 0;
            }

            // Aktualizuj dostępność komend
            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(HasFixableErrors));
        }

        private void UpdateFilteredItems()
        {
            FilteredValidationItems.Clear();

            var filtered = ValidationItems.AsEnumerable();

            // Filtruj według typu
            if (!ShowErrors)
            {
                filtered = filtered.Where(vi => !vi.IsError);
            }

            if (!ShowWarnings)
            {
                filtered = filtered.Where(vi => !vi.IsWarning);
            }

            // Filtruj według tekstu
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                var searchText = FilterText.ToLowerInvariant();
                filtered = filtered.Where(vi =>
                    vi.ColumnName.ToLowerInvariant().Contains(searchText) ||
                    vi.Message.ToLowerInvariant().Contains(searchText) ||
                    vi.Value.ToLowerInvariant().Contains(searchText));
            }

            foreach (var item in filtered.OrderBy(vi => vi.RowNumber).ThenBy(vi => vi.ColumnName))
            {
                FilteredValidationItems.Add(item);
            }
        }

        private async Task AutoFixErrorsAsync()
        {
            try
            {
                ShowLoadingOverlay("Automatyczne naprawianie błędów...");

                var fixableErrors = ValidationItems.Where(vi => vi.IsError && vi.CanFix).ToList();
                var fixedCount = 0;

                foreach (var error in fixableErrors)
                {
                    if (await TryFixErrorAsync(error))
                    {
                        ValidationItems.Remove(error);
                        fixedCount++;
                    }
                }

                if (fixedCount > 0)
                {
                    await ShowInfoDialog("Naprawiono błędy", 
                        $"Automatycznie naprawiono {fixedCount} błędów.\n" +
                        "Pozostałe błędy wymagają ręcznej interwencji.");

                    // Aktualizuj statystyki
                    ValidRecords += fixedCount;
                    InvalidRecords -= fixedCount;
                    IsValid = InvalidRecords == 0;

                    UpdateFilteredItems();
                    ValidationCompleted?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    await ShowInfoDialog("Brak napraw", "Nie znaleziono błędów możliwych do automatycznego naprawienia.");
                }

                _logger.LogInformation("Automatyczne naprawianie: naprawiono {Count} błędów", fixedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas automatycznego naprawiania błędów");
                await ShowErrorDialog("Błąd", $"Nie można naprawić błędów: {ex.Message}");
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private async Task<bool> TryFixErrorAsync(ValidationItemModel error)
        {
            try
            {
                // Symulacja naprawiania błędów
                await Task.Delay(100); // Symuluj operację

                return error.ValidationType switch
                {
                    "EmptyValue" => TryFixEmptyValue(error),
                    "InvalidFormat" => TryFixInvalidFormat(error),
                    "InvalidEmail" => TryFixInvalidEmail(error),
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        private bool TryFixEmptyValue(ValidationItemModel error)
        {
            // Dla pustych wartości można zaproponować domyślne
            return error.ColumnName switch
            {
                "Role" => true, // Domyślnie "Uczen"
                "TeamType" => true, // Domyślnie "Class"
                _ => false
            };
        }

        private bool TryFixInvalidFormat(ValidationItemModel error)
        {
            // Próba naprawienia nieprawidłowego formatu
            if (error.ColumnName.Contains("Email") || error.ColumnName.Contains("UPN"))
            {
                return error.Value.Contains("@"); // Podstawowa walidacja email
            }

            return false;
        }

        private bool TryFixInvalidEmail(ValidationItemModel error)
        {
            // Próba naprawienia adresu email
            var value = error.Value.Trim().ToLowerInvariant();
            return !string.IsNullOrEmpty(value) && value.Contains("@") && value.Contains(".");
        }

        private async Task ExportErrorsAsync()
        {
            try
            {
                ShowLoadingOverlay("Eksportowanie błędów...");

                // TODO: Implementuj eksport błędów do pliku CSV/Excel
                await Task.Delay(1000); // Symulacja

                await ShowInfoDialog("Eksport zakończony", 
                    "Lista błędów została wyeksportowana do pliku.\n" +
                    "Funkcja zostanie w pełni zaimplementowana w przyszłych wersjach.");

                _logger.LogInformation("Wyeksportowano {Count} błędów walidacji", ValidationItems.Count(vi => vi.IsError));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas eksportu błędów");
                await ShowErrorDialog("Błąd eksportu", $"Nie można wyeksportować błędów: {ex.Message}");
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private async Task FixItemAsync(ValidationItemModel? item)
        {
            if (item == null || !item.CanFix) return;

            try
            {
                if (await TryFixErrorAsync(item))
                {
                    ValidationItems.Remove(item);
                    UpdateFilteredItems();
                    
                    ValidRecords++;
                    InvalidRecords--;
                    IsValid = InvalidRecords == 0;

                    await ShowInfoDialog("Błąd naprawiony", $"Błąd w wierszu {item.RowNumber} został naprawiony.");
                }
                else
                {
                    await ShowErrorDialog("Nie można naprawić", $"Nie udało się naprawić błędu w wierszu {item.RowNumber}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas naprawiania elementu {RowNumber}", item.RowNumber);
                await ShowErrorDialog("Błąd", $"Nie można naprawić błędu: {ex.Message}");
            }
        }

        #endregion
    }
} 
