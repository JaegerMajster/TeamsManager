using System;
using System.ComponentModel;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.Models.SchoolYearModels
{
    /// <summary>
    /// Model wyświetlania dla roku szkolnego w interfejsie użytkownika
    /// Zawiera właściwości obliczane i implementuje INotifyPropertyChanged
    /// </summary>
    public class SchoolYearDisplayModel : INotifyPropertyChanged
    {
        private SchoolYear _schoolYear;

        public SchoolYearDisplayModel(SchoolYear schoolYear)
        {
            _schoolYear = schoolYear ?? throw new ArgumentNullException(nameof(schoolYear));
        }

        // ===== WŁAŚCIWOŚCI PODSTAWOWE =====
        public string Id => _schoolYear.Id;
        
        public string Name
        {
            get => _schoolYear.Name;
            set
            {
                if (_schoolYear.Name != value)
                {
                    _schoolYear.Name = value;
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public DateTime StartDate
        {
            get => _schoolYear.StartDate;
            set
            {
                if (_schoolYear.StartDate != value)
                {
                    _schoolYear.StartDate = value;
                    OnPropertyChanged(nameof(StartDate));
                    OnPropertyChanged(nameof(Period));
                    OnPropertyChanged(nameof(CompletionPercentage));
                    OnPropertyChanged(nameof(CurrentSemester));
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        public DateTime EndDate
        {
            get => _schoolYear.EndDate;
            set
            {
                if (_schoolYear.EndDate != value)
                {
                    _schoolYear.EndDate = value;
                    OnPropertyChanged(nameof(EndDate));
                    OnPropertyChanged(nameof(Period));
                    OnPropertyChanged(nameof(CompletionPercentage));
                    OnPropertyChanged(nameof(CurrentSemester));
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        public bool IsCurrent
        {
            get => _schoolYear.IsCurrent;
            set
            {
                if (_schoolYear.IsCurrent != value)
                {
                    _schoolYear.IsCurrent = value;
                    OnPropertyChanged(nameof(IsCurrent));
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public string Description
        {
            get => _schoolYear.Description;
            set
            {
                if (_schoolYear.Description != value)
                {
                    _schoolYear.Description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        // ===== WŁAŚCIWOŚCI SEMESTRÓW =====
        public DateTime? FirstSemesterStart
        {
            get => _schoolYear.FirstSemesterStart;
            set
            {
                if (_schoolYear.FirstSemesterStart != value)
                {
                    _schoolYear.FirstSemesterStart = value;
                    OnPropertyChanged(nameof(FirstSemesterStart));
                    OnPropertyChanged(nameof(CurrentSemester));
                    OnPropertyChanged(nameof(HasSemesters));
                }
            }
        }

        public DateTime? FirstSemesterEnd
        {
            get => _schoolYear.FirstSemesterEnd;
            set
            {
                if (_schoolYear.FirstSemesterEnd != value)
                {
                    _schoolYear.FirstSemesterEnd = value;
                    OnPropertyChanged(nameof(FirstSemesterEnd));
                    OnPropertyChanged(nameof(CurrentSemester));
                    OnPropertyChanged(nameof(HasSemesters));
                }
            }
        }

        public DateTime? SecondSemesterStart
        {
            get => _schoolYear.SecondSemesterStart;
            set
            {
                if (_schoolYear.SecondSemesterStart != value)
                {
                    _schoolYear.SecondSemesterStart = value;
                    OnPropertyChanged(nameof(SecondSemesterStart));
                    OnPropertyChanged(nameof(CurrentSemester));
                    OnPropertyChanged(nameof(HasSemesters));
                }
            }
        }

        public DateTime? SecondSemesterEnd
        {
            get => _schoolYear.SecondSemesterEnd;
            set
            {
                if (_schoolYear.SecondSemesterEnd != value)
                {
                    _schoolYear.SecondSemesterEnd = value;
                    OnPropertyChanged(nameof(SecondSemesterEnd));
                    OnPropertyChanged(nameof(CurrentSemester));
                    OnPropertyChanged(nameof(HasSemesters));
                }
            }
        }

        // ===== WŁAŚCIWOŚCI OBLICZANE =====
        
        /// <summary>
        /// Nazwa wyświetlana (z ikoną dla bieżącego roku)
        /// </summary>
        public string DisplayName => IsCurrent ? $"⭐ {Name}" : Name;

        /// <summary>
        /// Okres trwania roku szkolnego w formacie "DD.MM.YYYY - DD.MM.YYYY"
        /// </summary>
        public string Period => $"{StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}";

        /// <summary>
        /// Status roku szkolnego (Bieżący, Przyszły, Przeszły)
        /// </summary>
        public string Status
        {
            get
            {
                if (IsCurrent) return "Bieżący";
                
                var now = DateTime.Now.Date;
                if (now < StartDate.Date) return "Przyszły";
                if (now > EndDate.Date) return "Przeszły";
                return "Aktywny";
            }
        }

        /// <summary>
        /// Kolor statusu
        /// </summary>
        public string StatusColor
        {
            get
            {
                return Status switch
                {
                    "Bieżący" => "#FF4CAF50",    // SuccessGreen
                    "Aktywny" => "#FF0078D4",    // AccentBlue
                    "Przyszły" => "#FFCDDC39",   // AccentLime
                    "Przeszły" => "#FF9E9E9E",   // Gray
                    _ => "#FF9E9E9E"
                };
            }
        }

        /// <summary>
        /// Procent ukończenia roku szkolnego (0-100)
        /// </summary>
        public double CompletionPercentage
        {
            get
            {
                var now = DateTime.Now.Date;
                var totalDays = (EndDate.Date - StartDate.Date).TotalDays;
                
                if (totalDays <= 0) return 0;
                if (now <= StartDate.Date) return 0;
                if (now >= EndDate.Date) return 100;
                
                var completedDays = (now - StartDate.Date).TotalDays;
                return Math.Round((completedDays / totalDays) * 100, 1);
            }
        }

        /// <summary>
        /// Aktualny semestr (jeśli są zdefiniowane)
        /// </summary>
        public string CurrentSemester
        {
            get
            {
                if (!HasSemesters) return "Brak semestrów";
                
                var now = DateTime.Now.Date;
                
                // Sprawdź pierwszy semestr
                if (FirstSemesterStart.HasValue && FirstSemesterEnd.HasValue)
                {
                    if (now >= FirstSemesterStart.Value.Date && now <= FirstSemesterEnd.Value.Date)
                        return "I semestr";
                }
                
                // Sprawdź drugi semestr
                if (SecondSemesterStart.HasValue && SecondSemesterEnd.HasValue)
                {
                    if (now >= SecondSemesterStart.Value.Date && now <= SecondSemesterEnd.Value.Date)
                        return "II semestr";
                }
                
                // Sprawdź okresy między semestrami
                if (FirstSemesterEnd.HasValue && SecondSemesterStart.HasValue)
                {
                    if (now > FirstSemesterEnd.Value.Date && now < SecondSemesterStart.Value.Date)
                        return "Przerwa zimowa";
                }
                
                return "Poza semestrami";
            }
        }

        /// <summary>
        /// Czy rok ma zdefiniowane semestry
        /// </summary>
        public bool HasSemesters => 
            FirstSemesterStart.HasValue || FirstSemesterEnd.HasValue ||
            SecondSemesterStart.HasValue || SecondSemesterEnd.HasValue;

        /// <summary>
        /// Czy rok jest aktywny w danej dacie
        /// </summary>
        public bool IsActive => DateTime.Now.Date >= StartDate.Date && DateTime.Now.Date <= EndDate.Date;

        /// <summary>
        /// Liczba dni do końca roku szkolnego
        /// </summary>
        public int DaysRemaining
        {
            get
            {
                var daysLeft = (EndDate.Date - DateTime.Now.Date).Days;
                return Math.Max(0, daysLeft);
            }
        }

        /// <summary>
        /// Tekst opisujący pozostały czas
        /// </summary>
        public string TimeRemaining
        {
            get
            {
                var days = DaysRemaining;
                if (days == 0) return "Ostatni dzień";
                if (days == 1) return "1 dzień";
                if (days <= 31) return $"{days} dni";
                
                var months = Math.Round(days / 30.0, 1);
                return $"{months} miesięcy";
            }
        }

        // ===== DOSTĘP DO MODELU CORE =====
        public SchoolYear ToSchoolYear() => _schoolYear;

        public void UpdateFromSchoolYear(SchoolYear schoolYear)
        {
            if (schoolYear == null) throw new ArgumentNullException(nameof(schoolYear));
            
            _schoolYear = schoolYear;
            
            // Powiadom o wszystkich zmianach
            OnPropertyChanged(string.Empty); // Odśwież wszystkie właściwości
        }

        // ===== IMPLEMENTACJA INotifyPropertyChanged =====
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 