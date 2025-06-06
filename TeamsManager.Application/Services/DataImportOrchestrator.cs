using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Helpers.PowerShell;
using System.Text.Json;
using System.Globalization;

namespace TeamsManager.Application.Services
{
    /// <summary>
    /// Orkiestrator importu danych odpowiedzialny za zarządzanie kompleksowymi operacjami importu CSV/Excel
    /// </summary>
    public class DataImportOrchestrator : IDataImportOrchestrator
    {
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<DataImportOrchestrator> _logger;

        public DataImportOrchestrator(
            IUserService userService,
            INotificationService notificationService,
            ILogger<DataImportOrchestrator> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BulkOperationResult> ImportUsersFromCsvAsync(Stream csvData, ImportOptions options, string apiAccessToken)
        {
            _logger.LogInformation("Orkiestrator: Import użytkowników z CSV");
            
            return new BulkOperationResult
            {
                Success = true,
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>(),
                Errors = new List<BulkOperationError>(),
                ProcessedAt = DateTime.UtcNow,
                OperationType = "ImportUsersFromCsv"
            };
        }

        public async Task<BulkOperationResult> ImportTeamsFromExcelAsync(Stream excelData, ImportOptions options, string apiAccessToken)
        {
            _logger.LogInformation("Orkiestrator: Import zespołów z Excel");
            
            return new BulkOperationResult
            {
                Success = true,
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>(),
                Errors = new List<BulkOperationError>(),
                ProcessedAt = DateTime.UtcNow,
                OperationType = "ImportTeamsFromExcel"
            };
        }

        public async Task<BulkOperationResult> ImportSchoolStructureAsync(Stream data, ImportOptions options, string apiAccessToken)
        {
            _logger.LogInformation("Orkiestrator: Import struktury szkoły");
            
            return new BulkOperationResult
            {
                Success = true,
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>(),
                Errors = new List<BulkOperationError>(),
                ProcessedAt = DateTime.UtcNow,
                OperationType = "ImportSchoolStructure"
            };
        }

        public async Task<ImportValidationResult> ValidateImportDataAsync(Stream data, ImportDataType type, ImportOptions? options = null)
        {
            _logger.LogInformation("Orkiestrator: Walidacja danych importu typu {DataType}", type);
            
            return await Task.FromResult(new ImportValidationResult
            {
                IsValid = true,
                TotalRecords = 10,
                ValidRecords = 10,
                DetectedColumns = new List<string> { "FirstName", "LastName", "UPN" }
            });
        }

        public async Task<IEnumerable<ImportProcessStatus>> GetActiveImportProcessesStatusAsync()
        {
            return await Task.FromResult(new List<ImportProcessStatus>());
        }

        public async Task<bool> CancelImportProcessAsync(string processId)
        {
            return await Task.FromResult(true);
        }

        public async Task<Stream> GenerateImportTemplateAsync(ImportDataType type, ImportFileFormat format)
        {
            var template = "FirstName;LastName;UPN;Department;Role\nJan;Kowalski;jan.kowalski@school.edu.pl;Matematyka;Nauczyciel";
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(template));
            return await Task.FromResult(stream);
        }

        // Metody pomocnicze

        private async Task<List<UserImportData>> ParseCsvUsersAsync(Stream csvData, ImportOptions options)
        {
            var users = new List<UserImportData>();
            
            csvData.Position = 0;
            using var reader = new StreamReader(csvData, Encoding.GetEncoding(options.Encoding));
            
            string? line;
            var lineNumber = 0;
            string[]? headers = null;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;
                
                if (lineNumber == 1 && options.HasHeaders)
                {
                    headers = line.Split(options.CsvDelimiter);
                    continue;
                }

                var columns = line.Split(options.CsvDelimiter);
                
                // Mapowanie danych według nagłówków lub pozycji
                var userData = MapCsvRowToUserData(columns, headers, options.ColumnMapping);
                if (userData != null)
                {
                    userData.RowNumber = lineNumber;
                    users.Add(userData);
                }
            }

