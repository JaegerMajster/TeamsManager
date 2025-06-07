using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.IO;
using System.Collections.Generic;
using Microsoft.Win32;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Teams
{
    /// <summary>
    /// ViewModel for managing team channels with card-based interface
    /// </summary>
    public class TeamChannelsViewModel : BaseViewModel
    {
        private readonly IChannelService _channelService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        
        private string _teamId = string.Empty;
        private Team? _team;
        private bool _isLoading;
        private ObservableCollection<ChannelCardViewModel> _channels;
        private ICollectionView? _channelsView;
        private string _searchText = string.Empty;

        public TeamChannelsViewModel(
            IChannelService channelService,
            INotificationService notificationService,
            ICurrentUserService currentUserService)
        {
            _channelService = channelService ?? throw new ArgumentNullException(nameof(channelService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));

            _channels = new ObservableCollection<ChannelCardViewModel>();
            InitializeCommands();
            SetupCollectionView();
        }

        // ===== PROPERTIES =====

        /// <summary>
        /// Current team ID
        /// </summary>
        public string TeamId
        {
            get => _teamId;
            set
            {
                if (SetProperty(ref _teamId, value))
                {
                    // When team ID changes, reload channels
                    _ = LoadChannelsAsync();
                }
            }
        }

        /// <summary>
        /// Current team information
        /// </summary>
        public Team? Team
        {
            get => _team;
            set
            {
                SetProperty(ref _team, value);
                OnPropertyChanged(nameof(TeamDisplayName));
            }
        }

        /// <summary>
        /// Team display name for header
        /// </summary>
        public string TeamDisplayName => Team?.DisplayName ?? "Wybierz zespół";

        /// <summary>
        /// Loading state
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                SetProperty(ref _isLoading, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Collection of channel cards
        /// </summary>
        public ObservableCollection<ChannelCardViewModel> Channels
        {
            get => _channels;
            set
            {
                _channels = value;
                OnPropertyChanged();
                SetupCollectionView();
            }
        }

        /// <summary>
        /// Filtered and grouped view of channels
        /// </summary>
        public ICollectionView? ChannelsView
        {
            get => _channelsView;
            private set => SetProperty(ref _channelsView, value);
        }

        /// <summary>
        /// Search text for filtering channels
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ChannelsView?.Refresh();
                }
            }
        }

        /// <summary>
        /// Whether to show empty state
        /// </summary>
        public bool HasNoChannels => !IsLoading && !Channels.Any();

        /// <summary>
        /// Count of active channels
        /// </summary>
        public int ActiveChannelsCount => Channels.Count(c => c.Channel.Status == Core.Enums.ChannelStatus.Active);

        /// <summary>
        /// Count of private channels
        /// </summary>
        public int PrivateChannelsCount => Channels.Count(c => c.Channel.IsPrivate);

        // ===== COMMANDS =====

        public ICommand LoadChannelsCommand { get; private set; } = null!;
        public ICommand CreateChannelCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand ImportChannelsCommand { get; private set; } = null!;
        public ICommand ClearSearchCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            LoadChannelsCommand = new AsyncRelayCommand(
                execute: async () => await LoadChannelsAsync(),
                canExecute: _ => !string.IsNullOrEmpty(TeamId) && !IsLoading);

            CreateChannelCommand = new AsyncRelayCommand(
                execute: CreateNewChannelAsync,
                canExecute: _ => !string.IsNullOrEmpty(TeamId) && !IsLoading);

            RefreshCommand = new AsyncRelayCommand(
                execute: async () => await LoadChannelsAsync(forceRefresh: true),
                canExecute: _ => !string.IsNullOrEmpty(TeamId) && !IsLoading);

            ImportChannelsCommand = new AsyncRelayCommand(
                execute: ImportChannelsFromCsvAsync,
                canExecute: _ => !string.IsNullOrEmpty(TeamId) && !IsLoading);

            ClearSearchCommand = new RelayCommand(
                execute: _ => SearchText = string.Empty,
                canExecute: _ => !string.IsNullOrEmpty(SearchText));
        }

        // ===== COLLECTION VIEW SETUP =====

        private void SetupCollectionView()
        {
            ChannelsView = CollectionViewSource.GetDefaultView(Channels);
            
            if (ChannelsView != null)
            {
                // Group by channel type
                ChannelsView.GroupDescriptions.Clear();
                ChannelsView.GroupDescriptions.Add(new PropertyGroupDescription("Channel.ChannelType"));
                
                // Sort by: General first, then by SortOrder, then by DisplayName
                ChannelsView.SortDescriptions.Clear();
                ChannelsView.SortDescriptions.Add(new SortDescription("Channel.IsGeneral", ListSortDirection.Descending));
                ChannelsView.SortDescriptions.Add(new SortDescription("Channel.SortOrder", ListSortDirection.Ascending));
                ChannelsView.SortDescriptions.Add(new SortDescription("Channel.DisplayName", ListSortDirection.Ascending));
                
                // Set up filtering
                ChannelsView.Filter = FilterChannels;
            }
        }

        private bool FilterChannels(object item)
        {
            if (item is not ChannelCardViewModel channelVm) return false;
            
            // If no search text, show all
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            
            var searchTerm = SearchText.ToLower();
            
            // Search in display name, description, and type
            return channelVm.Channel.DisplayName.ToLower().Contains(searchTerm) ||
                   channelVm.Channel.Description.ToLower().Contains(searchTerm) ||
                   channelVm.Channel.ChannelType.ToLower().Contains(searchTerm) ||
                   channelVm.Channel.StatusDescription.ToLower().Contains(searchTerm);
        }

        // ===== COMMAND METHODS =====

        /// <summary>
        /// Load channels for the current team
        /// </summary>
        public async Task LoadChannelsAsync(bool forceRefresh = false)
        {
            if (string.IsNullOrEmpty(TeamId)) return;

            try
            {
                IsLoading = true;

                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie można uzyskać tokenu dostępu.",
                        "error");
                    return;
                }

                var channels = await _channelService.GetTeamChannelsAsync(TeamId, accessToken, forceRefresh);
                
                if (channels == null)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie udało się pobrać kanałów zespołu.",
                        "error");
                    return;
                }

                // Clear existing channels
                foreach (var existingChannelVm in Channels)
                {
                    existingChannelVm.ChannelUpdated -= OnChannelUpdated;
                    existingChannelVm.ChannelDeleted -= OnChannelDeleted;
                }
                Channels.Clear();

                // Add new channel view models
                foreach (var channel in channels)
                {
                    var channelVm = new ChannelCardViewModel(channel, _channelService, _notificationService, _currentUserService);
                    channelVm.ChannelUpdated += OnChannelUpdated;
                    channelVm.ChannelDeleted += OnChannelDeleted;
                    Channels.Add(channelVm);
                }

                // Update counts
                OnPropertyChanged(nameof(HasNoChannels));
                OnPropertyChanged(nameof(ActiveChannelsCount));
                OnPropertyChanged(nameof(PrivateChannelsCount));

                if (forceRefresh)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Odświeżono {Channels.Count} kanałów.",
                        "success");
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas ładowania kanałów: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Create a new channel
        /// </summary>
        private async Task CreateNewChannelAsync()
        {
            // For now, show a simple input dialog
            // In a real implementation, you would show a proper CreateChannelDialog
            var channelName = "Nowy kanał"; // This should come from a dialog
            var description = "Opis nowego kanału";
            var isPrivate = false;

            try
            {
                IsLoading = true;

                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie można uzyskać tokenu dostępu.",
                        "error");
                    return;
                }

                var newChannel = await _channelService.CreateTeamChannelAsync(
                    TeamId, 
                    channelName, 
                    accessToken, 
                    description, 
                    isPrivate);

                if (newChannel != null)
                {
                    // Add to collection
                    var channelVm = new ChannelCardViewModel(newChannel, _channelService, _notificationService, _currentUserService);
                    channelVm.ChannelUpdated += OnChannelUpdated;
                    channelVm.ChannelDeleted += OnChannelDeleted;
                    Channels.Add(channelVm);

                    // Update counts
                    OnPropertyChanged(nameof(HasNoChannels));
                    OnPropertyChanged(nameof(ActiveChannelsCount));
                    OnPropertyChanged(nameof(PrivateChannelsCount));

                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Kanał '{newChannel.DisplayName}' został utworzony.",
                        "success");
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie udało się utworzyć kanału.",
                        "error");
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas tworzenia kanału: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Import channels from CSV file
        /// </summary>
        private async Task ImportChannelsFromCsvAsync()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Wybierz plik CSV z kanałami",
                    Filter = "Pliki CSV (*.csv)|*.csv|Wszystkie pliki (*.*)|*.*",
                    FilterIndex = 1
                };

                if (openFileDialog.ShowDialog() != true) return;

                IsLoading = true;
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    "Importowanie kanałów z pliku CSV...",
                    "info");

                var csvContent = await File.ReadAllTextAsync(openFileDialog.FileName);
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                var importedCount = 0;
                var accessToken = await GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie można uzyskać tokenu dostępu.",
                        "error");
                    return;
                }

                // Skip header row if present
                var startIndex = lines.Length > 0 && lines[0].Contains("nazwa") ? 1 : 0;

                for (var i = startIndex; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 2) continue;

                    var channelName = parts[0].Trim().Trim('"');
                    var description = parts.Length > 1 ? parts[1].Trim().Trim('"') : "";
                    var isPrivate = parts.Length > 2 && parts[2].Trim().Equals("tak", StringComparison.OrdinalIgnoreCase);

                    if (string.IsNullOrEmpty(channelName)) continue;

                    try
                    {
                        var newChannel = await _channelService.CreateTeamChannelAsync(
                            TeamId, 
                            channelName, 
                            accessToken, 
                            description, 
                            isPrivate);

                        if (newChannel != null)
                        {
                            var channelVm = new ChannelCardViewModel(newChannel, _channelService, _notificationService, _currentUserService);
                            channelVm.ChannelUpdated += OnChannelUpdated;
                            channelVm.ChannelDeleted += OnChannelDeleted;
                            Channels.Add(channelVm);
                            importedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other channels
                        await _notificationService.SendNotificationToUserAsync(
                            _currentUserService.GetCurrentUserUpn() ?? "system",
                            $"Nie udało się utworzyć kanału '{channelName}': {ex.Message}",
                            "warning");
                    }
                }

                // Update counts
                OnPropertyChanged(nameof(HasNoChannels));
                OnPropertyChanged(nameof(ActiveChannelsCount));
                OnPropertyChanged(nameof(PrivateChannelsCount));

                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Zaimportowano {importedCount} kanałów z pliku CSV.",
                    "success");
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas importu z CSV: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ===== EVENT HANDLERS =====

        private void OnChannelUpdated(object? sender, ChannelUpdatedEventArgs e)
        {
            // Update counts when channel is updated
            OnPropertyChanged(nameof(ActiveChannelsCount));
            OnPropertyChanged(nameof(PrivateChannelsCount));
        }

        private void OnChannelDeleted(object? sender, ChannelDeletedEventArgs e)
        {
            // Remove from collection
            var channelVm = Channels.FirstOrDefault(c => c.Channel.Id == e.DeletedChannel.Id);
            if (channelVm != null)
            {
                channelVm.ChannelUpdated -= OnChannelUpdated;
                channelVm.ChannelDeleted -= OnChannelDeleted;
                Channels.Remove(channelVm);
            }

            // Update counts
            OnPropertyChanged(nameof(HasNoChannels));
            OnPropertyChanged(nameof(ActiveChannelsCount));
            OnPropertyChanged(nameof(PrivateChannelsCount));
        }

        // ===== PUBLIC METHODS =====

        /// <summary>
        /// Initialize the view model with team data
        /// </summary>
        public async Task InitializeAsync(string teamId, Team? team = null)
        {
            TeamId = teamId;
            Team = team;
            await LoadChannelsAsync();
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Cleanup()
        {
            foreach (var channelVm in Channels)
            {
                channelVm.ChannelUpdated -= OnChannelUpdated;
                channelVm.ChannelDeleted -= OnChannelDeleted;
            }
            Channels.Clear();
        }

        // ===== HELPER METHODS =====

        private async Task<string> GetAccessTokenAsync()
        {
            // TODO: Implementacja pobierania tokenu dostępu
            // Należy zaimplementować w oparciu o istniejący mechanizm w aplikacji
            return string.Empty;
        }
    }
} 