using System.ComponentModel;

namespace TeamsManager.UI.Models.Import
{
    /// <summary>
    /// Model typu danych importu dla UI
    /// </summary>
    public class ImportDataTypeModel
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKind { get; set; } = string.Empty;
        public TeamsManager.Core.Abstractions.Services.ImportDataType Type { get; set; }
        public string[] SupportedFormats { get; set; } = Array.Empty<string>();
        public string[] RequiredColumns { get; set; } = Array.Empty<string>();
        public string SampleFileName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model kolumny dla mapowania
    /// </summary>
    public class ColumnMappingModel : INotifyPropertyChanged
    {
        private string _targetField = string.Empty;
        private bool _isRequired;
        private bool _isMapped;

        public string SourceColumn { get; set; } = string.Empty;
        public string SampleValue { get; set; } = string.Empty;
        
        public string TargetField
        {
            get => _targetField;
            set
            {
                _targetField = value;
                IsMapped = !string.IsNullOrEmpty(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMapped));
            }
        }

        public bool IsRequired
        {
            get => _isRequired;
            set
            {
                _isRequired = value;
                OnPropertyChanged();
            }
        }

        public bool IsMapped
        {
            get => _isMapped;
            private set
            {
                _isMapped = value;
                OnPropertyChanged();
            }
        }

        public string FieldDescription { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Model wiersza walidacji dla UI
    /// </summary>
    public class ValidationItemModel
    {
        public int RowNumber { get; set; }
        public string ColumnName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ValidationType { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public bool IsWarning { get; set; }
        public bool CanFix { get; set; }
        
        public string IconKind => IsError ? "AlertCircle" : "Alert";
        public string IconColor => IsError ? "AccentRed" : "WarningOrange";
    }

    /// <summary>
    /// Model podglądu danych
    /// </summary>
    public class PreviewRowModel
    {
        public Dictionary<string, object> Values { get; set; } = new();
        public int RowNumber { get; set; }
        public bool HasErrors { get; set; }
        public bool HasWarnings { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
    }
} 