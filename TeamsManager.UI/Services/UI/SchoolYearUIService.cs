using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.Models.SchoolYearModels;

namespace TeamsManager.UI.Services.UI
{
    /// <summary>
    /// Serwis UI dla operacji na latach szkolnych
    /// Wrapper nad SchoolYearService z dodatkowymi funkcjami UI
    /// </summary>
    public class SchoolYearUIService
    {
        private readonly ISchoolYearService _schoolYearService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SchoolYearUIService> _logger;

        public SchoolYearUIService(
            ISchoolYearService schoolYearService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<SchoolYearUIService> logger)
        {
            _schoolYearService = schoolYearService ?? throw new ArgumentNullException(nameof(schoolYearService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Pobiera wszystkie aktywne lata szkolne jako modele UI
        /// </summary>
        public async Task<IEnumerable<SchoolYearDisplayModel>> GetAllActiveSchoolYearsAsync(bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Pobieranie wszystkich aktywnych lat szkolnych dla UI");
                
                var schoolYears = await _schoolYearService.GetAllActiveSchoolYearsAsync(forceRefresh);
                var displayModels = schoolYears
                    .Select(sy => new SchoolYearDisplayModel(sy))
                    .OrderByDescending(sy => sy.IsCurrent)
                    .ThenByDescending(sy => sy.StartDate)
                    .ToList();
                
                _logger.LogInformation("Pobrano {Count} lat szkolnych dla UI", displayModels.Count);
                return displayModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania lat szkolnych dla UI");
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    "Błąd podczas pobierania lat szkolnych",
                    "error"
                );
                
                return Enumerable.Empty<SchoolYearDisplayModel>();
            }
        }

        /// <summary>
        /// Pobiera bieżący rok szkolny jako model UI
        /// </summary>
        public async Task<SchoolYearDisplayModel?> GetCurrentSchoolYearAsync(bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Pobieranie bieżącego roku szkolnego dla UI");
                
                var currentYear = await _schoolYearService.GetCurrentSchoolYearAsync(forceRefresh);
                if (currentYear == null)
                {
                    _logger.LogWarning("Brak bieżącego roku szkolnego w systemie");
                    return null;
                }
                
                return new SchoolYearDisplayModel(currentYear);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania bieżącego roku szkolnego dla UI");
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    "Błąd podczas pobierania bieżącego roku szkolnego",
                    "error"
                );
                
                return null;
            }
        }

        /// <summary>
        /// Ustawia rok szkolny jako bieżący z powiadomieniem UI
        /// </summary>
        public async Task<bool> SetCurrentSchoolYearAsync(string schoolYearId, string schoolYearName)
        {
            try
            {
                _logger.LogInformation("Ustawianie roku szkolnego {Name} jako bieżący", schoolYearName);
                
                var result = await _schoolYearService.SetCurrentSchoolYearAsync(schoolYearId);
                
                if (result)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Rok szkolny '{schoolYearName}' został ustawiony jako bieżący",
                        "success"
                    );
                    
                    _logger.LogInformation("Pomyślnie ustawiono rok szkolny {Name} jako bieżący", schoolYearName);
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Nie udało się ustawić roku szkolnego '{schoolYearName}' jako bieżący",
                        "error"
                    );
                    
