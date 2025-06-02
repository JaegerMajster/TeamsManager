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
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Services
{
    public class ChannelService : IChannelService
    {
        private readonly IPowerShellService _powerShellService;
        private readonly IConfidentialClientApplication _confidentialClientApplication;
        private readonly IGenericRepository<Channel> _channelRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ChannelService> _logger;
        private readonly IMemoryCache _cache;

        private readonly string[] _graphChannelReadScopes = new[] { "Group.Read.All", "Channel.ReadBasic.All", "ChannelSettings.Read.All" };
        private readonly string[] _graphChannelWriteScopes = new[] { "Group.ReadWrite.All", "Channel.Create", "ChannelSettings.ReadWrite.All", "ChannelMember.ReadWrite.All" };

        private const string TeamChannelsCacheKeyPrefix = "Channels_TeamId_";
        private const string ChannelByGraphIdCacheKeyPrefix = "Channel_GraphId_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);
        private static CancellationTokenSource _channelsCacheTokenSource = new CancellationTokenSource();

        public ChannelService(
            IPowerShellService powerShellService,
            IConfidentialClientApplication confidentialClientApplication,
            IGenericRepository<Channel> channelRepository,
            ITeamRepository teamRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<ChannelService> logger,
            IMemoryCache memoryCache)
        {
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _confidentialClientApplication = confidentialClientApplication ?? throw new ArgumentNullException(nameof(confidentialClientApplication));
            _channelRepository = channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));
            _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_channelsCacheTokenSource.Token));
        }

        private async Task<bool> ConnectToGraphOnBehalfOfUserAsync(string? apiAccessToken, string[] scopes)
        {
            if (string.IsNullOrEmpty(apiAccessToken))
            {
                _logger.LogWarning("ConnectToGraphOnBehalfOfUserAsync (ChannelService): Token dostępu API (apiAccessToken) jest pusty lub null.");
                return false;
            }
            try
            {
                var userAssertion = new UserAssertion(apiAccessToken);
                _logger.LogDebug("ConnectToGraphOnBehalfOfUserAsync (ChannelService): Próba uzyskania tokenu OBO dla zakresów: {Scopes}", string.Join(", ", scopes));
                var authResult = await _confidentialClientApplication.AcquireTokenOnBehalfOf(scopes, userAssertion).ExecuteAsync();
                if (string.IsNullOrEmpty(authResult.AccessToken))
                {
                    _logger.LogError("ConnectToGraphOnBehalfOfUserAsync (ChannelService): Nie udało się uzyskać tokenu dostępu do Graph w przepływie OBO.");
                    return false;
                }
                _logger.LogInformation("ConnectToGraphOnBehalfOfUserAsync (ChannelService): Pomyślnie uzyskano token OBO dla Graph.");
                return await _powerShellService.ConnectWithAccessTokenAsync(authResult.AccessToken, scopes);
            }
            // POPRAWKA BŁĘDU CS1061: Zamiast ex.SubError używamy ex.Classification
            catch (MsalUiRequiredException ex) { _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync (ChannelService): Wymagana interakcja użytkownika lub zgoda (MsalUiRequiredException) w przepływie OBO. Scopes: {Scopes}. Błąd: {Classification}. Szczegóły: {MsalErrorMessage}", string.Join(", ", scopes), ex.Classification, ex.Message); return false; }
            catch (MsalServiceException ex) { _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync (ChannelService): Błąd usługi MSAL (OBO). Scopes: {Scopes}. Kod błędu: {MsalErrorCode}. Szczegóły: {MsalErrorMessage}", string.Join(", ", scopes), ex.ErrorCode, ex.Message); return false; }
            catch (Exception ex) { _logger.LogError(ex, "ConnectToGraphOnBehalfOfUserAsync (ChannelService): Nieoczekiwany błąd (OBO). Scopes: {Scopes}.", string.Join(", ", scopes)); return false; }
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
                ExternalUrl = psChannel.Properties["WebUrl"]?.Value?.ToString()
                // Status i IsActive (obliczeniowe) zostaną ustawione później lub na podstawie domyślnych wartości modelu.
                // BaseEntity.IsActive jest domyślnie true.
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
            channel.Status = ChannelStatus.Active; // Domyślnie, pobrany kanał jest aktywny.
                                                   // To automatycznie ustawi Channel.IsActive na true.
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

            if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphChannelReadScopes))
            {
                _logger.LogError("Nie udało się połączyć z Graph w GetTeamChannelsAsync dla zespołu GraphID {TeamGraphId}.", teamGraphId);
                return null;
            }

            var psObjects = await _powerShellService.GetTeamChannelsAsync(teamGraphId);
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

            foreach (var graphChannel in channelsFromGraph)
            {
                var localChannel = localChannels.FirstOrDefault(lc => lc.Id == graphChannel.Id);
                if (localChannel == null)
                {
                    graphChannel.CreatedBy = currentUser;
                    graphChannel.CreatedDate = DateTime.UtcNow;
                    // graphChannel.IsActive jest zarządzane przez Status, a BaseEntity.IsActive jest domyślnie true
                    await _channelRepository.AddAsync(graphChannel);
                    _logger.LogInformation("Dodano nowy kanał lokalnie: {ChannelDisplayName} (GraphID: {ChannelGraphId}) dla zespołu {TeamId}", graphChannel.DisplayName, graphChannel.Id, teamId);
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
                    if (localChannel.Status != ChannelStatus.Active) { localChannel.Restore(currentUser); updated = true; }

                    if (updated)
                    {
                        localChannel.MarkAsModified(currentUser);
                        _channelRepository.Update(localChannel);
                        _logger.LogInformation("Zaktualizowano lokalny kanał: {ChannelDisplayName} (GraphID: {ChannelGraphId}) dla zespołu {TeamId}", localChannel.DisplayName, localChannel.Id, teamId);
                    }
                }
            }

            var finalChannelList = (await _channelRepository.FindAsync(c => c.TeamId == teamId && c.IsActive)).ToList(); // Filtrujemy po BaseEntity.IsActive

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

            if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphChannelReadScopes))
            {
                _logger.LogError("Nie udało się połączyć z Graph w GetTeamChannelByIdAsync dla kanału {ChannelGraphId} w zespole {TeamGraphId}.", channelGraphId, teamGraphId);
                return null;
            }

            // UWAGA: Zakładamy, że PowerShellService będzie miał metodę GetTeamChannelByIdAsync(string teamGraphId, string channelGraphId)
            // Jeśli nie, trzeba będzie pobrać wszystkie i filtrować (co jest nieefektywne)
            // Na razie symulujemy pobranie wszystkich i filtrowanie, jeśli nie ma dedykowanej metody
            var allPsChannels = await _powerShellService.GetTeamChannelsAsync(teamGraphId); // To powinno być GetTeamChannelById
            var psChannel = allPsChannels?.FirstOrDefault(pso => pso.Properties["Id"]?.Value?.ToString() == channelGraphId);

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
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_create_channel";
            var operation = new OperationHistory { Id = Guid.NewGuid().ToString(), Type = OperationType.ChannelCreated, TargetEntityType = nameof(Channel), CreatedBy = currentUserUpn, IsActive = true };
            operation.TargetEntityName = $"{displayName} (w zespole lokalnym ID: {teamId})";
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Tworzenie kanału '{DisplayName}' w lokalnym zespole ID: {TeamId} przez {User}", displayName, teamId, currentUserUpn);
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null || !team.IsActive || string.IsNullOrEmpty(team.ExternalId))
                {
                    operation.MarkAsFailed($"Zespół o lokalnym ID '{teamId}' nie istnieje, jest nieaktywny lub nie ma ExternalId (GraphID).");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Nie można utworzyć kanału: Zespół o lokalnym ID '{TeamId}' nie istnieje, jest nieaktywny lub nie ma ExternalId.", teamId);
                    return null;
                }
                string teamGraphId = team.ExternalId;
                operation.TargetEntityName = $"{displayName} (w zespole GraphID: {teamGraphId})";

                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphChannelWriteScopes))
                {
                    operation.MarkAsFailed("Nie udało się połączyć z Graph w CreateTeamChannelAsync."); await SaveOperationHistoryAsync(operation); return null;
                }

                var psChannel = await _powerShellService.CreateTeamChannelAsync(teamGraphId, displayName, isPrivate, description);
                if (psChannel == null)
                {
                    operation.MarkAsFailed("Nie udało się utworzyć kanału w Microsoft Teams.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie udało się utworzyć kanału '{DisplayName}' w zespole '{TeamGraphId}' poprzez PowerShell.", displayName, teamGraphId);
                    return null;
                }

                var newChannel = MapPsObjectToLocalChannel(psChannel, teamId);
                newChannel.CreatedBy = currentUserUpn;
                newChannel.CreatedDate = DateTime.UtcNow;
                newChannel.Status = ChannelStatus.Active; // Nowy kanał jest aktywny
                // BaseEntity.IsActive domyślnie true, Channel.IsActive (obliczeniowe) będzie true

                await _channelRepository.AddAsync(newChannel);
                // SaveChangesAsync() na wyższym poziomie

                operation.TargetEntityId = newChannel.Id; // Graph ID kanału
                operation.MarkAsCompleted($"Kanał '{newChannel.DisplayName}' utworzony. GraphID: {newChannel.Id}");
                _logger.LogInformation("Kanał '{DisplayName}' (GraphID: {ChannelGraphId}) utworzony pomyślnie w zespole {TeamGraphId} i dodany do lokalnej bazy dla TeamId {LocalTeamId}.", newChannel.DisplayName, newChannel.Id, teamGraphId, teamId);

                InvalidateChannelCache(teamId, newChannel.Id);
                return newChannel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia kanału '{DisplayName}' w zespole {TeamId}.", displayName, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return null;
            }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<Channel?> UpdateTeamChannelAsync(string teamId, string channelId, string apiAccessToken, string? newDisplayName = null, string? newDescription = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update_channel";
            var operation = new OperationHistory { Id = Guid.NewGuid().ToString(), Type = OperationType.ChannelUpdated, TargetEntityType = nameof(Channel), TargetEntityId = channelId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Aktualizowanie kanału GraphID: {ChannelId} w lokalnym zespole ID: {TeamId} przez {User}", channelId, teamId, currentUserUpn);
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null || !team.IsActive || string.IsNullOrEmpty(team.ExternalId))
                {
                    operation.MarkAsFailed($"Zespół o lokalnym ID '{teamId}' nie istnieje, jest nieaktywny lub nie ma ExternalId.");
                    await SaveOperationHistoryAsync(operation); return null;
                }
                string teamGraphId = team.ExternalId;

                var localChannel = (await _channelRepository.FindAsync(c => c.Id == channelId && c.TeamId == teamId)).FirstOrDefault();
                if (localChannel == null)
                {
                    operation.MarkAsFailed($"Lokalny rekord kanału o GraphID '{channelId}' w zespole '{teamId}' nie został znaleziony.");
                    _logger.LogWarning("Nie znaleziono lokalnego rekordu dla kanału GraphID {ChannelId} w zespole {TeamId} do aktualizacji.", channelId, teamId);
                    await SaveOperationHistoryAsync(operation); return null;
                }
                operation.TargetEntityName = $"Kanał '{localChannel.DisplayName}' (ID: {channelId}) w zespole {teamGraphId}";

                if (string.IsNullOrWhiteSpace(newDisplayName) && newDescription == null)
                {
                    _logger.LogInformation("Brak zmian do zastosowania dla kanału {ChannelId}.", channelId);
                    operation.MarkAsCompleted("Brak zmian do zastosowania.");
                    await SaveOperationHistoryAsync(operation);
                    return localChannel;
                }

                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphChannelWriteScopes))
                {
                    operation.MarkAsFailed("Nie udało się połączyć z Graph w UpdateTeamChannelAsync."); await SaveOperationHistoryAsync(operation); return null;
                }

                // Używamy channelId, zgodnie ze zmodyfikowanym IPowerShellService
                bool psSuccess = await _powerShellService.UpdateTeamChannelAsync(teamGraphId, channelId, newDisplayName, newDescription);

                if (!psSuccess)
                {
                    operation.MarkAsFailed("Nie udało się zaktualizować kanału w Microsoft Teams.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie udało się zaktualizować kanału GraphID '{ChannelId}' w zespole '{TeamGraphId}' poprzez PowerShell.", channelId, teamGraphId);
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(newDisplayName)) localChannel.DisplayName = newDisplayName;
                if (newDescription != null) localChannel.Description = newDescription;
                localChannel.MarkAsModified(currentUserUpn);
                _channelRepository.Update(localChannel);
                // SaveChangesAsync() na wyższym poziomie

                operation.MarkAsCompleted($"Kanał ID '{channelId}' zaktualizowany.");
                _logger.LogInformation("Kanał GraphID {ChannelId} zaktualizowany pomyślnie w Graph i lokalnie.", channelId);
                InvalidateChannelCache(teamId, channelId);
                return localChannel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji kanału GraphID {ChannelId} w zespole {TeamId}.", channelId, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return null;
            }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public async Task<bool> RemoveTeamChannelAsync(string teamId, string channelId, string apiAccessToken)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_channel";
            var operation = new OperationHistory { Id = Guid.NewGuid().ToString(), Type = OperationType.ChannelDeleted, TargetEntityType = nameof(Channel), TargetEntityId = channelId, CreatedBy = currentUserUpn, IsActive = true };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Usuwanie kanału GraphID: {ChannelId} z lokalnego zespołu ID: {TeamId} przez {User}", channelId, teamId, currentUserUpn);
                var team = await _teamRepository.GetByIdAsync(teamId);
                if (team == null || !team.IsActive || string.IsNullOrEmpty(team.ExternalId))
                {
                    operation.MarkAsFailed($"Zespół o lokalnym ID '{teamId}' nie istnieje, jest nieaktywny lub nie ma ExternalId.");
                    await SaveOperationHistoryAsync(operation); return false;
                }
                string teamGraphId = team.ExternalId;

                var localChannel = (await _channelRepository.FindAsync(c => c.Id == channelId && c.TeamId == teamId)).FirstOrDefault();
                if (localChannel == null)
                {
                    _logger.LogWarning("Nie znaleziono lokalnego rekordu dla kanału GraphID {ChannelId} w zespole {TeamId}. Usunięcie z Graph może się nie powieść bez sprawdzenia 'IsGeneral'.", channelId, teamId);
                    operation.TargetEntityName = $"Kanał ID: {channelId} (lokalnie nieznaleziony) w zespole {teamGraphId}";
                    // Jeśli nie ma lokalnie, a mimo to próbujemy usunąć z Graph, nie możemy sprawdzić IsGeneral
                    // Rozważ zwrócenie błędu lub logowanie wysokiego ryzyka.
                    // Na razie, dla uproszczenia, pozwalamy na próbę, ale PowerShellService powinien obsłużyć błąd Graph.
                }
                else
                {
                    operation.TargetEntityName = $"Kanał '{localChannel.DisplayName}' (ID: {channelId}) w zespole {teamGraphId}";
                    if (localChannel.IsGeneral) // Sprawdzenie na podstawie lokalnego rekordu
                    {
                        operation.MarkAsFailed("Nie można usunąć kanału General/Ogólny.");
                        _logger.LogWarning("Próba usunięcia kanału General/Ogólny (GraphID: {ChannelId}) dla zespołu {TeamGraphId}.", channelId, teamGraphId);
                        await SaveOperationHistoryAsync(operation);
                        return false;
                    }
                }

                if (!await ConnectToGraphOnBehalfOfUserAsync(apiAccessToken, _graphChannelWriteScopes))
                {
                    operation.MarkAsFailed("Nie udało się połączyć z Graph w RemoveTeamChannelAsync."); await SaveOperationHistoryAsync(operation); return false;
                }

                // Używamy channelId, zgodnie ze zmodyfikowanym IPowerShellService
                bool psSuccess = await _powerShellService.RemoveTeamChannelAsync(teamGraphId, channelId);

                if (!psSuccess)
                {
                    operation.MarkAsFailed("Nie udało się usunąć kanału w Microsoft Teams.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Nie udało się usunąć kanału GraphID '{ChannelId}' w zespole '{TeamGraphId}' poprzez PowerShell.", channelId, teamGraphId);
                    return false;
                }

                if (localChannel != null)
                {
                    localChannel.Archive($"Usunięty z Microsoft Teams przez {currentUserUpn}", currentUserUpn); // Używa metody z modelu Channel
                    // localChannel.Status = ChannelStatus.Archived; // Metoda Archive już to robi
                    // localChannel.MarkAsDeleted(currentUserUpn); // Metoda Archive już wywołuje MarkAsModified
                    _channelRepository.Update(localChannel);
                    // SaveChangesAsync() na wyższym poziomie
                }

                operation.MarkAsCompleted($"Kanał ID '{channelId}' usunięty z Microsoft Teams i oznaczony jako nieaktywny/zarchiwizowany lokalnie.");
                _logger.LogInformation("Kanał GraphID {ChannelId} ('{ChannelDisplayName}') pomyślnie usunięty z zespołu {TeamGraphId}.", channelId, localChannel?.DisplayName ?? "N/A", teamGraphId);
                InvalidateChannelCache(teamId, channelId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania kanału GraphID {ChannelId} z zespołu {TeamId}.", channelId, teamId);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.StackTrace); return false;
            }
            finally { await SaveOperationHistoryAsync(operation); }
        }

        public Task RefreshChannelCacheAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogWarning("Próba odświeżenia cache kanałów dla pustego teamId (lokalnego).");
                return Task.CompletedTask;
            }
            _logger.LogInformation("Odświeżanie cache dla kanałów lokalnego zespołu ID: {TeamId}", teamId);
            InvalidateChannelCache(teamId, invalidateAllForTeam: true);
            return Task.CompletedTask;
        }

        private void InvalidateChannelCache(string localTeamId, string? channelGraphId = null, bool invalidateAllForTeam = false)
        {
            var team = _teamRepository.GetByIdAsync(localTeamId).GetAwaiter().GetResult();
            if (team == null || string.IsNullOrEmpty(team.ExternalId))
            {
                _logger.LogWarning("Nie można unieważnić cache kanałów, ponieważ zespół lokalny o ID {LocalTeamId} nie ma ExternalId.", localTeamId);
                return;
            }
            string teamGraphId = team.ExternalId; // Używane tylko do logowania tutaj

            _logger.LogDebug("Inwalidacja cache'u kanałów. localTeamId: {LocalTeamId}, teamGraphId: {TeamGraphId}, channelGraphId: {ChannelGraphId}, invalidateAllForTeam: {InvalidateAllForTeam}", localTeamId, teamGraphId, channelGraphId, invalidateAllForTeam);

            var oldTokenSource = Interlocked.Exchange(ref _channelsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }

            // Klucze cache dla listy kanałów używają lokalnego TeamId dla spójności z logiką pobierania
            if (invalidateAllForTeam)
            {
                _cache.Remove(TeamChannelsCacheKeyPrefix + localTeamId);
                _logger.LogDebug("Usunięto z cache listę kanałów dla lokalnego zespołu ID: {LocalTeamId}", localTeamId);
            }
            if (!string.IsNullOrWhiteSpace(channelGraphId))
            {
                _cache.Remove(ChannelByGraphIdCacheKeyPrefix + channelGraphId);
                _logger.LogDebug("Usunięto z cache kanał o GraphID: {ChannelGraphId}", channelGraphId);
                // Jeśli usunięto/zaktualizowano konkretny kanał, warto też usunąć listę kanałów dla zespołu
                _cache.Remove(TeamChannelsCacheKeyPrefix + localTeamId);
                _logger.LogDebug("Usunięto z cache listę kanałów dla lokalnego zespołu ID: {LocalTeamId} (z powodu zmiany w kanale {ChannelGraphId})", localTeamId, channelGraphId);
            }
        }

        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_channel_op";

            if (operation.StartedAt == default && (operation.Status != OperationStatus.Pending))
            {
                operation.StartedAt = DateTime.UtcNow;
            }
            if (operation.IsCompleted && !operation.CompletedAt.HasValue)
            {
                operation.CompletedAt = DateTime.UtcNow;
            }
            if (operation.CompletedAt.HasValue && operation.StartedAt != default && !operation.Duration.HasValue)
            {
                operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
            }
            await _operationHistoryRepository.AddAsync(operation);
            _logger.LogDebug("Zapisano wpis historii operacji ID: {OperationId} dla operacji na kanale.", operation.Id);
        }
    }
}