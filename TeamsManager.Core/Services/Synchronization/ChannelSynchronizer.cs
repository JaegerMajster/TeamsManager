using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using System.Linq;

namespace TeamsManager.Core.Services.Synchronization
{
    /// <summary>
    /// Implementacja synchronizatora dla kanałów Microsoft Teams.
    /// Wykorzystuje GraphChannel z Graph API.
    /// </summary>
    public class ChannelSynchronizer : GraphSynchronizerBase<Channel, GraphChannel>
    {
        private readonly ILogger<ChannelSynchronizer> _channelLogger;

        public ChannelSynchronizer(ILogger<ChannelSynchronizer> logger) 
            : base(logger)
        {
            _channelLogger = logger;
        }

        /// <inheritdoc />
        public override void MapProperties(GraphChannel graphObject, Channel entity, bool isUpdate = false)
        {
            // Podstawowe właściwości
            entity.Id = GetGraphId(graphObject) ?? Guid.NewGuid().ToString();
            entity.DisplayName = graphObject.DisplayName ?? string.Empty;
            entity.Description = graphObject.Description ?? string.Empty;
            
            // Typ kanału
            entity.ChannelType = graphObject.MembershipType ?? "Standard";
            
            // URL
            entity.ExternalUrl = graphObject.WebUrl;
            
            // Statystyki z GraphChannelStats
            entity.FilesCount = graphObject.Stats?.FilesCount ?? 0;
            entity.FilesSize = graphObject.Stats?.FilesSize ?? 0;
            entity.LastActivityDate = graphObject.Stats?.LastActivityDate;
            entity.LastMessageDate = graphObject.Stats?.LastMessageDate;
            entity.MessageCount = graphObject.Stats?.MessageCount ?? 0;
            
            // Ustawienia z GraphChannelSettings
            entity.NotificationSettings = graphObject.Settings?.NotificationSettings?.ToString();
            entity.IsModerationEnabled = graphObject.Settings?.IsModerationEnabled ?? false;
            entity.Category = graphObject.Settings?.Category;
            entity.Tags = graphObject.Settings?.Tags != null ? string.Join(", ", graphObject.Settings.Tags.Cast<string>()) : null;
            entity.SortOrder = graphObject.Settings?.SortOrder ?? 0;
            
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
            if ((entity.DisplayName.Equals("General", StringComparison.OrdinalIgnoreCase) ||
                 entity.DisplayName.Equals("Ogólny", StringComparison.OrdinalIgnoreCase)) ||
                 graphObject.IsFavoriteByDefault == true)
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
        public override void ValidateGraphObject(GraphChannel graphObject)
        {
            var id = GetGraphId(graphObject);
            var displayName = graphObject.DisplayName;

            if (string.IsNullOrEmpty(id))
            {
                _channelLogger.LogError("Obiekt GraphChannel nie zawiera wymaganego pola 'Id'");
                // Generujemy ID zamiast rzucać wyjątek - zgodnie z obecną logiką
                return;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                throw new ArgumentException("Obiekt GraphChannel nie zawiera wymaganego pola 'DisplayName'");
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
        protected override async Task PerformAdditionalSynchronizationAsync(GraphChannel graphObject, Channel entity, bool isUpdate)
        {
            // Sprawdź czy kanał został usunięty z Graph
            // Jeśli lokalny kanał jest aktywny ale nie ma go w Graph, oznacz jako zarchiwizowany
            
            _channelLogger.LogDebug("Dodatkowa synchronizacja dla kanału {ChannelId} - obecnie pominięta", entity.Id);
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public override string GetGraphId(GraphChannel graphObject)
        {
            return graphObject?.Id ?? string.Empty;
        }
    }
} 