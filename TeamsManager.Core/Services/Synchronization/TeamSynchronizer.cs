using System;
using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Helpers.PowerShell;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Services.Synchronization
{
    /// <summary>
    /// Implementacja synchronizatora dla zespołów Microsoft Teams.
    /// Mapuje dane z Microsoft Graph do lokalnej encji Team.
    /// </summary>
    public class TeamSynchronizer : GraphSynchronizerBase<Team>
    {
        private readonly ILogger<TeamSynchronizer> _teamLogger;

        public TeamSynchronizer(ILogger<TeamSynchronizer> logger) 
            : base(logger)
        {
            _teamLogger = logger;
        }

        /// <inheritdoc />
        public override void MapProperties(PSObject graphObject, Team entity, bool isUpdate = false)
        {
            // Podstawowe właściwości zespołu
            entity.ExternalId = GetGraphId(graphObject);
            entity.DisplayName = GetPropertyValue<string>(graphObject, "DisplayName", string.Empty) ?? string.Empty;
            entity.Description = GetPropertyValue<string>(graphObject, "Description", string.Empty) ?? string.Empty;
            
            // Widoczność zespołu
            var visibilityString = GetPropertyValue<string>(graphObject, "Visibility", "Private");
            entity.Visibility = ParseVisibility(visibilityString);

            // Status archiwizacji
            var isArchived = GetPropertyValue<bool>(graphObject, "IsArchived", false);
            
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

            // Właściciel zespołu - może wymagać dodatkowego wywołania API
            var owners = GetPropertyValue<object[]>(graphObject, "Owners", null);
            if (owners != null && owners.Length > 0)
            {
                // Pobierz pierwszy UPN właściciela jeśli dostępny
                if (owners[0] is PSObject ownerObj)
                {
                    var ownerUpn = PSObjectMapper.GetString(ownerObj, "UserPrincipalName");
                    if (!string.IsNullOrEmpty(ownerUpn))
                    {
                        entity.Owner = ownerUpn;
                    }
                }
            }

            // Daty utworzenia/modyfikacji z Graph
            var createdDateTime = GetPropertyValue<DateTime?>(graphObject, "CreatedDateTime", null);
            if (createdDateTime.HasValue && !isUpdate)
            {
                entity.CreatedDate = createdDateTime.Value.ToUniversalTime();
            }

            // Dodatkowe właściwości Teams
            var teamSettings = GetPropertyValue<PSObject>(graphObject, "TeamSettings", null);
            if (teamSettings != null)
            {
                MapTeamSettings(teamSettings, entity);
            }

            // Statystyki zespołu
            var memberCount = GetPropertyValue<int?>(graphObject, "MemberCount", null);
            if (memberCount.HasValue)
            {
                // Można użyć do walidacji czy lokalna liczba członków jest aktualna
                _teamLogger.LogDebug("Zespół {TeamId} ma {MemberCount} członków w Graph", 
                    entity.ExternalId, memberCount.Value);
            }

            _teamLogger.LogDebug("Zmapowano właściwości zespołu {TeamId} ({DisplayName})", 
                entity.ExternalId, entity.DisplayName);
        }

        /// <inheritdoc />
        public override void ValidateGraphObject(PSObject graphObject)
        {
            var id = GetGraphId(graphObject);
            var displayName = GetPropertyValue<string>(graphObject, "DisplayName", null);

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
        private void MapTeamSettings(PSObject teamSettings, Team entity)
        {
            try
            {
                // Mapowanie ustawień członkostwa
                var memberSettings = GetPropertyValue<PSObject>(teamSettings, "MemberSettings", null);
                if (memberSettings != null)
                {
                    var allowCreateUpdateChannels = PSObjectMapper.GetBoolean(memberSettings, "AllowCreateUpdateChannels", true);
                    var allowDeleteChannels = PSObjectMapper.GetBoolean(memberSettings, "AllowDeleteChannels", true);
                    // Można rozszerzyć model Team o te właściwości jeśli potrzebne
                }

                // Mapowanie ustawień wiadomości
                var messagingSettings = GetPropertyValue<PSObject>(teamSettings, "MessagingSettings", null);
                if (messagingSettings != null)
                {
                    var allowUserEditMessages = PSObjectMapper.GetBoolean(messagingSettings, "AllowUserEditMessages", true);
                    var allowUserDeleteMessages = PSObjectMapper.GetBoolean(messagingSettings, "AllowUserDeleteMessages", true);
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
        protected override async Task PerformAdditionalSynchronizationAsync(PSObject graphObject, Team entity, bool isUpdate)
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
    }
} 