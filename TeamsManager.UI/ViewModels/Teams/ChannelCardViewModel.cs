using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Threading.Tasks;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Extensions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace TeamsManager.UI.ViewModels.Teams
{
    /// <summary>
    /// ViewModel for individual channel card with inline editing capabilities
    /// </summary>
    public class ChannelCardViewModel : INotifyPropertyChanged
    {
        private readonly IChannelService _channelService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMsalAuthService _msalAuthService;
        private readonly ILogger<ChannelCardViewModel> _logger;
        
        private Channel _channel;
        private bool _isEditMode;
        private bool _isLoading;
        private string _editingDisplayName = string.Empty;
        private string _editingDescription = string.Empty;
        private string _originalDisplayName = string.Empty;
        private string _originalDescription = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<ChannelUpdatedEventArgs>? ChannelUpdated;
        public event EventHandler<ChannelDeletedEventArgs>? ChannelDeleted;

        public ChannelCardViewModel(
            Channel channel, 
            IChannelService channelService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            IMsalAuthService msalAuthService,
            ILogger<ChannelCardViewModel> logger)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _channelService = channelService ?? throw new ArgumentNullException(nameof(channelService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _msalAuthService = msalAuthService ?? throw new ArgumentNullException(nameof(msalAuthService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeCommands();
        }

        // ===== PROPERTIES =====

        public Channel Channel
        {
            get => _channel;
            set
            {
                _channel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChannelIcon));
                OnPropertyChanged(nameof(CardStyle));
                OnPropertyChanged(nameof(CanBeDeleted));
                OnPropertyChanged(nameof(StatusColor));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                _isEditMode = value;
                if (value)
                {
                    // Enter edit mode - backup original values
                    _originalDisplayName = Channel.DisplayName;
                    _originalDescription = Channel.Description;
                    EditingDisplayName = Channel.DisplayName;
                    EditingDescription = Channel.Description;
                }
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string EditingDisplayName
        {
            get => _editingDisplayName;
            set
            {
                _editingDisplayName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string EditingDescription
        {
            get => _editingDescription;
            set
            {
                _editingDescription = value;
                OnPropertyChanged();
            }
        }

        // ===== UI BINDING PROPERTIES =====

        /// <summary>
        /// Icon for channel based on type and status
        /// </summary>
        public string ChannelIcon
        {
            get
            {
                if (Channel.IsGeneral) return "Home";
                if (Channel.IsPrivate) return "Lock";
                if (Channel.Status == ChannelStatus.Archived) return "Archive";
                return "Forum";
            }
        }

        /// <summary>
        /// Card style based on channel type and status
        /// </summary>
        public string CardStyle
        {
            get
            {
                if (Channel.Status == ChannelStatus.Archived) return "DangerCardStyle";
                if (Channel.IsPrivate) return "WarningCardStyle";
                return "InfoCardStyle";
            }
        }

        /// <summary>
        /// Status color for the badge
        /// </summary>
        public string StatusColor
        {
            get
            {
                return Channel.Status switch
                {
                    ChannelStatus.Active when Channel.IsPrivate => "#FFF39800", // Orange for private
                    ChannelStatus.Active => "#FF4CAF50", // Green for active
                    ChannelStatus.Archived => "#FFF44336", // Red for archived
                    _ => "#FF9E9E9E" // Gray for unknown
                };
            }
        }

        /// <summary>
        /// Whether this channel can be deleted (General channel cannot be deleted)
        /// </summary>
        public bool CanBeDeleted => !Channel.IsGeneral && Channel.Status == ChannelStatus.Active;

        /// <summary>
        /// Status kanału w języku polskim
        /// </summary>
        public string PolishStatus => Channel.Status.ToPolishString();

        /// <summary>
        /// Whether save is enabled (has changes and required fields filled)
        /// </summary>
        public bool CanSave => !string.IsNullOrWhiteSpace(EditingDisplayName) && HasChanges;

        /// <summary>
        /// Whether there are changes to save
        /// </summary>
        public bool HasChanges => 
            EditingDisplayName != _originalDisplayName || 
            EditingDescription != _originalDescription;

        // ===== COMMANDS =====

        public ICommand EditCommand { get; private set; } = null!;
        public ICommand SaveCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;
        public ICommand DeleteCommand { get; private set; } = null!;
        public ICommand ToggleArchiveCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            EditCommand = new RelayCommand(
                execute: _ => EnterEditMode(),
                canExecute: _ => !IsEditMode && !IsLoading && Channel.Status == ChannelStatus.Active);

            SaveCommand = new AsyncRelayCommand(
                execute: SaveChangesAsync,
                canExecute: _ => IsEditMode && CanSave && !IsLoading);

            CancelCommand = new RelayCommand(
                execute: _ => CancelEdit(),
                canExecute: _ => IsEditMode && !IsLoading);

            DeleteCommand = new AsyncRelayCommand(
                execute: DeleteChannelAsync,
                canExecute: _ => CanBeDeleted && !IsLoading);

            ToggleArchiveCommand = new AsyncRelayCommand(
                execute: ToggleArchiveAsync,
                canExecute: _ => !Channel.IsGeneral && !IsLoading);
        }

        // ===== COMMAND METHODS =====

        private void EnterEditMode()
        {
            IsEditMode = true;
        }

        private async Task SaveChangesAsync()
        {
            if (!CanSave) return;

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

                var updatedChannel = await _channelService.UpdateTeamChannelAsync(
                    Channel.TeamId,
                    Channel.Id,
                    accessToken,
                    EditingDisplayName.Trim(),
                    EditingDescription?.Trim());

                if (updatedChannel != null)
                {
                    Channel = updatedChannel;
                    IsEditMode = false;
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Kanał '{Channel.DisplayName}' został zaktualizowany.",
                        "success");
                    
                    // Notify parent that channel was updated
                    ChannelUpdated?.Invoke(this, new ChannelUpdatedEventArgs(updatedChannel));
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie udało się zaktualizować kanału.",
                        "error");
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas aktualizacji: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CancelEdit()
        {
            EditingDisplayName = _originalDisplayName;
            EditingDescription = _originalDescription;
            IsEditMode = false;
        }

        private async Task DeleteChannelAsync()
        {
            if (!CanBeDeleted) return;

            try
            {
                // Show confirmation dialog would be better, but for now just proceed
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

                var success = await _channelService.RemoveTeamChannelAsync(
                    Channel.TeamId,
                    Channel.Id,
                    accessToken);

                if (success)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Kanał '{Channel.DisplayName}' został usunięty.",
                        "success");
                    
                    // Notify parent that channel was deleted
                    ChannelDeleted?.Invoke(this, new ChannelDeletedEventArgs(Channel));
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie udało się usunąć kanału.",
                        "error");
                }
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas usuwania: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ToggleArchiveAsync()
        {
            if (Channel.IsGeneral) return;

            try
            {
                IsLoading = true;

                if (Channel.Status == ChannelStatus.Active)
                {
                    // Archive channel
                    Channel.Archive("Zarchiwizowano przez użytkownika", _currentUserService.GetCurrentUserUpn() ?? "system");
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Kanał '{Channel.DisplayName}' został zarchiwizowany.",
                        "success");
                }
                else if (Channel.Status == ChannelStatus.Archived)
                {
                    // Restore channel
                    Channel.Restore(_currentUserService.GetCurrentUserUpn() ?? "system");
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Kanał '{Channel.DisplayName}' został przywrócony.",
                        "success");
                }

                OnPropertyChanged(nameof(Channel));
                OnPropertyChanged(nameof(ChannelIcon));
                OnPropertyChanged(nameof(CardStyle));
                OnPropertyChanged(nameof(CanBeDeleted));
                OnPropertyChanged(nameof(StatusColor));
                
                // Notify parent that channel was updated
                ChannelUpdated?.Invoke(this, new ChannelUpdatedEventArgs(Channel));
            }
            catch (Exception ex)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas zmiany statusu: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ===== HELPER METHODS =====

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

        // ===== PROPERTY CHANGED =====

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ===== EVENT ARGS =====

    public class ChannelUpdatedEventArgs : EventArgs
    {
        public Channel UpdatedChannel { get; }

        public ChannelUpdatedEventArgs(Channel updatedChannel)
        {
            UpdatedChannel = updatedChannel;
        }
    }

    public class ChannelDeletedEventArgs : EventArgs
    {
        public Channel DeletedChannel { get; }

        public ChannelDeletedEventArgs(Channel deletedChannel)
        {
            DeletedChannel = deletedChannel;
        }
    }
} 