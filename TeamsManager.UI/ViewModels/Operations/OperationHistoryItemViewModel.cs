using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.ViewModels.Operations
{
    /// <summary>
    /// ViewModel dla pojedynczej operacji w historii
    /// </summary>
    public class OperationHistoryItemViewModel : INotifyPropertyChanged
    {
        private readonly OperationHistory _operationHistory;

        public OperationHistoryItemViewModel(OperationHistory operationHistory)
        {
            _operationHistory = operationHistory ?? throw new ArgumentNullException(nameof(operationHistory));
        }

        public string Id => _operationHistory.Id;
        public DateTime StartTime => _operationHistory.StartedAt;
        public DateTime? EndTime => _operationHistory.CompletedAt;
        public string OperationType => _operationHistory.Type.ToString();
        public string OperationTarget => _operationHistory.TargetEntityType;
        public string TargetId => _operationHistory.TargetEntityId ?? string.Empty;
        public string Status => _operationHistory.Status.ToString();
        public string UserUpn => _operationHistory.CreatedBy ?? "System";
        public string? ErrorMessage => _operationHistory.ErrorMessage;
        public string? OperationDetails => _operationHistory.OperationDetails;
        public int ProcessedItems => _operationHistory.ProcessedItems ?? 0;
        public int TotalItems => _operationHistory.TotalItems ?? 0;

        /// <summary>
        /// Czas trwania operacji
        /// </summary>
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

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
        /// Przyjazna nazwa użytkownika (bez domeny)
        /// </summary>
        public string DisplayUser
        {
            get
            {
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
                                return $"{OperationTarget} ({name})";
                            }
                        }
                    }
                    catch
                    {
                        // Jeśli nie udało się wyciągnąć nazwy, użyj standardowej
                    }
                }
                
                return OperationTarget;
            }
        }

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