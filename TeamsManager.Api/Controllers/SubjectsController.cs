using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning; // Dla atrybutu ApiVersion
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

    public class CreateSubjectRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Description { get; set; }
        public int? Hours { get; set; }
        public string? DefaultSchoolTypeId { get; set; }
        public string? Category { get; set; }
    }

    public class UpdateSubjectRequestDto
    {
        // Id przedmiotu będzie pobierane z URL
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Description { get; set; }
        public int? Hours { get; set; }
        public string? DefaultSchoolTypeId { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // --- Kontroler ---

    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")] // Trasa bazowa: /api/v1.0/Subjects
    [Authorize] // Wszystkie operacje na przedmiotach domyślnie wymagają autoryzacji
    public class SubjectsController : ControllerBase
    {
        private readonly ISubjectService _subjectService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SubjectsController> _logger;

        public SubjectsController(
            ISubjectService subjectService, 
            ICurrentUserService currentUserService,
            ILogger<SubjectsController> logger)
        {
            _subjectService = subjectService ?? throw new ArgumentNullException(nameof(subjectService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{subjectId}")]
        public async Task<IActionResult> GetSubjectById(string subjectId)
        {
            _logger.LogInformation("Pobieranie przedmiotu o ID: {SubjectId}", subjectId);
            var subject = await _subjectService.GetSubjectByIdAsync(subjectId);
            if (subject == null)
            {
                _logger.LogInformation("Przedmiot o ID: {SubjectId} nie został znaleziony.", subjectId);
                return NotFound(new { Message = $"Przedmiot o ID '{subjectId}' nie został znaleziony." });
            }
            return Ok(subject);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllActiveSubjects()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych przedmiotów.");
            var subjects = await _subjectService.GetAllActiveSubjectsAsync();
            return Ok(subjects);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie utworzenia przedmiotu: {SubjectName}, Kod: {SubjectCode}", requestDto.Name, requestDto.Code ?? "N/A");

            var subject = await _subjectService.CreateSubjectAsync(
                requestDto.Name,
                requestDto.Code,
                requestDto.Description,
                requestDto.Hours,
                requestDto.DefaultSchoolTypeId,
                requestDto.Category
            );

            if (subject != null)
            {
                _logger.LogInformation("Przedmiot '{SubjectName}' (ID: {SubjectId}) utworzony pomyślnie.", subject.Name, subject.Id);
                return CreatedAtAction(nameof(GetSubjectById), new { subjectId = subject.Id }, subject);
            }
            _logger.LogWarning("Nie udało się utworzyć przedmiotu '{SubjectName}'.", requestDto.Name);
            return BadRequest(new { Message = "Nie udało się utworzyć przedmiotu. Sprawdź logi serwera." });
        }

        [HttpPut("{subjectId}")]
        public async Task<IActionResult> UpdateSubject(string subjectId, [FromBody] UpdateSubjectRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie aktualizacji przedmiotu ID: {SubjectId}", subjectId);

            var existingSubject = await _subjectService.GetSubjectByIdAsync(subjectId);
            if (existingSubject == null)
            {
                _logger.LogWarning("Nie znaleziono przedmiotu o ID: {SubjectId} do aktualizacji.", subjectId);
                return NotFound(new { Message = $"Przedmiot o ID '{subjectId}' nie został znaleziony." });
            }

            // Mapowanie z DTO na obiekt encji
            existingSubject.Name = requestDto.Name;
            existingSubject.Code = requestDto.Code;
            existingSubject.Description = requestDto.Description;
            existingSubject.Hours = requestDto.Hours;
            existingSubject.DefaultSchoolTypeId = requestDto.DefaultSchoolTypeId;
            existingSubject.Category = requestDto.Category;
            existingSubject.IsActive = requestDto.IsActive;

            var success = await _subjectService.UpdateSubjectAsync(existingSubject);
            if (success)
            {
                _logger.LogInformation("Przedmiot ID: {SubjectId} zaktualizowany pomyślnie.", subjectId);
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować przedmiotu ID: {SubjectId}.", subjectId);
            return BadRequest(new { Message = "Nie udało się zaktualizować przedmiotu." });
        }

        [HttpDelete("{subjectId}")]
        public async Task<IActionResult> DeleteSubject(string subjectId)
        {
            _logger.LogInformation("Żądanie usunięcia przedmiotu ID: {SubjectId}", subjectId);
            var success = await _subjectService.DeleteSubjectAsync(subjectId);
            if (success)
            {
                _logger.LogInformation("Przedmiot ID: {SubjectId} usunięty (zdezaktywowany) pomyślnie.", subjectId);
                return Ok(new { Message = "Przedmiot usunięty (zdezaktywowany) pomyślnie." });
            }
            // Serwis DeleteSubjectAsync powinien zwrócić false jeśli przedmiot nie istnieje lub już jest nieaktywny
            // Sprawdźmy, czy przedmiot w ogóle istnieje, aby zwrócić odpowiedni kod błędu
            var subject = await _subjectService.GetSubjectByIdAsync(subjectId);
            if (subject == null)
            {
                _logger.LogWarning("Nie można usunąć przedmiotu ID: {SubjectId} - nie znaleziono.", subjectId);
                return NotFound(new { Message = $"Przedmiot o ID '{subjectId}' nie został znaleziony." });
            }
            _logger.LogWarning("Nie udało się usunąć (zdezaktywować) przedmiotu ID: {SubjectId}. Możliwe, że był już nieaktywny lub wystąpił inny problem.", subjectId);
            return BadRequest(new { Message = "Nie udało się usunąć (zdezaktywować) przedmiotu." });
        }

        [HttpGet("{subjectId}/teachers")]
        public async Task<IActionResult> GetTeachersForSubject(string subjectId)
        {
            _logger.LogInformation("Pobieranie nauczycieli dla przedmiotu ID: {SubjectId}", subjectId);
            var teachers = await _subjectService.GetTeachersForSubjectAsync(subjectId);
            // GetTeachersForSubjectAsync zwróci pustą listę jeśli przedmiot nie istnieje, jest nieaktywny lub nie ma nauczycieli.
            // Nie ma potrzeby zwracać 404, chyba że chcemy jawnie sprawdzić istnienie przedmiotu.
            return Ok(teachers);
        }
    }
}