using System;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Enums;

using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Services.Synchronization
{
    /// <summary>
    /// Implementacja synchronizatora dla zespołów Microsoft Teams.
    /// Mapuje dane z Microsoft Graph do lokalnej encji Team.
    /// </summary>
    public class TeamSynchronizer : GraphSynchronizerBase<Team, GraphTeam>
    {
        private readonly ILogger<TeamSynchronizer> _teamLogger;

        public TeamSynchronizer(ILogger<TeamSynchronizer> logger) 
            : base(logger)
        {
            _teamLogger = logger;
        }

        /// <inheritdoc />
        public override void MapProperties(GraphTeam graphObject, Team entity, bool isUpdate = false)
        {
            // Podstawowe właściwości zespołu
            entity.ExternalId = GetGraphId(graphObject);
            entity.DisplayName = graphObject.DisplayName ?? string.Empty;
            entity.Description = graphObject.Description ?? string.Empty;
            
            // Widoczność zespołu
            entity.Visibility = ParseVisibility(graphObject.Visibility);

            // Status archiwizacji
            var isArchived = graphObject.IsArchived;
            
            // WAŻNE: Obsługa statusu i prefiksów archiwizacji
            if (isArchived && entity.Status != TeamStatus.Archived)
            {
                // Zespół jest zarchiwizowany w Graph ale nie lokalnie
                _teamLogger.LogInformation("Zespół {TeamId} jest zarchiwizowany w Graph, aktualizacja statusu lokalnie", entity.ExternalId);
                entity.Status = TeamStatus.Archived;
                
                // Dodaj prefiks jeśli go nie ma
                const string archivePrefix = "ARCHIWALNY - ";
                if (!entity.DisplayName.StartsWith(archivePrefix))
                {
                    entity.DisplayName = archivePrefix + entity.DisplayName;
                }
                if (!string.IsNullOrEmpty(entity.Description) && !entity.Description.StartsWith(archivePrefix))
                {
                    entity.Description = archivePrefix + entity.Description;
                }
            }
            else if (!isArchived && entity.Status == TeamStatus.Archived)
            {
                // Zespół został przywrócony w Graph
                _teamLogger.LogInformation("Zespół {TeamId} został przywrócony w Graph, aktualizacja statusu lokalnie", entity.ExternalId);
                entity.Status = TeamStatus.Active;
                
                // Usuń prefiks używając metod z modelu Team
                entity.DisplayName = entity.GetBaseDisplayName();
                entity.Description = entity.GetBaseDescription();
            }

            // Daty utworzenia/modyfikacji z Graph
            if (graphObject.CreatedDateTime.HasValue && !isUpdate)
            {
                entity.CreatedDate = graphObject.CreatedDateTime.Value.ToUniversalTime();
            }

            // Właściciel zespołu - może wymagać dodatkowego wywołania API
            string[]? owners = null;
            if (owners != null && owners.Length > 0)
            {
                // Pobierz pierwszy UPN właściciela jeśli dostępny
                var firstOwner = owners[0];
                if (!string.IsNullOrEmpty(firstOwner))
                {
                    // Zakładamy że owners zawiera UPN-y jako stringi
                    entity.Owner = firstOwner;
                }
            }

            // Dodatkowe właściwości Teams
            object? teamSettings = null;
            if (teamSettings != null)
            {
                MapTeamSettings(teamSettings, entity);
            }

            int? memberCount = null;
            if (memberCount.HasValue)
            {
                // Można użyć do walidacji czy lokalna liczba członków jest aktualna
                _teamLogger.LogDebug("Zespół {TeamId} ma {MemberCount} członków w Graph", 
                    entity.ExternalId, memberCount.Value);
            }

            // Dodatkowe mapowanie
            string? displayName = graphObject.DisplayName;
            if (!string.IsNullOrEmpty(displayName))
            {
                entity.DisplayName = displayName;
            }

            _teamLogger.LogDebug("Zmapowano właściwości zespołu {TeamId} ({DisplayName})", 
                entity.ExternalId, entity.DisplayName);
        }

        /// <inheritdoc />
        public override void ValidateGraphObject(GraphTeam graphObject)
        {
            var id = GetGraphId(graphObject);
            string? displayName = null;

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Obiekt Graph nie zawiera wymaganego pola 'Id'", nameof(graphObject));
            }

            if (string.IsNullOrEmpty(displayName))
            {
                throw new ArgumentException("Obiekt Graph nie zawiera wymaganego pola 'DisplayName'", nameof(graphObject));
            }

            _teamLogger.LogDebug("Walidacja obiektu Graph zespołu {TeamId} zakończona pomyślnie", id);
        }

        /// <inheritdoc />
        protected override async Task<bool> DetectChangesAsync(Team tempEntity, Team existingEntity)
        {
            var hasChanges = false;

            // Sprawdź podstawowe właściwości
            if (HasStringChanged(tempEntity.DisplayName, existingEntity.DisplayName))
            {
                _teamLogger.LogDebug("Wykryto zmianę DisplayName: '{Old}' -> '{New}'", 
                    existingEntity.DisplayName, tempEntity.DisplayName);
                hasChanges = true;
            }

            if (HasStringChanged(tempEntity.Description, existingEntity.Description))
            {
                _teamLogger.LogDebug("Wykryto zmianę Description");
                hasChanges = true;
            }

            if (tempEntity.Visibility != existingEntity.Visibility)
            {
                _teamLogger.LogDebug("Wykryto zmianę Visibility: {Old} -> {New}", 
                    existingEntity.Visibility, tempEntity.Visibility);
                hasChanges = true;
            }

            if (tempEntity.Status != existingEntity.Status)
            {
                _teamLogger.LogDebug("Wykryto zmianę Status: {Old} -> {New}", 
                    existingEntity.Status, tempEntity.Status);
                hasChanges = true;
            }

            if (HasStringChanged(tempEntity.Owner, existingEntity.Owner))
            {
                _teamLogger.LogDebug("Wykryto zmianę Owner: '{Old}' -> '{New}'", 
                    existingEntity.Owner, tempEntity.Owner);
                hasChanges = true;
            }

            // Sprawdź czy ExternalId się zgadza (krytyczne)
            if (!string.Equals(tempEntity.ExternalId, existingEntity.ExternalId, StringComparison.OrdinalIgnoreCase))
            {
                _teamLogger.LogWarning("ExternalId nie zgadza się! Graph: {GraphId}, Local: {LocalId}", 
                    tempEntity.ExternalId, existingEntity.ExternalId);
                // To może wskazywać na problem z mapowaniem
            }

            return await Task.FromResult(hasChanges);
        }

        /// <summary>
        /// Mapuje ustawienia zespołu z obiektu TeamSettings.
        /// </summary>
        private void MapTeamSettings(object? teamSettings, Team entity)
        {
            try
            {
                // Mapowanie ustawień członkostwa
                object? memberSettings = null;
                if (memberSettings != null)
                {
                    var allowCreateUpdateChannels = GetPropertyValueFromObject<bool>(memberSettings, "AllowCreateUpdateChannels", true);
                    var allowDeleteChannels = GetPropertyValueFromObject<bool>(memberSettings, "AllowDeleteChannels", true);
                    // Można rozszerzyć model Team o te właściwości jeśli potrzebne
                }

                // Mapowanie ustawień wiadomości
                object? messagingSettings = null;
                if (messagingSettings != null)
                {
                    var allowUserEditMessages = GetPropertyValueFromObject<bool>(messagingSettings, "AllowUserEditMessages", true);
                    var allowUserDeleteMessages = GetPropertyValueFromObject<bool>(messagingSettings, "AllowUserDeleteMessages", true);
                    // Można rozszerzyć model Team o te właściwości jeśli potrzebne
                }

                _teamLogger.LogDebug("Zmapowano ustawienia zespołu {TeamId}", entity.ExternalId);
            }
            catch (Exception ex)
            {
                _teamLogger.LogWarning(ex, "Nie udało się zmapować wszystkich ustawień zespołu {TeamId}", 
                    entity.ExternalId);
            }
        }

        /// <summary>
        /// Pobiera wartość właściwości z obiektu dynamic.
        /// </summary>
        private TValue? GetPropertyValueFromObject<TValue>(object? obj, string propertyName, TValue? defaultValue = default)
        {
            if (obj == null) return defaultValue;
            
            try
            {
                dynamic dynamicObj = obj;
                var property = dynamicObj.GetType().GetProperty(propertyName);
                if (property != null)
                {
                    var value = property.GetValue(dynamicObj);
                    if (value is TValue typedValue)
                        return typedValue;
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Parsuje string widoczności do enuma TeamVisibility.
        /// </summary>
        private TeamVisibility ParseVisibility(string? visibility)
        {
            if (string.IsNullOrEmpty(visibility))
                return TeamVisibility.Private;

            return visibility.ToLowerInvariant() switch
            {
                "public" => TeamVisibility.Public,
                "private" => TeamVisibility.Private,
                _ => TeamVisibility.Private
            };
        }

        /// <inheritdoc />
        protected override async Task PerformAdditionalSynchronizationAsync(GraphTeam graphObject, Team entity, bool isUpdate)
        {
            // W przyszłości można tutaj dodać:
            // 1. Synchronizację członków zespołu
            // 2. Synchronizację kanałów
            // 3. Synchronizację uprawnień
            // 4. Synchronizację plików/folderów

            _teamLogger.LogDebug("Dodatkowa synchronizacja dla zespołu {TeamId} - obecnie pominięta", 
                entity.ExternalId);

            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public override string GetGraphId(GraphTeam graphObject)
        {
            return graphObject?.Id ?? string.Empty;
        }
    }
} 
