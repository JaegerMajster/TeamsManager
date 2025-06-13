using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.ViewModels.OrganizationalUnits
{
    /// <summary>
    /// ViewModel dla pojedynczego elementu w TreeView jednostek organizacyjnych
    /// </summary>
    public class OrganizationalUnitTreeItemViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private OrganizationalUnit? _organizationalUnit;
        private Department? _department;

        public OrganizationalUnitTreeItemViewModel(OrganizationalUnit organizationalUnit, OrganizationalUnitTreeItemViewModel? parent = null)
        {
            _organizationalUnit = organizationalUnit ?? throw new ArgumentNullException(nameof(organizationalUnit));
            _department = null;
            Parent = parent;
            Children = new ObservableCollection<OrganizationalUnitTreeItemViewModel>();
            Departments = new ObservableCollection<Department>();
        }

        public OrganizationalUnitTreeItemViewModel(Department department, OrganizationalUnitTreeItemViewModel? parent = null)
        {
            _department = department ?? throw new ArgumentNullException(nameof(department));
            _organizationalUnit = null;
            Parent = parent;
            Children = new ObservableCollection<OrganizationalUnitTreeItemViewModel>();
            Departments = new ObservableCollection<Department>();
        }

        public OrganizationalUnit? OrganizationalUnit
        {
            get => _organizationalUnit;
            set
            {
                _organizationalUnit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(Level));
                OnPropertyChanged(nameof(FullPath));
                OnPropertyChanged(nameof(SortOrder));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsRootUnit));
                OnPropertyChanged(nameof(IsDepartment));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(IconKind));
                OnPropertyChanged(nameof(LevelText));
            }
        }

        public Department? Department
        {
            get => _department;
            set
            {
                _department = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Description));
                OnPropertyChanged(nameof(SortOrder));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(IsDepartment));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(IconKind));
            }
        }

        // Właściwości uniwersalne
        public bool IsDepartment => _department != null;
        public string Id => IsDepartment ? _department!.Id : _organizationalUnit!.Id;
        public string Name => IsDepartment ? _department!.Name : _organizationalUnit!.Name;
        public string? Description => IsDepartment ? _department!.Description : _organizationalUnit!.Description;
        public int SortOrder => IsDepartment ? _department!.SortOrder : _organizationalUnit!.SortOrder;
        public bool IsActive => IsDepartment ? _department!.IsActive : _organizationalUnit!.IsActive;
        
        // Właściwości specyficzne dla jednostek organizacyjnych
        public int Level => IsDepartment ? 0 : _organizationalUnit!.Level;
        public string FullPath => IsDepartment ? _department!.Name : _organizationalUnit!.FullPath;
        public bool IsRootUnit => IsDepartment ? false : _organizationalUnit!.IsRootUnit;

        // Właściwości dla UI
        public string DisplayName => IsDepartment ? _department!.Name : _organizationalUnit!.Name;
        public string IconKind => IsDepartment ? "Domain" : (IsRootUnit ? "FileTree" : "FolderOutline");
        public string IconColor => IsActive ? "#2196F3" : "#9E9E9E";
        public string? LevelText => IsDepartment ? null : $"Poziom {Level}";

        public OrganizationalUnitTreeItemViewModel? Parent { get; }
        public ObservableCollection<OrganizationalUnitTreeItemViewModel> Children { get; }
        public ObservableCollection<Department> Departments { get; }

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

        /// <summary>
        /// Czy ma podjednostki
        /// </summary>
        public bool HasChildren => Children.Count > 0;

        /// <summary>
        /// Czy ma działy
        /// </summary>
        public bool HasDepartments => Departments.Count > 0;

        /// <summary>
        /// Łączna liczba elementów podrzędnych (podjednostki + działy)
        /// </summary>
        public int TotalChildrenCount => Children.Count + Departments.Count;

        /// <summary>
        /// Tekst wyświetlany w drzewie
        /// </summary>
        public string DisplayText => IsDepartment ? Name : $"{Name} ({TotalChildrenCount})";

        /// <summary>
        /// Ikona dla jednostki organizacyjnej (stara właściwość dla kompatybilności)
        /// </summary>
        public string Icon => IconKind;

        /// <summary>
        /// Tooltip z informacjami o jednostce/dziale
        /// </summary>
        public string ToolTip => IsDepartment 
            ? $"Dział: {Name}\n" +
              $"Status: {(IsActive ? "Aktywny" : "Nieaktywny")}"
            : $"Jednostka: {Name}\n" +
              $"Poziom: {Level}\n" +
              $"Ścieżka: {FullPath}\n" +
              $"Podjednostki: {Children.Count}\n" +
              $"Działy: {Departments.Count}\n" +
              $"Status: {(IsActive ? "Aktywna" : "Nieaktywna")}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Dodaje podjednostkę
        /// </summary>
        public void AddChild(OrganizationalUnitTreeItemViewModel child)
        {
            if (child == null) return;
            
            Children.Add(child);
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(TotalChildrenCount));
            OnPropertyChanged(nameof(DisplayText));
        }

        /// <summary>
        /// Usuwa podjednostkę
        /// </summary>
        public void RemoveChild(OrganizationalUnitTreeItemViewModel child)
        {
            if (child == null) return;
            
            Children.Remove(child);
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(TotalChildrenCount));
            OnPropertyChanged(nameof(DisplayText));
        }

        /// <summary>
        /// Dodaje dział
        /// </summary>
        public void AddDepartment(Department department)
        {
            if (department == null) return;
            
            Departments.Add(department);
            OnPropertyChanged(nameof(HasDepartments));
            OnPropertyChanged(nameof(TotalChildrenCount));
            OnPropertyChanged(nameof(DisplayText));
        }

        /// <summary>
        /// Usuwa dział
        /// </summary>
        public void RemoveDepartment(Department department)
        {
            if (department == null) return;
            
            Departments.Remove(department);
            OnPropertyChanged(nameof(HasDepartments));
            OnPropertyChanged(nameof(TotalChildrenCount));
            OnPropertyChanged(nameof(DisplayText));
        }

        /// <summary>
        /// Rozwiń wszystkie podjednostki
        /// </summary>
        public void ExpandAll()
        {
            IsExpanded = true;
            foreach (var child in Children)
            {
                child.ExpandAll();
            }
        }

        /// <summary>
        /// Zwiń wszystkie podjednostki
        /// </summary>
        public void CollapseAll()
        {
            IsExpanded = false;
            foreach (var child in Children)
            {
                child.CollapseAll();
            }
        }
    }
} 