using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeamsManager.UI.Models.Teams
{
    /// <summary>
    /// Model do przechowywania wartości placeholdera szablonu w wizardzie.
    /// </summary>
    public class TemplateValueViewModel : INotifyPropertyChanged
    {
        private string _value = string.Empty;
        
        public string Placeholder { get; set; } = string.Empty;
        
        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    ValueChanged?.Invoke();
                }
            }
        }
        
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; } = true;
        
        public event System.Action? ValueChanged;
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 