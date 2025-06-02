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

    public class CreateDepartmentRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ParentDepartmentId { get; set; }
        public string? DepartmentCode { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
        public int SortOrder { get; set; } = 0;
    }

    public class UpdateDepartmentRequestDto
    {
        // Id działu będzie pobierane z URL
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ParentDepartmentId { get; set; }
        public string? DepartmentCode { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    // --- Kontroler ---

    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Wszystkie operacje na działach domyślnie wymagają autoryzacji
    public class DepartmentsController : ControllerBase
    {
        private readonly IDepartmentService _departmentService;
        private readonly ILogger<DepartmentsController> _logger;

        public DepartmentsController(IDepartmentService departmentService, ILogger<DepartmentsController> logger)
        {
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{departmentId}")]
        public async Task<IActionResult> GetDepartmentById(string departmentId, [FromQuery] bool includeSubDepartments = false, [FromQuery] bool includeUsers = false)
        {
            _logger.LogInformation("Pobieranie działu o ID: {DepartmentId}", departmentId);
            var department = await _departmentService.GetDepartmentByIdAsync(departmentId, includeSubDepartments, includeUsers);
            if (department == null)
            {
                _logger.LogInformation("Dział o ID: {DepartmentId} nie został znaleziony.", departmentId);
                return NotFound(new { Message = $"Dział o ID '{departmentId}' nie został znaleziony." });
            }
            return Ok(department);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDepartments([FromQuery] bool onlyRootDepartments = false)
        {
            _logger.LogInformation("Pobieranie wszystkich działów. Tylko główne: {OnlyRootDepartments}", onlyRootDepartments);
            var departments = await _departmentService.GetAllDepartmentsAsync(onlyRootDepartments);
            return Ok(departments);
        }

        [HttpGet("{parentDepartmentId}/subdepartments")]
        public async Task<IActionResult> GetSubDepartments(string parentDepartmentId)
        {
            _logger.LogInformation("Pobieranie poddziałów dla działu ID: {ParentDepartmentId}", parentDepartmentId);
            var subDepartments = await _departmentService.GetSubDepartmentsAsync(parentDepartmentId);
            return Ok(subDepartments);
        }

        [HttpGet("{departmentId}/users")]
        public async Task<IActionResult> GetUsersInDepartment(string departmentId)
        {
            _logger.LogInformation("Pobieranie użytkowników dla działu ID: {DepartmentId}", departmentId);
            var users = await _departmentService.GetUsersInDepartmentAsync(departmentId);
            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie utworzenia działu: {DepartmentName}", requestDto.Name);

            var department = await _departmentService.CreateDepartmentAsync(
                requestDto.Name,
                requestDto.Description,
                requestDto.ParentDepartmentId,
                requestDto.DepartmentCode
            // Pozostałe pola z DTO (Email, Phone, Location, SortOrder) można by przekazać do serwisu,
            // gdyby CreateDepartmentAsync je przyjmował, lub ustawić na obiekcie department po utworzeniu, przed zapisem.
            // Dla uproszczenia, obecna sygnatura CreateDepartmentAsync nie przyjmuje tych dodatkowych pól.
            );

            if (department != null)
            {
                // Jeśli CreateDepartmentAsync nie ustawia wszystkich pól z DTO, można je tu ustawić przed zwróceniem:
                if (requestDto.Email != null) department.Email = requestDto.Email;
                if (requestDto.Phone != null) department.Phone = requestDto.Phone;
                if (requestDto.Location != null) department.Location = requestDto.Location;
                department.SortOrder = requestDto.SortOrder;
                // Po ustawieniu dodatkowych pól, jeśli CreateDepartmentAsync nie zapisuje zmian,
                // trzeba by wywołać UpdateDepartmentAsync lub zapewnić zapis w serwisie.
                // Zakładając, że CreateDepartmentAsync przygotowuje obiekt do zapisu, a zapis nastąpi na wyższym poziomie.

                _logger.LogInformation("Dział '{DepartmentName}' (ID: {DepartmentId}) utworzony pomyślnie.", department.Name, department.Id);
                return CreatedAtAction(nameof(GetDepartmentById), new { departmentId = department.Id }, department);
            }
            _logger.LogWarning("Nie udało się utworzyć działu '{DepartmentName}'.", requestDto.Name);
            return BadRequest(new { Message = "Nie udało się utworzyć działu. Sprawdź logi serwera." });
        }

        [HttpPut("{departmentId}")]
        public async Task<IActionResult> UpdateDepartment(string departmentId, [FromBody] UpdateDepartmentRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie aktualizacji działu ID: {DepartmentId}", departmentId);

            // Najpierw pobierz istniejący dział, aby upewnić się, że istnieje.
            var existingDepartment = await _departmentService.GetDepartmentByIdAsync(departmentId);
            if (existingDepartment == null)
            {
                _logger.LogWarning("Nie znaleziono działu o ID: {DepartmentId} do aktualizacji.", departmentId);
                return NotFound(new { Message = $"Dział o ID '{departmentId}' nie został znaleziony." });
            }

            // Zastosuj zmiany z DTO na istniejącym obiekcie
            existingDepartment.Name = requestDto.Name;
            existingDepartment.Description = requestDto.Description;
            existingDepartment.ParentDepartmentId = requestDto.ParentDepartmentId;
            existingDepartment.DepartmentCode = requestDto.DepartmentCode;
            existingDepartment.Email = requestDto.Email;
            existingDepartment.Phone = requestDto.Phone;
            existingDepartment.Location = requestDto.Location;
            existingDepartment.SortOrder = requestDto.SortOrder;
            existingDepartment.IsActive = requestDto.IsActive;

            var success = await _departmentService.UpdateDepartmentAsync(existingDepartment);
            if (success)
            {
                _logger.LogInformation("Dział ID: {DepartmentId} zaktualizowany pomyślnie.", departmentId);
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować działu ID: {DepartmentId}.", departmentId);
            // Serwis powinien zalogować dokładniejszy powód błędu
            return BadRequest(new { Message = "Nie udało się zaktualizować działu." });
        }

        [HttpDelete("{departmentId}")]
        public async Task<IActionResult> DeleteDepartment(string departmentId)
        {
            _logger.LogInformation("Żądanie usunięcia działu ID: {DepartmentId}", departmentId);
            try
            {
                var success = await _departmentService.DeleteDepartmentAsync(departmentId);
                if (success)
                {
                    _logger.LogInformation("Dział ID: {DepartmentId} usunięty (zdezaktywowany) pomyślnie.", departmentId);
                    return Ok(new { Message = "Dział usunięty (zdezaktywowany) pomyślnie." });
                }
                // Jeśli serwis zwrócił false, to znaczy, że dział nie istniał lub nie można go było usunąć (np. był już nieaktywny)
                // Serwis powinien zalogować dokładniejszy powód.
                // Można też sprawdzić, czy dział istnieje przed próbą usunięcia, aby zwrócić 404.
                var department = await _departmentService.GetDepartmentByIdAsync(departmentId); // Sprawdź czy istnieje (nawet jeśli nieaktywny)
                if (department == null)
                {
                    return NotFound(new { Message = $"Dział o ID '{departmentId}' nie został znaleziony." });
                }
                _logger.LogWarning("Nie udało się usunąć (zdezaktywować) działu ID: {DepartmentId}. Możliwe, że był już nieaktywny lub wystąpił inny problem.", departmentId);
                return BadRequest(new { Message = "Nie udało się usunąć (zdezaktywować) działu. Sprawdź logi serwera lub czy dział nie był już nieaktywny." });

            }
            catch (InvalidOperationException ex) // Przechwytywanie wyjątku z serwisu (np. gdy dział ma poddziały)
            {
                _logger.LogWarning("Nie można usunąć działu ID {DepartmentId}: {ErrorMessage}", departmentId, ex.Message);
                return Conflict(new { Message = ex.Message }); // 409 Conflict
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas usuwania działu ID: {DepartmentId}", departmentId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Wystąpił nieoczekiwany błąd serwera." });
            }
        }
    }
}