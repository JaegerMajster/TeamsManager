using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace TeamsManager.UI.ViewModels.SchoolTypes
{
    /// <summary>
    /// ViewModel dla edycji typu szkoły
    /// </summary>
    public class SchoolTypeEditViewModel : BaseViewModel, IDataErrorInfo
    {
        private SchoolType _editedSchoolType;
        private string _windowTitle;
        private bool _isEditMode;
        private Color _selectedColor = Colors.Gray;

        public SchoolTypeEditViewModel(SchoolType? schoolType = null)
        {
            if (schoolType != null)
            {
                // Tryb edycji - klonuj obiekt
                _editedSchoolType = new SchoolType
                {
                    Id = schoolType.Id,
                    ShortName = schoolType.ShortName,
                    FullName = schoolType.FullName,
                    Description = schoolType.Description,
                    ColorCode = schoolType.ColorCode,
                    SortOrder = schoolType.SortOrder,
                    IsActive = schoolType.IsActive,
                    CreatedBy = schoolType.CreatedBy,
                    CreatedDate = schoolType.CreatedDate
                };
                _isEditMode = true;
                _windowTitle = $"Edycja typu szkoły: {schoolType.DisplayName}";
                
                // Ustaw kolor
                if (!string.IsNullOrWhiteSpace(schoolType.ColorCode))
                {
                    try
                    {
                        _selectedColor = (Color)ColorConverter.ConvertFromString(schoolType.ColorCode);
                    }
                    catch { }
                }
            }
            else
            {
                // Tryb tworzenia
                _editedSchoolType = new SchoolType
                {
                    Id = Guid.NewGuid().ToString(),
                    IsActive = true,
                    SortOrder = 0
                };
                _isEditMode = false;
                _windowTitle = "Nowy typ szkoły";
            }

            // Inicjalizacja komend
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => Cancel());
            ResetColorCommand = new RelayCommand(_ => ResetColor());
            SelectColorCommand = new RelayCommand(param => SelectColor(param?.ToString()));
        }

        #region Properties

        public SchoolType EditedSchoolType
        {
            get => _editedSchoolType;
            set => SetProperty(ref _editedSchoolType, value);
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public string ShortName
        {
            get => _editedSchoolType.ShortName;
            set
            {
                if (_editedSchoolType.ShortName != value)
                {
                    _editedSchoolType.ShortName = value;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string FullName
        {
            get => _editedSchoolType.FullName;
            set
            {
                if (_editedSchoolType.FullName != value)
                {
                    _editedSchoolType.FullName = value;
                    OnPropertyChanged();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string Description
        {
            get => _editedSchoolType.Description;
            set
            {
                if (_editedSchoolType.Description != value)
                {
                    _editedSchoolType.Description = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? ColorCode
        {
            get => _editedSchoolType.ColorCode;
            set
            {
                if (_editedSchoolType.ColorCode != value)
                {
                    _editedSchoolType.ColorCode = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SortOrder
        {
            get => _editedSchoolType.SortOrder;
            set
            {
                if (_editedSchoolType.SortOrder != value)
                {
                    _editedSchoolType.SortOrder = value;
                    OnPropertyChanged();
                }
            }
        }

        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (SetProperty(ref _selectedColor, value))
                {
                    ColorCode = value.ToString();
                }
            }
        }

        public bool HasChanges { get; set; }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetColorCommand { get; }
        public ICommand SelectColorCommand { get; }

        public bool? DialogResult { get; set; }

        #endregion

        #region Methods

        private void Save()
        {
            if (!CanSave()) return;

            // Walidacja jest już wykonana przez CanSave
            DialogResult = true;
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(ShortName) &&
                   !string.IsNullOrWhiteSpace(FullName) &&
                   string.IsNullOrEmpty(this[nameof(ShortName)]) &&
                   string.IsNullOrEmpty(this[nameof(FullName)]);
        }

        private void Cancel()
        {
            DialogResult = false;
        }

        private void ResetColor()
        {
            SelectedColor = Colors.Gray;
            ColorCode = null;
        }

        private void SelectColor(string? colorCode)
        {
            if (!string.IsNullOrWhiteSpace(colorCode))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorCode);
                    SelectedColor = color;
                    ColorCode = colorCode;
                }
                catch
                {
                    // Ignoruj błędny kolor
                }
            }
        }

        #endregion

        #region IDataErrorInfo

        public string Error => string.Empty;

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(ShortName):
                        if (string.IsNullOrWhiteSpace(ShortName))
                            return "Skrót nazwy jest wymagany";
                        if (ShortName.Length > 10)
                            return "Skrót nazwy nie może być dłuższy niż 10 znaków";
                        if (!System.Text.RegularExpressions.Regex.IsMatch(ShortName, @"^[A-Za-z0-9_-]+$"))
                            return "Skrót może zawierać tylko litery, cyfry, myślniki i podkreślenia";
                        break;

                    case nameof(FullName):
                        if (string.IsNullOrWhiteSpace(FullName))
                            return "Pełna nazwa jest wymagana";
                        if (FullName.Length > 100)
                            return "Pełna nazwa nie może być dłuższa niż 100 znaków";
                        break;

                    case nameof(Description):
                        if (Description?.Length > 500)
                            return "Opis nie może być dłuższy niż 500 znaków";
                        break;

                    case nameof(SortOrder):
                        if (SortOrder < 0)
                            return "Kolejność sortowania nie może być ujemna";
                        break;
                }

                return string.Empty;
            }
        }

        #endregion
    }
} 