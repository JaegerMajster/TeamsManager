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
    /// Implementacja synchronizatora dla użytkowników Microsoft 365.
    /// KRYTYCZNE: Nie nadpisuje soft-deleted users!
    /// </summary>
    public class UserSynchronizer : GraphSynchronizerBase<User>
    {
        private readonly ILogger<UserSynchronizer> _userLogger;

        public UserSynchronizer(ILogger<UserSynchronizer> logger) 
            : base(logger)
        {
            _userLogger = logger;
        }

        /// <inheritdoc />
        public override void MapProperties(PSObject graphObject, User entity, bool isUpdate = false)
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
            var givenName = GetPropertyValue<string>(graphObject, "GivenName", string.Empty);
            var surname = GetPropertyValue<string>(graphObject, "Surname", string.Empty);
            entity.FirstName = givenName ?? string.Empty;
            entity.LastName = surname ?? string.Empty;
            
            // UPN - krytyczne pole
            var upn = GetPropertyValue<string>(graphObject, "UserPrincipalName", string.Empty);
            if (!string.IsNullOrEmpty(upn))
            {
                entity.UPN = upn;
            }
            
            // Dane kontaktowe
            entity.Phone = GetPropertyValue<string>(graphObject, "MobilePhone") 
                          ?? GetPropertyValue<string>(graphObject, "BusinessPhones[0]");
            entity.AlternateEmail = GetPropertyValue<string>(graphObject, "Mail");
            
            // Department z Graph
            var department = GetPropertyValue<string>(graphObject, "Department");
            // UWAGA: DepartmentId wymaga dodatkowego mapowania - nie synchronizujemy tutaj
            
            // Stanowisko
            entity.Position = GetPropertyValue<string>(graphObject, "JobTitle");
            
            // Status konta w M365
            var accountEnabled = GetPropertyValue<bool>(graphObject, "AccountEnabled", true);
            if (!accountEnabled && entity.IsActive)
            {
                _userLogger.LogWarning("Użytkownik {UPN} jest wyłączony w M365 ale aktywny lokalnie", entity.UPN);
                // NIE zmieniamy IsActive automatycznie - to wymaga świadomej decyzji
            }
            
            // Daty
            var createdDateTime = GetPropertyValue<DateTime?>(graphObject, "CreatedDateTime");
            if (createdDateTime.HasValue && !isUpdate)
            {
                entity.CreatedDate = createdDateTime.Value.ToUniversalTime();
            }
            
            // Rozszerzone właściwości
            MapExtendedProperties(graphObject, entity);
            
            _userLogger.LogDebug("Zmapowano właściwości użytkownika {UPN} (ID: {UserId})", 
                entity.UPN, entity.ExternalId);
        }

        /// <inheritdoc />
        public override void ValidateGraphObject(PSObject graphObject)
        {
            var id = GetGraphId(graphObject);
            var upn = GetPropertyValue<string>(graphObject, "UserPrincipalName");

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

        private void MapExtendedProperties(PSObject graphObject, User entity)
        {
            try
            {
                // Mapowanie rozszerzonych atrybutów jeśli dostępne
                var onPremisesDomainName = GetPropertyValue<string>(graphObject, "OnPremisesDomainName");
                var onPremisesSamAccountName = GetPropertyValue<string>(graphObject, "OnPremisesSamAccountName");
                
                // Można rozszerzyć model User o te właściwości jeśli potrzebne
                
                _userLogger.LogDebug("Zmapowano rozszerzone właściwości dla użytkownika {UPN}", entity.UPN);
            }
            catch (Exception ex)
            {
                _userLogger.LogWarning(ex, "Nie udało się zmapować wszystkich rozszerzonych właściwości użytkownika");
            }
        }

        /// <inheritdoc />
        protected override async Task PerformAdditionalSynchronizationAsync(PSObject graphObject, User entity, bool isUpdate)
        {
            // W przyszłości można tutaj dodać:
            // 1. Synchronizację grup użytkownika
            // 2. Synchronizację licencji
            // 3. Synchronizację uprawnień
            // 4. Pobieranie zdjęcia profilowego

            _userLogger.LogDebug("Dodatkowa synchronizacja dla użytkownika {UPN} - obecnie pominięta", entity.UPN);
            await Task.CompletedTask;
        }
    }
} 