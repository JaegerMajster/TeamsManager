using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Api.Controllers
{
    // --- Data Transfer Objects (DTO) ---
    // Dla tego kontrolera DTO mogą nie być potrzebne, jeśli parametry są proste
    // i przekazywane przez QueryString.

    public class OperationHistoryFilterDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public OperationType? OperationType { get; set; }
        public OperationStatus? OperationStatus { get; set; }
        public string? CreatedBy { get; set; } // UPN użytkownika
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // --- Kontroler ---

    [ApiController]
    [Route("api/[controller]")] // Trasa bazowa: /api/OperationHistories
    [Authorize] // Dostęp do historii operacji domyślnie wymaga autoryzacji
    public class OperationHistoriesController : ControllerBase
    {
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly ILogger<OperationHistoriesController> _logger;

        public OperationHistoriesController(IOperationHistoryService operationHistoryService, ILogger<OperationHistoriesController> logger)
        {
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{operationId}")]
        public async Task<IActionResult> GetOperationById(string operationId)
        {
            _logger.LogInformation("Pobieranie wpisu historii operacji o ID: {OperationId}", operationId);
            var operation = await _operationHistoryService.GetOperationByIdAsync(operationId);
            if (operation == null)
            {
                _logger.LogInformation("Wpis historii operacji o ID: {OperationId} nie został znaleziony.", operationId);
                return NotFound(new { Message = $"Wpis historii operacji o ID '{operationId}' nie został znaleziony." });
            }
            return Ok(operation);
        }

        [HttpGet("entity/{targetEntityType}/{targetEntityId}")]
        public async Task<IActionResult> GetHistoryForEntity(string targetEntityType, string targetEntityId, [FromQuery] int? count = null)
        {
            // Dekodowanie parametrów z URL
            var decodedEntityType = System.Net.WebUtility.UrlDecode(targetEntityType);
            var decodedEntityId = System.Net.WebUtility.UrlDecode(targetEntityId);

            _logger.LogInformation("Pobieranie historii dla encji: Typ={EntityType}, ID={EntityId}, Limit={Count}", decodedEntityType, decodedEntityId, count ?? -1);
            var history = await _operationHistoryService.GetHistoryForEntityAsync(decodedEntityType, decodedEntityId, count);
            return Ok(history);
        }

        [HttpGet("user/{userUpn}")]
        public async Task<IActionResult> GetHistoryByUser(string userUpn, [FromQuery] int? count = null)
        {
            var decodedUserUpn = System.Net.WebUtility.UrlDecode(userUpn);
            _logger.LogInformation("Pobieranie historii dla użytkownika: {UserUpn}, Limit={Count}", decodedUserUpn, count ?? -1);
            var history = await _operationHistoryService.GetHistoryByUserAsync(decodedUserUpn, count);
            return Ok(history);
        }

        [HttpGet("filter")]
        public async Task<IActionResult> GetHistoryByFilter([FromQuery] OperationHistoryFilterDto filter)
        {
            _logger.LogInformation("Pobieranie historii operacji z filtrowaniem. Zakres dat: {StartDate}-{EndDate}, Typ: {OperationType}, Status: {OperationStatus}, Użytkownik: {CreatedBy}, Strona: {Page}, Rozmiar: {PageSize}",
                filter.StartDate, filter.EndDate, filter.OperationType, filter.OperationStatus, filter.CreatedBy, filter.Page, filter.PageSize);

            if (filter.Page <= 0) filter.Page = 1;
            if (filter.PageSize <= 0) filter.PageSize = 20;
            if (filter.PageSize > 100) filter.PageSize = 100; // Ograniczenie maksymalnego rozmiaru strony

            var history = await _operationHistoryService.GetHistoryByFilterAsync(
                filter.StartDate,
                filter.EndDate,
                filter.OperationType,
                filter.OperationStatus,
                filter.CreatedBy,
                filter.Page,
                filter.PageSize
            );
            return Ok(history);
        }

        // Uwaga: Ten kontroler jest głównie do odczytu historii.
        // Logowanie operacji (tworzenie wpisów OperationHistory) odbywa się
        // wewnątrz serwisów aplikacyjnych (np. TeamService, UserService itp.)
        // podczas wykonywania przez nie operacji biznesowych.
        // Nie ma potrzeby udostępniania endpointu POST do tworzenia wpisów historii bezpośrednio przez API.
    }
}