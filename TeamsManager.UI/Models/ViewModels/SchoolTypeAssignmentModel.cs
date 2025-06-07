using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeamsManager.UI.Models.ViewModels
{
    /// <summary>
    /// Model danych dla pojedynczego przypisania nauczyciela do typu szkoły.
    /// Służy jako DTO dla widoku z walidacją i właściwościami UI.
    /// </summary>
    public class SchoolTypeAssignmentModel : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _schoolTypeId = string.Empty;
        private string _schoolTypeName = string.Empty;
        private string _schoolTypeShortName = string.Empty;
        private string _schoolTypeColor = "#0078D4";
        private decimal _workloadPercentage = 20;
        private DateTime _assignedDate = DateTime.Now;
        private DateTime? _endDate;
        private string _notes = string.Empty;
        private bool _isCurrentlyActive = true;
        private bool _isModified = false;

        /// <summary>
        /// Identyfikator przypisania
        /// </summary>
        public string Id 
        { 
            get => _id; 
            set { _id = value; OnPropertyChanged(); } 
        }

        /// <summary>
        /// Identyfikator typu szkoły
        /// </summary>
        public string SchoolTypeId 
        { 
            get => _schoolTypeId; 
            set { _schoolTypeId = value; OnPropertyChanged(); } 
        }

        /// <summary>
        /// Nazwa typu szkoły
        /// </summary>
        public string SchoolTypeName 
        { 
            get => _schoolTypeName; 
            set { _schoolTypeName = value; OnPropertyChanged(); } 
        }

        /// <summary>
        /// Skrót nazwy typu szkoły
        /// </summary>
        public string SchoolTypeShortName 
        { 
            get => _schoolTypeShortName; 
            set { _schoolTypeShortName = value; OnPropertyChanged(); } 
        }

        /// <summary>
        /// Kolor typu szkoły w formacie hex
        /// </summary>
        public string SchoolTypeColor 
        { 
            get => _schoolTypeColor; 
            set { _schoolTypeColor = value ?? "#0078D4"; OnPropertyChanged(); } 
        }

        /// <summary>
        /// Procent etatu (0-100%)
        /// </summary>
        public decimal WorkloadPercentage 
        { 
            get => _workloadPercentage; 
            set 
            { 
                _workloadPercentage = Math.Max(0, Math.Min(100, value)); 
                IsModified = true;
                OnPropertyChanged(); 
            } 
        }

        /// <summary>
        /// Data rozpoczęcia przypisania
        /// </summary>
        public DateTime AssignedDate 
        { 
            get => _assignedDate; 
            set 
            { 
                _assignedDate = value; 
                IsModified = true;
                OnPropertyChanged(); 
            } 
        }

        /// <summary>
        /// Data zakończenia przypisania (opcjonalna)
        /// </summary>
        public DateTime? EndDate 
        { 
            get => _endDate; 
            set 
            { 
                _endDate = value; 
                IsModified = true;
                OnPropertyChanged(); 
            } 
        }

        /// <summary>
        /// Notatki dotyczące przypisania
        /// </summary>
        public string Notes 
        { 
            get => _notes; 
            set 
            { 
                _notes = value ?? string.Empty; 
                IsModified = true;
                OnPropertyChanged(); 
            } 
        }

        /// <summary>
        /// Czy przypisanie jest obecnie aktywne
        /// </summary>
        public bool IsCurrentlyActive 
        { 
            get => _isCurrentlyActive; 
            set { _isCurrentlyActive = value; OnPropertyChanged(); } 
        }

        /// <summary>
        /// Czy model został zmodyfikowany i wymaga zapisu
        /// </summary>
        public bool IsModified 
        { 
            get => _isModified; 
            set { _isModified = value; OnPropertyChanged(); } 
        }

        /// <summary>
        /// Sprawdza czy model jest prawidłowy
        /// </summary>
        public bool IsValid => 
            WorkloadPercentage > 0 && 
            WorkloadPercentage <= 100 && 
            (!EndDate.HasValue || EndDate.Value > AssignedDate);

        /// <summary>
        /// Nazwa wyświetlana typu szkoły dla UI
        /// </summary>
        public string DisplayName => string.IsNullOrEmpty(SchoolTypeShortName) 
            ? SchoolTypeName 
            : $"{SchoolTypeShortName} - {SchoolTypeName}";

        /// <summary>
        /// Tekstowa reprezentacja okresu przypisania
        /// </summary>
        public string PeriodText
        {
            get
            {
                if (EndDate.HasValue)
                    return $"{AssignedDate:dd.MM.yyyy} - {EndDate.Value:dd.MM.yyyy}";
                return $"od {AssignedDate:dd.MM.yyyy}";
            }
        }

        /// <summary>
        /// Czy to nowe przypisanie (nie zapisane w bazie)
        /// </summary>
        public bool IsNewAssignment => Guid.TryParse(Id, out _);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Resetuje flagę modyfikacji
        /// </summary>
        public void ResetModified()
        {
            IsModified = false;
        }

        /// <summary>
        /// Tworzy kopię modelu
        /// </summary>
        public SchoolTypeAssignmentModel Clone()
        {
            return new SchoolTypeAssignmentModel
            {
                Id = Id,
                SchoolTypeId = SchoolTypeId,
                SchoolTypeName = SchoolTypeName,
                SchoolTypeShortName = SchoolTypeShortName,
                SchoolTypeColor = SchoolTypeColor,
                WorkloadPercentage = WorkloadPercentage,
                AssignedDate = AssignedDate,
                EndDate = EndDate,
                Notes = Notes,
                IsCurrentlyActive = IsCurrentlyActive,
                IsModified = false
            };
        }
    }
} 