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

    /// <summary>
    /// Model żądania utworzenia nowego działu organizacyjnego
    /// </summary>
    /// <example>
    /// {
    ///   "name": "Informatyka",
    ///   "description": "Wydział Informatyki i Technologii",
    ///   "departmentCode": "IT",
    ///   "email": "informatyka@szkola.edu.pl",
    ///   "phone": "+48 123 456 700",
    ///   "location": "Budynek A, piętro 2",
    ///   "sortOrder": 10
    /// }
    /// </example>
    public class CreateDepartmentRequestDto
    {
        /// <summary>
        /// Nazwa działu (wymagana)
        /// </summary>
        /// <example>Informatyka</example>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Opis działu i jego zadań
        /// </summary>
        /// <example>Wydział Informatyki i Technologii odpowiedzialny za edukację w zakresie IT</example>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// ID działu nadrzędnego (opcjonalne) - dla struktury hierarchicznej
        /// </summary>
        /// <example>dept-main-001</example>
        public string? ParentDepartmentId { get; set; }
        
        /// <summary>
        /// Krótki kod działu używany w raportach i identyfikacji
        /// </summary>
        /// <example>IT</example>
        public string? DepartmentCode { get; set; }
        
        /// <summary>
        /// Adres email działu
        /// </summary>
        /// <example>informatyka@szkola.edu.pl</example>
        public string? Email { get; set; }
        
        /// <summary>
        /// Numer telefonu działu
        /// </summary>
        /// <example>+48 123 456 700</example>
        public string? Phone { get; set; }
        
        /// <summary>
        /// Lokalizacja fizyczna działu
        /// </summary>
        /// <example>Budynek A, piętro 2, pokoje 201-210</example>
        public string? Location { get; set; }
        
        /// <summary>
        /// Kolejność sortowania dla wyświetlania na listach (domyślnie 0)
        /// </summary>
        /// <example>10</example>
        public int SortOrder { get; set; } = 0;
    }

    /// <summary>
    /// Model żądania aktualizacji istniejącego działu organizacyjnego
    /// </summary>
    /// <example>
    /// {
    ///   "name": "Informatyka i Robotyka",
    ///   "description": "Rozszerzony wydział obejmujący informatykę i robotykę",
    ///   "departmentCode": "IT-ROB",
    ///   "email": "informatyka@szkola.edu.pl",
    ///   "phone": "+48 123 456 700",
    ///   "location": "Budynek A, piętro 2-3",
    ///   "sortOrder": 15,
    ///   "isActive": true
    /// }
    /// </example>
    public class UpdateDepartmentRequestDto
    {
        // Id działu będzie pobierane z URL
        /// <summary>
        /// Nowa nazwa działu
        /// </summary>
        /// <example>Informatyka i Robotyka</example>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Zaktualizowany opis działu
        /// </summary>
        /// <example>Rozszerzony wydział obejmujący informatykę, programowanie i robotykę</example>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// ID nowego działu nadrzędnego (opcjonalne)
        /// </summary>
        /// <example>dept-tech-001</example>
        public string? ParentDepartmentId { get; set; }
        
        /// <summary>
        /// Zaktualizowany kod działu
        /// </summary>
        /// <example>IT-ROB</example>
        public string? DepartmentCode { get; set; }
        
        /// <summary>
        /// Zaktualizowany adres email działu
        /// </summary>
        /// <example>informatyka.robotyka@szkola.edu.pl</example>
        public string? Email { get; set; }
        
        /// <summary>
        /// Zaktualizowany numer telefonu działu
        /// </summary>
        /// <example>+48 123 456 701</example>
        public string? Phone { get; set; }
        
        /// <summary>
        /// Zaktualizowana lokalizacja działu
        /// </summary>
        /// <example>Budynek A, piętro 2-3, laboratoria 201-305</example>
        public string? Location { get; set; }
        
        /// <summary>
        /// Nowa kolejność sortowania
        /// </summary>
        /// <example>15</example>
        public int SortOrder { get; set; } = 0;
        
        /// <summary>
        /// Status aktywności działu
        /// </summary>
        /// <example>true</example>
        public bool IsActive { get; set; } = true;
    }

    // --- Kontroler ---

    /// <summary>
    /// 🏢 Kontroler zarządzania działami organizacyjnymi
    /// </summary>
    /// <remarks>
    /// Umożliwia pełne zarządzanie strukturą organizacyjną szkoły:
    /// 
    /// ## 📋 Funkcjonalności:
    /// - **Tworzenie działów** - definiowanie nowych jednostek organizacyjnych
    /// - **Aktualizacja działów** - modyfikacja danych i struktury
    /// - **Przeglądanie działów** - lista wszystkich działów i szczegóły
    /// - **Zarządzanie hierarchią** - obsługa struktury nadrzędnej/podrzędnej
    /// - **Przypisywanie użytkowników** - związanie pracowników z działami
    /// - **Dezaktywacja działów** - bezpieczne usuwanie z zachowaniem historii
    /// 
    /// ## 🔗 Struktura hierarchiczna:
    /// Działy mogą tworzyć strukturę drzewiastą poprzez powiązania parent-child.
    /// 
    /// ## 👥 Integracja z użytkownikami:
    /// Każdy użytkownik może być przypisany do działu, co wpływa na jego uprawnienia.
    /// 
    /// ## 🛡️ Zabezpieczenia:
    /// Wszystkie operacje wymagają uwierzytelniania JWT Bearer Token.
    /// </remarks>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize] // Wszystkie operacje na działach domyślnie wymagają autoryzacji
    public class DepartmentsController : ControllerBase
    {
        private readonly IDepartmentService _departmentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DepartmentsController> _logger;

        public DepartmentsController(
            IDepartmentService departmentService, 
            ICurrentUserService currentUserService,
            ILogger<DepartmentsController> logger)
        {
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
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
                requestDto.DepartmentCode,
                requestDto.Email,
                requestDto.Phone,
                requestDto.Location,
                requestDto.SortOrder
            );

            if (department != null)
            {
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

            var existingDepartment = await _departmentService.GetDepartmentByIdAsync(departmentId);
            if (existingDepartment == null)
            {
                _logger.LogWarning("Nie znaleziono działu o ID: {DepartmentId} do aktualizacji.", departmentId);
                return NotFound(new { Message = $"Dział o ID '{departmentId}' nie został znaleziony." });
            }

            // Mapowanie z DTO na obiekt encji
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
                return NoContent();
            }
            _logger.LogWarning("Nie udało się zaktualizować działu ID: {DepartmentId}.", departmentId);
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

                var department = await _departmentService.GetDepartmentByIdAsync(departmentId);
                if (department == null)
                {
                    return NotFound(new { Message = $"Dział o ID '{departmentId}' nie został znaleziony." });
                }
                _logger.LogWarning("Nie udało się usunąć (zdezaktywować) działu ID: {DepartmentId}.", departmentId);
                return BadRequest(new { Message = "Nie udało się usunąć (zdezaktywować) działu." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Nie można usunąć działu ID {DepartmentId}: {ErrorMessage}", departmentId, ex.Message);
                return Conflict(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas usuwania działu ID: {DepartmentId}", departmentId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Wystąpił nieoczekiwany błąd serwera." });
            }
        }

        [HttpPost("{departmentId}/users/{userId}")]
        public async Task<IActionResult> AssignUserToDepartment(string departmentId, string userId)
        {
            _logger.LogInformation("Żądanie przypisania użytkownika {UserId} do działu {DepartmentId}", userId, departmentId);
            var success = await _departmentService.AssignUserToDepartmentAsync(userId, departmentId);
            if (success)
            {
                _logger.LogInformation("Użytkownik {UserId} przypisany do działu {DepartmentId} pomyślnie.", userId, departmentId);
                return Ok(new { Message = "Użytkownik pomyślnie przypisany do działu." });
            }
            _logger.LogWarning("Nie udało się przypisać użytkownika {UserId} do działu {DepartmentId}.", userId, departmentId);
            return BadRequest(new { Message = "Nie udało się przypisać użytkownika do działu." });
        }

        [HttpDelete("{departmentId}/users/{userId}")]
        public async Task<IActionResult> RemoveUserFromDepartment(string departmentId, string userId)
        {
            _logger.LogInformation("Żądanie usunięcia użytkownika {UserId} z działu {DepartmentId}", userId, departmentId);
            var success = await _departmentService.RemoveUserFromDepartmentAsync(userId, departmentId);
            if (success)
            {
                _logger.LogInformation("Użytkownik {UserId} usunięty z działu {DepartmentId} pomyślnie.", userId, departmentId);
                return Ok(new { Message = "Użytkownik pomyślnie usunięty z działu." });
            }
            _logger.LogWarning("Nie udało się usunąć użytkownika {UserId} z działu {DepartmentId}.", userId, departmentId);
            return BadRequest(new { Message = "Nie udało się usunąć użytkownika z działu." });
        }
    }
}