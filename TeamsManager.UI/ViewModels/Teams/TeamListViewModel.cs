using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.UI.Models.Teams;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.ViewModels.Shell;
using TeamsManager.UI.Views.Teams;
using System.Windows;

namespace TeamsManager.UI.ViewModels.Teams
{
    public class TeamListViewModel : BaseViewModel
    {
        private readonly ITeamService _teamService;
        private readonly ISchoolTypeService _schoolTypeService;
        private readonly ISchoolYearService _schoolYearService;
        private readonly ICurrentUserService _currentUserService;
        private readonly INotificationService _notificationService;
        private readonly MainShellViewModel _mainShellViewModel;

        // Kolekcje
        private ObservableCollection<TeamGrouping> _teamGroups = new();
        private ObservableCollection<SchoolType> _schoolTypes = new();
        private ObservableCollection<SchoolYear> _schoolYears = new();
        
        // Filtry
        private TeamStatus? _selectedStatus = TeamStatus.Active;
        private SchoolType? _selectedSchoolType;
        private SchoolYear? _selectedSchoolYear;
        private string _searchText = string.Empty;
        
        // Stan UI
        private bool _isLoading;
        private bool _hasNoTeams;
        private Team? _selectedTeam;
        
        // Właściwości publiczne
        public ObservableCollection<TeamGrouping> TeamGroups
        {
            get => _teamGroups;
            set => SetProperty(ref _teamGroups, value);
        }
        
        public ObservableCollection<SchoolType> SchoolTypes
        {
            get => _schoolTypes;
            set => SetProperty(ref _schoolTypes, value);
        }
        
        public ObservableCollection<SchoolYear> SchoolYears
        {
            get => _schoolYears;
            set => SetProperty(ref _schoolYears, value);
        }
        
        public TeamStatus? SelectedStatus
        {
            get => _selectedStatus;
            set 
            { 
                if (SetProperty(ref _selectedStatus, value))
                {
                    _ = LoadTeamsAsync();
                }
            }
        }
        
        public SchoolType? SelectedSchoolType
        {
            get => _selectedSchoolType;
            set 
            { 
                if (SetProperty(ref _selectedSchoolType, value))
                {
                    _ = LoadTeamsAsync();
                }
            }
        }
        
        public SchoolYear? SelectedSchoolYear
        {
            get => _selectedSchoolYear;
            set 
            { 
                if (SetProperty(ref _selectedSchoolYear, value))
                {
                    _ = LoadTeamsAsync();
                }
            }
        }
        
        public string SearchText
        {
            get => _searchText;
            set 
            { 
                if (SetProperty(ref _searchText, value))
                {
                    _ = LoadTeamsAsync();
                }
            }
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        public bool HasNoTeams
        {
            get => _hasNoTeams;
            set => SetProperty(ref _hasNoTeams, value);
        }
        
        public Team? SelectedTeam
        {
            get => _selectedTeam;
            set => SetProperty(ref _selectedTeam, value);
        }
        
        // Komendy
        public ICommand LoadTeamsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CreateTeamCommand { get; }
        public ICommand ArchiveTeamCommand { get; }
        public ICommand RestoreTeamCommand { get; }
        public ICommand EditTeamCommand { get; }
        public ICommand DeleteTeamCommand { get; }
        public ICommand ShowLifecycleOperationsCommand { get; }
        
        public TeamListViewModel(
            ITeamService teamService,
            ISchoolTypeService schoolTypeService,
            ISchoolYearService schoolYearService,
            ICurrentUserService currentUserService,
            INotificationService notificationService,
            MainShellViewModel mainShellViewModel)
        {
            _teamService = teamService;
            _schoolTypeService = schoolTypeService;
            _schoolYearService = schoolYearService;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
            _mainShellViewModel = mainShellViewModel ?? throw new ArgumentNullException(nameof(mainShellViewModel));
            
            // Inicjalizacja komend
            LoadTeamsCommand = new RelayCommand(async () => await LoadTeamsAsync());
            RefreshCommand = new RelayCommand(async () => await LoadTeamsAsync(forceRefresh: true));
            ArchiveTeamCommand = new RelayCommand<Team>(async (team) => await ArchiveTeamAsync(team));
            RestoreTeamCommand = new RelayCommand<Team>(async (team) => await RestoreTeamAsync(team));
            EditTeamCommand = new RelayCommand<Team>(EditTeam);
            DeleteTeamCommand = new RelayCommand<Team>(async (team) => await DeleteTeamAsync(team));
            CreateTeamCommand = new RelayCommand(CreateNewTeam);
            ShowLifecycleOperationsCommand = new RelayCommand(ShowLifecycleOperations);
        }
        
        public async Task InitializeAsync()
        {
            await LoadFiltersAsync();
            await LoadTeamsAsync();
        }
        
        private async Task LoadFiltersAsync()
        {
            try
            {
                var schoolTypes = await _schoolTypeService.GetAllActiveSchoolTypesAsync();
                SchoolTypes = new ObservableCollection<SchoolType>(schoolTypes);
                
                var schoolYears = await _schoolYearService.GetAllActiveSchoolYearsAsync();
                SchoolYears = new ObservableCollection<SchoolYear>(schoolYears);
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd ładowania filtrów: {ex.Message}",
                    "error"
                );
            }
        }
        
