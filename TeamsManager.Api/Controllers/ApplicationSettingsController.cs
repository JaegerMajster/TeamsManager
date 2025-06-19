using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Api.Controllers
{
    public class SaveSettingRequestDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public SettingType Type { get; set; } = SettingType.String;
        public string? Description { get; set; }
        public string? Category { get; set; }
    }

    public class UpdateSettingRequestDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public SettingType Type { get; set; } = SettingType.String;
        public string? Category { get; set; }
        public bool IsRequired { get; set; } = false;
        public bool IsVisible { get; set; } = true;
        public string? DefaultValue { get; set; }
        public string? ValidationPattern { get; set; }
        public string? ValidationMessage { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }


    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ApplicationSettingsController : ControllerBase
    {
        private readonly IApplicationSettingService _applicationSettingService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ApplicationSettingsController> _logger;

        public ApplicationSettingsController(
            IApplicationSettingService applicationSettingService, 
            ICurrentUserService currentUserService,
            ILogger<ApplicationSettingsController> logger)
        {
            _applicationSettingService = applicationSettingService ?? throw new ArgumentNullException(nameof(applicationSettingService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("key/{key}")]
        public async Task<IActionResult> GetSettingByKey(string key)
        {
            // Dekodowanie klucza z URL
            var decodedKey = System.Net.WebUtility.UrlDecode(key);
            _logger.LogInformation("Pobieranie ustawienia aplikacji o kluczu: {SettingKey}", decodedKey);
            var setting = await _applicationSettingService.GetSettingByKeyAsync(decodedKey);
            if (setting == null)
            {
                _logger.LogInformation("Ustawienie aplikacji o kluczu: {SettingKey} nie zostało znalezione.", decodedKey);
                return NotFound(new { Message = $"Ustawienie aplikacji o kluczu '{decodedKey}' nie zostało znalezione." });
            }
            return Ok(setting);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSettings()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych ustawień aplikacji.");
            var settings = await _applicationSettingService.GetAllSettingsAsync();
            return Ok(settings);
        }

        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetSettingsByCategory(string category)
        {
            var decodedCategory = System.Net.WebUtility.UrlDecode(category);
            _logger.LogInformation("Pobieranie aktywnych ustawień aplikacji dla kategorii: {Category}", decodedCategory);
            var settings = await _applicationSettingService.GetSettingsByCategoryAsync(decodedCategory);
            return Ok(settings);
        }

        [HttpPost]
        public async Task<IActionResult> SaveSetting([FromBody] SaveSettingRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie zapisania/aktualizacji ustawienia: Klucz={SettingKey}", requestDto.Key);

            var success = await _applicationSettingService.SaveSettingAsync(
                requestDto.Key,
                requestDto.Value,
                requestDto.Type,
                requestDto.Description,
                requestDto.Category
            );

            if (success)
            {
                _logger.LogInformation("Ustawienie o kluczu: {SettingKey} zapisane/zaktualizowane pomyślnie.", requestDto.Key);
                // Pobranie zapisanego ustawienia do zwrócenia w odpowiedzi
                var savedSetting = await _applicationSettingService.GetSettingByKeyAsync(requestDto.Key);
                if (savedSetting != null)
                {
                    return Ok(savedSetting);
                }
                return Ok(new { Message = $"Ustawienie '{requestDto.Key}' zostało przetworzone." });
            }
            _logger.LogWarning("Nie udało się zapisać/zaktualizować ustawienia o kluczu: {SettingKey}.", requestDto.Key);
            return BadRequest(new { Message = "Nie udało się zapisać/zaktualizować ustawienia. Sprawdź logi serwera." });
        }

        [HttpPut("{settingId}")]
        // [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateSetting(string settingId, [FromBody] UpdateSettingRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie aktualizacji ustawienia ID: {SettingId}", settingId);

            var existingSetting = await _applicationSettingService.GetSettingByKeyAsync(requestDto.Key); // Lub GetByIdAsync jeśli ID jest głównym identyfikatorem
            if (existingSetting == null || existingSetting.Id != settingId) // Dodatkowe sprawdzenie, czy klucz pasuje do ID
            {
                _logger.LogWarning("Nie znaleziono ustawienia o ID: {SettingId} i kluczu: {Key} do aktualizacji.", settingId, requestDto.Key);
                return NotFound(new { Message = $"Ustawienie o ID '{settingId}' (lub kluczu '{requestDto.Key}') nie zostało znalezione." });
            }

            // Mapowanie z DTO
            existingSetting.Key = requestDto.Key; // Klucz może się zmienić, jeśli logika na to pozwala
            existingSetting.Value = requestDto.Value;
            existingSetting.Description = requestDto.Description ?? string.Empty;
            existingSetting.Type = requestDto.Type;
            existingSetting.Category = requestDto.Category ?? "General";
            existingSetting.IsRequired = requestDto.IsRequired;
            existingSetting.IsVisible = requestDto.IsVisible;
            existingSetting.DefaultValue = requestDto.DefaultValue;
            existingSetting.ValidationPattern = requestDto.ValidationPattern;
            existingSetting.ValidationMessage = requestDto.ValidationMessage;
            existingSetting.DisplayOrder = requestDto.DisplayOrder;
            existingSetting.IsActive = requestDto.IsActive;

            var success = await _applicationSettingService.UpdateSettingAsync(existingSetting);
            if (success)
            {
                _logger.LogInformation("Ustawienie ID: {SettingId} zaktualizowane pomyślnie.", settingId);
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować ustawienia ID: {SettingId}.", settingId);
            return BadRequest(new { Message = "Nie udało się zaktualizować ustawienia." });
        }


        [HttpDelete("key/{key}")]
        // [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteSetting(string key)
        {
            var decodedKey = System.Net.WebUtility.UrlDecode(key);
            _logger.LogInformation("Żądanie usunięcia ustawienia o kluczu: {SettingKey}", decodedKey);
            var success = await _applicationSettingService.DeleteSettingAsync(decodedKey);
            if (success)
            {
                _logger.LogInformation("Ustawienie o kluczu: {SettingKey} usunięte (zdezaktywowane) pomyślnie.", decodedKey);
                return Ok(new { Message = "Ustawienie aplikacji usunięte (zdezaktywowane) pomyślnie." });
            }
            // Serwis powinien obsłużyć przypadek, gdy ustawienie nie istnieje
            _logger.LogWarning("Nie udało się usunąć (zdezaktywować) ustawienia o kluczu: {SettingKey}. Możliwe, że nie istnieje lub było już nieaktywne.", decodedKey);
            return BadRequest(new { Message = "Nie udało się usunąć (zdezaktywować) ustawienia aplikacji." });
        }
    }
}