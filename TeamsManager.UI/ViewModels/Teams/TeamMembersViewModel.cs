using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Extensions;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services.Abstractions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Generic;

namespace TeamsManager.UI.ViewModels.Teams
{
    public class TeamMembersViewModel : BaseViewModel
    {
        private readonly ITeamService _teamService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly IMsalAuthService _msalAuthService;
        private readonly ILogger<TeamMembersViewModel> _logger;
        
        private string _teamId = string.Empty;
        private Team? _team;
        
        // Collections
        public ObservableCollection<User> AvailableUsers { get; }
        public ObservableCollection<TeamMember> TeamMembers { get; }
        public ICollectionView FilteredAvailableUsers { get; }
        
        // Available roles for dropdown
        public ObservableCollection<TeamMemberRole> AvailableRoles { get; }

        // Properties
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                FilteredAvailableUsers.Refresh();
            }
        }

        private User? _selectedAvailableUser;
        public User? SelectedAvailableUser
        {
            get => _selectedAvailableUser;
            set => SetProperty(ref _selectedAvailableUser, value);
        }

        private TeamMember? _selectedTeamMember;
        public TeamMember? SelectedTeamMember
        {
            get => _selectedTeamMember;
            set => SetProperty(ref _selectedTeamMember, value);
        }

        public string MembersCount => $"Członków: {TeamMembers.Count}";

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                SetProperty(ref _isLoading, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Commands
        public ICommand AddAsMemberCommand { get; }
        public ICommand AddAsOwnerCommand { get; }
        public ICommand RemoveMemberCommand { get; }
        public ICommand RemoveSpecificMemberCommand { get; }
        public ICommand BulkImportCommand { get; }
        public ICommand RefreshCommand { get; }

        public TeamMembersViewModel(
            ITeamService teamService,
            IUserService userService,
            INotificationService notificationService,
            IMsalAuthService msalAuthService,
            ILogger<TeamMembersViewModel> logger)
        {
            _teamService = teamService;
            _userService = userService;
            _notificationService = notificationService;
            _msalAuthService = msalAuthService ?? throw new ArgumentNullException(nameof(msalAuthService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize collections
            AvailableUsers = new ObservableCollection<User>();
            TeamMembers = new ObservableCollection<TeamMember>();
            AvailableRoles = new ObservableCollection<TeamMemberRole> 
            { 
                TeamMemberRole.Member, 
                TeamMemberRole.Owner 
            };

                            // Skonfiguruj widok z filtrowaniem
            FilteredAvailableUsers = CollectionViewSource.GetDefaultView(AvailableUsers);
            FilteredAvailableUsers.Filter = FilterUsers;

            // Initialize commands
            AddAsMemberCommand = new RelayCommand(
                async () => await AddUserToTeamAsync(TeamMemberRole.Member),
                () => SelectedAvailableUser != null && !IsLoading);

            AddAsOwnerCommand = new RelayCommand(
                async () => await AddUserToTeamAsync(TeamMemberRole.Owner),
                () => SelectedAvailableUser != null && !IsLoading);

            RemoveMemberCommand = new RelayCommand(
                async () => await RemoveMemberFromTeamAsync(),
                () => SelectedTeamMember != null && !IsLoading);

            RemoveSpecificMemberCommand = new RelayCommand<TeamMember>(
                async (member) => await RemoveSpecificMemberAsync(member),
                (member) => member != null && !IsLoading);

            BulkImportCommand = new RelayCommand(
                async () => await BulkImportMembersAsync(),
                () => !IsLoading);

            RefreshCommand = new RelayCommand(
                async () => await LoadDataAsync(),
                () => !IsLoading);
        }

        public async Task InitializeAsync(string teamId)
        {
            _teamId = teamId;
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;

                // Load team with members
                _team = await _teamService.GetTeamByIdAsync(_teamId, includeMembers: true);
                if (_team == null)
                {
                    await _notificationService.SendNotificationToUserAsync(string.Empty, "Nie znaleziono zespołu", "error");
                    return;
                }

                // Load all active users
                var allUsers = await _userService.GetAllActiveUsersAsync();
                
                // Pobierz ID bieżących członków
                var memberUserIds = _team.Members
                    .Where(m => m.IsActive)
                    .Select(m => m.UserId)
                    .ToHashSet();

                // Populate collections
                AvailableUsers.Clear();
                foreach (var user in allUsers.Where(u => !memberUserIds.Contains(u.Id)))
                {
                    AvailableUsers.Add(user);
                }

                TeamMembers.Clear();
                foreach (var member in _team.Members.Where(m => m.IsActive))
                {
                    TeamMembers.Add(member);
                }

                OnPropertyChanged(nameof(MembersCount));
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(string.Empty, $"Błąd ładowania danych: {ex.Message}", "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool FilterUsers(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            if (obj is User user)
            {
                return user.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                       user.Email.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private async Task AddUserToTeamAsync(TeamMemberRole role)
        {
            if (SelectedAvailableUser == null) return;

            try
            {
                IsLoading = true;

                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    await _notificationService.SendNotificationToUserAsync(
                        string.Empty,
                        "Nie można uzyskać tokenu dostępu. Spróbuj ponownie zalogować się.",
                        "error");
                    return;
                }

                var newMember = await _teamService.AddMemberAsync(
                    _teamId, 
                    SelectedAvailableUser.UPN, 
                    role,
                    accessToken);

                if (newMember != null)
                {
                    // Aktualizuj kolekcje UI
                    TeamMembers.Add(newMember);
                    AvailableUsers.Remove(SelectedAvailableUser);
                    SelectedAvailableUser = null;
                    
                    OnPropertyChanged(nameof(MembersCount));
                    
                    await _notificationService.SendNotificationToUserAsync(
                        string.Empty,
                        $"Użytkownik został dodany do zespołu jako {GetRoleDisplayName(role)}",
                        "success");
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(string.Empty, $"Błąd dodawania użytkownika: {ex.Message}", "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RemoveMemberFromTeamAsync()
        {
            await RemoveSpecificMemberAsync(SelectedTeamMember);
        }

        private async Task RemoveSpecificMemberAsync(TeamMember? member)
        {
            if (member == null) return;

                            // Sprawdź czy to ostatni właściciel
            if (member.Role == TeamMemberRole.Owner && 
                TeamMembers.Count(m => m.Role == TeamMemberRole.Owner) <= 1)
            {
                await _notificationService.SendNotificationToUserAsync(
                    string.Empty,
                    "Nie można usunąć ostatniego właściciela zespołu",
                    "warning");
                return;
            }

            try
            {
                IsLoading = true;

                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    await _notificationService.SendNotificationToUserAsync(
                        string.Empty,
                        "Nie można uzyskać tokenu dostępu. Spróbuj ponownie zalogować się.",
                        "error");
                    return;
                }

                var result = await _teamService.RemoveMemberAsync(
                    _teamId, 
                    member.UserId,
                    accessToken);

                if (result)
                {
                    // Find the user to add back to available list
                    var user = member.User;
                    if (user != null)
                    {
                        AvailableUsers.Add(user);
                    }

                    TeamMembers.Remove(member);
                    OnPropertyChanged(nameof(MembersCount));
                    
                    await _notificationService.SendNotificationToUserAsync(string.Empty, "Użytkownik został usunięty z zespołu", "success");
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(string.Empty, $"Błąd usuwania użytkownika: {ex.Message}", "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task BulkImportMembersAsync()
        {
            try
            {
                // Show file dialog
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Wybierz plik z listą członków",
                    Filter = "Pliki CSV (*.csv)|*.csv|Wszystkie pliki (*.*)|*.*",
                    FilterIndex = 1,
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() != true)
                    return;

                IsLoading = true;

                // Parse CSV file
                var userUpns = await ParseUserListAsync(openFileDialog.FileName);
                
                if (!userUpns.Any())
                {
                    await _notificationService.SendNotificationToUserAsync(string.Empty, "Nie znaleziono użytkowników w pliku", "warning");
                    return;
                }

                // Dodaj użytkowników do zespołu
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    await _notificationService.SendNotificationToUserAsync(
                        string.Empty,
                        "Nie można uzyskać tokenu dostępu. Spróbuj ponownie zalogować się.",
                        "error");
                    return;
                }

                var results = await _teamService.AddUsersToTeamAsync(
                    _teamId, 
                    userUpns,
                    accessToken);

                // Show summary
                var successCount = results.Count(r => r.Value);
                var failCount = results.Count(r => !r.Value);

                await _notificationService.SendNotificationToUserAsync(
                    string.Empty,
                    $"Import zakończony:\n✓ Dodano: {successCount}\n✗ Błędy: {failCount}",
                    "info");

                // Refresh data
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(string.Empty, $"Błąd importu: {ex.Message}", "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<List<string>> ParseUserListAsync(string filePath)
        {
            var upns = new List<string>();
            
            using (var reader = new StreamReader(filePath))
            {
                // Skip header if exists
                var firstLine = await reader.ReadLineAsync();
                
                // Simple parsing - expecting UPN in first column
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var parts = line.Split(',', ';');
                    if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        upns.Add(parts[0].Trim());
                    }
                }
            }
            
            return upns;
        }

        private string GetRoleDisplayName(TeamMemberRole role)
        {
            return role.ToPolishString().ToLower();
        }

        /// <summary>
        /// Pobiera token dostępu z MSAL Auth Service
        /// </summary>
        private async Task<string?> GetAccessTokenAsync()
        {
            try
            {
                var authResult = await _msalAuthService.AcquireTokenSilentAsync();
                if (authResult?.AccessToken != null)
                {
                    return authResult.AccessToken;
                }

                _logger.LogWarning("Nie można pobrać tokenu w trybie cichym, może być wymagana ponowna autoryzacja");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania tokenu dostępu");
                return null;
            }
        }
    }
} 