        private async Task LoadTeamsAsync(bool forceRefresh = false)
        {
            try
            {
                IsLoading = true;
                TeamGroups.Clear();
                
                var accessToken = await GetAccessTokenAsync();
                
                // Pobierz zespoły według statusu
                var teams = _selectedStatus == TeamStatus.Archived
                    ? await _teamService.GetArchivedTeamsAsync(forceRefresh, accessToken)
                    : await _teamService.GetActiveTeamsAsync(forceRefresh, accessToken);
                
                // Zastosuj filtry
                var filteredTeams = teams.AsEnumerable();
                
                if (_selectedSchoolType != null)
                    filteredTeams = filteredTeams.Where(t => t.SchoolTypeId == _selectedSchoolType.Id);
                    
                if (_selectedSchoolYear != null)
                    filteredTeams = filteredTeams.Where(t => t.SchoolYearId == _selectedSchoolYear.Id);
                    
                if (!string.IsNullOrWhiteSpace(_searchText))
                    filteredTeams = filteredTeams.Where(t => 
                        t.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                        t.Description?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) == true);
                
                // Grupuj zespoły
                var groupedTeams = filteredTeams.GroupBy(t => new 
                { 
                    SchoolType = t.SchoolType?.FullName ?? "Bez typu",
                    SchoolYear = t.SchoolYear?.Name ?? "Bez roku",
                    ColorCode = t.SchoolType?.ColorCode
                });
                
                foreach (var group in groupedTeams.OrderBy(g => g.Key.SchoolType).ThenBy(g => g.Key.SchoolYear))
                {
                    var teamGroup = new TeamGrouping
                    {
                        GroupName = $"{group.Key.SchoolType} - {group.Key.SchoolYear}",
                        GroupKey = $"{group.Key.SchoolType}_{group.Key.SchoolYear}",
                        ColorCode = group.Key.ColorCode,
                        Teams = new ObservableCollection<Team>(group.OrderBy(t => t.DisplayName))
                    };
                    
                    TeamGroups.Add(teamGroup);
                }
                
                HasNoTeams = !TeamGroups.Any();
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd ładowania zespołów: {ex.Message}",
                    "error"
                );
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task ArchiveTeamAsync(Team? team)
        {
            if (team == null) return;
            
            var result = MessageBox.Show(
                $"Czy na pewno chcesz zarchiwizować zespół '{team.DisplayName}'?",
                "Potwierdzenie archiwizacji",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result != MessageBoxResult.Yes) return;
            
            try
            {
                IsLoading = true;
                var accessToken = await GetAccessTokenAsync();
                var success = await _teamService.ArchiveTeamAsync(team.Id, "Archiwizacja z interfejsu użytkownika", accessToken);
                
                if (success)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Zespół '{team.DisplayName}' został zarchiwizowany.",
                        "success"
                    );
                    await LoadTeamsAsync();
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie udało się zarchiwizować zespołu.",
                        "error"
                    );
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd archiwizacji: {ex.Message}",
                    "error"
                );
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task RestoreTeamAsync(Team? team)
        {
            if (team == null) return;
            
            var result = MessageBox.Show(
                $"Czy na pewno chcesz przywrócić zespół '{team.DisplayName}'?",
                "Potwierdzenie przywrócenia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result != MessageBoxResult.Yes) return;
            
            try
            {
                IsLoading = true;
                var accessToken = await GetAccessTokenAsync();
                var success = await _teamService.RestoreTeamAsync(team.Id, accessToken);
                
                if (success)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Zespół '{team.DisplayName}' został przywrócony.",
                        "success"
                    );
                    await LoadTeamsAsync();
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie udało się przywrócić zespołu.",
                        "error"
                    );
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd przywrócenia: {ex.Message}",
                    "error"
                );
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void EditTeam(Team? team)
        {
            if (team == null) return;
            
            // TODO: Implementacja edycji zespołu
            MessageBox.Show($"Edycja zespołu '{team.DisplayName}' - funkcjonalność w przygotowaniu.", "Informacja");
        }
        
