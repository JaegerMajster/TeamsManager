// Plik: TeamsManager.Core/Services/ChannelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Services
{
    public class ChannelService : IChannelService
    {
        private readonly IPowerShellService _powerShellService;
        private readonly IGenericRepository<Channel> _channelRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ChannelService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IPowerShellCacheService _powerShellCacheService;

        private const string TeamChannelsCacheKeyPrefix = "Channels_TeamId_";
        private const string ChannelByGraphIdCacheKeyPrefix = "Channel_GraphId_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);

        public ChannelService(
            IPowerShellService powerShellService,
            IGenericRepository<Channel> channelRepository,
            ITeamRepository teamRepository,
            IOperationHistoryService operationHistoryService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<ChannelService> logger,
            IMemoryCache memoryCache,
            IPowerShellCacheService powerShellCacheService)
        {
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _channelRepository = channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));
            _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return _powerShellCacheService.GetDefaultCacheEntryOptions();
        }

        private Channel MapPsObjectToLocalChannel(PSObject psChannel, string localTeamId)
        {
            var graphChannelId = psChannel.Properties["Id"]?.Value?.ToString();
            if (string.IsNullOrWhiteSpace(graphChannelId))
            {
                graphChannelId = Guid.NewGuid().ToString();
                _logger.LogError("MapPsObjectToLocalChannel: PSObject dla kanału nie zawierał poprawnego ID z Graph. Wygenerowano nowe lokalne ID: {GeneratedId}", graphChannelId);
            }

            var channel = new Channel
            {
                Id = graphChannelId,
                DisplayName = psChannel.Properties["DisplayName"]?.Value?.ToString() ?? string.Empty,
                Description = psChannel.Properties["Description"]?.Value?.ToString() ?? string.Empty,
                TeamId = localTeamId,
                ChannelType = psChannel.Properties["MembershipType"]?.Value?.ToString() ?? "Standard",
                ExternalUrl = psChannel.Properties["WebUrl"]?.Value?.ToString(),
                // Nowe mapowania dla pełnej synchronizacji
                FilesCount = psChannel.Properties["FilesCount"]?.Value as int? ?? 0,
                FilesSize = psChannel.Properties["FilesSize"]?.Value as long? ?? 0,
                LastActivityDate = psChannel.Properties["LastActivityDate"]?.Value as DateTime?,
                LastMessageDate = psChannel.Properties["LastMessageDate"]?.Value as DateTime?,
                MessageCount = psChannel.Properties["MessageCount"]?.Value as int? ?? 0,
                NotificationSettings = psChannel.Properties["NotificationSettings"]?.Value?.ToString(),
                IsModerationEnabled = psChannel.Properties["IsModerationEnabled"]?.Value as bool? ?? false,
                Category = psChannel.Properties["Category"]?.Value?.ToString(),
                Tags = psChannel.Properties["Tags"]?.Value?.ToString(),
                SortOrder = psChannel.Properties["SortOrder"]?.Value as int? ?? 0
            };

            if (channel.ChannelType.Equals("private", StringComparison.OrdinalIgnoreCase))
            {
                channel.IsPrivate = true;
            }

            bool? isFavoriteByDefault = psChannel.Properties["isFavoriteByDefault"]?.Value as bool?;
            if ((channel.DisplayName.Equals("General", StringComparison.OrdinalIgnoreCase) ||
                 channel.DisplayName.Equals("Ogólny", StringComparison.OrdinalIgnoreCase)) ||
                 isFavoriteByDefault == true)
            {
                channel.IsGeneral = true;
                if (string.IsNullOrWhiteSpace(channel.ChannelType) || channel.ChannelType.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    channel.ChannelType = "Standard";
                }
            }
            channel.Status = ChannelStatus.Active;
            return channel;
        }

        public async Task<IEnumerable<Channel>?> GetTeamChannelsAsync(string teamId, string apiAccessToken, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie kanałów dla lokalnego zespołu ID: {TeamId} (serwis). ForceRefresh: {ForceRefresh}", teamId, forceRefresh);
            var team = await _teamRepository.GetByIdAsync(teamId);
            if (team == null || string.IsNullOrEmpty(team.ExternalId))
            {
                _logger.LogWarning("Zespół lokalny o ID {LocalTeamId} nie został znaleziony lub nie ma ExternalId (Graph GroupId).", teamId);
                return null;
            }
            string teamGraphId = team.ExternalId;
            string cacheKey = TeamChannelsCacheKeyPrefix + teamId;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Channel>? cachedChannels) && cachedChannels != null)
            {
                _logger.LogDebug("Kanały dla lokalnego zespołu ID {TeamId} (GraphID: {TeamGraphId}) znalezione w cache (serwis).", teamId, teamGraphId);
                return cachedChannels;
            }

            var psObjects = await _powerShellService.ExecuteWithAutoConnectAsync(
                apiAccessToken,
                async () => await _powerShellService.Teams.GetTeamChannelsAsync(teamGraphId),
                $"GetTeamChannelsAsync dla zespołu {teamGraphId}"
            );

            if (psObjects == null)
            {
                _logger.LogWarning("Nie udało się pobrać kanałów z PowerShell dla zespołu GraphID {TeamGraphId}.", teamGraphId);
                return Enumerable.Empty<Channel>();
            }

            var channelsFromGraph = new List<Channel>();
            foreach (var pso in psObjects)
            {
                channelsFromGraph.Add(MapPsObjectToLocalChannel(pso, teamId));
            }

            var localChannels = (await _channelRepository.FindAsync(c => c.TeamId == teamId)).ToList();
            var currentUser = _currentUserService.GetCurrentUserUpn() ?? "system_sync_channels";

            // Synchronizacja kanałów z Graph do lokalnej bazy
            var graphChannelIds = new HashSet<string>(channelsFromGraph.Select(c => c.Id));

            foreach (var graphChannel in channelsFromGraph)
            {
                var localChannel = localChannels.FirstOrDefault(lc => lc.Id == graphChannel.Id);
                if (localChannel == null)
                {
                    graphChannel.CreatedBy = currentUser;
                    graphChannel.CreatedDate = DateTime.UtcNow;
                    await _channelRepository.AddAsync(graphChannel);
                    _logger.LogInformation("Dodano nowy kanał lokalnie: {ChannelDisplayName} (GraphID: {ChannelGraphId}) dla zespołu {TeamId}", 
                        graphChannel.DisplayName, graphChannel.Id, teamId);
                }
                else
                {
                    bool updated = false;
                    if (localChannel.DisplayName != graphChannel.DisplayName) { localChannel.DisplayName = graphChannel.DisplayName; updated = true; }
                    if (localChannel.Description != graphChannel.Description) { localChannel.Description = graphChannel.Description; updated = true; }
                    if (localChannel.ChannelType != graphChannel.ChannelType) { localChannel.ChannelType = graphChannel.ChannelType; updated = true; }
                    if (localChannel.IsPrivate != graphChannel.IsPrivate) { localChannel.IsPrivate = graphChannel.IsPrivate; updated = true; }
                    if (localChannel.IsGeneral != graphChannel.IsGeneral) { localChannel.IsGeneral = graphChannel.IsGeneral; updated = true; }
                    if (localChannel.ExternalUrl != graphChannel.ExternalUrl) { localChannel.ExternalUrl = graphChannel.ExternalUrl; updated = true; }
                    
                    // Aktualizacja nowych pól
                    if (localChannel.FilesCount != graphChannel.FilesCount) { localChannel.FilesCount = graphChannel.FilesCount; updated = true; }
                    if (localChannel.FilesSize != graphChannel.FilesSize) { localChannel.FilesSize = graphChannel.FilesSize; updated = true; }
                    if (localChannel.LastActivityDate != graphChannel.LastActivityDate) { localChannel.LastActivityDate = graphChannel.LastActivityDate; updated = true; }
                    if (localChannel.LastMessageDate != graphChannel.LastMessageDate) { localChannel.LastMessageDate = graphChannel.LastMessageDate; updated = true; }
                    if (localChannel.MessageCount != graphChannel.MessageCount) { localChannel.MessageCount = graphChannel.MessageCount; updated = true; }
                    if (localChannel.NotificationSettings != graphChannel.NotificationSettings) { localChannel.NotificationSettings = graphChannel.NotificationSettings; updated = true; }
                    if (localChannel.IsModerationEnabled != graphChannel.IsModerationEnabled) { localChannel.IsModerationEnabled = graphChannel.IsModerationEnabled; updated = true; }
                    if (localChannel.Category != graphChannel.Category) { localChannel.Category = graphChannel.Category; updated = true; }
                    if (localChannel.Tags != graphChannel.Tags) { localChannel.Tags = graphChannel.Tags; updated = true; }
                    if (localChannel.SortOrder != graphChannel.SortOrder) { localChannel.SortOrder = graphChannel.SortOrder; updated = true; }
                    
                    // Przywróć kanał jeśli był zarchiwizowany
                    if (localChannel.Status != ChannelStatus.Active) 
                    { 
                        localChannel.Restore(currentUser); 
                        updated = true; 
                    }

                    if (updated)
                    {
                        localChannel.MarkAsModified(currentUser);
                        _channelRepository.Update(localChannel);
                        _logger.LogInformation("Zaktualizowano lokalny kanał: {ChannelDisplayName} (GraphID: {ChannelGraphId}) dla zespołu {TeamId}", 
                            localChannel.DisplayName, localChannel.Id, teamId);
                    }
                }
            }

            // Oznacz kanały usunięte z Graph jako nieaktywne lokalnie
            foreach (var localChannel in localChannels.Where(lc => lc.Status == ChannelStatus.Active))
            {
                if (!graphChannelIds.Contains(localChannel.Id))
                {
                    localChannel.Archive($"Kanał usunięty z Microsoft Teams", currentUser);
                    _channelRepository.Update(localChannel);
                    _logger.LogWarning("Kanał {ChannelDisplayName} (GraphID: {ChannelGraphId}) został usunięty z Microsoft Teams. Oznaczono jako zarchiwizowany.", 
                        localChannel.DisplayName, localChannel.Id);
                }
            }

            var finalChannelList = (await _channelRepository.FindAsync(c => c.TeamId == teamId && c.IsActive)).ToList();

            _cache.Set(cacheKey, finalChannelList, GetDefaultCacheEntryOptions());
            _logger.LogInformation("Pobrano i zsynchronizowano {Count} kanałów dla zespołu ID {TeamId}. Zcache'owano.", finalChannelList.Count, teamId);
            return finalChannelList;
        }

        public async Task<Channel?> GetTeamChannelByIdAsync(string teamId, string channelGraphId, string apiAccessToken, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie kanału GraphID: {ChannelGraphId} dla lokalnego zespołu ID: {TeamId} (serwis). ForceRefresh: {ForceRefresh}", channelGraphId, teamId, forceRefresh);
            var team = await _teamRepository.GetByIdAsync(teamId);
            if (team == null || string.IsNullOrEmpty(team.ExternalId))
            {
                _logger.LogWarning("Zespół lokalny o ID {LocalTeamId} nie został znaleziony lub nie ma ExternalId.", teamId);
                return null;
            }
            string teamGraphId = team.ExternalId;
            string cacheKey = ChannelByGraphIdCacheKeyPrefix + channelGraphId;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out Channel? cachedChannel) && cachedChannel != null)
            {
                _logger.LogDebug("Kanał GraphID: {ChannelGraphId} (zespół {TeamGraphId}) znaleziony w cache.", channelGraphId, teamGraphId);
                return cachedChannel;
            }

            if (!forceRefresh)
            {
                var localChannel = (await _channelRepository.FindAsync(c => c.Id == channelGraphId && c.TeamId == teamId)).FirstOrDefault();
                if (localChannel != null && localChannel.IsActive)
                {
                    _logger.LogDebug("Kanał GraphID: {ChannelGraphId} (zespół {TeamGraphId}) znaleziony w lokalnej bazie (bez forceRefresh).", channelGraphId, teamGraphId);
                    _cache.Set(cacheKey, localChannel, GetDefaultCacheEntryOptions());
                    return localChannel;
                }
            }

            var psChannel = await _powerShellService.ExecuteWithAutoConnectAsync(
                apiAccessToken,
                async () => await _powerShellService.Teams.GetTeamChannelByIdAsync(teamGraphId, channelGraphId),
                $"GetTeamChannelByIdAsync dla kanału {channelGraphId} w zespole {teamGraphId}"
            );

            if (psChannel == null)
            {
                _logger.LogInformation("Kanał GraphID: {ChannelGraphId} w zespole GraphID: {TeamGraphId} nie znaleziony przez PowerShell.", channelGraphId, teamGraphId);
                _cache.Remove(cacheKey);
                return null;
            }

            var channelFromGraph = MapPsObjectToLocalChannel(psChannel, teamId);
            var existingLocalChannel = (await _channelRepository.FindAsync(c => c.Id == channelGraphId && c.TeamId == teamId)).FirstOrDefault();
            var currentUser = _currentUserService.GetCurrentUserUpn() ?? "system_sync_channel";

            if (existingLocalChannel == null)
            {
                channelFromGraph.CreatedBy = currentUser;
                channelFromGraph.CreatedDate = DateTime.UtcNow;
                // channelFromGraph.IsActive jest zarządzane przez Status
                await _channelRepository.AddAsync(channelFromGraph);
            }
            else
            {
                existingLocalChannel.DisplayName = channelFromGraph.DisplayName;
                existingLocalChannel.Description = channelFromGraph.Description;
                existingLocalChannel.ChannelType = channelFromGraph.ChannelType;
                existingLocalChannel.IsPrivate = channelFromGraph.IsPrivate;
                existingLocalChannel.IsGeneral = channelFromGraph.IsGeneral;
                existingLocalChannel.ExternalUrl = channelFromGraph.ExternalUrl;
                if (existingLocalChannel.Status != ChannelStatus.Active) existingLocalChannel.Restore(currentUser);
                existingLocalChannel.MarkAsModified(currentUser);
                _channelRepository.Update(existingLocalChannel);
                channelFromGraph = existingLocalChannel;
            }
            // SaveChangesAsync na wyższym poziomie

            _cache.Set(cacheKey, channelFromGraph, GetDefaultCacheEntryOptions());
            return channelFromGraph;
        }

        public async Task<Channel?> GetTeamChannelByDisplayNameAsync(string teamId, string channelDisplayName, string apiAccessToken, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie kanału '{ChannelDisplayName}' dla lokalnego zespołu ID: {TeamId} (serwis). ForceRefresh: {ForceRefresh}", channelDisplayName, teamId, forceRefresh);
            var team = await _teamRepository.GetByIdAsync(teamId);
            if (team == null || string.IsNullOrEmpty(team.ExternalId))
            {
                _logger.LogWarning("Zespół lokalny o ID {TeamId} nie został znaleziony lub nie ma ExternalId.", teamId);
                return null;
            }
            string teamGraphId = team.ExternalId;

            var allChannelsInTeam = await GetTeamChannelsAsync(teamId, apiAccessToken, forceRefresh: true);
            var foundChannel = allChannelsInTeam?.FirstOrDefault(c =>
                c.DisplayName.Equals(channelDisplayName, StringComparison.OrdinalIgnoreCase) &&
                c.TeamId == teamId &&
                c.Status == ChannelStatus.Active
            );

            if (foundChannel == null)
            {
                _logger.LogInformation("Kanał '{ChannelDisplayName}' w zespole ID: {TeamId} nie znaleziony lub nieaktywny.", channelDisplayName, teamId);
            }
            return foundChannel;
        }

        public async Task<Channel?> CreateTeamChannelAsync(string teamId, string displayName, string apiAccessToken, string? description = null, bool isPrivate = false)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Tworzenie kanału '{DisplayName}' w lokalnym zespole ID: {TeamId} przez {User}", displayName, teamId, currentUserUpn);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ChannelCreated,
                nameof(Channel),
                targetEntityName: displayName
            );

            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null || !team.IsActive || string.IsNullOrEmpty(team.ExternalId))
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o lokalnym ID '{teamId}' nie istnieje, jest nieaktywny lub nie ma ExternalId (GraphID)."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można utworzyć kanału: zespół nie istnieje lub jest nieaktywny",
                        "error"
                    );

                    _logger.LogWarning("Nie można utworzyć kanału: Zespół o lokalnym ID '{TeamId}' nie istnieje, jest nieaktywny lub nie ma ExternalId.", teamId);
                    return null;
                }
                string teamGraphId = team.ExternalId;

                var psChannel = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellService.Teams.CreateTeamChannelAsync(teamGraphId, displayName, isPrivate, description),
                    $"CreateTeamChannelAsync dla kanału '{displayName}' w zespole {teamGraphId}"
                );
                if (psChannel == null)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się utworzyć kanału w Microsoft Teams"
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się utworzyć kanału w Microsoft Teams",
                        "error"
                    );

                    _logger.LogError("Nie udało się utworzyć kanału '{DisplayName}' w zespole '{TeamGraphId}' poprzez PowerShell.", displayName, teamGraphId);
                    return null;
                }

                var newChannel = MapPsObjectToLocalChannel(psChannel, teamId);
                newChannel.CreatedBy = currentUserUpn;
                newChannel.CreatedDate = DateTime.UtcNow;
                newChannel.Status = ChannelStatus.Active;

                // 2. Synchronizacja lokalnej bazy danych
                await _channelRepository.AddAsync(newChannel);

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Kanał '{newChannel.DisplayName}' został utworzony pomyślnie",
                    "success"
                );

                // 4. Finalizacja audytu
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Kanał '{newChannel.DisplayName}' utworzony pomyślnie. GraphID: {newChannel.Id}"
                );

                _logger.LogInformation("Kanał '{DisplayName}' (GraphID: {ChannelGraphId}) utworzony pomyślnie w zespole {TeamGraphId} i dodany do lokalnej bazy dla TeamId {LocalTeamId}.", newChannel.DisplayName, newChannel.Id, teamGraphId, teamId);

                _powerShellCacheService.InvalidateChannelsForTeam(teamId);
                return newChannel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia kanału '{DisplayName}' w zespole {TeamId}.", displayName, teamId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas tworzenia kanału: {ex.Message}",
                    "error"
                );

                return null;
            }
        }

        public async Task<Channel?> UpdateTeamChannelAsync(string teamId, string channelId, string apiAccessToken, string? newDisplayName = null, string? newDescription = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Aktualizowanie kanału GraphID: {ChannelId} w lokalnym zespole ID: {TeamId} przez {User}", channelId, teamId, currentUserUpn);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ChannelUpdated,
                nameof(Channel),
                targetEntityId: channelId
            );

            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null || !team.IsActive || string.IsNullOrEmpty(team.ExternalId))
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o lokalnym ID '{teamId}' nie istnieje, jest nieaktywny lub nie ma ExternalId."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować kanału: zespół nie istnieje lub jest nieaktywny",
                        "error"
                    );
                    return null;
                }
                string teamGraphId = team.ExternalId;

                var localChannel = (await _channelRepository.FindAsync(c => c.Id == channelId && c.TeamId == teamId)).FirstOrDefault();
                if (localChannel == null)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Lokalny rekord kanału o GraphID '{channelId}' w zespole '{teamId}' nie został znaleziony."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować kanału: kanał nie został znaleziony",
                        "error"
                    );
                    _logger.LogWarning("Nie znaleziono lokalnego rekordu dla kanału GraphID {ChannelId} w zespole {TeamId} do aktualizacji.", channelId, teamId);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(newDisplayName) && newDescription == null)
                {
                    _logger.LogInformation("Brak zmian do zastosowania dla kanału {ChannelId}.", channelId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        "Brak zmian do zastosowania."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Kanał został sprawdzony - brak zmian do zastosowania",
                        "info"
                    );
                    return localChannel;
                }

                bool psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellService.Teams.UpdateTeamChannelAsync(teamGraphId, channelId, newDisplayName, newDescription),
                    $"UpdateTeamChannelAsync dla kanału {channelId} w zespole {teamGraphId}"
                );

                if (!psSuccess)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się zaktualizować kanału w Microsoft Teams."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się zaktualizować kanału w Microsoft Teams",
                        "error"
                    );

                    _logger.LogError("Nie udało się zaktualizować kanału GraphID '{ChannelId}' w zespole '{TeamGraphId}' poprzez PowerShell.", channelId, teamGraphId);
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(newDisplayName)) localChannel.DisplayName = newDisplayName;
                if (newDescription != null) localChannel.Description = newDescription;
                localChannel.MarkAsModified(currentUserUpn);
                _channelRepository.Update(localChannel);

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Kanał '{localChannel.DisplayName}' został zaktualizowany",
                    "success"
                );

                // 4. Finalizacja audytu
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Kanał ID '{channelId}' zaktualizowany."
                );

                _logger.LogInformation("Kanał GraphID {ChannelId} zaktualizowany pomyślnie w Graph i lokalnie.", channelId);
                _powerShellCacheService.InvalidateChannelAndTeam(teamId, channelId);
                return localChannel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji kanału GraphID {ChannelId} w zespole {TeamId}.", channelId, teamId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas aktualizacji kanału: {ex.Message}",
                    "error"
                );
                return null;
            }
        }

        public async Task<bool> RemoveTeamChannelAsync(string teamId, string channelId, string apiAccessToken)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Usuwanie kanału GraphID: {ChannelId} z lokalnego zespołu ID: {TeamId} przez {User}", channelId, teamId, currentUserUpn);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ChannelDeleted,
                nameof(Channel),
                targetEntityId: channelId
            );

            try
            {
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null || !team.IsActive || string.IsNullOrEmpty(team.ExternalId))
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Zespół o lokalnym ID '{teamId}' nie istnieje, jest nieaktywny lub nie ma ExternalId."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można usunąć kanału: zespół nie istnieje lub jest nieaktywny",
                        "error"
                    );
                    return false;
                }
                string teamGraphId = team.ExternalId;

                var localChannel = (await _channelRepository.FindAsync(c => c.Id == channelId && c.TeamId == teamId)).FirstOrDefault();
                if (localChannel == null)
                {
                    _logger.LogWarning("Nie znaleziono lokalnego rekordu dla kanału GraphID {ChannelId} w zespole {TeamId}. Usunięcie z Graph może się nie powieść bez sprawdzenia 'IsGeneral'.", channelId, teamId);
                }
                else
                {
                    if (localChannel.IsGeneral)
                    {
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            "Nie można usunąć kanału General/Ogólny."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można usunąć kanału General/Ogólny",
                            "error"
                        );

                        _logger.LogWarning("Próba usunięcia kanału General/Ogólny (GraphID: {ChannelId}) dla zespołu {TeamGraphId}.", channelId, teamGraphId);
                        return false;
                    }
                }

                bool psSuccess = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _powerShellService.Teams.RemoveTeamChannelAsync(teamGraphId, channelId),
                    $"RemoveTeamChannelAsync dla kanału {channelId} w zespole {teamGraphId}"
                );

                if (!psSuccess)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie udało się usunąć kanału w Microsoft Teams."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się usunąć kanału w Microsoft Teams",
                        "error"
                    );

                    _logger.LogError("Nie udało się usunąć kanału GraphID '{ChannelId}' w zespole '{TeamGraphId}' poprzez PowerShell.", channelId, teamGraphId);
                    return false;
                }

                if (localChannel != null)
                {
                    localChannel.Archive($"Usunięty z Microsoft Teams przez {currentUserUpn}", currentUserUpn);
                    _channelRepository.Update(localChannel);
                }

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Kanał '{localChannel?.DisplayName ?? "N/A"}' został usunięty",
                    "success"
                );

                // 4. Finalizacja audytu
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Kanał ID '{channelId}' usunięty z Microsoft Teams i oznaczony jako nieaktywny/zarchiwizowany lokalnie."
                );

                _logger.LogInformation("Kanał GraphID {ChannelId} ('{ChannelDisplayName}') pomyślnie usunięty z zespołu {TeamGraphId}.", channelId, localChannel?.DisplayName ?? "N/A", teamGraphId);
                _powerShellCacheService.InvalidateChannelAndTeam(teamId, channelId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania kanału GraphID {ChannelId} z zespołu {TeamId}.", channelId, teamId);
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas usuwania kanału: {ex.Message}",
                    "error"
                );
                return false;
            }
        }

        public Task RefreshChannelCacheAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogWarning("Próba odświeżenia cache kanałów dla pustego teamId (lokalnego).");
                return Task.CompletedTask;
            }
            
            _logger.LogInformation("Odświeżanie cache dla kanałów lokalnego zespołu ID: {TeamId}", teamId);
            
            // Usunięcie cache dla zespołu
            _cache.Remove(TeamChannelsCacheKeyPrefix + teamId);
            
            return Task.CompletedTask;
        }
    }
}