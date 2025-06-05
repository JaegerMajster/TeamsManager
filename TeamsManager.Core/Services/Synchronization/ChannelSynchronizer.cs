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
    /// Implementacja synchronizatora dla kanałów Microsoft Teams.
    /// Wykorzystuje istniejącą logikę MapPsObjectToLocalChannel.
    /// </summary>
    public class ChannelSynchronizer : GraphSynchronizerBase<Channel>
    {
        private readonly ILogger<ChannelSynchronizer> _channelLogger;

        public ChannelSynchronizer(ILogger<ChannelSynchronizer> logger) 
            : base(logger)
        {
            _channelLogger = logger;
        }

        /// <inheritdoc />
        public override void MapProperties(PSObject graphObject, Channel entity, bool isUpdate = false)
        {
            // Podstawowe właściwości
            entity.Id = GetGraphId(graphObject) ?? Guid.NewGuid().ToString();
            entity.DisplayName = GetPropertyValue<string>(graphObject, "DisplayName", string.Empty) ?? string.Empty;
            entity.Description = GetPropertyValue<string>(graphObject, "Description", string.Empty) ?? string.Empty;
            
            // Typ kanału
            entity.ChannelType = GetPropertyValue<string>(graphObject, "MembershipType", "Standard") ?? "Standard";
            
            // URL
            entity.ExternalUrl = GetPropertyValue<string>(graphObject, "WebUrl");
            
            // Statystyki
            entity.FilesCount = GetPropertyValue<int>(graphObject, "FilesCount", 0);
            entity.FilesSize = GetPropertyValue<long>(graphObject, "FilesSize", 0);
            entity.LastActivityDate = GetPropertyValue<DateTime?>(graphObject, "LastActivityDate");
            entity.LastMessageDate = GetPropertyValue<DateTime?>(graphObject, "LastMessageDate");
            entity.MessageCount = GetPropertyValue<int>(graphObject, "MessageCount", 0);
            
            // Ustawienia
            entity.NotificationSettings = GetPropertyValue<string>(graphObject, "NotificationSettings");
            entity.IsModerationEnabled = GetPropertyValue<bool>(graphObject, "IsModerationEnabled", false);
            entity.Category = GetPropertyValue<string>(graphObject, "Category");
            entity.Tags = GetPropertyValue<string>(graphObject, "Tags");
            entity.SortOrder = GetPropertyValue<int>(graphObject, "SortOrder", 0);
            
            // Walidacja wartości
            if (entity.FilesCount < 0) entity.FilesCount = 0;
            if (entity.FilesSize < 0) entity.FilesSize = 0;
            if (entity.MessageCount < 0) entity.MessageCount = 0;
            
            // Określ czy kanał jest prywatny
            if (entity.ChannelType.Equals("private", StringComparison.OrdinalIgnoreCase))
            {
                entity.IsPrivate = true;
            }
            
            // Określ czy to kanał ogólny
            var isFavoriteByDefault = GetPropertyValue<bool?>(graphObject, "isFavoriteByDefault");
            if ((entity.DisplayName.Equals("General", StringComparison.OrdinalIgnoreCase) ||
                 entity.DisplayName.Equals("Ogólny", StringComparison.OrdinalIgnoreCase)) ||
                 isFavoriteByDefault == true)
            {
                entity.IsGeneral = true;
                if (string.IsNullOrWhiteSpace(entity.ChannelType) || 
                    entity.ChannelType.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    entity.ChannelType = "Standard";
                }
            }
            
            // Status - zawsze aktywny przy synchronizacji z Graph
            if (!isUpdate || entity.Status != ChannelStatus.Active)
            {
                entity.Status = ChannelStatus.Active;
            }
            
            _channelLogger.LogDebug("Zmapowano właściwości kanału {ChannelId} ({DisplayName})", 
                entity.Id, entity.DisplayName);
        }

        /// <inheritdoc />
        public override void ValidateGraphObject(PSObject graphObject)
        {
            var id = GetGraphId(graphObject);
            var displayName = GetPropertyValue<string>(graphObject, "DisplayName");

            if (string.IsNullOrEmpty(id))
            {
                _channelLogger.LogError("Obiekt Graph kanału nie zawiera wymaganego pola 'Id'");
                // Generujemy ID zamiast rzucać wyjątek - zgodnie z obecną logiką
                return;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                throw new ArgumentException("Obiekt Graph kanału nie zawiera wymaganego pola 'DisplayName'");
            }
        }

        /// <inheritdoc />
        protected override async Task<bool> DetectChangesAsync(Channel tempEntity, Channel existingEntity)
        {
            var hasChanges = false;

            // Sprawdź podstawowe właściwości
            if (HasStringChanged(tempEntity.DisplayName, existingEntity.DisplayName))
            {
                _channelLogger.LogDebug("Wykryto zmianę DisplayName: '{Old}' -> '{New}'", 
                    existingEntity.DisplayName, tempEntity.DisplayName);
                hasChanges = true;
            }

            if (HasStringChanged(tempEntity.Description, existingEntity.Description))
            {
                hasChanges = true;
            }

            if (HasStringChanged(tempEntity.ChannelType, existingEntity.ChannelType))
            {
                hasChanges = true;
            }

            if (tempEntity.IsPrivate != existingEntity.IsPrivate)
            {
                hasChanges = true;
            }

            if (tempEntity.IsGeneral != existingEntity.IsGeneral)
            {
                hasChanges = true;
            }

            // Sprawdź statystyki
            if (tempEntity.MessageCount != existingEntity.MessageCount ||
                tempEntity.FilesCount != existingEntity.FilesCount ||
                tempEntity.FilesSize != existingEntity.FilesSize)
            {
                _channelLogger.LogDebug("Wykryto zmianę statystyk kanału");
                hasChanges = true;
            }

            return await Task.FromResult(hasChanges);
        }

        /// <inheritdoc />
        protected override async Task PerformAdditionalSynchronizationAsync(PSObject graphObject, Channel entity, bool isUpdate)
        {
            // Sprawdź czy kanał został usunięty z Graph
            // Jeśli lokalny kanał jest aktywny ale nie ma go w Graph, oznacz jako zarchiwizowany
            
            _channelLogger.LogDebug("Dodatkowa synchronizacja dla kanału {ChannelId} - obecnie pominięta", entity.Id);
            await Task.CompletedTask;
        }
    }
} 