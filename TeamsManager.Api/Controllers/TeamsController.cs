using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using TeamsManager.Core.Abstractions; // Dla ICurrentUserService
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Dla .Select()

namespace TeamsManager.Api.Controllers
{
    // --- Data Transfer Objects (DTO) ---
    // W docelowym projekcie te klasy powinny znaleźć się w osobnym projekcie/folderze (np. TeamsManager.Contracts lub TeamsManager.Api.Dtos)

    public class CreateTeamRequestDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OwnerUpn { get; set; } = string.Empty;
        public TeamVisibility Visibility { get; set; } = TeamVisibility.Private;
        public string? TeamTemplateId { get; set; }
        public string? SchoolTypeId { get; set; }
        public string? SchoolYearId { get; set; }
        public Dictionary<string, string>? AdditionalTemplateValues { get; set; }
    }

    public class UpdateTeamRequestDto
    {
        // Zakładamy, że ID zespołu jest przekazywane w URL, a nie w ciele
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OwnerUpn { get; set; } = string.Empty;
        public TeamVisibility Visibility { get; set; } = TeamVisibility.Private;
        public string? SchoolTypeId { get; set; }
        public string? SchoolYearId { get; set; }
        public string? TemplateId { get; set; } // Może być potrzebne, jeśli pozwalamy na zmianę szablonu
        public string? AcademicYear { get; set; }
        public string? Semester { get; set; }
        public bool RequiresApproval { get; set; }
        public int? MaxMembers { get; set; }
        // Uwaga: Status zespołu (Active/Archived) jest zarządzany przez dedykowane endpointy Archive/Restore
    }

    public class AddMemberRequestDto
    {
        public string UserUpn { get; set; } = string.Empty;
        public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;
    }

    public class ArchiveTeamRequestDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    // --- Kontroler ---

    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")] // Trasa bazowa: /api/v1.0/Teams
    [Authorize] // Większość operacji na zespołach wymaga autoryzacji
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teamService;
        private readonly ICurrentUserService _currentUserService; // Do pobierania UPN użytkownika wykonującego żądanie, jeśli potrzebne
        private readonly ILogger<TeamsController> _logger;

        public TeamsController(
            ITeamService teamService,
            ICurrentUserService currentUserService,
            ILogger<TeamsController> logger)
        {
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string? GetAccessTokenFromHeader()
        {
            if (Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader.Substring("Bearer ".Length).Trim();
                }
            }
            _logger.LogWarning("Nie znaleziono tokenu dostępu w nagłówku Authorization.");
            return null;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie utworzenia zespołu: {DisplayName}, Właściciel: {OwnerUpn}", requestDto.DisplayName, requestDto.OwnerUpn);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }

            var team = await _teamService.CreateTeamAsync(
                requestDto.DisplayName,
                requestDto.Description,
                requestDto.OwnerUpn,
                requestDto.Visibility,
                accessToken, // Przekazanie tokenu
                requestDto.TeamTemplateId,
                requestDto.SchoolTypeId,
                requestDto.SchoolYearId,
                requestDto.AdditionalTemplateValues
            );

            if (team != null)
            {
                _logger.LogInformation("Zespół '{DisplayName}' (ID: {TeamId}) utworzony pomyślnie.", team.DisplayName, team.Id);
                return CreatedAtAction(nameof(GetTeamById), new { teamId = team.Id }, team);
            }
            _logger.LogWarning("Nie udało się utworzyć zespołu '{DisplayName}'.", requestDto.DisplayName);
            return BadRequest(new { Message = "Nie udało się utworzyć zespołu. Sprawdź logi serwera." });
        }

        [HttpGet("{teamId}")]
        public async Task<IActionResult> GetTeamById(string teamId, [FromQuery] bool includeMembers = false, [FromQuery] bool includeChannels = false)
        {
            _logger.LogInformation("Pobieranie zespołu o ID: {TeamId}", teamId);
            var accessToken = GetAccessTokenFromHeader(); // Token może być potrzebny dla forceRefresh z Graph

            var team = await _teamService.GetTeamByIdAsync(teamId, includeMembers, includeChannels, accessToken: accessToken);
            if (team == null)
            {
                _logger.LogInformation("Zespół o ID: {TeamId} nie został znaleziony.", teamId);
                return NotFound(new { Message = $"Zespół o ID '{teamId}' nie został znaleziony." });
            }
            return Ok(team);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTeams()
        {
            _logger.LogInformation("Pobieranie wszystkich zespołów (tylko z Team.Status = Active).");
            var accessToken = GetAccessTokenFromHeader();
            var teams = await _teamService.GetAllTeamsAsync(accessToken: accessToken);
            return Ok(teams);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveTeams()
        {
            _logger.LogInformation("Pobieranie zespołów o statusie Active.");
            var accessToken = GetAccessTokenFromHeader();
            var teams = await _teamService.GetActiveTeamsAsync(accessToken: accessToken);
            return Ok(teams);
        }

        [HttpGet("archived")]
        public async Task<IActionResult> GetArchivedTeams()
        {
            _logger.LogInformation("Pobieranie zespołów o statusie Archived.");
            var accessToken = GetAccessTokenFromHeader();
            var teams = await _teamService.GetArchivedTeamsAsync(accessToken: accessToken);
            return Ok(teams);
        }

        [HttpGet("owner/{ownerUpn}")]
        public async Task<IActionResult> GetTeamsByOwner(string ownerUpn)
        {
            _logger.LogInformation("Pobieranie zespołów dla właściciela: {OwnerUpn}", ownerUpn);
            var accessToken = GetAccessTokenFromHeader();
            var teams = await _teamService.GetTeamsByOwnerAsync(ownerUpn, accessToken: accessToken);
            return Ok(teams);
        }

        [HttpPut("{teamId}")]
        public async Task<IActionResult> UpdateTeam(string teamId, [FromBody] UpdateTeamRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie aktualizacji zespołu ID: {TeamId}", teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }

            // TODO: Rozważyć pobranie istniejącego zespołu i zmapowanie DTO na niego,
            // zamiast tworzyć nowy obiekt Team, aby uniknąć nadpisania pól, których DTO nie zawiera.
            // Na razie zakładamy, że serwis obsługuje aktualizację na podstawie przekazanego obiektu.
            var teamToUpdate = new Team
            {
                Id = teamId, // ID musi być ustawione dla serwisu
                DisplayName = requestDto.DisplayName,
                Description = requestDto.Description,
                Owner = requestDto.OwnerUpn,
                Visibility = requestDto.Visibility,
                SchoolTypeId = requestDto.SchoolTypeId,
                SchoolYearId = requestDto.SchoolYearId,
                TemplateId = requestDto.TemplateId,
                AcademicYear = requestDto.AcademicYear,
                Semester = requestDto.Semester,
                RequiresApproval = requestDto.RequiresApproval,
                MaxMembers = requestDto.MaxMembers
                // Status nie jest tutaj aktualizowany, użyj Archive/Restore
            };

            var success = await _teamService.UpdateTeamAsync(teamToUpdate, accessToken);
            if (success)
            {
                _logger.LogInformation("Zespół ID: {TeamId} zaktualizowany pomyślnie.", teamId);
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować zespołu ID: {TeamId}.", teamId);
            return BadRequest(new { Message = "Nie udało się zaktualizować zespołu." });
        }

        [HttpPost("{teamId}/archive")]
        public async Task<IActionResult> ArchiveTeam(string teamId, [FromBody] ArchiveTeamRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie archiwizacji zespołu ID: {TeamId}, Powód: {Reason}", teamId, requestDto.Reason);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }
            if (string.IsNullOrWhiteSpace(requestDto.Reason))
            {
                return BadRequest(new { Message = "Powód archiwizacji jest wymagany." });
            }

            var success = await _teamService.ArchiveTeamAsync(teamId, requestDto.Reason, accessToken);
            if (success)
            {
                _logger.LogInformation("Zespół ID: {TeamId} zarchiwizowany pomyślnie.", teamId);
                return Ok(new { Message = "Zespół zarchiwizowany pomyślnie." });
            }
            _logger.LogWarning("Nie udało się zarchiwizować zespołu ID: {TeamId}.", teamId);
            return BadRequest(new { Message = "Nie udało się zarchiwizować zespołu." });
        }

        [HttpPost("{teamId}/restore")]
        public async Task<IActionResult> RestoreTeam(string teamId)
        {
            _logger.LogInformation("Żądanie przywrócenia zespołu ID: {TeamId}", teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }

            var success = await _teamService.RestoreTeamAsync(teamId, accessToken);
            if (success)
            {
                _logger.LogInformation("Zespół ID: {TeamId} przywrócony pomyślnie.", teamId);
                return Ok(new { Message = "Zespół przywrócony pomyślnie." });
            }
            _logger.LogWarning("Nie udało się przywrócić zespołu ID: {TeamId}.", teamId);
            return BadRequest(new { Message = "Nie udało się przywrócić zespołu." });
        }

        [HttpDelete("{teamId}")]
        public async Task<IActionResult> DeleteTeam(string teamId)
        {
            _logger.LogInformation("Żądanie usunięcia zespołu ID: {TeamId}", teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }

            var success = await _teamService.DeleteTeamAsync(teamId, accessToken);
            if (success)
            {
                _logger.LogInformation("Zespół ID: {TeamId} usunięty (zarchiwizowany) pomyślnie.", teamId);
                return Ok(new { Message = "Zespół usunięty (zarchiwizowany) pomyślnie." }); // Lub NoContent()
            }
            _logger.LogWarning("Nie udało się usunąć zespołu ID: {TeamId}.", teamId);
            return BadRequest(new { Message = "Nie udało się usunąć zespołu." });
        }

        [HttpPost("{teamId}/members")]
        public async Task<IActionResult> AddMember(string teamId, [FromBody] AddMemberRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie dodania członka {UserUpn} do zespołu ID: {TeamId} z rolą {Role}", requestDto.UserUpn, teamId, requestDto.Role);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }

            var teamMember = await _teamService.AddMemberAsync(teamId, requestDto.UserUpn, requestDto.Role, accessToken);
            if (teamMember != null)
            {
                _logger.LogInformation("Członek {UserUpn} dodany do zespołu ID: {TeamId} pomyślnie.", requestDto.UserUpn, teamId);
                // Zwraca 201 Created z lokalizacją (jeśli mamy endpoint GetMember) lub obiektem członka
                return Ok(teamMember); // Prostsze niż CreatedAtAction dla członka
            }
            _logger.LogWarning("Nie udało się dodać członka {UserUpn} do zespołu ID: {TeamId}.", requestDto.UserUpn, teamId);
            return BadRequest(new { Message = "Nie udało się dodać członka do zespołu." });
        }

        [HttpDelete("{teamId}/members/{userId}")] // Można użyć UPN zamiast userId, jeśli jest wygodniejsze
        public async Task<IActionResult> RemoveMember(string teamId, string userId)
        {
            _logger.LogInformation("Żądanie usunięcia członka ID/UPN: {UserId} z zespołu ID: {TeamId}", userId, teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }

            // W ITeamService metoda RemoveMemberAsync przyjmuje userId (GUID).
            // Jeśli przekazujemy tu UPN, serwis musiałby najpierw zamienić UPN na ID użytkownika.
            // Dla uproszczenia zakładam, że przekazujemy ID użytkownika.
            var success = await _teamService.RemoveMemberAsync(teamId, userId, accessToken);
            if (success)
            {
                _logger.LogInformation("Członek ID: {UserId} usunięty z zespołu ID: {TeamId} pomyślnie.", userId, teamId);
                return Ok(new { Message = "Członek usunięty pomyślnie." }); // Lub NoContent()
            }
            _logger.LogWarning("Nie udało się usunąć członka ID: {UserId} z zespołu ID: {TeamId}.", userId, teamId);
            return BadRequest(new { Message = "Nie udało się usunąć członka z zespołu." });
        }
    }
}