using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.Models.SchoolTypeModels;

namespace TeamsManager.UI.Services.UI
{
    /// <summary>
    /// Serwis UI dla operacji na typach szkół
    /// Wrapper nad SchoolTypeService z dodatkowymi funkcjami UI
    /// </summary>
    public class SchoolTypeUIService
    {
        private readonly ISchoolTypeService _schoolTypeService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SchoolTypeUIService> _logger;

        public SchoolTypeUIService(
            ISchoolTypeService schoolTypeService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<SchoolTypeUIService> logger)
        {
            _schoolTypeService = schoolTypeService ?? throw new ArgumentNullException(nameof(schoolTypeService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<SchoolTypeDisplayModel>> GetAllActiveSchoolTypesAsync()
        {
            try
            {
                var schoolTypes = await _schoolTypeService.GetAllActiveSchoolTypesAsync();
                return schoolTypes
                    .OrderBy(st => st.SortOrder)
                    .ThenBy(st => st.ShortName)
                    .Select(st => new SchoolTypeDisplayModel(st))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania listy typów szkół");
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn(), 
                    "Nie udało się pobrać listy typów szkół", 
                    "error");
                return new List<SchoolTypeDisplayModel>();
            }
        }

        public async Task<SchoolTypeDisplayModel?> GetSchoolTypeByIdAsync(string id)
        {
            try
            {
                var schoolType = await _schoolTypeService.GetSchoolTypeByIdAsync(id);
                return schoolType != null ? new SchoolTypeDisplayModel(schoolType) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania typu szkoły o ID: {SchoolTypeId}", id);
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn(),
                    "Nie udało się pobrać typu szkoły", 
                    "error");
                return null;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> CreateSchoolTypeAsync(
            string shortName, 
            string fullName, 
            string description, 
            string? colorCode, 
            int sortOrder)
        {
            try
            {
                // Walidacja wejściowa
                if (string.IsNullOrWhiteSpace(shortName))
                    return (false, "Skrót nazwy jest wymagany");

                if (string.IsNullOrWhiteSpace(fullName))
                    return (false, "Pełna nazwa jest wymagana");

                // Walidacja koloru
                if (!string.IsNullOrWhiteSpace(colorCode) && !IsValidHexColor(colorCode))
                    return (false, "Nieprawidłowy format koloru. Użyj formatu HEX (np. #FF5722)");

                var result = await _schoolTypeService.CreateSchoolTypeAsync(
                    shortName.Trim(),
                    fullName.Trim(),
                    description?.Trim() ?? string.Empty,
                    colorCode?.Trim(),
                    sortOrder);

                if (result != null)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn(),
                        $"Typ szkoły '{result.FullName}' został utworzony", 
                        "success");
                    return (true, null);
                }

                return (false, "Nie udało się utworzyć typu szkoły");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Błąd komunikacji z API podczas tworzenia typu szkoły");
                return (false, "Błąd połączenia z serwerem. Sprawdź połączenie sieciowe.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas tworzenia typu szkoły");
                return (false, "Wystąpił nieoczekiwany błąd");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateSchoolTypeAsync(SchoolType schoolType)
        {
            try
            {
                // Walidacja
                if (string.IsNullOrWhiteSpace(schoolType.ShortName))
                    return (false, "Skrót nazwy jest wymagany");

                if (string.IsNullOrWhiteSpace(schoolType.FullName))
                    return (false, "Pełna nazwa jest wymagana");

                if (!string.IsNullOrWhiteSpace(schoolType.ColorCode) && !IsValidHexColor(schoolType.ColorCode))
                    return (false, "Nieprawidłowy format koloru");

                var success = await _schoolTypeService.UpdateSchoolTypeAsync(schoolType);
                
                if (success)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn(),
                        $"Typ szkoły '{schoolType.FullName}' został zaktualizowany", 
                        "success");
                    return (true, null);
                }

                return (false, "Nie udało się zaktualizować typu szkoły");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji typu szkoły {SchoolTypeId}", schoolType.Id);
                return (false, "Wystąpił błąd podczas aktualizacji");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteSchoolTypeAsync(string id, string displayName)
        {
            try
            {
                var result = await _schoolTypeService.DeleteSchoolTypeAsync(id);
                
                if (result)
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn(),
                        $"Typ szkoły '{displayName}' został usunięty", 
                        "success");
                    return (true, null);
                }

                return (false, "Nie udało się usunąć typu szkoły");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Nie można usunąć typu szkoły {SchoolTypeId}", id);
                return (false, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania typu szkoły {SchoolTypeId}", id);
                return (false, "Wystąpił błąd podczas usuwania");
            }
        }

        private bool IsValidHexColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return false;

            // Obsługa formatu z # i bez
            var hex = color.StartsWith("#") ? color : $"#{color}";
            
            return System.Text.RegularExpressions.Regex.IsMatch(
                hex, 
                @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");
        }
    }
} 