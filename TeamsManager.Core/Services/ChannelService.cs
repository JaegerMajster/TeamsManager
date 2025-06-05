// Plik: TeamsManager.Core/Services/ChannelService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Abstractions.Services.Synchronization;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Helpers.PowerShell;

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
        private readonly IPowerShellCacheService _powerShellCacheService;
        private readonly IGraphSynchronizer<Channel> _channelSynchronizer;
        private readonly IUnitOfWork _unitOfWork;

        private const string TeamChannelsCacheKeyPrefix = "Channels_TeamId_";
        private const string ChannelByGraphIdCacheKeyPrefix = "Channel_GraphId_";

        public ChannelService(
            IPowerShellService powerShellService,
            IGenericRepository<Channel> channelRepository,
            ITeamRepository teamRepository,
            IOperationHistoryService operationHistoryService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<ChannelService> logger,
            IPowerShellCacheService powerShellCacheService,
            IGraphSynchronizer<Channel> channelSynchronizer,
            IUnitOfWork unitOfWork)
        {
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _channelRepository = channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));
            _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
            _channelSynchronizer = channelSynchronizer ?? throw new ArgumentNullException(nameof(channelSynchronizer));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }



        private Channel MapPsObjectToLocalChannel(PSObject psChannel, string localTeamId)
        {
            // Użyj PSObjectMapper dla debugowania (opcjonalne)
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                PSObjectMapper.LogProperties(psChannel, _logger, $"Channel for team {localTeamId}");
            }

            // Walidacja i pobranie ID
            var graphChannelId = PSObjectMapper.GetString(psChannel, "Id");
            if (string.IsNullOrWhiteSpace(graphChannelId))
            {
                graphChannelId = Guid.NewGuid().ToString();
                _logger.LogError("MapPsObjectToLocalChannel: PSObject dla kanału nie zawierał poprawnego ID z Graph. Wygenerowano nowe lokalne ID: {GeneratedId}", graphChannelId);
            }

            var channel = new Channel
            {
                Id = graphChannelId,
                DisplayName = PSObjectMapper.GetString(psChannel, "DisplayName", defaultValue: string.Empty)!,
                Description = PSObjectMapper.GetString(psChannel, "Description", defaultValue: string.Empty)!,
                TeamId = localTeamId,
                ChannelType = PSObjectMapper.GetString(psChannel, "MembershipType", defaultValue: "Standard")!,
                ExternalUrl = PSObjectMapper.GetString(psChannel, "WebUrl"),
                
                // Użyj typowanych metod dla różnych typów
                FilesCount = PSObjectMapper.GetInt32(psChannel, "FilesCount", defaultValue: 0),
                FilesSize = PSObjectMapper.GetInt64(psChannel, "FilesSize", defaultValue: 0),
                LastActivityDate = PSObjectMapper.GetDateTime(psChannel, "LastActivityDate"),
                LastMessageDate = PSObjectMapper.GetDateTime(psChannel, "LastMessageDate"),
                MessageCount = PSObjectMapper.GetInt32(psChannel, "MessageCount", defaultValue: 0),
                NotificationSettings = PSObjectMapper.GetString(psChannel, "NotificationSettings"),
                IsModerationEnabled = PSObjectMapper.GetBoolean(psChannel, "IsModerationEnabled", defaultValue: false),
                Category = PSObjectMapper.GetString(psChannel, "Category"),
                Tags = PSObjectMapper.GetString(psChannel, "Tags"),
                SortOrder = PSObjectMapper.GetInt32(psChannel, "SortOrder", defaultValue: 0),
                
                // Ustaw domyślne wartości dla właściwości z BaseEntity
                // IsActive jest obliczane na podstawie Status, nie ustawiamy go bezpośrednio
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "PowerShell Sync"
            };

            // Dodatkowa walidacja biznesowa
            if (channel.FilesCount < 0) channel.FilesCount = 0;
            if (channel.FilesSize < 0) channel.FilesSize = 0;
            if (channel.MessageCount < 0) channel.MessageCount = 0;

            if (channel.ChannelType.Equals("private", StringComparison.OrdinalIgnoreCase))
            {
                channel.IsPrivate = true;
            }

            bool? isFavoriteByDefault = PSObjectMapper.GetBoolean(psChannel, "isFavoriteByDefault");
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

            if (!forceRefresh && _powerShellCacheService.TryGetValueWithMetrics(cacheKey, out IEnumerable<Channel>? cachedChannels) && cachedChannels != null)
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

            // NOWA LOGIKA: Użyj ChannelSynchronizer zamiast MapPsObjectToLocalChannel
            var localChannels = (await _channelRepository.FindAsync(c => c.TeamId == teamId)).ToList();
            var currentUser = _currentUserService.GetCurrentUserUpn() ?? "system_sync_channels";
            var graphChannelIds = new HashSet<string>();

                         // Synchronizacja kanałów z Graph w transakcji
             await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var pso in psObjects)
                {
                    // Utwórz tymczasowy kanał z TeamId
                    var tempChannel = new Channel { TeamId = teamId };
                    
                    // Użyj synchronizatora do mapowania właściwości
                    await _channelSynchronizer.SynchronizeAsync(pso, tempChannel);
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
                    else if (await _channelSynchronizer.RequiresSynchronizationAsync(pso, localChannel))
                    {
                        // Aktualizacja istniejącego
                        await _channelSynchronizer.SynchronizeAsync(pso, localChannel);
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

            _powerShellCacheService.Set(cacheKey, finalChannelList);
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

            if (!forceRefresh && _powerShellCacheService.TryGetValueWithMetrics(cacheKey, out Channel? cachedChannel) && cachedChannel != null)
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
                    _powerShellCacheService.Set(cacheKey, localChannel);
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
                _powerShellCacheService.Remove(cacheKey);
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

            _powerShellCacheService.Set(cacheKey, channelFromGraph);
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
            _powerShellCacheService.Remove(TeamChannelsCacheKeyPrefix + teamId);

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

            _powerShellCacheService.BatchInvalidateKeys(
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
            
            await _powerShellCacheService.WarmCacheAsync(
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
            _powerShellCacheService.InvalidateByPattern(
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
            var metrics = _powerShellCacheService.GetCacheMetrics();
            return $"Cache Hit Rate: {metrics.HitRate:F1}%, " +
                   $"Total Operations: {metrics.TotalOperations}, " +
                   $"Cache Hits: {metrics.CacheHits}, " +
                   $"Cache Misses: {metrics.CacheMisses}, " +
                   $"Invalidations: {metrics.CacheInvalidations}";
        }
    }
}