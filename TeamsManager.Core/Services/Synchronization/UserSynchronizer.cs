using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Enums;

using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Services.Synchronization
{
    /// <summary>
    /// Implementacja synchronizatora dla użytkowników Microsoft 365.
    /// KRYTYCZNE: Nie nadpisuje soft-deleted users!
    /// </summary>
    public class UserSynchronizer : GraphSynchronizerBase<User, GraphUser>
    {
        private readonly ILogger<UserSynchronizer> _userLogger;

        public UserSynchronizer(ILogger<UserSynchronizer> logger) 
            : base(logger)
        {
            _userLogger = logger;
        }

        /// <inheritdoc />
        public override void MapProperties(GraphUser graphObject, User entity, bool isUpdate = false)
        {
            // KRYTYCZNE: Jeśli użytkownik jest soft-deleted, NIE synchronizuj
            if (isUpdate && !entity.IsActive)
            {
                _userLogger.LogWarning("Pomijam synchronizację soft-deleted użytkownika {UserId}", entity.Id);
                return;
            }

            // Podstawowe właściwości z Graph
            entity.ExternalId = GetGraphId(graphObject);
            
            // Mapowanie imienia i nazwiska
            entity.FirstName = graphObject.GivenName ?? string.Empty;
            entity.LastName = graphObject.Surname ?? string.Empty;
            
            // UPN - krytyczne pole
            if (!string.IsNullOrEmpty(graphObject.UserPrincipalName))
            {
                entity.UPN = graphObject.UserPrincipalName;
            }
            
            // Dane kontaktowe
            entity.Phone = graphObject.MobilePhone ?? graphObject.BusinessPhones?.FirstOrDefault();
            entity.AlternateEmail = graphObject.Mail;
            
            // Stanowisko
            entity.Position = graphObject.JobTitle;
            
            // Status konta w M365
            if (!graphObject.AccountEnabled && entity.IsActive)
            {
                _userLogger.LogWarning("Użytkownik {UPN} jest wyłączony w M365 ale aktywny lokalnie", entity.UPN);
                // NIE zmieniamy IsActive automatycznie - to wymaga świadomej decyzji
            }
            
            // Daty
            if (graphObject.CreatedDateTime.HasValue && !isUpdate)
            {
                entity.CreatedDate = graphObject.CreatedDateTime.Value.ToUniversalTime();
            }
            
            _userLogger.LogDebug("Zmapowano właściwości użytkownika {UPN} (ID: {UserId})", 
                entity.UPN, entity.ExternalId);
        }

        /// <inheritdoc />
        public override void ValidateGraphObject(GraphUser graphObject)
        {
            var id = GetGraphId(graphObject);
            var upn = graphObject.UserPrincipalName;

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Obiekt Graph użytkownika nie zawiera wymaganego pola 'Id'");
            }

            if (string.IsNullOrEmpty(upn))
            {
                throw new ArgumentException("Obiekt Graph użytkownika nie zawiera wymaganego pola 'UserPrincipalName'");
            }
        }

        /// <inheritdoc />
        protected override async Task<bool> DetectChangesAsync(User tempEntity, User existingEntity)
        {
            // KRYTYCZNE: Nie synchronizuj soft-deleted
            if (!existingEntity.IsActive)
            {
                _userLogger.LogInformation("Pomijam wykrywanie zmian dla soft-deleted użytkownika {UserId}", existingEntity.Id);
                return false;
            }

            var hasChanges = false;

            // Sprawdź podstawowe właściwości
            if (HasStringChanged(tempEntity.FirstName, existingEntity.FirstName))
            {
                _userLogger.LogDebug("Wykryto zmianę FirstName: '{Old}' -> '{New}'", 
                    existingEntity.FirstName, tempEntity.FirstName);
                hasChanges = true;
            }

            if (HasStringChanged(tempEntity.LastName, existingEntity.LastName))
            {
                hasChanges = true;
            }

            if (HasStringChanged(tempEntity.UPN, existingEntity.UPN))
            {
                _userLogger.LogWarning("Wykryto zmianę UPN: '{Old}' -> '{New}'", 
                    existingEntity.UPN, tempEntity.UPN);
                hasChanges = true;
            }

            if (HasStringChanged(tempEntity.Phone, existingEntity.Phone))
            {
                hasChanges = true;
            }

            if (HasStringChanged(tempEntity.Position, existingEntity.Position))
            {
                hasChanges = true;
            }

            return await Task.FromResult(hasChanges);
        }

        private void MapExtendedProperties(GraphUser graphObject, User entity)
        {
            try
            {
                // Mapowanie rozszerzonych atrybutów jeśli dostępne
                string? onPremisesDomainName = null;
                string? onPremisesSamAccountName = null;
                
                // Można rozszerzyć model User o te właściwości jeśli potrzebne
                
                _userLogger.LogDebug("Zmapowano rozszerzone właściwości dla użytkownika {UPN}", entity.UPN);
            }
            catch (Exception ex)
            {
                _userLogger.LogWarning(ex, "Nie udało się zmapować wszystkich rozszerzonych właściwości użytkownika");
            }
        }

        /// <inheritdoc />
        protected override async Task PerformAdditionalSynchronizationAsync(GraphUser graphObject, User entity, bool isUpdate)
        {
            // W przyszłości można tutaj dodać:
            // 1. Synchronizację grup użytkownika
            // 2. Synchronizację licencji
            // 3. Synchronizację uprawnień
            // 4. Pobieranie zdjęcia profilowego

            _userLogger.LogDebug("Dodatkowa synchronizacja dla użytkownika {UPN} - obecnie pominięta", entity.UPN);
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public override string GetGraphId(GraphUser graphObject)
        {
            return graphObject?.Id ?? string.Empty;
        }
    }
} 

