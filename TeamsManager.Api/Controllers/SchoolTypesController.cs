using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Api.Controllers
{
    // --- Data Transfer Objects (DTO) ---
    // W docelowym projekcie te klasy powinny znaleźć się w osobnym projekcie/folderze

    public class CreateSchoolTypeRequestDto
    {
        public string ShortName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ColorCode { get; set; }
        public int SortOrder { get; set; } = 0;
    }

    public class UpdateSchoolTypeRequestDto
    {
        // Id typu szkoły będzie pobierane z URL
        public string ShortName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ColorCode { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    // --- Kontroler ---

    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")] // Trasa bazowa: /api/v1.0/SchoolTypes
    [Authorize] // Wszystkie operacje na typach szkół domyślnie wymagają autoryzacji
    public class SchoolTypesController : ControllerBase
    {
        private readonly ISchoolTypeService _schoolTypeService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SchoolTypesController> _logger;

        public SchoolTypesController(
            ISchoolTypeService schoolTypeService, 
            ICurrentUserService currentUserService,
            ILogger<SchoolTypesController> logger)
        {
            _schoolTypeService = schoolTypeService ?? throw new ArgumentNullException(nameof(schoolTypeService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{schoolTypeId}")]
        public async Task<IActionResult> GetSchoolTypeById(string schoolTypeId)
        {
            _logger.LogInformation("Pobieranie typu szkoły o ID: {SchoolTypeId}", schoolTypeId);
            var schoolType = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);
            if (schoolType == null)
            {
                _logger.LogInformation("Typ szkoły o ID: {SchoolTypeId} nie został znaleziony.", schoolTypeId);
                return NotFound(new { Message = $"Typ szkoły o ID '{schoolTypeId}' nie został znaleziony." });
            }
            return Ok(schoolType);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllActiveSchoolTypes()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych typów szkół.");
            var schoolTypes = await _schoolTypeService.GetAllActiveSchoolTypesAsync();
            return Ok(schoolTypes);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSchoolType([FromBody] CreateSchoolTypeRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie utworzenia typu szkoły: {ShortName} - {FullName}", requestDto.ShortName, requestDto.FullName);

            var schoolType = await _schoolTypeService.CreateSchoolTypeAsync(
                requestDto.ShortName,
                requestDto.FullName,
                requestDto.Description,
                requestDto.ColorCode,
                requestDto.SortOrder
            );

            if (schoolType != null)
            {
                _logger.LogInformation("Typ szkoły '{FullName}' (ID: {SchoolTypeId}) utworzony pomyślnie.", schoolType.FullName, schoolType.Id);
                return CreatedAtAction(nameof(GetSchoolTypeById), new { schoolTypeId = schoolType.Id }, schoolType);
            }
            _logger.LogWarning("Nie udało się utworzyć typu szkoły '{ShortName} - {FullName}'.", requestDto.ShortName, requestDto.FullName);
            return BadRequest(new { Message = "Nie udało się utworzyć typu szkoły. Sprawdź logi serwera." });
        }

        [HttpPut("{schoolTypeId}")]
        public async Task<IActionResult> UpdateSchoolType(string schoolTypeId, [FromBody] UpdateSchoolTypeRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie aktualizacji typu szkoły ID: {SchoolTypeId}", schoolTypeId);

            var existingSchoolType = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);
            if (existingSchoolType == null)
            {
                _logger.LogWarning("Nie znaleziono typu szkoły o ID: {SchoolTypeId} do aktualizacji.", schoolTypeId);
                return NotFound(new { Message = $"Typ szkoły o ID '{schoolTypeId}' nie został znaleziony." });
            }

            // Zastosuj zmiany z DTO na istniejącym obiekcie
            existingSchoolType.ShortName = requestDto.ShortName;
            existingSchoolType.FullName = requestDto.FullName;
            existingSchoolType.Description = requestDto.Description;
            existingSchoolType.ColorCode = requestDto.ColorCode;
            existingSchoolType.SortOrder = requestDto.SortOrder;
            existingSchoolType.IsActive = requestDto.IsActive;

            var success = await _schoolTypeService.UpdateSchoolTypeAsync(existingSchoolType);
            if (success)
            {
                _logger.LogInformation("Typ szkoły ID: {SchoolTypeId} zaktualizowany pomyślnie.", schoolTypeId);
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować typu szkoły ID: {SchoolTypeId}.", schoolTypeId);
            return BadRequest(new { Message = "Nie udało się zaktualizować typu szkoły." });
        }

        [HttpDelete("{schoolTypeId}")]
        public async Task<IActionResult> DeleteSchoolType(string schoolTypeId)
        {
            _logger.LogInformation("Żądanie usunięcia typu szkoły ID: {SchoolTypeId}", schoolTypeId);
            try
            {
                var success = await _schoolTypeService.DeleteSchoolTypeAsync(schoolTypeId);
                if (success)
                {
                    _logger.LogInformation("Typ szkoły ID: {SchoolTypeId} usunięty (zdezaktywowany) pomyślnie.", schoolTypeId);
                    return Ok(new { Message = "Typ szkoły usunięty (zdezaktywowany) pomyślnie." });
                }
                var schoolType = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);
                if (schoolType == null)
                {
                    return NotFound(new { Message = $"Typ szkoły o ID '{schoolTypeId}' nie został znaleziony." });
                }
                _logger.LogWarning("Nie udało się usunąć (zdezaktywować) typu szkoły ID: {SchoolTypeId}. Możliwe, że był już nieaktywny lub wystąpił inny problem.", schoolTypeId);
                return BadRequest(new { Message = "Nie udało się usunąć (zdezaktywować) typu szkoły." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Nie można usunąć typu szkoły ID {SchoolTypeId}: {ErrorMessage}", schoolTypeId, ex.Message);
                return Conflict(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas usuwania typu szkoły ID: {SchoolTypeId}", schoolTypeId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Wystąpił nieoczekiwany błąd serwera." });
            }
        }

        [HttpPost("{schoolTypeId}/vicedirectors/{viceDirectorUserId}")]
        public async Task<IActionResult> AssignViceDirectorToSchoolType(string schoolTypeId, string viceDirectorUserId)
        {
            _logger.LogInformation("Żądanie przypisania wicedyrektora {ViceDirectorUserId} do typu szkoły {SchoolTypeId}", viceDirectorUserId, schoolTypeId);
            var success = await _schoolTypeService.AssignViceDirectorToSchoolTypeAsync(viceDirectorUserId, schoolTypeId);
            if (success)
            {
                _logger.LogInformation("Wicedyrektor {ViceDirectorUserId} przypisany do typu szkoły {SchoolTypeId} pomyślnie.", viceDirectorUserId, schoolTypeId);
                return Ok(new { Message = "Wicedyrektor pomyślnie przypisany do nadzoru typu szkoły." });
            }
            _logger.LogWarning("Nie udało się przypisać wicedyrektora {ViceDirectorUserId} do typu szkoły {SchoolTypeId}.", viceDirectorUserId, schoolTypeId);
            return BadRequest(new { Message = "Nie udało się przypisać wicedyrektora do typu szkoły." });
        }

        [HttpDelete("{schoolTypeId}/vicedirectors/{viceDirectorUserId}")]
        public async Task<IActionResult> RemoveViceDirectorFromSchoolType(string schoolTypeId, string viceDirectorUserId)
        {
            _logger.LogInformation("Żądanie usunięcia nadzoru wicedyrektora {ViceDirectorUserId} z typu szkoły {SchoolTypeId}", viceDirectorUserId, schoolTypeId);
            var success = await _schoolTypeService.RemoveViceDirectorFromSchoolTypeAsync(viceDirectorUserId, schoolTypeId);
            if (success)
            {
                _logger.LogInformation("Nadzór wicedyrektora {ViceDirectorUserId} usunięty z typu szkoły {SchoolTypeId} pomyślnie.", viceDirectorUserId, schoolTypeId);
                return Ok(new { Message = "Nadzór wicedyrektora pomyślnie usunięty z typu szkoły." });
            }
            _logger.LogWarning("Nie udało się usunąć nadzoru wicedyrektora {ViceDirectorUserId} z typu szkoły {SchoolTypeId}.", viceDirectorUserId, schoolTypeId);
            return BadRequest(new { Message = "Nie udało się usunąć nadzoru wicedyrektora z typu szkoły." });
        }
    }
}