                    _logger.LogWarning("Nie udało się ustawić roku szkolnego {Name} jako bieżący", schoolYearName);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ustawiania roku szkolnego {Name} jako bieżący", schoolYearName);
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas ustawiania roku szkolnego '{schoolYearName}' jako bieżący: {ex.Message}",
                    "error"
                );
                
                return false;
            }
        }

        /// <summary>
        /// Tworzy nowy rok szkolny z powiadomieniem UI
        /// </summary>
        public async Task<SchoolYearDisplayModel?> CreateSchoolYearAsync(
            string name,
            DateTime startDate,
            DateTime endDate,
            string? description = null,
            DateTime? firstSemesterStart = null,
            DateTime? firstSemesterEnd = null,
            DateTime? secondSemesterStart = null,
            DateTime? secondSemesterEnd = null)
        {
            try
            {
                _logger.LogInformation("Tworzenie nowego roku szkolnego {Name}", name);
                
                var newSchoolYear = await _schoolYearService.CreateSchoolYearAsync(
                    name, startDate, endDate, description,
                    firstSemesterStart, firstSemesterEnd,
                    secondSemesterStart, secondSemesterEnd);
                
                if (newSchoolYear != null)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Rok szkolny '{name}' został utworzony",
                        "success"
                    );
                    
                    _logger.LogInformation("Pomyślnie utworzono rok szkolny {Name}", name);
                    return new SchoolYearDisplayModel(newSchoolYear);
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Nie udało się utworzyć roku szkolnego '{name}'",
                        "error"
                    );
                    
                    _logger.LogWarning("Nie udało się utworzyć roku szkolnego {Name}", name);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia roku szkolnego {Name}", name);
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas tworzenia roku szkolnego '{name}': {ex.Message}",
                    "error"
                );
                
                return null;
            }
        }

        /// <summary>
        /// Aktualizuje rok szkolny z powiadomieniem UI
        /// </summary>
        public async Task<bool> UpdateSchoolYearAsync(SchoolYearDisplayModel displayModel)
        {
            try
            {
                _logger.LogInformation("Aktualizacja roku szkolnego {Name}", displayModel.Name);
                
                var schoolYear = displayModel.ToSchoolYear();
                var result = await _schoolYearService.UpdateSchoolYearAsync(schoolYear);
                
                if (result)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Rok szkolny '{displayModel.Name}' został zaktualizowany",
                        "success"
                    );
                    
                    _logger.LogInformation("Pomyślnie zaktualizowano rok szkolny {Name}", displayModel.Name);
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Nie udało się zaktualizować roku szkolnego '{displayModel.Name}'",
                        "error"
                    );
                    
                    _logger.LogWarning("Nie udało się zaktualizować roku szkolnego {Name}", displayModel.Name);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji roku szkolnego {Name}", displayModel.Name);
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas aktualizacji roku szkolnego '{displayModel.Name}': {ex.Message}",
                    "error"
                );
                
                return false;
            }
        }

        /// <summary>
        /// Usuwa (dezaktywuje) rok szkolny z powiadomieniem UI
        /// </summary>
        public async Task<bool> DeleteSchoolYearAsync(string schoolYearId, string schoolYearName)
        {
            try
            {
                _logger.LogInformation("Usuwanie roku szkolnego {Name}", schoolYearName);
                
                var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);
                
                if (result)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Rok szkolny '{schoolYearName}' został usunięty",
                        "success"
                    );
                    
                    _logger.LogInformation("Pomyślnie usunięto rok szkolny {Name}", schoolYearName);
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"Nie udało się usunąć roku szkolnego '{schoolYearName}'",
                        "error"
                    );
                    
                    _logger.LogWarning("Nie udało się usunąć roku szkolnego {Name}", schoolYearName);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania roku szkolnego {Name}", schoolYearName);
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd podczas usuwania roku szkolnego '{schoolYearName}': {ex.Message}",
                    "error"
                );
                
                return false;
            }
        }

        /// <summary>
        /// Odświeża cache lat szkolnych
        /// </summary>
        public async Task RefreshCacheAsync()
        {
            try
            {
                _logger.LogInformation("Odświeżanie cache lat szkolnych");
                await _schoolYearService.RefreshCacheAsync();
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    "Cache lat szkolnych został odświeżony",
                    "success"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas odświeżania cache lat szkolnych");
                
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    "Błąd podczas odświeżania cache lat szkolnych",
                    "error"
                );
            }
        }

        /// <summary>
        /// Sprawdza, czy możliwe jest usunięcie roku szkolnego
        /// </summary>
        public async Task<bool> CanDeleteSchoolYearAsync(string schoolYearId)
        {
            try
            {
                // Sprawdź czy rok szkolny nie jest bieżący
                var currentYear = await _schoolYearService.GetCurrentSchoolYearAsync();
                if (currentYear?.Id == schoolYearId)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie można usunąć bieżącego roku szkolnego",
                        "warning"
                    );
                    return false;
                }
                
                // W przyszłości można dodać sprawdzenie czy rok ma przypisane zespoły
                // var teams = await _teamService.GetTeamsBySchoolYearAsync(schoolYearId);
                // if (teams.Any()) { ... }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania możliwości usunięcia roku szkolnego {SchoolYearId}", schoolYearId);
                return false;
            }
        }

        /// <summary>
        /// Pobiera lata szkolne aktywne w określonym dniu
        /// </summary>
        public async Task<IEnumerable<SchoolYearDisplayModel>> GetSchoolYearsActiveOnDateAsync(DateTime date)
        {
            try
            {
                var schoolYears = await _schoolYearService.GetSchoolYearsActiveOnDateAsync(date);
                return schoolYears.Select(sy => new SchoolYearDisplayModel(sy));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania lat szkolnych aktywnych w dniu {Date}", date);
                return Enumerable.Empty<SchoolYearDisplayModel>();
            }
        }
    }
} 