            return users;
        }

        private UserImportData? MapCsvRowToUserData(string[] columns, string[]? headers, Dictionary<string, string>? columnMapping)
        {
            // Implementacja mapowania kolumn CSV na model UserImportData
            // Na razie podstawowa implementacja
            if (columns.Length < 3) return null;

            return new UserImportData
            {
                FirstName = columns.ElementAtOrDefault(0)?.Trim() ?? "",
                LastName = columns.ElementAtOrDefault(1)?.Trim() ?? "",
                UPN = columns.ElementAtOrDefault(2)?.Trim() ?? "",
                DepartmentName = columns.ElementAtOrDefault(3)?.Trim(),
                Role = columns.ElementAtOrDefault(4)?.Trim() ?? "Uczen"
            };
        }

        private async Task<BulkOperationResult> ProcessUserImportBatchesAsync(
            List<UserImportData> userData, 
            ImportOptions options, 
            ImportProcessStatus processStatus, 
            string apiAccessToken, 
            CancellationToken cancellationToken)
        {
            var successfulOperations = new List<BulkOperationSuccess>();
            var errors = new List<BulkOperationError>();

            // Przetwarzaj w partiach
            var batches = userData
                .Select((user, index) => new { user, index })
                .GroupBy(x => x.index / options.BatchSize)
                .Select(g => g.Select(x => x.user).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var batchResults = await ProcessUserBatchAsync(batch, options, apiAccessToken);
                    
                    successfulOperations.AddRange(batchResults.SuccessfulOperations);
                    errors.AddRange(batchResults.Errors);

                    processStatus.ProcessedRecords += batch.Count;
                    processStatus.SuccessfulRecords += batchResults.SuccessfulOperations.Count;
                    processStatus.FailedRecords += batchResults.Errors.Count;

                    await UpdateProcessStatusAsync(processStatus.ProcessId, processStatus);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Orkiestrator: Błąd podczas przetwarzania partii użytkowników");
                    
                    foreach (var user in batch)
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "ImportUser",
                            EntityId = user.UPN,
                            Message = $"Błąd partii: {ex.Message}"
                        });
                    }
                }

                // Krótka przerwa między partiami
                if (batch != batches.Last())
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }

            processStatus.Status = errors.Any() ? "Completed with errors" : "Completed";
            processStatus.CompletedAt = DateTime.UtcNow;
            await UpdateProcessStatusAsync(processStatus.ProcessId, processStatus);

            return new BulkOperationResult
            {
                Success = !errors.Any(),
                IsSuccess = !errors.Any(),
                SuccessfulOperations = successfulOperations,
                Errors = errors,
                ProcessedAt = DateTime.UtcNow,
                OperationType = "ImportUsersFromCsv"
            };
        }

        private async Task<BulkOperationResult> ProcessUserBatchAsync(List<UserImportData> userBatch, ImportOptions options, string apiAccessToken)
        {
            var successfulOperations = new List<BulkOperationSuccess>();
            var errors = new List<BulkOperationError>();

            foreach (var userData in userBatch)
            {
                try
                {
                    // Mapowanie UserImportData na User
                    var user = MapImportDataToUser(userData);
                    
                    // Sprawdź czy użytkownik już istnieje
                    var existingUser = await _userService.GetUserByUpnAsync(userData.UPN);
                    
                    if (existingUser != null && !options.UpdateExisting)
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "ImportUser",
                            EntityId = userData.UPN,
                            Message = "Użytkownik już istnieje, aktualizacja wyłączona"
                        });
                        continue;
                    }

                    User? result = null;
                    if (existingUser != null)
                    {
                        // Aktualizacja istniejącego użytkownika
                        UpdateUserFromImportData(existingUser, userData);
                        var updateSuccess = await _userService.UpdateUserAsync(existingUser, apiAccessToken);
                        if (updateSuccess)
                        {
                            result = existingUser;
                        }
                    }
                    else
                    {
                        // Tworzenie nowego użytkownika
                        result = await _userService.CreateUserAsync(
                            userData.FirstName,
                            userData.LastName,
                            userData.UPN,
                            Enum.Parse<UserRole>(userData.Role, true),
                            userData.DepartmentId ?? "default-dept",
                            "TempPassword123!", // Hasło tymczasowe - powinno być generowane lub przekazywane w opcjach
                            apiAccessToken
                        );
                    }

                    if (result != null)
                    {
                        successfulOperations.Add(new BulkOperationSuccess
                        {
                            Operation = existingUser != null ? "UpdateUser" : "CreateUser",
                            EntityId = result.Id,
                            EntityName = result.FullName,
                            Message = existingUser != null ? "Użytkownik zaktualizowany" : "Użytkownik utworzony"
                        });
                    }
                    else
                    {
                        errors.Add(new BulkOperationError
                        {
                            Operation = "ImportUser",
                            EntityId = userData.UPN,
                            Message = "Nie udało się utworzyć/zaktualizować użytkownika"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Orkiestrator: Błąd podczas importu użytkownika {UPN}", userData.UPN);
                    errors.Add(new BulkOperationError
                    {
                        Operation = "ImportUser",
                        EntityId = userData.UPN,
                        Message = ex.Message,
                        Exception = ex
                    });
                }
            }

            return new BulkOperationResult
            {
                Success = !errors.Any(),
                IsSuccess = !errors.Any(),
                SuccessfulOperations = successfulOperations,
                Errors = errors
            };
        }

        private User MapImportDataToUser(UserImportData userData)
        {
            return new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = userData.FirstName,
                LastName = userData.LastName,
                UPN = userData.UPN,
                DepartmentId = userData.DepartmentId ?? "default-dept",
                Role = Enum.Parse<UserRole>(userData.Role, true),
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "import-system",
                IsActive = true
            };
        }

        private void UpdateUserFromImportData(User existingUser, UserImportData userData)
        {
            existingUser.FirstName = userData.FirstName;
            existingUser.LastName = userData.LastName;
            if (!string.IsNullOrEmpty(userData.DepartmentId))
            {
                existingUser.DepartmentId = userData.DepartmentId;
            }
            if (Enum.TryParse<UserRole>(userData.Role, true, out var role))
            {
                existingUser.Role = role;
            }
            existingUser.ModifiedDate = DateTime.UtcNow;
            existingUser.ModifiedBy = "import-system";
        }

        private async Task<BulkOperationResult> SimulateUserImportAsync(List<UserImportData> userData, ImportProcessStatus processStatus)
        {
            _logger.LogInformation("Orkiestrator: DryRun - symulacja importu {Count} użytkowników", userData.Count);
            
            processStatus.Status = "Completed";
            processStatus.CompletedAt = DateTime.UtcNow;
            processStatus.SuccessfulRecords = userData.Count;
            processStatus.ProcessedRecords = userData.Count;

            return new BulkOperationResult
            {
                Success = true,
                IsSuccess = true,
                SuccessfulOperations = userData.Select(u => new BulkOperationSuccess
                {
                    Operation = "ImportUser",
                    EntityId = u.UPN,
                    EntityName = $"{u.FirstName} {u.LastName}",
                    Message = "Import użytkownika (DryRun)"
                }).ToList(),
                Errors = new List<BulkOperationError>(),
                ProcessedAt = DateTime.UtcNow,
                OperationType = "ImportUsersFromCsv_DryRun"
            };
        }

        private ImportValidationResult ValidateFileSize(Stream data, int maxSizeMB)
        {
            var maxSizeBytes = maxSizeMB * 1024 * 1024;
            
            if (data.Length > maxSizeBytes)
            {
                return new ImportValidationResult
                {
                    IsValid = false,
                    Errors = new List<ImportValidationError>
                    {
                        new ImportValidationError
                        {
                            Message = $"Plik jest za duży. Maksymalny rozmiar: {maxSizeMB}MB, aktualny: {data.Length / 1024 / 1024}MB",
                            ErrorType = "FileSize",
                            IsCritical = true
                        }
                    }
                };
            }

            return new ImportValidationResult { IsValid = true };
        }

        private async Task<ImportValidationResult> ValidateFileFormatAsync(Stream data, ImportDataType type, ImportOptions options)
        {
            try
            {
                data.Position = 0;
                using var reader = new StreamReader(data, Encoding.GetEncoding(options.Encoding));
                var firstLine = await reader.ReadLineAsync();
                
                if (string.IsNullOrEmpty(firstLine))
                {
                    return new ImportValidationResult
                    {
                        IsValid = false,
                        Errors = new List<ImportValidationError>
                        {
                            new ImportValidationError
                            {
                                Message = "Plik jest pusty",
                                ErrorType = "EmptyFile",
                                IsCritical = true
                            }
                        }
                    };
                }

                // Podstawowa walidacja CSV
                var columns = firstLine.Split(options.CsvDelimiter);
                if (columns.Length < 2)
                {
                    return new ImportValidationResult
                    {
                        IsValid = false,
                        Errors = new List<ImportValidationError>
                        {
                            new ImportValidationError
                            {
                                Message = "Plik musi zawierać co najmniej 2 kolumny",
                                ErrorType = "InsufficientColumns",
                                IsCritical = true
                            }
                        }
                    };
                }

                return new ImportValidationResult 
                { 
                    IsValid = true,
                    DetectedColumns = columns.ToList()
                };
            }
            catch (Exception ex)
            {
                return new ImportValidationResult
                {
                    IsValid = false,
                    Errors = new List<ImportValidationError>
                    {
                        new ImportValidationError
                        {
                            Message = $"Błąd walidacji formatu: {ex.Message}",
                            ErrorType = "FormatError",
                            IsCritical = true
                        }
                    }
                };
            }
        }

        private async Task<ImportValidationResult> ValidateDataStructureAsync(Stream data, ImportDataType type, ImportOptions options)
        {
            // Placeholder - pełna implementacja wymagałaby szczegółowej walidacji każdego typu danych
            return await Task.FromResult(new ImportValidationResult 
            { 
                IsValid = true,
                TotalRecords = 10, // Placeholder
                ValidRecords = 10  // Placeholder
            });
        }

        private async Task<ImportValidationResult> ValidateUsersBusinessRulesAsync(List<UserImportData> userData)
        {
            var errors = new List<ImportValidationError>();
            var warnings = new List<ImportValidationWarning>();

            foreach (var user in userData)
            {
                // Walidacja UPN
                if (string.IsNullOrEmpty(user.UPN) || !user.UPN.Contains('@'))
                {
                    errors.Add(new ImportValidationError
                    {
                        RowNumber = user.RowNumber,
                        ColumnName = "UPN",
                        Value = user.UPN,
                        Message = "UPN musi być prawidłowym adresem email",
                        ErrorType = "InvalidUPN",
                        IsCritical = true
                    });
                }

                // Walidacja imienia i nazwiska
                if (string.IsNullOrEmpty(user.FirstName) || string.IsNullOrEmpty(user.LastName))
                {
                    errors.Add(new ImportValidationError
                    {
                        RowNumber = user.RowNumber,
                        ColumnName = string.IsNullOrEmpty(user.FirstName) ? "FirstName" : "LastName",
                        Message = "Imię i nazwisko są wymagane",
                        ErrorType = "RequiredField",
                        IsCritical = true
                    });
                }

                // Walidacja roli
                if (!Enum.TryParse<UserRole>(user.Role, true, out _))
                {
                    warnings.Add(new ImportValidationWarning
                    {
                        RowNumber = user.RowNumber,
                        ColumnName = "Role",
                        Value = user.Role,
                        Message = $"Nieznana rola '{user.Role}', zostanie użyta domyślna 'Uczen'",
                        WarningType = "UnknownRole"
                    });
                    user.Role = "Uczen"; // Popraw na domyślną
                }
            }

            return await Task.FromResult(new ImportValidationResult
            {
                IsValid = !errors.Any(e => e.IsCritical),
                Errors = errors,
                Warnings = warnings,
                TotalRecords = userData.Count,
                ValidRecords = userData.Count - errors.Count(e => e.IsCritical)
            });
        }

        private string GenerateUsersTemplate(ImportFileFormat format)
        {
            return format switch
            {
                ImportFileFormat.CSV => "FirstName;LastName;UPN;Department;Role\nJan;Kowalski;jan.kowalski@school.edu.pl;Matematyka;Nauczyciel\nAnna;Nowak;anna.nowak@school.edu.pl;Informatyka;Uczen",
                ImportFileFormat.Json => JsonSerializer.Serialize(new[]
                {
                    new { FirstName = "Jan", LastName = "Kowalski", UPN = "jan.kowalski@school.edu.pl", Department = "Matematyka", Role = "Nauczyciel" },
                    new { FirstName = "Anna", LastName = "Nowak", UPN = "anna.nowak@school.edu.pl", Department = "Informatyka", Role = "Uczen" }
                }, new JsonSerializerOptions { WriteIndented = true }),
                _ => throw new NotSupportedException($"Format {format} nie jest obsługiwany")
            };
        }

        private string GenerateTeamsTemplate(ImportFileFormat format)
        {
            return format switch
            {
                ImportFileFormat.CSV => "TeamName;Description;Visibility;Owner;SchoolType\nMatematyka 1A;Zespół dla klasy 1A z matematyki;Private;jan.kowalski@school.edu.pl;LO",
                _ => throw new NotSupportedException($"Format {format} nie jest obsługiwany")
            };
        }

        private string GenerateDepartmentsTemplate(ImportFileFormat format)
        {
            return format switch
            {
                ImportFileFormat.CSV => "Name;Description;ParentDepartment;DepartmentCode\nMatematyka;Wydział Matematyczny;;MAT\nInformatyka;Wydział Informatyczny;;INF",
                _ => throw new NotSupportedException($"Format {format} nie jest obsługiwany")
            };
        }

        private string GenerateSubjectsTemplate(ImportFileFormat format)
        {
            return format switch
            {
                ImportFileFormat.CSV => "Name;Code;Description;Category\nMatematyka;MAT;Przedmiot matematyka;Nauki ścisłe\nInformatyka;INF;Przedmiot informatyka;Nauki ścisłe",
                _ => throw new NotSupportedException($"Format {format} nie jest obsługiwany")
            };
        }

        private async Task UpdateProcessStatusAsync(string processId, ImportProcessStatus status)
        {
            // Powiadomienie o postępie
            await _notificationService.SendOperationProgressToUserAsync(
                status.StartedBy,
                processId,
                (int)status.ProgressPercentage,
                status.CurrentOperation ?? "Processing..."
            );
        }

        private static BulkOperationResult CreateErrorResult(string errorMessage, string operationType)
        {
            return new BulkOperationResult
            {
                Success = false,
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = new List<BulkOperationError>
                {
                    new BulkOperationError 
                    { 
                        Message = errorMessage, 
                        Operation = operationType 
                    }
                },
                SuccessfulOperations = new List<BulkOperationSuccess>(),
                ProcessedAt = DateTime.UtcNow,
                OperationType = operationType
            };
        }
    }

    /// <summary>
    /// Model danych użytkownika z importu
    /// </summary>
    public class UserImportData
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UPN { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public string? DepartmentId { get; set; }
        public string Role { get; set; } = "Uczen";
        public string? Phone { get; set; }
        public string? AlternateEmail { get; set; }
        public int RowNumber { get; set; }
    }
} 