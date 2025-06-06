using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;

namespace TeamsManager.Api.Controllers
{
    /// <summary>
    /// Kontroler do zarządzania operacjami importu danych CSV/Excel
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DataImportController : ControllerBase
    {
        private readonly IDataImportOrchestrator _dataImportOrchestrator;
        private readonly ILogger<DataImportController> _logger;

        public DataImportController(
            IDataImportOrchestrator dataImportOrchestrator,
            ILogger<DataImportController> logger)
        {
            _dataImportOrchestrator = dataImportOrchestrator ?? throw new ArgumentNullException(nameof(dataImportOrchestrator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Importuje użytkowników z pliku CSV
        /// </summary>
        /// <param name="file">Plik CSV z danymi użytkowników</param>
        /// <returns>Wynik operacji importu</returns>
        [HttpPost("users/csv")]
        public async Task<ActionResult<BulkOperationResult>> ImportUsersFromCsv(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("Nie przesłano pliku CSV");
                }

                var options = new ImportOptions();
                
                using var stream = file.OpenReadStream();
                var result = await _dataImportOrchestrator.ImportUsersFromCsvAsync(stream, options, "token");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas importu użytkowników z CSV");
                return StatusCode(500, "Wystąpił błąd podczas importu danych");
            }
        }

        /// <summary>
        /// Importuje zespoły z pliku Excel
        /// </summary>
        /// <param name="file">Plik Excel z danymi zespołów</param>
        /// <returns>Wynik operacji importu</returns>
        [HttpPost("teams/excel")]
        public async Task<ActionResult<BulkOperationResult>> ImportTeamsFromExcel(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("Nie przesłano pliku Excel");
                }

                var options = new ImportOptions();
                
                using var stream = file.OpenReadStream();
                var result = await _dataImportOrchestrator.ImportTeamsFromExcelAsync(stream, options, "token");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas importu zespołów z Excel");
                return StatusCode(500, "Wystąpił błąd podczas importu danych");
            }
        }

        /// <summary>
        /// Importuje strukturę szkoły z pliku
        /// </summary>
        /// <param name="file">Plik z danymi struktury szkoły</param>
        /// <returns>Wynik operacji importu</returns>
        [HttpPost("school-structure")]
        public async Task<ActionResult<BulkOperationResult>> ImportSchoolStructure(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("Nie przesłano pliku");
                }

                var options = new ImportOptions();
                
                using var stream = file.OpenReadStream();
                var result = await _dataImportOrchestrator.ImportSchoolStructureAsync(stream, options, "token");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas importu struktury szkoły");
                return StatusCode(500, "Wystąpił błąd podczas importu danych");
            }
        }

        /// <summary>
        /// Waliduje dane importu przed rzeczywistym importem
        /// </summary>
        /// <param name="file">Plik do walidacji</param>
        /// <param name="dataType">Typ danych do importu</param>
        /// <returns>Wynik walidacji</returns>
        [HttpPost("validate")]
        public async Task<ActionResult<ImportValidationResult>> ValidateImportData(IFormFile file, ImportDataType dataType)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("Nie przesłano pliku do walidacji");
                }

                using var stream = file.OpenReadStream();
                var result = await _dataImportOrchestrator.ValidateImportDataAsync(stream, dataType);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas walidacji danych importu");
                return StatusCode(500, "Wystąpił błąd podczas walidacji danych");
            }
        }

        /// <summary>
        /// Pobiera status aktywnych procesów importu
        /// </summary>
        /// <returns>Lista aktywnych procesów importu</returns>
        [HttpGet("status")]
        public async Task<ActionResult<IEnumerable<ImportProcessStatus>>> GetActiveImportProcesses()
        {
            try
            {
                var processes = await _dataImportOrchestrator.GetActiveImportProcessesStatusAsync();
                return Ok(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania statusu procesów importu");
                return StatusCode(500, "Wystąpił błąd podczas pobierania statusu");
            }
        }

        /// <summary>
        /// Anuluje aktywny proces importu
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>Wynik operacji anulowania</returns>
        [HttpDelete("cancel/{processId}")]
        public async Task<ActionResult<bool>> CancelImportProcess(string processId)
        {
            try
            {
                var result = await _dataImportOrchestrator.CancelImportProcessAsync(processId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas anulowania procesu importu {ProcessId}", processId);
                return StatusCode(500, "Wystąpił błąd podczas anulowania procesu");
            }
        }

        /// <summary>
        /// Generuje szablon importu dla określonego typu danych
        /// </summary>
        /// <param name="dataType">Typ danych dla szablonu</param>
        /// <param name="format">Format pliku (CSV/Excel)</param>
        /// <returns>Plik szablonu do pobrania</returns>
        [HttpGet("template")]
        public async Task<ActionResult> GenerateImportTemplate(ImportDataType dataType, ImportFileFormat format = ImportFileFormat.CSV)
        {
            try
            {
                var templateStream = await _dataImportOrchestrator.GenerateImportTemplateAsync(dataType, format);
                
                var fileName = $"template_{dataType}_{DateTime.UtcNow:yyyyMMdd}.csv";
                return File(templateStream, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas generowania szablonu dla {DataType}", dataType);
                return StatusCode(500, "Wystąpił błąd podczas generowania szablonu");
            }
        }
    }
} 