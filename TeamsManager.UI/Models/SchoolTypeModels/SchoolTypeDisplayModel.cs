using System;
using System.ComponentModel;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.Models.SchoolTypeModels
{
    /// <summary>
    /// Model wyświetlania typu szkoły w UI z obsługą INotifyPropertyChanged
    /// </summary>
    public class SchoolTypeDisplayModel : INotifyPropertyChanged
    {
        private SchoolType _schoolType;
        private bool _isSelected;
        private bool _isEditing;

        public SchoolTypeDisplayModel(SchoolType schoolType)
        {
            _schoolType = schoolType ?? throw new ArgumentNullException(nameof(schoolType));
        }

        // Właściwości z modelu domenowego
        public string Id => _schoolType.Id;
        public string ShortName => _schoolType.ShortName;
        public string FullName => _schoolType.FullName;
        public string Description => _schoolType.Description;
        public string? ColorCode => _schoolType.ColorCode;
        public int SortOrder => _schoolType.SortOrder;
        public string DisplayName => _schoolType.DisplayName;
        public int ActiveTeamsCount => _schoolType.ActiveTeamsCount;
        public int AssignedTeachersCount => _schoolType.AssignedTeachersCount;

        // Właściwości UI
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (_isEditing != value)
                {
                    _isEditing = value;
                    OnPropertyChanged(nameof(IsEditing));
                }
            }
        }

        // Metoda pomocnicza do aktualizacji
        public void UpdateFromSchoolType(SchoolType schoolType)
        {
            _schoolType = schoolType ?? throw new ArgumentNullException(nameof(schoolType));
            
            // Powiadom o zmianie wszystkich właściwości
            OnPropertyChanged(string.Empty);
        }

        public SchoolType ToSchoolType() => _schoolType;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 