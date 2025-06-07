using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Models;

namespace TeamsManager.Api.Controllers
{
    /// <summary>
    /// Kontroler API dla operacji raportowania i eksportu danych
    /// Główne endpointy dla generowania raportów biznesowych i compliance
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportingController : ControllerBase
    {
        private readonly IReportingOrchestrator _orchestrator;
        private readonly ITokenManager _tokenManager;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ReportingController> _logger;

        public ReportingController(
            IReportingOrchestrator orchestrator,
            ITokenManager tokenManager,
            ICurrentUserService currentUserService,
            ILogger<ReportingController> logger)
        {
            _orchestrator = orchestrator;
            _tokenManager = tokenManager;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Generuje raport podsumowujący dla roku szkolnego
        /// </summary>
        /// <param name="request">Parametry generowania raportu</param>
        /// <returns>Raport roku szkolnego do pobrania</returns>
        /// <response code="200">Raport został wygenerowany pomyślnie</response>
        /// <response code="400">Nieprawidłowe parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpPost("school-year-report")]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GenerateSchoolYearReport([FromBody] SchoolYearReportRequest request)
        {
            try
            {
                _logger.LogInformation("[ReportingAPI] Rozpoczynam generowanie raportu roku szkolnego {SchoolYearId}", request.SchoolYearId);

                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                _logger.LogDebug("[ReportingAPI] Generowanie raportu roku szkolnego dla {UserUpn}", currentUserUpn);

                var result = await _orchestrator.GenerateSchoolYearReportAsync(request.SchoolYearId, request.Options ?? new ReportOptions());

                if (result.Success && result.ReportStream != null)
                {
                    _logger.LogInformation("[ReportingAPI] Raport roku szkolnego {SchoolYearId} wygenerowany pomyślnie: {FileName}", 
                        request.SchoolYearId, result.FileName);

                    var contentType = GetContentType(request.Options?.Format ?? ReportFormat.PDF);
                    return File(result.ReportStream, contentType, result.FileName ?? "raport.pdf");
                }
                else
                {
                    _logger.LogWarning("[ReportingAPI] Generowanie raportu roku szkolnego {SchoolYearId} nie powiodło się: {Error}", 
                        request.SchoolYearId, result.ErrorMessage);
                    return BadRequest(new { Message = result.ErrorMessage ?? "Wystąpił błąd podczas generowania raportu" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingAPI] Błąd podczas generowania raportu roku szkolnego {SchoolYearId}", request.SchoolYearId);
                return StatusCode(500, new { Message = $"Błąd wewnętrzny: {ex.Message}" });
            }
        }

        /// <summary>
        /// Generuje raport aktywności użytkowników w systemie
        /// </summary>
        /// <param name="request">Parametry raportu aktywności</param>
        /// <returns>Raport aktywności użytkowników do pobrania</returns>
        /// <response code="200">Raport został wygenerowany pomyślnie</response>
        /// <response code="400">Nieprawidłowe parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpPost("user-activity-report")]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GenerateUserActivityReport([FromBody] UserActivityReportRequest request)
        {
            try
            {
                _logger.LogInformation("[ReportingAPI] Rozpoczynam generowanie raportu aktywności użytkowników {FromDate} - {ToDate}", 
                    request.FromDate, request.ToDate);

                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                _logger.LogDebug("[ReportingAPI] Generowanie raportu aktywności dla {UserUpn}", currentUserUpn);

                var result = await _orchestrator.GenerateUserActivityReportAsync(request.FromDate, request.ToDate);

                if (result.Success && result.ReportStream != null)
                {
                    _logger.LogInformation("[ReportingAPI] Raport aktywności użytkowników wygenerowany pomyślnie: {FileName}", result.FileName);

                    return File(result.ReportStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                        result.FileName ?? "raport_aktywnosc.xlsx");
                }
                else
                {
                    _logger.LogWarning("[ReportingAPI] Generowanie raportu aktywności nie powiodło się: {Error}", result.ErrorMessage);
                    return BadRequest(new { Message = result.ErrorMessage ?? "Wystąpił błąd podczas generowania raportu" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingAPI] Błąd podczas generowania raportu aktywności użytkowników");
                return StatusCode(500, new { Message = $"Błąd wewnętrzny: {ex.Message}" });
            }
        }

        /// <summary>
        /// Generuje raport compliance zgodnie z wymaganiami
        /// </summary>
        /// <param name="request">Parametry raportu compliance</param>
        /// <returns>Raport compliance do pobrania</returns>
        /// <response code="200">Raport został wygenerowany pomyślnie</response>
        /// <response code="400">Nieprawidłowe parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpPost("compliance-report")]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GenerateComplianceReport([FromBody] ComplianceReportRequest request)
        {
            try
            {
                _logger.LogInformation("[ReportingAPI] Rozpoczynam generowanie raportu compliance {ComplianceType}", request.Type);

                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                _logger.LogDebug("[ReportingAPI] Generowanie raportu compliance dla {UserUpn}", currentUserUpn);

                var result = await _orchestrator.GenerateComplianceReportAsync(request.Type);

                if (result.Success && result.ReportStream != null)
                {
                    _logger.LogInformation("[ReportingAPI] Raport compliance {ComplianceType} wygenerowany pomyślnie: {FileName}", 
                        request.Type, result.FileName);

                    return File(result.ReportStream, "application/pdf", result.FileName ?? "raport_compliance.pdf");
                }
                else
                {
                    _logger.LogWarning("[ReportingAPI] Generowanie raportu compliance {ComplianceType} nie powiodło się: {Error}", 
                        request.Type, result.ErrorMessage);
                    return BadRequest(new { Message = result.ErrorMessage ?? "Wystąpił błąd podczas generowania raportu" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingAPI] Błąd podczas generowania raportu compliance {ComplianceType}", request.Type);
                return StatusCode(500, new { Message = $"Błąd wewnętrzny: {ex.Message}" });
            }
        }

        /// <summary>
        /// Eksportuje dane systemowe do pliku
        /// </summary>
        /// <param name="request">Parametry eksportu danych</param>
        /// <returns>Plik z eksportowanymi danymi do pobrania</returns>
        /// <response code="200">Eksport został wygenerowany pomyślnie</response>
        /// <response code="400">Nieprawidłowe parametry żądania</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpPost("export-system-data")]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> ExportSystemData([FromBody] SystemDataExportRequest request)
        {
            try
            {
                _logger.LogInformation("[ReportingAPI] Rozpoczynam eksport danych systemu {DataType} w formacie {Format}", 
                    request.Options.DataType, request.Options.Format);

                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                _logger.LogDebug("[ReportingAPI] Eksport danych systemu dla {UserUpn}", currentUserUpn);

                var result = await _orchestrator.ExportSystemDataAsync(request.Options);

                if (result.Success && result.ReportStream != null)
                {
                    _logger.LogInformation("[ReportingAPI] Eksport danych {DataType} wygenerowany pomyślnie: {FileName}", 
                        request.Options.DataType, result.FileName);

                    var contentType = GetExportContentType(request.Options.Format);
                    return File(result.ReportStream, contentType, result.FileName ?? "eksport.xlsx");
                }
                else
                {
                    _logger.LogWarning("[ReportingAPI] Eksport danych {DataType} nie powiódł się: {Error}", 
                        request.Options.DataType, result.ErrorMessage);
                    return BadRequest(new { Message = result.ErrorMessage ?? "Wystąpił błąd podczas eksportu danych" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingAPI] Błąd podczas eksportu danych systemu {DataType}", request.Options.DataType);
                return StatusCode(500, new { Message = $"Błąd wewnętrzny: {ex.Message}" });
            }
        }

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów raportowania
        /// </summary>
        /// <returns>Lista aktywnych procesów raportowania</returns>
        /// <response code="200">Lista aktywnych procesów</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpGet("status")]
        [ProducesResponseType(typeof(IEnumerable<ReportingProcessStatus>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<IEnumerable<ReportingProcessStatus>>> GetActiveProcessesStatus()
        {
            try
            {
                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                _logger.LogDebug("[ReportingAPI] Pobieranie statusu procesów raportowania dla {UserUpn}", currentUserUpn);

                var processes = await _orchestrator.GetActiveProcessesStatusAsync();
                
                _logger.LogInformation("[ReportingAPI] Zwrócono {ProcessCount} aktywnych procesów raportowania dla {UserUpn}", 
                    processes.Count(), currentUserUpn);

                return Ok(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingAPI] Błąd podczas pobierania statusu procesów raportowania");
                return StatusCode(500, $"Błąd wewnętrzny: {ex.Message}");
            }
        }

        /// <summary>
        /// Anuluje aktywny proces raportowania (jeśli to możliwe)
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>Wynik anulowania procesu</returns>
        /// <response code="200">Proces został anulowany pomyślnie</response>
        /// <response code="400">Nieprawidłowy ID procesu</response>
        /// <response code="401">Brak autoryzacji</response>
        /// <response code="404">Proces nie istnieje lub nie może być anulowany</response>
        /// <response code="500">Błąd wewnętrzny serwera</response>
        [HttpDelete("{processId}")]
        [ProducesResponseType(typeof(ProcessCancelResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ProcessCancelResponse>> CancelProcess([FromRoute] string processId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(processId))
                {
                    return BadRequest(ProcessCancelResponse.CreateError("", "ID procesu nie może być pusty"));
                }

                var currentUserUpn = _currentUserService.GetCurrentUserUpn();
                _logger.LogDebug("[ReportingAPI] Anulowanie procesu raportowania {ProcessId} przez {UserUpn}", processId, currentUserUpn);

                var success = await _orchestrator.CancelProcessAsync(processId);
                
                if (success)
                {
                    _logger.LogInformation("[ReportingAPI] Proces raportowania {ProcessId} został anulowany przez {UserUpn}", processId, currentUserUpn);
                    return Ok(ProcessCancelResponse.CreateSuccess(processId, "Proces raportowania został anulowany", "Reporting"));
                }
                else
                {
                    _logger.LogWarning("[ReportingAPI] Nie można anulować procesu raportowania {ProcessId} - proces nie istnieje lub już się zakończył", processId);
                    return NotFound(ProcessCancelResponse.CreateError(processId, "Proces nie istnieje lub już się zakończył", "Reporting"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportingAPI] Błąd podczas anulowania procesu raportowania {ProcessId}", processId);
                return StatusCode(500, ProcessCancelResponse.CreateError(processId, $"Błąd wewnętrzny: {ex.Message}", "Reporting"));
            }
        }

        // ===== METODY POMOCNICZE =====

        private string GetContentType(ReportFormat format)
        {
            return format switch
            {
                ReportFormat.PDF => "application/pdf",
                ReportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ReportFormat.CSV => "text/csv",
                ReportFormat.JSON => "application/json",
                ReportFormat.HTML => "text/html",
                _ => "application/pdf"
            };
        }

        private string GetExportContentType(ExportFileFormat format)
        {
            return format switch
            {
                ExportFileFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ExportFileFormat.CSV => "text/csv",
                ExportFileFormat.JSON => "application/json",
                ExportFileFormat.XML => "application/xml",
                ExportFileFormat.ZIP => "application/zip",
                _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }
    }

    #region Request/Response DTOs

    /// <summary>
    /// Żądanie generowania raportu roku szkolnego
    /// </summary>
    public class SchoolYearReportRequest
    {
        /// <summary>
        /// ID roku szkolnego
        /// </summary>
        [Required(ErrorMessage = "ID roku szkolnego jest wymagane")]
        public string SchoolYearId { get; set; } = string.Empty;

        /// <summary>
        /// Opcje generowania raportu
        /// </summary>
        public ReportOptions? Options { get; set; }
    }

    /// <summary>
    /// Żądanie generowania raportu aktywności użytkowników
    /// </summary>
    public class UserActivityReportRequest
    {
        /// <summary>
        /// Data początkowa okresu
        /// </summary>
        [Required(ErrorMessage = "Data początkowa jest wymagana")]
        public DateTime FromDate { get; set; }

        /// <summary>
        /// Data końcowa okresu
        /// </summary>
        [Required(ErrorMessage = "Data końcowa jest wymagana")]
        public DateTime ToDate { get; set; }

        /// <summary>
        /// Walidacja zakresu dat
        /// </summary>
        public bool IsValidDateRange => FromDate <= ToDate;
    }

    /// <summary>
    /// Żądanie generowania raportu compliance
    /// </summary>
    public class ComplianceReportRequest
    {
        /// <summary>
        /// Typ raportu compliance
        /// </summary>
        [Required(ErrorMessage = "Typ raportu compliance jest wymagany")]
        public ComplianceReportType Type { get; set; }
    }

    /// <summary>
    /// Żądanie eksportu danych systemu
    /// </summary>
    public class SystemDataExportRequest
    {
        /// <summary>
        /// Opcje eksportu danych
        /// </summary>
        [Required(ErrorMessage = "Opcje eksportu są wymagane")]
        public ExportOptions Options { get; set; } = new ExportOptions();
    }

    #endregion
} 