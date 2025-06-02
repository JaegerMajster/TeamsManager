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

    public class CreateSchoolYearRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Description { get; set; }
        public DateTime? FirstSemesterStart { get; set; }
        public DateTime? FirstSemesterEnd { get; set; }
        public DateTime? SecondSemesterStart { get; set; }
        public DateTime? SecondSemesterEnd { get; set; }
        // IsCurrent jest zarządzane przez dedykowany endpoint SetCurrentSchoolYear
    }

    public class UpdateSchoolYearRequestDto
    {
        // Id roku szkolnego będzie pobierane z URL
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Description { get; set; }
        public DateTime? FirstSemesterStart { get; set; }
        public DateTime? FirstSemesterEnd { get; set; }
        public DateTime? SecondSemesterStart { get; set; }
        public DateTime? SecondSemesterEnd { get; set; }
        public bool IsActive { get; set; } = true;
        // IsCurrent nie jest tutaj modyfikowane, użyj dedykowanego endpointu
    }

    // --- Kontroler ---

    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")] // Trasa bazowa: /api/v1.0/SchoolYears
    [Authorize] // Wszystkie operacje na latach szkolnych domyślnie wymagają autoryzacji
    public class SchoolYearsController : ControllerBase
    {
        private readonly ISchoolYearService _schoolYearService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SchoolYearsController> _logger;

        public SchoolYearsController(
            ISchoolYearService schoolYearService, 
            ICurrentUserService currentUserService,
            ILogger<SchoolYearsController> logger)
        {
            _schoolYearService = schoolYearService ?? throw new ArgumentNullException(nameof(schoolYearService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{schoolYearId}")]
        public async Task<IActionResult> GetSchoolYearById(string schoolYearId)
        {
            _logger.LogInformation("Pobieranie roku szkolnego o ID: {SchoolYearId}", schoolYearId);
            var schoolYear = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId);
            if (schoolYear == null)
            {
                _logger.LogInformation("Rok szkolny o ID: {SchoolYearId} nie został znaleziony.", schoolYearId);
                return NotFound(new { Message = $"Rok szkolny o ID '{schoolYearId}' nie został znaleziony." });
            }
            return Ok(schoolYear);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllActiveSchoolYears()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych lat szkolnych.");
            var schoolYears = await _schoolYearService.GetAllActiveSchoolYearsAsync();
            return Ok(schoolYears);
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentSchoolYear()
        {
            _logger.LogInformation("Pobieranie bieżącego roku szkolnego.");
            var schoolYear = await _schoolYearService.GetCurrentSchoolYearAsync();
            if (schoolYear == null)
            {
                _logger.LogInformation("Nie znaleziono bieżącego roku szkolnego.");
                // Zwracamy 200 OK z nullem lub pustym obiektem, zamiast 404,
                // ponieważ brak bieżącego roku nie jest błędem, a informacją.
                return Ok(null);
            }
            return Ok(schoolYear);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSchoolYear([FromBody] CreateSchoolYearRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie utworzenia roku szkolnego: {Name}", requestDto.Name);

            var schoolYear = await _schoolYearService.CreateSchoolYearAsync(
                requestDto.Name,
                requestDto.StartDate,
                requestDto.EndDate,
                requestDto.Description,
                requestDto.FirstSemesterStart,
                requestDto.FirstSemesterEnd,
                requestDto.SecondSemesterStart,
                requestDto.SecondSemesterEnd
            );

            if (schoolYear != null)
            {
                _logger.LogInformation("Rok szkolny '{Name}' (ID: {SchoolYearId}) utworzony pomyślnie.", schoolYear.Name, schoolYear.Id);
                return CreatedAtAction(nameof(GetSchoolYearById), new { schoolYearId = schoolYear.Id }, schoolYear);
            }
            _logger.LogWarning("Nie udało się utworzyć roku szkolnego '{Name}'.", requestDto.Name);
            return BadRequest(new { Message = "Nie udało się utworzyć roku szkolnego. Sprawdź logi serwera." });
        }

        [HttpPut("{schoolYearId}")]
        public async Task<IActionResult> UpdateSchoolYear(string schoolYearId, [FromBody] UpdateSchoolYearRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie aktualizacji roku szkolnego ID: {SchoolYearId}", schoolYearId);

            var existingSchoolYear = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId);
            if (existingSchoolYear == null)
            {
                _logger.LogWarning("Nie znaleziono roku szkolnego o ID: {SchoolYearId} do aktualizacji.", schoolYearId);
                return NotFound(new { Message = $"Rok szkolny o ID '{schoolYearId}' nie został znaleziony." });
            }

            // Mapowanie z DTO do obiektu encji
            existingSchoolYear.Name = requestDto.Name;
            existingSchoolYear.StartDate = requestDto.StartDate;
            existingSchoolYear.EndDate = requestDto.EndDate;
            existingSchoolYear.Description = requestDto.Description ?? string.Empty;
            existingSchoolYear.FirstSemesterStart = requestDto.FirstSemesterStart;
            existingSchoolYear.FirstSemesterEnd = requestDto.FirstSemesterEnd;
            existingSchoolYear.SecondSemesterStart = requestDto.SecondSemesterStart;
            existingSchoolYear.SecondSemesterEnd = requestDto.SecondSemesterEnd;
            existingSchoolYear.IsActive = requestDto.IsActive;
            // Flaga IsCurrent jest zarządzana przez dedykowany endpoint

            var success = await _schoolYearService.UpdateSchoolYearAsync(existingSchoolYear);
            if (success)
            {
                _logger.LogInformation("Rok szkolny ID: {SchoolYearId} zaktualizowany pomyślnie.", schoolYearId);
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować roku szkolnego ID: {SchoolYearId}.", schoolYearId);
            return BadRequest(new { Message = "Nie udało się zaktualizować roku szkolnego." });
        }

        [HttpPost("{schoolYearId}/setcurrent")]
        public async Task<IActionResult> SetCurrentSchoolYear(string schoolYearId)
        {
            _logger.LogInformation("Żądanie ustawienia roku szkolnego ID: {SchoolYearId} jako bieżący.", schoolYearId);
            var success = await _schoolYearService.SetCurrentSchoolYearAsync(schoolYearId);
            if (success)
            {
                _logger.LogInformation("Rok szkolny ID: {SchoolYearId} ustawiony jako bieżący pomyślnie.", schoolYearId);
                return Ok(new { Message = $"Rok szkolny ID '{schoolYearId}' został ustawiony jako bieżący." });
            }
            _logger.LogWarning("Nie udało się ustawić roku szkolnego ID: {SchoolYearId} jako bieżący.", schoolYearId);
            // Serwis powinien logować dokładniejszy powód (np. rok nie istnieje, nieaktywny)
            return BadRequest(new { Message = "Nie udało się ustawić roku szkolnego jako bieżący." });
        }

        [HttpDelete("{schoolYearId}")]
        public async Task<IActionResult> DeleteSchoolYear(string schoolYearId)
        {
            _logger.LogInformation("Żądanie usunięcia roku szkolnego ID: {SchoolYearId}", schoolYearId);
            try
            {
                var success = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);
                if (success)
                {
                    _logger.LogInformation("Rok szkolny ID: {SchoolYearId} usunięty (zdezaktywowany) pomyślnie.", schoolYearId);
                    return Ok(new { Message = "Rok szkolny usunięty (zdezaktywowany) pomyślnie." });
                }

                var schoolYear = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId);
                if (schoolYear == null)
                {
                    return NotFound(new { Message = $"Rok szkolny o ID '{schoolYearId}' nie został znaleziony." });
                }
                _logger.LogWarning("Nie udało się usunąć (zdezaktywować) roku szkolnego ID: {SchoolYearId}.", schoolYearId);
                return BadRequest(new { Message = "Nie udało się usunąć (zdezaktywować) roku szkolnego. Sprawdź logi serwera lub zależności." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Nie można usunąć roku szkolnego ID {SchoolYearId}: {ErrorMessage}", schoolYearId, ex.Message);
                return Conflict(new { Message = ex.Message }); // 409 Conflict, np. gdy rok jest bieżący lub ma aktywne zespoły
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas usuwania roku szkolnego ID: {SchoolYearId}", schoolYearId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Wystąpił nieoczekiwany błąd serwera." });
            }
        }
    }
}