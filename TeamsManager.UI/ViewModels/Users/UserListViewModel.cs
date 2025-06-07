using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace TeamsManager.UI.ViewModels.Users
{
    /// <summary>
    /// ViewModel dla widoku listy użytkowników.
    /// Obsługuje ładowanie, filtrowanie, sortowanie i bulk operations.
    /// </summary>
    public class UserListViewModel : INotifyPropertyChanged
    {
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly ILogger<UserListViewModel> _logger;
        
        private ObservableCollection<UserListItemViewModel> _users;
        private CollectionViewSource _usersViewSource;
        private bool _isLoading;
        private string? _errorMessage;
        private string _searchText = string.Empty;
        private UserRole? _selectedRoleFilter;
        private string? _selectedDepartmentId;
        private bool? _activeFilter = true; // null = all, true = active only, false = inactive only
        
        // Paginacja
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;
        private int _totalItems = 0;

        // Bulk selection
        private bool _isAllSelected;

        public UserListViewModel(
            IUserService userService,
            IDepartmentService departmentService,
            ILogger<UserListViewModel> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _users = new ObservableCollection<UserListItemViewModel>();
            _usersViewSource = new CollectionViewSource { Source = _users };
            _usersViewSource.Filter += OnFilter;

            // Initialize commands
            LoadUsersCommand = new RelayCommand(async () => await LoadUsersAsync());
            RefreshCommand = new RelayCommand(async () => await LoadUsersAsync(forceRefresh: true));
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            
            // Pagination commands
            FirstPageCommand = new RelayCommand(() => CurrentPage = 1, () => CurrentPage > 1);
            PreviousPageCommand = new RelayCommand(() => CurrentPage--, () => CurrentPage > 1);
            NextPageCommand = new RelayCommand(() => CurrentPage++, () => CurrentPage < TotalPages);
            LastPageCommand = new RelayCommand(() => CurrentPage = TotalPages, () => CurrentPage < TotalPages);
            
            // Bulk operations
            SelectAllCommand = new RelayCommand(SelectAll);
            DeselectAllCommand = new RelayCommand(DeselectAll);
            ActivateSelectedCommand = new RelayCommand(async () => await ActivateSelectedAsync(), () => SelectedUsers.Any());
            DeactivateSelectedCommand = new RelayCommand(async () => await DeactivateSelectedAsync(), () => SelectedUsers.Any());
            
            // Navigation commands
            ViewUserDetailsCommand = new RelayCommand<UserListItemViewModel>(ViewUserDetails);
            CreateNewUserCommand = new RelayCommand(CreateNewUser);

            // Load initial data
            Task.Run(async () =>
            {
                await LoadDepartmentsAsync();
                await LoadUsersAsync();
            });
        }

        #region Properties

        public ObservableCollection<UserListItemViewModel> Users
        {
            get => _users;
            set
            {
                _users = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView UsersView => _usersViewSource.View;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                UsersView.Refresh();
                UpdatePagination();
            }
        }

        public UserRole? SelectedRoleFilter
        {
            get => _selectedRoleFilter;
            set
            {
                _selectedRoleFilter = value;
                OnPropertyChanged();
                UsersView.Refresh();
                UpdatePagination();
            }
        }

        public string? SelectedDepartmentId
        {
            get => _selectedDepartmentId;
            set
            {
                _selectedDepartmentId = value;
                OnPropertyChanged();
                UsersView.Refresh();
                UpdatePagination();
            }
        }

        public bool? ActiveFilter
        {
            get => _activeFilter;
            set
            {
                _activeFilter = value;
                OnPropertyChanged();
                UsersView.Refresh();
                UpdatePagination();
            }
        }

        // Pagination properties
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value && value > 0 && value <= TotalPages)
                {
                    _currentPage = value;
                    OnPropertyChanged();
                    UpdatePagination();
                    
                    // Update command states
                    (FirstPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (PreviousPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (LastPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value && value > 0)
                {
                    _pageSize = value;
                    OnPropertyChanged();
                    UpdatePagination();
                }
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                _totalPages = value;
                OnPropertyChanged();
            }
        }

        public int TotalItems
        {
            get => _totalItems;
            private set
            {
                _totalItems = value;
                OnPropertyChanged();
            }
        }

        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                _isAllSelected = value;
                OnPropertyChanged();
                
                if (_isAllSelected)
                    SelectAll();
                else
                    DeselectAll();
            }
        }

        // Collections for filters
        public ObservableCollection<Department> Departments { get; } = new();
        public IEnumerable<UserRole> AvailableRoles => Enum.GetValues<UserRole>();
        
        // Computed properties
        public IEnumerable<UserListItemViewModel> SelectedUsers => _users.Where(u => u.IsSelected);
        public int SelectedCount => SelectedUsers.Count();
        public string SelectionText => SelectedCount > 0 ? $"Zaznaczono: {SelectedCount}" : "Brak zaznaczonych";
        public string PaginationInfo => $"Strona {CurrentPage} z {TotalPages} (Razem: {TotalItems})";

        #endregion

        #region Commands

        public ICommand LoadUsersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        
        // Pagination
        public ICommand FirstPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }
        
        // Bulk operations
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand ActivateSelectedCommand { get; }
        public ICommand DeactivateSelectedCommand { get; }
        
        // Navigation
        public ICommand ViewUserDetailsCommand { get; }
        public ICommand CreateNewUserCommand { get; }

        #endregion

        #region Methods

        private async Task LoadUsersAsync(bool forceRefresh = false)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                var users = await _userService.GetAllActiveUsersAsync(forceRefresh);
                
                Users.Clear();
                foreach (var user in users)
                {
                    var userVm = new UserListItemViewModel(user);
                    userVm.PropertyChanged += OnUserSelectionChanged;
                    Users.Add(userVm);
                }

                UpdatePagination();
                _logger.LogInformation("Załadowano {Count} użytkowników", users.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania użytkowników");
                ErrorMessage = "Wystąpił błąd podczas ładowania listy użytkowników.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadDepartmentsAsync()
        {
            try
            {
                var departments = await _departmentService.GetAllDepartmentsAsync();
                
                Departments.Clear();
                foreach (var dept in departments.Where(d => d.IsActive))
                {
                    Departments.Add(dept);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania działów");
            }
        }

        private void OnFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is not UserListItemViewModel user)
            {
                e.Accepted = false;
                return;
            }

            // Active filter
            if (ActiveFilter.HasValue)
            {
                if (ActiveFilter.Value && !user.IsActive)
                {
                    e.Accepted = false;
                    return;
                }
                if (!ActiveFilter.Value && user.IsActive)
                {
                    e.Accepted = false;
                    return;
                }
            }

            // Role filter
            if (SelectedRoleFilter.HasValue && user.Role != SelectedRoleFilter.Value)
            {
                e.Accepted = false;
                return;
            }

            // Department filter
            if (!string.IsNullOrEmpty(SelectedDepartmentId) && user.Model.DepartmentId != SelectedDepartmentId)
            {
                e.Accepted = false;
                return;
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                e.Accepted = user.FullName.ToLower().Contains(searchLower) ||
                           user.Email.ToLower().Contains(searchLower) ||
                           (user.Position?.ToLower().Contains(searchLower) ?? false);
                return;
            }

            e.Accepted = true;
        }

        private void UpdatePagination()
        {
            // Get filtered items count
            var filteredItems = UsersView.Cast<UserListItemViewModel>().ToList();
            TotalItems = filteredItems.Count;
            TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
            
            // Ensure current page is valid
            if (CurrentPage > TotalPages && TotalPages > 0)
                CurrentPage = TotalPages;
            else if (CurrentPage < 1 && TotalPages > 0)
                CurrentPage = 1;

            OnPropertyChanged(nameof(PaginationInfo));
        }

        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedRoleFilter = null;
            SelectedDepartmentId = null;
            ActiveFilter = true;
            CurrentPage = 1;
        }

        private void SelectAll()
        {
            foreach (var user in UsersView.Cast<UserListItemViewModel>())
            {
                user.IsSelected = true;
            }
        }

        private void DeselectAll()
        {
            foreach (var user in Users)
            {
                user.IsSelected = false;
            }
        }

        private async Task ActivateSelectedAsync()
        {
            try
            {
                IsLoading = true;
                var selectedIds = SelectedUsers.Select(u => u.Id).ToList();
                
                // W rzeczywistej implementacji użyj batch operation
                foreach (var userId in selectedIds)
                {
                    await _userService.ActivateUserAsync(userId, string.Empty); // Token będzie dostarczony przez HTTP context
                }

                await LoadUsersAsync(forceRefresh: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktywacji użytkowników");
                ErrorMessage = "Wystąpił błąd podczas aktywacji wybranych użytkowników.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeactivateSelectedAsync()
        {
            try
            {
                IsLoading = true;
                var selectedIds = SelectedUsers.Select(u => u.Id).ToList();
                
                foreach (var userId in selectedIds)
                {
                    await _userService.DeactivateUserAsync(userId, string.Empty);
                }

                await LoadUsersAsync(forceRefresh: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas deaktywacji użytkowników");
                ErrorMessage = "Wystąpił błąd podczas deaktywacji wybranych użytkowników.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void ViewUserDetails(UserListItemViewModel? user)
        {
            if (user == null) return;
            
            try
            {
                _logger.LogInformation("Opening user details for: {UserId}", user.Id);
                
                // Get UserDetailWindow from DI
                var serviceProvider = App.ServiceProvider;
                if (serviceProvider == null)
                {
                    _logger.LogError("ServiceProvider not available");
                    return;
                }

                var userDetailWindow = serviceProvider.GetRequiredService<Views.Users.UserDetailWindow>();
                userDetailWindow.Owner = System.Windows.Application.Current.MainWindow;
                
                // Initialize for editing existing user
                await userDetailWindow.InitializeAsync(user.Id);
                
                var result = userDetailWindow.ShowDialog();
                if (result == true)
                {
                    // Refresh the user list after successful edit
                    await LoadUsersAsync(forceRefresh: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening user details for user {UserId}", user.Id);
                ErrorMessage = "Nie udało się otworzyć szczegółów użytkownika.";
            }
        }

        private async void CreateNewUser()
        {
            try
            {
                _logger.LogInformation("Opening create new user dialog");
                
                // Get UserDetailWindow from DI
                var serviceProvider = App.ServiceProvider;
                if (serviceProvider == null)
                {
                    _logger.LogError("ServiceProvider not available");
                    return;
                }

                var userDetailWindow = serviceProvider.GetRequiredService<Views.Users.UserDetailWindow>();
                userDetailWindow.Owner = System.Windows.Application.Current.MainWindow;
                
                // Initialize for creating new user (no userId parameter)
                await userDetailWindow.InitializeAsync();
                
                var result = userDetailWindow.ShowDialog();
                if (result == true)
                {
                    // Refresh the user list after successful creation
                    await LoadUsersAsync(forceRefresh: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening create user dialog");
                ErrorMessage = "Nie udało się otworzyć formularza tworzenia użytkownika.";
            }
        }

        private void OnUserSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserListItemViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(SelectionText));
                
                // Update IsAllSelected state
                var allSelected = Users.All(u => u.IsSelected);
                if (_isAllSelected != allSelected)
                {
                    _isAllSelected = allSelected;
                    OnPropertyChanged(nameof(IsAllSelected));
                }
                
                // Update command states
                (ActivateSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeactivateSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
} 