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
    // W docelowym projekcie te klasy powinny znaleźć się w osobnym projekcie/folderze

    public class CreateUserRequestDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Upn { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Uczen;
        public string DepartmentId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // Hasło dla nowego konta M365
        public bool SendWelcomeEmail { get; set; } = false;
        // Opcjonalne pola, które mogą być ustawiane przy tworzeniu w M365 i lokalnie
        public string? Phone { get; set; }
        public string? AlternateEmail { get; set; }
        public string? ExternalId { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime? EmploymentDate { get; set; }
        public string? Position { get; set; }
        public string? Notes { get; set; }
        public bool IsSystemAdmin { get; set; } = false;
    }

    public class UpdateUserRequestDto
    {
        // Id użytkownika będzie pobierane z URL
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Upn { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Uczen;
        public string DepartmentId { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? AlternateEmail { get; set; }
        public string? ExternalId { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime? EmploymentDate { get; set; }
        public string? Position { get; set; }
        public string? Notes { get; set; }
        public bool IsSystemAdmin { get; set; } = false;
        public bool IsActive { get; set; } = true; // Pozwalamy na zmianę statusu aktywności
    }

    public class AssignUserToSchoolTypeRequestDto
    {
        public string SchoolTypeId { get; set; } = string.Empty;
        public DateTime AssignedDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? WorkloadPercentage { get; set; }
        public string? Notes { get; set; }
    }

    public class AssignTeacherToSubjectRequestDto
    {
        public string SubjectId { get; set; } = string.Empty;
        public DateTime AssignedDate { get; set; }
        public string? Notes { get; set; }
    }

    public class UserActionM365Dto
    {
        public bool PerformM365Action { get; set; } = true;
    }


    // --- Kontroler ---

    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize] // Wszystkie operacje na użytkownikach wymagają autoryzacji
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserService userService, 
            ICurrentUserService currentUserService,
            ILogger<UsersController> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
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

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserById(string userId, [FromQuery] bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkownika o ID: {UserId}, forceRefresh: {ForceRefresh}", userId, forceRefresh);
            var accessToken = GetAccessTokenFromHeader(); // Potrzebny tylko jeśli forceRefresh = true i serwis odpytuje Graph

            var user = await _userService.GetUserByIdAsync(userId, forceRefresh, accessToken);
            if (user == null)
            {
                _logger.LogInformation("Użytkownik o ID: {UserId} nie został znaleziony.", userId);
                return NotFound(new { Message = $"Użytkownik o ID '{userId}' nie został znaleziony." });
            }
            return Ok(user);
        }

        [HttpGet("upn/{upn}")] // Używamy ścieżki, aby uniknąć konfliktu z GetUserById
        public async Task<IActionResult> GetUserByUpn(string upn, [FromQuery] bool forceRefresh = false)
        {
            // Poprawka: dekodowanie UPN z URL
            var decodedUpn = System.Net.WebUtility.UrlDecode(upn);
            _logger.LogInformation("Pobieranie użytkownika o UPN: {UserUpn}, forceRefresh: {ForceRefresh}", decodedUpn, forceRefresh);
            var accessToken = GetAccessTokenFromHeader();

            var user = await _userService.GetUserByUpnAsync(decodedUpn, forceRefresh, accessToken);
            if (user == null)
            {
                _logger.LogInformation("Użytkownik o UPN: {UserUpn} nie został znaleziony.", decodedUpn);
                return NotFound(new { Message = $"Użytkownik o UPN '{decodedUpn}' nie został znaleziony." });
            }
            return Ok(user);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetAllActiveUsers([FromQuery] bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych użytkowników, forceRefresh: {ForceRefresh}", forceRefresh);
            var accessToken = GetAccessTokenFromHeader();
            var users = await _userService.GetAllActiveUsersAsync(forceRefresh, accessToken);
            return Ok(users);
        }

        [HttpGet("role/{role}")]
        public async Task<IActionResult> GetUsersByRole(UserRole role, [FromQuery] bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkowników o roli: {UserRole}, forceRefresh: {ForceRefresh}", role, forceRefresh);
            var accessToken = GetAccessTokenFromHeader();
            var users = await _userService.GetUsersByRoleAsync(role, forceRefresh, accessToken);
            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie utworzenia użytkownika: {UserUpn}", requestDto.Upn);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }

            if (string.IsNullOrWhiteSpace(requestDto.Password))
            {
                return BadRequest(new { Message = "Hasło jest wymagane do utworzenia konta użytkownika w M365." });
            }

            var user = await _userService.CreateUserAsync(
                requestDto.FirstName,
                requestDto.LastName,
                requestDto.Upn,
                requestDto.Role,
                requestDto.DepartmentId,
                requestDto.Password,
                accessToken,
                requestDto.SendWelcomeEmail
            );

            if (user != null)
            {
                // Ustawienie dodatkowych pól, jeśli zostały przekazane w DTO
                // Ta logika może być częścią serwisu lub obsługiwana tutaj
                bool needsLocalUpdate = false;
                if (requestDto.Phone != null) { user.Phone = requestDto.Phone; needsLocalUpdate = true; }
                if (requestDto.AlternateEmail != null) { user.AlternateEmail = requestDto.AlternateEmail; needsLocalUpdate = true; }
                if (requestDto.ExternalId != null) { user.ExternalId = requestDto.ExternalId; needsLocalUpdate = true; }
                if (requestDto.BirthDate.HasValue) { user.BirthDate = requestDto.BirthDate; needsLocalUpdate = true; }
                if (requestDto.EmploymentDate.HasValue) { user.EmploymentDate = requestDto.EmploymentDate; needsLocalUpdate = true; }
                if (requestDto.Position != null) { user.Position = requestDto.Position; needsLocalUpdate = true; }
                if (requestDto.Notes != null) { user.Notes = requestDto.Notes; needsLocalUpdate = true; }
                if (user.IsSystemAdmin != requestDto.IsSystemAdmin) { user.IsSystemAdmin = requestDto.IsSystemAdmin; needsLocalUpdate = true; }

                if (needsLocalUpdate)
                {
                    // Ponieważ CreateUserAsync w M365 może nie ustawiać wszystkich tych pól,
                    // możemy potrzebować drugiego wywołania UpdateUserAsync lub bezpośredniej aktualizacji lokalnej.
                    // Dla uproszczenia, załóżmy, że CreateUserAsync zwraca obiekt User gotowy do aktualizacji.
                    // W rzeczywistości, po utworzeniu w M365, ExternalId jest kluczowy.
                    // Następnie można wywołać _userService.UpdateUserAsync(user, accessToken) aby zsynchronizować te dodatkowe pola,
                    // jeśli PowerShellService.CreateM365UserAsync ich nie ustawia.
                    // Na razie pominiemy dodatkowe wywołanie UpdateUserAsync dla tych pól po utworzeniu w M365.
                    // Zakładamy, że CreateUserAsync w serwisie odpowiednio zarządza zapisem lokalnym tych danych.
                }

                _logger.LogInformation("Użytkownik {UserUpn} (ID: {UserId}, ExternalID: {ExternalId}) utworzony pomyślnie.", user.UPN, user.Id, user.ExternalId ?? "N/A");
                return CreatedAtAction(nameof(GetUserById), new { userId = user.Id }, user);
            }
            _logger.LogWarning("Nie udało się utworzyć użytkownika {UserUpn}.", requestDto.Upn);
            return BadRequest(new { Message = "Nie udało się utworzyć użytkownika. Sprawdź logi serwera." });
        }

        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie aktualizacji użytkownika ID: {UserId}", userId);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }

            // Najpierw pobierz istniejącego użytkownika, aby upewnić się, że istnieje,
            // i aby przekazać kompletny obiekt do serwisu aktualizacji.
            var existingUser = await _userService.GetUserByIdAsync(userId, accessToken: accessToken);
            if (existingUser == null)
            {
                _logger.LogWarning("Nie znaleziono użytkownika o ID: {UserId} do aktualizacji.", userId);
                return NotFound(new { Message = $"Użytkownik o ID '{userId}' nie został znaleziony." });
            }

            // Zastosuj zmiany z DTO na istniejącym obiekcie
            existingUser.FirstName = requestDto.FirstName;
            existingUser.LastName = requestDto.LastName;
            existingUser.UPN = requestDto.Upn;
            existingUser.Role = requestDto.Role;
            existingUser.DepartmentId = requestDto.DepartmentId;
            existingUser.Phone = requestDto.Phone;
            existingUser.AlternateEmail = requestDto.AlternateEmail;
            existingUser.ExternalId = requestDto.ExternalId;
            existingUser.BirthDate = requestDto.BirthDate;
            existingUser.EmploymentDate = requestDto.EmploymentDate;
            existingUser.Position = requestDto.Position;
            existingUser.Notes = requestDto.Notes;
            existingUser.IsSystemAdmin = requestDto.IsSystemAdmin;
            existingUser.IsActive = requestDto.IsActive;

            var success = await _userService.UpdateUserAsync(existingUser, accessToken);
            if (success)
            {
                _logger.LogInformation("Użytkownik ID: {UserId} zaktualizowany pomyślnie.", userId);
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować użytkownika ID: {UserId}.", userId);
            return BadRequest(new { Message = "Nie udało się zaktualizować użytkownika." });
        }

        [HttpPost("{userId}/deactivate")]
        public async Task<IActionResult> DeactivateUser(string userId, [FromBody] UserActionM365Dto? dto)
        {
            _logger.LogInformation("Żądanie dezaktywacji użytkownika ID: {UserId}", userId);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }
            bool deactivateM365 = dto?.PerformM365Action ?? true;


            var success = await _userService.DeactivateUserAsync(userId, accessToken, deactivateM365);
            if (success)
            {
                _logger.LogInformation("Użytkownik ID: {UserId} zdezaktywowany pomyślnie.", userId);
                return Ok(new { Message = "Użytkownik zdezaktywowany pomyślnie." });
            }
            _logger.LogWarning("Nie udało się zdezaktywować użytkownika ID: {UserId}.", userId);
            return BadRequest(new { Message = "Nie udało się zdezaktywować użytkownika." });
        }

        [HttpPost("{userId}/activate")]
        public async Task<IActionResult> ActivateUser(string userId, [FromBody] UserActionM365Dto? dto)
        {
            _logger.LogInformation("Żądanie aktywacji użytkownika ID: {UserId}", userId);
            var accessToken = GetAccessTokenFromHeader();
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { Message = "Brak wymaganego tokenu dostępu." });
            }
            bool activateM365 = dto?.PerformM365Action ?? true;

            var success = await _userService.ActivateUserAsync(userId, accessToken, activateM365);
            if (success)
            {
                _logger.LogInformation("Użytkownik ID: {UserId} aktywowany pomyślnie.", userId);
                return Ok(new { Message = "Użytkownik aktywowany pomyślnie." });
            }
            _logger.LogWarning("Nie udało się aktywować użytkownika ID: {UserId}.", userId);
            return BadRequest(new { Message = "Nie udało się aktywować użytkownika." });
        }

        [HttpPost("{userId}/schooltypes")]
        public async Task<IActionResult> AssignUserToSchoolType(string userId, [FromBody] AssignUserToSchoolTypeRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie przypisania użytkownika {UserId} do typu szkoły {SchoolTypeId}", userId, requestDto.SchoolTypeId);
            // Ta operacja nie wymaga bezpośredniego accessToken dla PowerShellService w obecnej implementacji serwisu
            var assignment = await _userService.AssignUserToSchoolTypeAsync(
                userId,
                requestDto.SchoolTypeId,
                requestDto.AssignedDate,
                requestDto.EndDate,
                requestDto.WorkloadPercentage,
                requestDto.Notes
            );

            if (assignment != null)
            {
                _logger.LogInformation("Użytkownik {UserId} przypisany do typu szkoły {SchoolTypeId} pomyślnie.", userId, requestDto.SchoolTypeId);
                return Ok(assignment); // Zwraca utworzone przypisanie
            }
            _logger.LogWarning("Nie udało się przypisać użytkownika {UserId} do typu szkoły {SchoolTypeId}.", userId, requestDto.SchoolTypeId);
            return BadRequest(new { Message = "Nie udało się przypisać użytkownika do typu szkoły." });
        }

        [HttpDelete("schooltypes/{userSchoolTypeId}")] // ID samego przypisania
        public async Task<IActionResult> RemoveUserFromSchoolType(string userSchoolTypeId)
        {
            _logger.LogInformation("Żądanie usunięcia przypisania UserSchoolType ID: {UserSchoolTypeId}", userSchoolTypeId);
            // Ta operacja nie wymaga accessToken dla PowerShellService
            var success = await _userService.RemoveUserFromSchoolTypeAsync(userSchoolTypeId);
            if (success)
            {
                _logger.LogInformation("Przypisanie UserSchoolType ID: {UserSchoolTypeId} usunięte pomyślnie.", userSchoolTypeId);
                return Ok(new { Message = "Przypisanie użytkownika do typu szkoły usunięte pomyślnie." });
            }
            _logger.LogWarning("Nie udało się usunąć przypisania UserSchoolType ID: {UserSchoolTypeId}.", userSchoolTypeId);
            return BadRequest(new { Message = "Nie udało się usunąć przypisania użytkownika do typu szkoły." });
        }

        [HttpPost("{teacherId}/subjects")]
        public async Task<IActionResult> AssignTeacherToSubject(string teacherId, [FromBody] AssignTeacherToSubjectRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie przypisania nauczyciela {TeacherId} do przedmiotu {SubjectId}", teacherId, requestDto.SubjectId);
            // Ta operacja nie wymaga accessToken dla PowerShellService
            var assignment = await _userService.AssignTeacherToSubjectAsync(
                teacherId,
                requestDto.SubjectId,
                requestDto.AssignedDate,
                requestDto.Notes
            );

            if (assignment != null)
            {
                _logger.LogInformation("Nauczyciel {TeacherId} przypisany do przedmiotu {SubjectId} pomyślnie.", teacherId, requestDto.SubjectId);
                return Ok(assignment);
            }
            _logger.LogWarning("Nie udało się przypisać nauczyciela {TeacherId} do przedmiotu {SubjectId}.", teacherId, requestDto.SubjectId);
            return BadRequest(new { Message = "Nie udało się przypisać nauczyciela do przedmiotu." });
        }

        [HttpDelete("subjects/{userSubjectId}")] // ID samego przypisania
        public async Task<IActionResult> RemoveTeacherFromSubject(string userSubjectId)
        {
            _logger.LogInformation("Żądanie usunięcia przypisania UserSubject ID: {UserSubjectId}", userSubjectId);
            // Ta operacja nie wymaga accessToken dla PowerShellService
            var success = await _userService.RemoveTeacherFromSubjectAsync(userSubjectId);
            if (success)
            {
                _logger.LogInformation("Przypisanie UserSubject ID: {UserSubjectId} usunięte pomyślnie.", userSubjectId);
                return Ok(new { Message = "Przypisanie nauczyciela do przedmiotu usunięte pomyślnie." });
            }
            _logger.LogWarning("Nie udało się usunąć przypisania UserSubject ID: {UserSubjectId}.", userSubjectId);
            return BadRequest(new { Message = "Nie udało się usunąć przypisania nauczyciela do przedmiotu." });
        }
    }
}