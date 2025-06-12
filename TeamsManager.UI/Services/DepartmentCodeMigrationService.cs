using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Serwis do migracji kodów działów zgodnie z nowym schematem automatycznego generowania
    /// </summary>
    public class DepartmentCodeMigrationService
    {
        private readonly IDepartmentService _departmentService;
        private readonly ILogger<DepartmentCodeMigrationService> _logger;

        public DepartmentCodeMigrationService(
            IDepartmentService departmentService,
            ILogger<DepartmentCodeMigrationService> logger)
        {
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Migruje kody wszystkich działów zgodnie z nowym schematem
        /// </summary>
        public async Task<MigrationResult> MigrateDepartmentCodesAsync()
        {
            var result = new MigrationResult();
            
            try
            {
                _logger.LogInformation("Rozpoczynanie migracji kodów działów...");
                
                // Pobierz wszystkie działy
                var allDepartments = await _departmentService.GetAllDepartmentsAsync();
                result.TotalDepartments = allDepartments.Count();
                
                _logger.LogInformation("Znaleziono {Count} działów do przetworzenia", result.TotalDepartments);
                
                // Grupuj działy według poziomów hierarchii (najpierw główne, potem potomne)
                var departmentsByLevel = OrganizeDepartmentsByHierarchyLevel(allDepartments);
                
                // Przetwarzaj poziom po poziomie
                foreach (var level in departmentsByLevel.Keys.OrderBy(k => k))
                {
                    var departmentsAtLevel = departmentsByLevel[level];
                    _logger.LogInformation("Przetwarzanie poziomu {Level} - {Count} działów", level, departmentsAtLevel.Count);
                    
                    foreach (var department in departmentsAtLevel)
                    {
                        try
                        {
                            var oldCode = department.DepartmentCode;
                            var newCode = await GenerateDepartmentCodeAsync(department);
                            
                            if (oldCode != newCode)
                            {
                                _logger.LogInformation("Aktualizacja działu '{Name}': '{OldCode}' -> '{NewCode}'", 
                                    department.Name, oldCode ?? "BRAK", newCode);
                                
                                department.DepartmentCode = newCode;
                                await _departmentService.UpdateDepartmentAsync(department);
                                
                                result.UpdatedDepartments++;
                                result.Changes.Add(new DepartmentCodeChange
                                {
                                    DepartmentId = department.Id,
                                    DepartmentName = department.Name,
                                    OldCode = oldCode,
                                    NewCode = newCode
                                });
                            }
                            else
                            {
                                _logger.LogDebug("Dział '{Name}' już ma poprawny kod: '{Code}'", department.Name, newCode);
                                result.SkippedDepartments++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Błąd podczas aktualizacji działu '{Name}' (ID: {Id})", 
                                department.Name, department.Id);
                            
                            result.ErroredDepartments++;
                            result.Errors.Add($"Dział '{department.Name}': {ex.Message}");
                        }
                    }
                }
                
                result.IsSuccess = result.ErroredDepartments == 0;
                
                _logger.LogInformation("Migracja zakończona. Zaktualizowano: {Updated}, Pominięto: {Skipped}, Błędy: {Errors}", 
                    result.UpdatedDepartments, result.SkippedDepartments, result.ErroredDepartments);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas migracji kodów działów");
                result.IsSuccess = false;
                result.Errors.Add($"Krytyczny błąd: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Organizuje działy według poziomów hierarchii
        /// </summary>
        private Dictionary<int, List<Department>> OrganizeDepartmentsByHierarchyLevel(IEnumerable<Department> departments)
        {
            var departmentsByLevel = new Dictionary<int, List<Department>>();
            var departmentDict = departments.ToDictionary(d => d.Id, d => d);
            
            foreach (var department in departments)
            {
                var level = CalculateHierarchyLevel(department, departmentDict);
                
                if (!departmentsByLevel.ContainsKey(level))
                {
                    departmentsByLevel[level] = new List<Department>();
                }
                
                departmentsByLevel[level].Add(department);
            }
            
            return departmentsByLevel;
        }

        /// <summary>
        /// Oblicza poziom hierarchii dla działu
        /// </summary>
        private int CalculateHierarchyLevel(Department department, Dictionary<string, Department> departmentDict)
        {
            if (string.IsNullOrEmpty(department.ParentDepartmentId))
            {
                return 0; // Dział główny
            }
            
            if (departmentDict.TryGetValue(department.ParentDepartmentId, out var parent))
            {
                return 1 + CalculateHierarchyLevel(parent, departmentDict);
            }
            
            return 0; // Fallback jeśli nie znaleziono rodzica
        }

        /// <summary>
        /// Generuje kod działu zgodnie z nowym schematem
        /// </summary>
        private async Task<string> GenerateDepartmentCodeAsync(Department department)
        {
            var codeParts = new List<string>();
            
            // Jeśli ma działu nadrzędnego, pobierz jego kod
            if (!string.IsNullOrEmpty(department.ParentDepartmentId))
            {
                try
                {
                    var parentDepartment = await _departmentService.GetDepartmentByIdAsync(department.ParentDepartmentId);
                    if (parentDepartment != null)
                    {
                        var parentCode = await GenerateDepartmentCodeAsync(parentDepartment);
                        if (!string.IsNullOrEmpty(parentCode))
                        {
                            codeParts.Add(parentCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Błąd podczas pobierania działu nadrzędnego dla '{DepartmentName}'", department.Name);
                }
            }

            // Dodaj znormalizowaną nazwę tego działu
            var normalizedName = NormalizeDepartmentName(department.Name);
            if (!string.IsNullOrEmpty(normalizedName))
            {
                codeParts.Add(normalizedName);
            }

            return string.Join("-", codeParts);
        }

        /// <summary>
        /// Normalizuje nazwę działu (usuwa polskie znaki i spacje)
        /// </summary>
        private string NormalizeDepartmentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Usuń polskie znaki
            var normalizedString = name.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            
            // Usuń spacje i znaki specjalne, zostaw tylko litery i cyfry
            result = new string(result.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            return result;
        }
    }

    /// <summary>
    /// Wynik migracji kodów działów
    /// </summary>
    public class MigrationResult
    {
        public bool IsSuccess { get; set; }
        public int TotalDepartments { get; set; }
        public int UpdatedDepartments { get; set; }
        public int SkippedDepartments { get; set; }
        public int ErroredDepartments { get; set; }
        public List<DepartmentCodeChange> Changes { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        public string GetSummary()
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Migracja kodów działów - {(IsSuccess ? "SUKCES" : "BŁĘDY")}");
            summary.AppendLine($"Łącznie działów: {TotalDepartments}");
            summary.AppendLine($"Zaktualizowano: {UpdatedDepartments}");
            summary.AppendLine($"Pominięto (bez zmian): {SkippedDepartments}");
            summary.AppendLine($"Błędy: {ErroredDepartments}");
            
            if (Changes.Any())
            {
                summary.AppendLine();
                summary.AppendLine("Szczegóły zmian:");
                foreach (var change in Changes)
                {
                    summary.AppendLine($"- {change.DepartmentName}: '{change.OldCode ?? "BRAK"}' -> '{change.NewCode}'");
                }
            }
            
            if (Errors.Any())
            {
                summary.AppendLine();
                summary.AppendLine("Błędy:");
                foreach (var error in Errors)
                {
                    summary.AppendLine($"- {error}");
                }
            }
            
            return summary.ToString();
        }
    }

    /// <summary>
    /// Informacja o zmianie kodu działu
    /// </summary>
    public class DepartmentCodeChange
    {
        public string DepartmentId { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string? OldCode { get; set; }
        public string NewCode { get; set; } = string.Empty;
    }
} 