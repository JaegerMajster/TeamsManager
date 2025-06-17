// Plik: TeamsManager.Core/Services/ChannelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Abstractions.Services.Synchronization;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Services
{
    public class ChannelService : IChannelService
    {
        private readonly IGraphService _graphService;
        private readonly IGenericRepository<Channel> _channelRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ChannelService> _logger;
        private readonly IGraphCacheService _graphCacheService;
        private readonly IGraphSynchronizer<Channel, GraphChannel> _channelSynchronizer;
        private readonly IUnitOfWork _unitOfWork;

        private const string TeamChannelsCacheKeyPrefix = "Channels_TeamId_";
        private const string ChannelByGraphIdCacheKeyPrefix = "Channel_GraphId_";

        public ChannelService(
            IGraphService graphService,
            IGenericRepository<Channel> channelRepository,
            ITeamRepository teamRepository,
            IOperationHistoryService operationHistoryService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<ChannelService> logger,
            IGraphCacheService graphCacheService,
            IGraphSynchronizer<Channel, GraphChannel> channelSynchronizer,
            IUnitOfWork unitOfWork)
        {
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
            _channelRepository = channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));
            _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _graphCacheService = graphCacheService ?? throw new ArgumentNullException(nameof(graphCacheService));
            _channelSynchronizer = channelSynchronizer ?? throw new ArgumentNullException(nameof(channelSynchronizer));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        private Channel MapGraphChannelToLocalChannel(GraphChannel graphChannel, string localTeamId)
        {
            _logger.LogDebug("Mapowanie GraphChannel do lokalnego Channel dla zespołu {TeamId}", localTeamId);

            // Walidacja i pobranie ID
            var graphChannelId = graphChannel.Id;
            if (string.IsNullOrWhiteSpace(graphChannelId))
            {
                graphChannelId = Guid.NewGuid().ToString();
                _logger.LogError("MapGraphChannelToLocalChannel: GraphChannel nie zawierał poprawnego ID. Wygenerowano nowe lokalne ID: {GeneratedId}", graphChannelId);
            }

            var channel = new Channel
            {
                Id = graphChannelId,
                DisplayName = graphChannel.DisplayName ?? string.Empty,
                Description = graphChannel.Description ?? string.Empty,
                TeamId = localTeamId,
                ChannelType = graphChannel.MembershipType ?? "Standard",
                ExternalUrl = graphChannel.WebUrl,
                
                // Mapowanie statystyk z GraphChannel
                FilesCount = graphChannel.Stats?.FilesCount ?? 0,
                FilesSize = graphChannel.Stats?.FilesSize ?? 0,
                LastActivityDate = graphChannel.Stats?.LastActivityDate,
                LastMessageDate = graphChannel.Stats?.LastMessageDate,
                MessageCount = graphChannel.Stats?.MessageCount ?? 0,
                NotificationSettings = graphChannel.Settings?.NotificationSettings?.ToString(),
                IsModerationEnabled = graphChannel.Settings?.IsModerationEnabled ?? false,
                Category = graphChannel.Settings?.Category,
                Tags = graphChannel.Settings?.Tags != null ? string.Join(",", graphChannel.Settings.Tags) : null,
                SortOrder = graphChannel.Settings?.SortOrder ?? 0,
                
                // Ustaw domyślne wartości dla właściwości z BaseEntity
                CreatedDate = graphChannel.CreatedDateTime ?? DateTime.UtcNow,
                CreatedBy = "Graph API Sync"
            };

            // Dodatkowa walidacja biznesowa
            if (channel.FilesCount < 0) channel.FilesCount = 0;
            if (channel.FilesSize < 0) channel.FilesSize = 0;
            if (channel.MessageCount < 0) channel.MessageCount = 0;

            // Określenie typu kanału
            if (graphChannel.MembershipType?.Equals("private", StringComparison.OrdinalIgnoreCase) == true)
            {
                channel.IsPrivate = true;
            }

            // Określenie czy to kanał główny
            if ((channel.DisplayName.Equals("General", StringComparison.OrdinalIgnoreCase) ||
                 channel.DisplayName.Equals("Ogólny", StringComparison.OrdinalIgnoreCase)) ||
                 graphChannel.IsFavoriteByDefault == true)
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

            if (!forceRefresh && _graphCacheService.TryGetValueWithMetrics(cacheKey, out IEnumerable<Channel>? cachedChannels) && cachedChannels != null)
            {
                _logger.LogDebug("Kanały dla lokalnego zespołu ID {TeamId} (GraphID: {TeamGraphId}) znalezione w cache (serwis).", teamId, teamGraphId);
                return cachedChannels;
            }

            var graphChannels = await _graphService.ExecuteWithAutoConnectAsync(
                apiAccessToken,
                async () => await _graphService.Teams.GetTeamChannelsAsync(teamGraphId),
                $"GetTeamChannelsAsync dla zespołu {teamGraphId}"
            );

            if (graphChannels?.Data == null || !graphChannels.IsSuccess)
            {
                _logger.LogWarning("Nie udało się pobrać kanałów z Graph API dla zespołu GraphID {TeamGraphId}. Błąd: {Error}", 
                    teamGraphId, graphChannels?.ErrorMessage ?? "Nieznany błąd");
                return Enumerable.Empty<Channel>();
            }

            // NOWA LOGIKA: Użyj ChannelSynchronizer zamiast MapGraphChannelToLocalChannel
            var localChannels = (await _channelRepository.FindAsync(c => c.TeamId == teamId)).ToList();
            var currentUser = _currentUserService.GetCurrentUserUpn() ?? "system_sync_channels";
            var graphChannelIds = new HashSet<string>();

                         // Synchronizacja kanałów z Graph w transakcji
             await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var graphChannel in graphChannels.Data)
                {
                    // Użyj synchronizatora do mapowania właściwości
                    var tempChannel = new Channel { TeamId = teamId };
                    await _channelSynchronizer.SynchronizeAsync(graphChannel, tempChannel);
                    graphChannelIds.Add(tempChannel.Id);
                    
                    var localChannel = localChannels.FirstOrDefault(lc => lc.Id == tempChannel.Id);
                    
                    if (localChannel == null)
                    {
                        // Nowy kanał
                        tempChannel.CreatedBy = currentUser;
                        tempChannel.CreatedDate = DateTime.UtcNow;
                        await _unitOfWork.Repository<Channel>().AddAsync(tempChannel);
                        _logger.LogInformation("Dodano nowy kanał: {ChannelDisplayName} (GraphID: {ChannelGraphId}) dla zespołu {TeamId}", 
                            tempChannel.DisplayName, tempChannel.Id, teamId);
                    }
                    else if (await _channelSynchronizer.RequiresSynchronizationAsync(graphChannel, localChannel))
                    {
                        // Aktualizacja istniejącego
                        await _channelSynchronizer.SynchronizeAsync(graphChannel, localChannel);
                        localChannel.MarkAsModified(currentUser);
                        _unitOfWork.Repository<Channel>().Update(localChannel);
                        _logger.LogInformation("Zaktualizowano kanał: {ChannelDisplayName} (GraphID: {ChannelGraphId}) dla zespołu {TeamId}", 
                            localChannel.DisplayName, localChannel.Id, teamId);
                    }
                }

                // Oznacz kanały usunięte z Graph jako zarchiwizowane
                foreach (var localChannel in localChannels.Where(lc => lc.Status == ChannelStatus.Active))
                {
                    if (!graphChannelIds.Contains(localChannel.Id))
                    {
                        localChannel.Archive($"Kanał usunięty z Microsoft Teams", currentUser);
                        _unitOfWork.Repository<Channel>().Update(localChannel);
                        _logger.LogWarning("Kanał {ChannelDisplayName} (GraphID: {ChannelGraphId}) został usunięty z Teams", 
                            localChannel.DisplayName, localChannel.Id);
                    }
                }

                await _unitOfWork.CommitAsync();
                await _unitOfWork.CommitTransactionAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                _logger.LogError(ex, "Błąd podczas synchronizacji kanałów dla zespołu {TeamId}", teamId);
                throw;
            }

            var finalChannelList = (await _channelRepository.FindAsync(c => c.TeamId == teamId && c.IsActive)).ToList();

            _graphCacheService.Set(cacheKey, finalChannelList);
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

            if (!forceRefresh && _graphCacheService.TryGetValueWithMetrics(cacheKey, out Channel? cachedChannel) && cachedChannel != null)
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
                    _graphCacheService.Set(cacheKey, localChannel);
                    return localChannel;
                }
            }

            var graphChannelResult = await _graphService.ExecuteWithAutoConnectAsync(
                apiAccessToken,
                async () => await _graphService.Teams.GetTeamChannelByIdAsync(teamGraphId, channelGraphId),
                $"GetTeamChannelByIdAsync dla kanału {channelGraphId} w zespole {teamGraphId}"
            );

            if (graphChannelResult?.Data == null || !graphChannelResult.IsSuccess)
            {
                _logger.LogInformation("Kanał GraphID: {ChannelGraphId} w zespole GraphID: {TeamGraphId} nie znaleziony przez Graph API. Błąd: {Error}", 
                    channelGraphId, teamGraphId, graphChannelResult?.ErrorMessage ?? "Nieznany błąd");
                _graphCacheService.Remove(cacheKey);
                return null;
            }

            var channelFromGraph = MapGraphChannelToLocalChannel(graphChannelResult.Data, teamId);
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

            _graphCacheService.Set(cacheKey, channelFromGraph);
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

                var graphChannelResult = await _graphService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _graphService.Teams.CreateTeamChannelAsync(teamGraphId, displayName, isPrivate, description),
                    $"CreateTeamChannelAsync dla kanału '{displayName}' w zespole {teamGraphId}"
                );
                
                if (graphChannelResult?.Data == null || !graphChannelResult.IsSuccess)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Nie udało się utworzyć kanału w Microsoft Teams. Błąd: {graphChannelResult?.ErrorMessage ?? "Nieznany błąd"}"
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się utworzyć kanału w Microsoft Teams",
                        "error"
                    );

                    _logger.LogError("Nie udało się utworzyć kanału '{DisplayName}' w zespole '{TeamGraphId}' poprzez Graph API. Błąd: {Error}", 
                        displayName, teamGraphId, graphChannelResult?.ErrorMessage ?? "Nieznany błąd");
                    return null;
                }

                var newChannel = MapGraphChannelToLocalChannel(graphChannelResult.Data, teamId);
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

                _graphCacheService.InvalidateChannelsForTeam(teamId);
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

                var updateResult = await _graphService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _graphService.Teams.UpdateTeamChannelAsync(teamGraphId, channelId, newDisplayName, newDescription),
                    $"UpdateTeamChannelAsync dla kanału {channelId} w zespole {teamGraphId}"
                );

                if (!updateResult.IsSuccess)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Nie udało się zaktualizować kanału w Microsoft Teams. Błąd: {updateResult.ErrorMessage}"
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się zaktualizować kanału w Microsoft Teams",
                        "error"
                    );

                    _logger.LogError("Nie udało się zaktualizować kanału GraphID '{ChannelId}' w zespole '{TeamGraphId}' poprzez Graph API. Błąd: {Error}", 
                        channelId, teamGraphId, updateResult.ErrorMessage);
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
                _graphCacheService.InvalidateChannelAndTeam(teamId, channelId);
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

                var deleteResult = await _graphService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => await _graphService.Teams.RemoveTeamChannelAsync(teamGraphId, channelId),
                    $"RemoveTeamChannelAsync dla kanału {channelId} w zespole {teamGraphId}"
                );

                if (!deleteResult.IsSuccess)
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Nie udało się usunąć kanału w Microsoft Teams. Błąd: {deleteResult.ErrorMessage}"
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie udało się usunąć kanału w Microsoft Teams",
                        "error"
                    );

                    _logger.LogError("Nie udało się usunąć kanału GraphID '{ChannelId}' w zespole '{TeamGraphId}' poprzez Graph API. Błąd: {Error}", 
                        channelId, teamGraphId, deleteResult.ErrorMessage);
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
                _graphCacheService.InvalidateChannelAndTeam(teamId, channelId);
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
            _graphCacheService.Remove(TeamChannelsCacheKeyPrefix + teamId);

            return Task.CompletedTask;
        }

        // ETAP 6/8: Zaawansowane funkcje cache P2

        /// <summary>
        /// Unieważnia wszystkie cache kanałów dla zespołu w jednej operacji batch
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        public async Task InvalidateAllChannelsForTeamAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogWarning("Próba batch invalidation cache kanałów dla pustego teamId.");
                return;
            }

            // Pobierz wszystkie kanały zespołu z bazy, aby znać ich GraphId
            var channels = await _channelRepository.FindAsync(c => c.TeamId == teamId);
            
            var keysToInvalidate = new List<string>
            {
                $"{TeamChannelsCacheKeyPrefix}{teamId}"
            };

            // Dodaj klucze dla poszczególnych kanałów
            foreach (var channel in channels)
            {
                if (!string.IsNullOrWhiteSpace(channel.Id))
                {
                    keysToInvalidate.Add($"{ChannelByGraphIdCacheKeyPrefix}{channel.Id}");
                }
            }

            _graphCacheService.BatchInvalidateKeys(
                keysToInvalidate, 
                $"InvalidateAllChannelsForTeam_{teamId}"
            );

            _logger.LogInformation("Batch invalidation wykonana dla {Count} kluczy cache zespołu {TeamId}", 
                keysToInvalidate.Count, teamId);
        }

        /// <summary>
        /// Wstępnie ładuje cache kanałów dla zespołu (cache warming)
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="apiAccessToken">Token dostępu do API</param>
        public async Task WarmChannelsCacheAsync(string teamId, string apiAccessToken)
        {
            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(apiAccessToken))
            {
                _logger.LogWarning("Próba cache warming z pustym teamId lub tokenem.");
                return;
            }

            var cacheKey = TeamChannelsCacheKeyPrefix + teamId;
            
            await _graphCacheService.WarmCacheAsync(
                cacheKey,
                async () => {
                    _logger.LogInformation("Cache warming: ładowanie kanałów dla zespołu {TeamId}", teamId);
                    var channels = await GetTeamChannelsAsync(teamId, apiAccessToken, forceRefresh: true);
                    return channels ?? Enumerable.Empty<Channel>();
                },
                TimeSpan.FromMinutes(30) // Dłuższy TTL dla warm cache
            );

            _logger.LogInformation("Cache warming wykonane dla kanałów zespołu {TeamId}", teamId);
        }

        /// <summary>
        /// Unieważnia wszystkie cache kanałów na podstawie wzorca
        /// </summary>
        public void InvalidateAllChannelCaches()
        {
            // Usuń wszystkie cache kanałów
            _graphCacheService.InvalidateByPattern(
                "Channel", 
                "InvalidateAllChannels"
            );

            _logger.LogInformation("Pattern-based invalidation wykonana dla wszystkich cache kanałów");
        }

        /// <summary>
        /// Pobiera metryki wydajności cache dla kanałów
        /// </summary>
        /// <returns>Informacje o wydajności cache</returns>
        public string GetChannelCacheMetrics()
        {
            var metrics = _graphCacheService.GetCacheMetrics();
            return $"Cache Hit Rate: {metrics.HitRatio:F1}%, " +
                   $"Total Operations: {metrics.TotalRequests}, " +
                   $"Cache Hits: {metrics.CacheHits}, " +
                   $"Cache Misses: {metrics.CacheMisses}, " +
                   $"Invalidations: {metrics.InvalidationCount}";
        }
    }
}