        private async Task DeleteTeamAsync(Team? team)
        {
            if (team == null) return;
            
            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć zespół '{team.DisplayName}'?\n\nTa operacja jest nieodwracalna!",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result != MessageBoxResult.Yes) return;
            
            try
            {
                IsLoading = true;
                var accessToken = await GetAccessTokenAsync();
                var success = await _teamService.DeleteTeamAsync(team.Id, accessToken);
                
                if (success)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Zespół '{team.DisplayName}' został usunięty.",
                        "success"
                    );
                    await LoadTeamsAsync();
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie udało się usunąć zespołu.",
                        "error"
                    );
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd usuwania: {ex.Message}",
                    "error"
                );
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void CreateNewTeam()
        {
            try
            {
                // Pokaż overlay
                _mainShellViewModel.IsDialogOpen = true;
                
                // Pobierz ViewModel z DI
                var wizardViewModel = App.ServiceProvider.GetRequiredService<TeamCreationWizardViewModel>();
                
                // Utwórz okno wizarda
                var wizardWindow = new TeamCreationWizardWindow(wizardViewModel);
                
                // Pokaż jako dialog
                var result = wizardWindow.ShowDialog();
                
                // Jeśli zespół został utworzony, odśwież listę
                if (result == true && wizardViewModel.CreatedTeam != null)
                {
                    _ = LoadTeamsAsync(forceRefresh: true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas otwierania kreatora zespołu: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Ukryj overlay
                _mainShellViewModel.IsDialogOpen = false;
            }
        }
        
        private void ShowLifecycleOperations()
        {
            // Pobierz wybrane zespoły z wszystkich grup
            var selectedTeams = TeamGroups
                .SelectMany(g => g.Teams)
                .Where(t => t.IsSelected)
                .ToList();

            if (!selectedTeams.Any())
            {
                MessageBox.Show("Wybierz co najmniej jeden zespół", 
                               "Brak zaznaczenia", 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Information);
                return;
            }

            try
            {
                // Pokaż overlay
                _mainShellViewModel.IsDialogOpen = true;
                
                // Pobierz dialog i ViewModel z DI
                var dialog = App.ServiceProvider.GetRequiredService<TeamLifecycleDialog>();
                var viewModel = App.ServiceProvider.GetRequiredService<TeamLifecycleDialogViewModel>();
                
                // Przekaż wybrane zespoły
                viewModel.SelectedTeams.Clear();
                foreach (var team in selectedTeams)
                {
                    viewModel.SelectedTeams.Add(team);
                }
                
                dialog.DataContext = viewModel;
                viewModel.RequestClose += () => dialog.Close();
                
                dialog.ShowDialog();
                
                // Odśwież listę po operacjach
                _ = LoadTeamsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas otwierania dialogu operacji cyklu życia: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Ukryj overlay
                _mainShellViewModel.IsDialogOpen = false;
            }
        }

        private async Task<string> GetAccessTokenAsync()
        {
            // TODO: Implementacja pobierania tokenu dostępu
            // Należy zaimplementować w oparciu o istniejący mechanizm w aplikacji
            return string.Empty;
        }
    }
} 