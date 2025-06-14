using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeamsManager.Core.Models;
using TeamsManager.Core.Extensions;

namespace TeamsManager.UI.ViewModels.Operations
{
    /// <summary>
    /// ViewModel dla pojedynczej operacji w historii
    /// </summary>
    public class OperationHistoryItemViewModel : INotifyPropertyChanged
    {
        private readonly OperationHistory _operationHistory;
        private readonly string? _userDisplayName;

        public OperationHistoryItemViewModel(OperationHistory operationHistory, string? userDisplayName = null)
        {
            _operationHistory = operationHistory ?? throw new ArgumentNullException(nameof(operationHistory));
            _userDisplayName = userDisplayName;
        }

        public string Id => _operationHistory.Id;
        public DateTime StartTime => _operationHistory.StartedAt;
        public DateTime? EndTime => _operationHistory.CompletedAt;
        
        /// <summary>
        /// Czas rozpoczęcia w lokalnej strefie czasowej
        /// </summary>
        public DateTime LocalStartTime => _operationHistory.StartedAt.ToLocalTime();
        
        /// <summary>
        /// Czas zakończenia w lokalnej strefie czasowej
        /// </summary>
        public DateTime? LocalEndTime => _operationHistory.CompletedAt?.ToLocalTime();
        
        /// <summary>
        /// Sformatowany czas rozpoczęcia
        /// </summary>
        public string FormattedStartTime => LocalStartTime.ToString("dd.MM.yyyy, HH:mm:ss");
        
        /// <summary>
        /// Sformatowany czas zakończenia
        /// </summary>
        public string FormattedEndTime => LocalEndTime?.ToString("dd.MM.yyyy, HH:mm:ss") ?? "-";
        
        /// <summary>
        /// Czas trwania operacji
        /// </summary>
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
        
