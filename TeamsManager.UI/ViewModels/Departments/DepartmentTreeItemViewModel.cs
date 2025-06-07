using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.ViewModels.Departments
{
    /// <summary>
    /// ViewModel dla pojedynczego elementu w TreeView działów
    /// </summary>
    public class DepartmentTreeItemViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private Department _department;

        public DepartmentTreeItemViewModel(Department department, DepartmentTreeItemViewModel? parent = null)
        {
            Department = department ?? throw new ArgumentNullException(nameof(department));
            Parent = parent;
            Children = new ObservableCollection<DepartmentTreeItemViewModel>();
        }

        public Department Department
        {
            get => _department;
            set
            {
                _department = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(Code));
                OnPropertyChanged(nameof(Location));
                OnPropertyChanged(nameof(Email));
                OnPropertyChanged(nameof(Phone));
            }
        }

        public string Id => Department.Id;
        public string Name => Department.Name;
        public string Description => Department.Description;
        public string? Code => Department.DepartmentCode;
        public string? Location => Department.Location;
        public string? Email => Department.Email;
        public string? Phone => Department.Phone;
        public int HierarchyLevel => Department.HierarchyLevel;
        public string FullPath => Department.FullPath;

        public DepartmentTreeItemViewModel? Parent { get; }
        public ObservableCollection<DepartmentTreeItemViewModel> Children { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasChildren => Children.Count > 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 