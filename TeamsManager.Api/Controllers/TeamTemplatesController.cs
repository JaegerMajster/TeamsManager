using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Api.Controllers
{
    // --- Data Transfer Objects (DTO) ---
    // W docelowym projekcie te klasy powinny znaleźć się w osobnym projekcie/folderze

    public class CreateTeamTemplateRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string TemplateContent { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsUniversal { get; set; } = false;
        public string? SchoolTypeId { get; set; }
        public string Category { get; set; } = "Ogólne";
        public string Language { get; set; } = "Polski";
        public int? MaxLength { get; set; }
        public bool RemovePolishChars { get; set; } = false;
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string Separator { get; set; } = " - ";
        public int SortOrder { get; set; } = 0;
    }

    public class UpdateTeamTemplateRequestDto
    {
        // Id szablonu będzie pobierane z URL
        public string Name { get; set; } = string.Empty;
        public string TemplateContent { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
        public bool IsUniversal { get; set; } = false;
        public string? SchoolTypeId { get; set; }
        public string? ExampleOutput { get; set; }
        public string Category { get; set; } = "Ogólne";
        public string Language { get; set; } = "Polski";
        public int? MaxLength { get; set; }
        public bool RemovePolishChars { get; set; } = false;
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string Separator { get; set; } = " - ";
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class GenerateTeamNameRequestDto
    {
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
    }

    public class CloneTeamTemplateRequestDto
    {
        public string NewTemplateName { get; set; } = string.Empty;
    }

    // --- Kontroler ---

    [ApiController]
    [Route("api/[controller]")] // Trasa bazowa: /api/TeamTemplates
    [Authorize] // Wszystkie operacje na szablonach zespołów domyślnie wymagają autoryzacji
    public class TeamTemplatesController : ControllerBase
    {
        private readonly ITeamTemplateService _teamTemplateService;
        private readonly ILogger<TeamTemplatesController> _logger;

        public TeamTemplatesController(ITeamTemplateService teamTemplateService, ILogger<TeamTemplatesController> logger)
        {
            _teamTemplateService = teamTemplateService ?? throw new ArgumentNullException(nameof(teamTemplateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{templateId}")]
        public async Task<IActionResult> GetTemplateById(string templateId)
        {
            _logger.LogInformation("Pobieranie szablonu zespołu o ID: {TemplateId}", templateId);
            var template = await _teamTemplateService.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                _logger.LogInformation("Szablon zespołu o ID: {TemplateId} nie został znaleziony.", templateId);
                return NotFound(new { Message = $"Szablon zespołu o ID '{templateId}' nie został znaleziony." });
            }
            return Ok(template);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllActiveTemplates()
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych szablonów zespołów.");
            var templates = await _teamTemplateService.GetAllActiveTemplatesAsync();
            return Ok(templates);
        }

        [HttpGet("universal")]
        public async Task<IActionResult> GetUniversalTemplates()
        {
            _logger.LogInformation("Pobieranie aktywnych szablonów uniwersalnych.");
            var templates = await _teamTemplateService.GetUniversalTemplatesAsync();
            return Ok(templates);
        }

        [HttpGet("schooltype/{schoolTypeId}")]
        public async Task<IActionResult> GetTemplatesBySchoolType(string schoolTypeId)
        {
            _logger.LogInformation("Pobieranie aktywnych szablonów dla typu szkoły ID: {SchoolTypeId}", schoolTypeId);
            var templates = await _teamTemplateService.GetTemplatesBySchoolTypeAsync(schoolTypeId);
            return Ok(templates);
        }

        [HttpGet("schooltype/{schoolTypeId}/default")]
        public async Task<IActionResult> GetDefaultTemplateForSchoolType(string schoolTypeId)
        {
            _logger.LogInformation("Pobieranie domyślnego szablonu dla typu szkoły ID: {SchoolTypeId}", schoolTypeId);
            var template = await _teamTemplateService.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId);
            if (template == null)
            {
                // To niekoniecznie błąd, po prostu może nie być domyślnego szablonu
                _logger.LogInformation("Nie znaleziono domyślnego szablonu dla typu szkoły ID: {SchoolTypeId}", schoolTypeId);
                return Ok(null); // lub NotFound, w zależności od oczekiwań klienta
            }
            return Ok(template);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTemplate([FromBody] CreateTeamTemplateRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie utworzenia szablonu zespołu: {TemplateName}", requestDto.Name);

            var template = await _teamTemplateService.CreateTemplateAsync(
                requestDto.Name,
                requestDto.TemplateContent,
                requestDto.Description,
                requestDto.IsUniversal,
                requestDto.SchoolTypeId,
                requestDto.Category
            // Pozostałe pola z DTO (Language, MaxLength etc.) można by przekazać do serwisu,
            // gdyby CreateTemplateAsync je przyjmował, lub ustawić na obiekcie template po utworzeniu.
            );

            if (template != null)
            {
                // Jeśli serwis nie ustawia wszystkich pól z DTO, można je tu uzupełnić
                if (requestDto.Language != null) template.Language = requestDto.Language;
                if (requestDto.MaxLength.HasValue) template.MaxLength = requestDto.MaxLength;
                template.RemovePolishChars = requestDto.RemovePolishChars;
                if (requestDto.Prefix != null) template.Prefix = requestDto.Prefix;
                if (requestDto.Suffix != null) template.Suffix = requestDto.Suffix;
                if (requestDto.Separator != null) template.Separator = requestDto.Separator; // Powinno być not null
                template.SortOrder = requestDto.SortOrder;

                // Jeśli serwis nie zapisuje od razu, a tylko przygotowuje obiekt, to tutaj byłby zapis.
                // Zakładamy, że CreateTemplateAsync w serwisie również obsługuje zapis lub przygotowuje obiekt do zapisu na wyższym poziomie.

                _logger.LogInformation("Szablon zespołu '{TemplateName}' (ID: {TemplateId}) utworzony pomyślnie.", template.Name, template.Id);
                return CreatedAtAction(nameof(GetTemplateById), new { templateId = template.Id }, template);
            }
            _logger.LogWarning("Nie udało się utworzyć szablonu zespołu '{TemplateName}'.", requestDto.Name);
            return BadRequest(new { Message = "Nie udało się utworzyć szablonu zespołu. Sprawdź logi serwera." });
        }

        [HttpPut("{templateId}")]
        public async Task<IActionResult> UpdateTemplate(string templateId, [FromBody] UpdateTeamTemplateRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie aktualizacji szablonu zespołu ID: {TemplateId}", templateId);

            var existingTemplate = await _teamTemplateService.GetTemplateByIdAsync(templateId);
            if (existingTemplate == null)
            {
                _logger.LogWarning("Nie znaleziono szablonu zespołu o ID: {TemplateId} do aktualizacji.", templateId);
                return NotFound(new { Message = $"Szablon zespołu o ID '{templateId}' nie został znaleziony." });
            }

            // Mapowanie z DTO na obiekt encji
            existingTemplate.Name = requestDto.Name;
            existingTemplate.Template = requestDto.TemplateContent;
            existingTemplate.Description = requestDto.Description;
            existingTemplate.IsDefault = requestDto.IsDefault;
            existingTemplate.IsUniversal = requestDto.IsUniversal;
            existingTemplate.SchoolTypeId = requestDto.SchoolTypeId; // Serwis powinien obsłużyć logikę powiązania z SchoolType
            existingTemplate.ExampleOutput = requestDto.ExampleOutput;
            existingTemplate.Category = requestDto.Category;
            existingTemplate.Language = requestDto.Language;
            existingTemplate.MaxLength = requestDto.MaxLength;
            existingTemplate.RemovePolishChars = requestDto.RemovePolishChars;
            existingTemplate.Prefix = requestDto.Prefix;
            existingTemplate.Suffix = requestDto.Suffix;
            existingTemplate.Separator = requestDto.Separator;
            existingTemplate.SortOrder = requestDto.SortOrder;
            existingTemplate.IsActive = requestDto.IsActive;

            var success = await _teamTemplateService.UpdateTemplateAsync(existingTemplate);
            if (success)
            {
                _logger.LogInformation("Szablon zespołu ID: {TemplateId} zaktualizowany pomyślnie.", templateId);
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować szablonu zespołu ID: {TemplateId}.", templateId);
            return BadRequest(new { Message = "Nie udało się zaktualizować szablonu zespołu." });
        }

        [HttpDelete("{templateId}")]
        public async Task<IActionResult> DeleteTemplate(string templateId)
        {
            _logger.LogInformation("Żądanie usunięcia szablonu zespołu ID: {TemplateId}", templateId);
            var success = await _teamTemplateService.DeleteTemplateAsync(templateId);
            if (success)
            {
                _logger.LogInformation("Szablon zespołu ID: {TemplateId} usunięty (zdezaktywowany) pomyślnie.", templateId);
                return Ok(new { Message = "Szablon zespołu usunięty (zdezaktywowany) pomyślnie." });
            }
            var template = await _teamTemplateService.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                _logger.LogWarning("Nie można usunąć szablonu ID: {TemplateId} - nie znaleziono.", templateId);
                return NotFound(new { Message = $"Szablon o ID '{templateId}' nie został znaleziony." });
            }
            _logger.LogWarning("Nie udało się usunąć (zdezaktywować) szablonu zespołu ID: {TemplateId}. Możliwe, że był już nieaktywny lub wystąpił inny problem.", templateId);
            return BadRequest(new { Message = "Nie udało się usunąć (zdezaktywować) szablonu zespołu." });
        }

        [HttpPost("{templateId}/generate-name")]
        public async Task<IActionResult> GenerateTeamNameFromTemplate(string templateId, [FromBody] GenerateTeamNameRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie wygenerowania nazwy zespołu z szablonu ID: {TemplateId}", templateId);
            if (requestDto == null || requestDto.Values == null)
            {
                return BadRequest(new { Message = "Słownik wartości dla placeholderów jest wymagany." });
            }

            var generatedName = await _teamTemplateService.GenerateTeamNameFromTemplateAsync(templateId, requestDto.Values);
            if (generatedName != null)
            {
                return Ok(new { GeneratedName = generatedName });
            }
            _logger.LogWarning("Nie udało się wygenerować nazwy z szablonu ID: {TemplateId}. Szablon może nie istnieć lub jest nieaktywny.", templateId);
            return NotFound(new { Message = $"Nie udało się wygenerować nazwy. Szablon o ID '{templateId}' nie istnieje lub jest nieaktywny." });
        }

        [HttpPost("{originalTemplateId}/clone")]
        public async Task<IActionResult> CloneTemplate(string originalTemplateId, [FromBody] CloneTeamTemplateRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie sklonowania szablonu ID: {OriginalTemplateId} do nowej nazwy: {NewTemplateName}", originalTemplateId, requestDto.NewTemplateName);
            if (string.IsNullOrWhiteSpace(requestDto.NewTemplateName))
            {
                return BadRequest(new { Message = "Nowa nazwa dla sklonowanego szablonu jest wymagana." });
            }

            var clonedTemplate = await _teamTemplateService.CloneTemplateAsync(originalTemplateId, requestDto.NewTemplateName);
            if (clonedTemplate != null)
            {
                _logger.LogInformation("Szablon ID: {OriginalTemplateId} sklonowany pomyślnie do nowego szablonu ID: {ClonedTemplateId} o nazwie '{NewTemplateName}'", originalTemplateId, clonedTemplate.Id, clonedTemplate.Name);
                return CreatedAtAction(nameof(GetTemplateById), new { templateId = clonedTemplate.Id }, clonedTemplate);
            }
            _logger.LogWarning("Nie udało się sklonować szablonu ID: {OriginalTemplateId}", originalTemplateId);
            // Serwis powinien logować dokładniejszy powód niepowodzenia
            return BadRequest(new { Message = "Nie udało się sklonować szablonu. Sprawdź logi serwera (np. czy szablon oryginalny istnieje lub czy nowa nazwa nie jest już zajęta)." });
        }
    }
}