        /// <summary>
        /// Sformatowany czas trwania
        /// </summary>
        public string FormattedDuration
        {
            get
            {
                if (!Duration.HasValue) return "-";
                
                var duration = Duration.Value;
                if (duration.TotalSeconds < 1)
                    return $"{duration.TotalMilliseconds:F0} ms";
                if (duration.TotalMinutes < 1)
                    return $"{duration.TotalSeconds:F1} s";
                if (duration.TotalHours < 1)
                    return $"{duration.TotalMinutes:F1} min";
                
                return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
        }
        
        public string OperationType => _operationHistory.Type.ToString();
        public string OperationTarget => _operationHistory.TargetEntityType;
        public string TargetId => _operationHistory.TargetEntityId ?? string.Empty;
        public string Status => _operationHistory.Status.ToString();
        
        /// <summary>
        /// Status w języku polskim
        /// </summary>
        public string PolishStatus => _operationHistory.Status.ToPolishString();
        
        /// <summary>
        /// Typ operacji w języku polskim
        /// </summary>
        public string PolishOperationType => _operationHistory.Type.ToPolishString();
        public string UserUpn => _operationHistory.CreatedBy ?? "System";
        public string? ErrorMessage => _operationHistory.ErrorMessage;
        public string? OperationDetails => _operationHistory.OperationDetails;
        public int ProcessedItems => _operationHistory.ProcessedItems ?? 0;
        public int TotalItems => _operationHistory.TotalItems ?? 0;

        /// <summary>
        /// Czy operacja jest w toku
        /// </summary>
        public bool IsInProgress => Status == "InProgress";

        /// <summary>
        /// Czy operacja się powiodła
        /// </summary>
        public bool IsSuccess => Status == "Completed";

        /// <summary>
        /// Czy operacja nie powiodła się
        /// </summary>
        public bool IsFailed => Status == "Failed";

        /// <summary>
        /// Czy operacja zakończyła się częściowym sukcesem
        /// </summary>
        public bool IsPartialSuccess => Status == "PartialSuccess";

        /// <summary>
        /// Procent postępu dla operacji batch
        /// </summary>
        public double ProgressPercentage
        {
            get
            {
                if (TotalItems == 0) return 0;
                return (double)ProcessedItems / TotalItems * 100;
            }
        }

        /// <summary>
        /// Przyjazna nazwa użytkownika (DisplayName lub bez domeny)
        /// </summary>
        public string DisplayUser
        {
            get
            {
                // Jeśli mamy DisplayName, użyj go
                if (!string.IsNullOrEmpty(_userDisplayName))
                    return _userDisplayName;
                
                // Fallback do poprzedniej logiki
                if (string.IsNullOrEmpty(UserUpn)) return "System";
                var atIndex = UserUpn.IndexOf('@');
                return atIndex > 0 ? UserUpn.Substring(0, atIndex) : UserUpn;
            }
        }

        /// <summary>
        /// Czytelna nazwa celu operacji
        /// </summary>
        public string DisplayTarget
        {
            get
            {
                if (string.IsNullOrEmpty(OperationTarget)) return "N/A";
                
                // Przetłumacz typ encji na polski
                var polishEntityType = OperationTarget.ToPolishEntityType();
                
                // Jeśli mamy szczegóły, spróbuj wyciągnąć nazwę z JSON
                if (!string.IsNullOrEmpty(OperationDetails))
                {
                    try
                    {
                        // Prosta ekstrakcja nazwy z JSON (bez parsowania)
                        if (OperationDetails.Contains("\"Name\":"))
                        {
                            var nameStart = OperationDetails.IndexOf("\"Name\":\"") + 8;
                            var nameEnd = OperationDetails.IndexOf("\"", nameStart);
                            if (nameEnd > nameStart)
                            {
                                var name = OperationDetails.Substring(nameStart, nameEnd - nameStart);
                                return $"{polishEntityType} ({name})";
                            }
                        }
                    }
                    catch
                    {
                        // Jeśli nie udało się wyciągnąć nazwy, użyj standardowej
                    }
                }
                
                return polishEntityType;
            }
        }

        /// <summary>
        /// Czytelny opis operacji z nazwą encji
        /// </summary>
        public string OperationDescription
        {
            get
            {
                var operationName = GetOperationTypeDescription();
                var entityName = GetEntityDisplayName();
                
                if (!string.IsNullOrEmpty(entityName))
                {
                    return $"{operationName}: {entityName}";
                }
                
                return operationName;
            }
        }

        /// <summary>
        /// Pobiera czytelną nazwę encji
        /// </summary>
        private string GetEntityDisplayName()
        {
            // Najpierw spróbuj użyć TargetEntityName jeśli jest dostępne
            if (!string.IsNullOrEmpty(_operationHistory.TargetEntityName))
            {
                return _operationHistory.TargetEntityName;
            }
            
            // Jeśli mamy szczegóły, spróbuj wyciągnąć nazwę z JSON
            if (!string.IsNullOrEmpty(OperationDetails))
            {
                try
                {
                    // Prosta ekstrakcja nazwy z JSON (bez parsowania)
                    if (OperationDetails.Contains("\"Name\":"))
                    {
                        var nameStart = OperationDetails.IndexOf("\"Name\":\"") + 8;
                        var nameEnd = OperationDetails.IndexOf("\"", nameStart);
                        if (nameEnd > nameStart)
                        {
                            return OperationDetails.Substring(nameStart, nameEnd - nameStart);
                        }
                    }
                }
                catch
                {
                    // Jeśli nie udało się wyciągnąć nazwy, kontynuuj
                }
            }
            
            // Jako ostateczność użyj typu encji
            return OperationTarget;
        }

        /// <summary>
        /// Zwraca czytelny opis typu operacji
        /// </summary>
        private string GetOperationTypeDescription() => _operationHistory.Type.ToPolishString();

        /// <summary>
        /// Względny czas rozpoczęcia operacji
        /// </summary>
        public string RelativeStartTime
        {
            get
            {
                var timeSpan = DateTime.Now - StartTime;
                
                if (timeSpan.TotalMinutes < 1)
                    return "Przed chwilą";
                if (timeSpan.TotalMinutes < 60)
                    return $"{(int)timeSpan.TotalMinutes} min temu";
                if (timeSpan.TotalHours < 24)
                    return $"{(int)timeSpan.TotalHours} godz temu";
                if (timeSpan.TotalDays < 7)
                    return $"{(int)timeSpan.TotalDays} dni temu";
                
                return StartTime.ToString("dd.MM.yyyy HH:mm");
            }
        }

        /// <summary>
        /// Czy operacja ma informacje o postępie
        /// </summary>
        public bool HasProgress => TotalItems > 0;

        /// <summary>
        /// Czy operacja ma błąd
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Czy operacja ma szczegóły
        /// </summary>
        public bool HasDetails => !string.IsNullOrEmpty(OperationDetails);

        /// <summary>
        /// Oryginalny model OperationHistory
        /// </summary>
        public OperationHistory OriginalModel => _operationHistory;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 