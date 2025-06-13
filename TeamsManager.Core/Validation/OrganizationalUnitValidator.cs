using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamsManager.Core.Models;
using TeamsManager.Core.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace TeamsManager.Core.Validation
{
    /// <summary>
    /// Validator odpowiedzialny za kompleksową walidację reguł biznesowych dla OrganizationalUnit
    /// </summary>
    public class OrganizationalUnitValidator
    {
        private readonly IOrganizationalUnitService _organizationalUnitService;
        private readonly ILogger<OrganizationalUnitValidator> _logger;

        public OrganizationalUnitValidator(
            IOrganizationalUnitService organizationalUnitService,
            ILogger<OrganizationalUnitValidator> logger)
        {
            _organizationalUnitService = organizationalUnitService ?? throw new ArgumentNullException(nameof(organizationalUnitService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Kompleksowa walidacja jednostki organizacyjnej przed utworzeniem
        /// </summary>
        public async Task<ValidationResult> ValidateForCreateAsync(OrganizationalUnit unit)
        {
            if (unit == null)
            {
                return ValidationResult.Failure("Jednostka organizacyjna nie może być null");
            }

            var result = new ValidationResult();

            // Walidacja podstawowych pól
            ValidateBasicFields(unit, result);

            if (!result.IsValid)
                return result;

            // Walidacja unikalności nazwy
            await ValidateNameUniquenessAsync(unit.Name, unit.ParentUnitId, null, result);

            // Walidacja jednostki nadrzędnej
            if (!string.IsNullOrEmpty(unit.ParentUnitId))
            {
                await ValidateParentUnitAsync(unit.ParentUnitId, result);
            }

            // Walidacja głębokości hierarchii
            await ValidateHierarchyDepthAsync(unit.ParentUnitId, result);

            // Walidacja reguł biznesowych specyficznych dla organizacji
            await ValidateOrganizationSpecificRulesAsync(unit, result);

            return result;
        }

        /// <summary>
        /// Kompleksowa walidacja jednostki organizacyjnej przed aktualizacją
        /// </summary>
        public async Task<ValidationResult> ValidateForUpdateAsync(OrganizationalUnit unit)
        {
            if (unit == null)
            {
                return ValidationResult.Failure("Jednostka organizacyjna nie może być null");
            }

            if (string.IsNullOrEmpty(unit.Id))
            {
                return ValidationResult.Failure("ID jednostki organizacyjnej jest wymagane przy aktualizacji");
            }

            var result = new ValidationResult();

            // Walidacja podstawowych pól
            ValidateBasicFields(unit, result);

            if (!result.IsValid)
                return result;

            // Sprawdź czy jednostka istnieje
            var existingUnit = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unit.Id);
            if (existingUnit == null)
            {
                result.AddError("Jednostka organizacyjna o podanym ID nie istnieje");
                return result;
            }

            // Walidacja unikalności nazwy (z wykluczeniem aktualnej jednostki)
            await ValidateNameUniquenessAsync(unit.Name, unit.ParentUnitId, unit.Id, result);

            // Walidacja zmiany jednostki nadrzędnej
            if (unit.ParentUnitId != existingUnit.ParentUnitId)
            {
                await ValidateParentChangeAsync(unit.Id, unit.ParentUnitId, result);
            }

            // Walidacja głębokości hierarchii po zmianie
            await ValidateHierarchyDepthAsync(unit.ParentUnitId, result);

            return result;
        }

        /// <summary>
        /// Walidacja przed usunięciem jednostki organizacyjnej
        /// </summary>
        public async Task<ValidationResult> ValidateForDeleteAsync(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return ValidationResult.Failure("ID jednostki organizacyjnej jest wymagane");
            }

            var result = new ValidationResult();

            // Sprawdź czy jednostka istnieje
            var unit = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId);
            if (unit == null)
            {
                result.AddError("Jednostka organizacyjna o podanym ID nie istnieje");
                return result;
            }

            // Sprawdź czy można usunąć
            var canDelete = await _organizationalUnitService.CanDeleteOrganizationalUnitAsync(unitId);
            if (!canDelete)
            {
                // Sprawdź szczegółowe przyczyny
                var subUnits = await _organizationalUnitService.GetSubUnitsAsync(unitId);
                var departments = await _organizationalUnitService.GetDepartmentsByOrganizationalUnitAsync(unitId);

                if (subUnits.Any())
                {
                    result.AddError($"Nie można usunąć jednostki organizacyjnej, ponieważ zawiera {subUnits.Count()} podjednostek");
                }

                if (departments.Any())
                {
                    result.AddError($"Nie można usunąć jednostki organizacyjnej, ponieważ zawiera {departments.Count()} działów");
                }
            }

            // Walidacja dodatkowych reguł biznesowych
            await ValidateDeleteBusinessRulesAsync(unit, result);

            return result;
        }

        /// <summary>
        /// Walidacja przeniesienia działów między jednostkami organizacyjnymi
        /// </summary>
        public async Task<ValidationResult> ValidateMoveDepartmentsAsync(
            IEnumerable<string> departmentIds, 
            string targetUnitId)
        {
            var result = new ValidationResult();

            if (departmentIds == null || !departmentIds.Any())
            {
                result.AddError("Lista działów do przeniesienia nie może być pusta");
                return result;
            }

            if (string.IsNullOrEmpty(targetUnitId))
            {
                result.AddError("ID docelowej jednostki organizacyjnej jest wymagane");
                return result;
            }

            // Sprawdź czy docelowa jednostka istnieje
            var targetUnit = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(targetUnitId);
            if (targetUnit == null)
            {
                result.AddError("Docelowa jednostka organizacyjna nie istnieje");
                return result;
            }

            // Walidacja każdego działu
            foreach (var deptId in departmentIds)
            {
                if (string.IsNullOrEmpty(deptId))
                {
                    result.AddError("ID działu nie może być puste");
                    continue;
                }

                // Tu można dodać dodatkowe walidacje specyficzne dla działów
                // np. sprawdzenie czy dział nie ma aktywnych zespołów, użytkowników itp.
            }

            return result;
        }

        #region Private Methods

        private void ValidateBasicFields(OrganizationalUnit unit, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(unit.Name))
            {
                result.AddError("Nazwa jednostki organizacyjnej jest wymagana");
            }
            else
            {
                if (unit.Name.Length > 100)
                {
                    result.AddError("Nazwa jednostki organizacyjnej nie może przekraczać 100 znaków");
                }

                if (unit.Name.Trim() != unit.Name)
                {
                    result.AddError("Nazwa jednostki organizacyjnej nie może zaczynać ani kończyć się spacjami");
                }

                // Sprawdź niedozwolone znaki
                var invalidChars = new[] { '<', '>', '"', '&', '\n', '\r', '\t' };
                if (unit.Name.IndexOfAny(invalidChars) >= 0)
                {
                    result.AddError("Nazwa jednostki organizacyjnej zawiera niedozwolone znaki");
                }
            }

            if (!string.IsNullOrEmpty(unit.Description) && unit.Description.Length > 500)
            {
                result.AddError("Opis jednostki organizacyjnej nie może przekraczać 500 znaków");
            }

            if (unit.SortOrder < 0)
            {
                result.AddError("Kolejność sortowania musi być nieujemna");
            }

            if (unit.SortOrder > 9999)
            {
                result.AddError("Kolejność sortowania nie może przekraczać 9999");
            }
        }

        private async Task ValidateNameUniquenessAsync(
            string name, 
            string? parentUnitId, 
            string? excludeUnitId, 
            ValidationResult result)
        {
            try
            {
                var isUnique = await _organizationalUnitService.IsNameUniqueAsync(name, parentUnitId, excludeUnitId);
                if (!isUnique)
                {
                    var parentInfo = parentUnitId != null ? "w ramach tej samej jednostki nadrzędnej" : "na poziomie głównym";
                    result.AddError($"Nazwa '{name}' już istnieje {parentInfo}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania unikalności nazwy jednostki organizacyjnej");
                result.AddError("Nie można sprawdzić unikalności nazwy");
            }
        }

        private async Task ValidateParentUnitAsync(string parentUnitId, ValidationResult result)
        {
            try
            {
                var parentUnit = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(parentUnitId);
                if (parentUnit == null)
                {
                    result.AddError("Określona jednostka nadrzędna nie istnieje");
                }
                else if (!parentUnit.IsActive)
                {
                    result.AddError("Jednostka nadrzędna jest nieaktywna");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania jednostki nadrzędnej");
                result.AddError("Nie można sprawdzić jednostki nadrzędnej");
            }
        }

        private async Task ValidateParentChangeAsync(string unitId, string? newParentUnitId, ValidationResult result)
        {
            try
            {
                if (newParentUnitId != null)
                {
                    // Sprawdź czy zmiana nie tworzy cyklu
                    var canMove = await _organizationalUnitService.CanMoveUnitAsync(unitId, newParentUnitId);
                    if (!canMove)
                    {
                        result.AddError("Zmiana jednostki nadrzędnej utworzyłaby cykl w hierarchii");
                    }

                    // Sprawdź czy nowa jednostka nadrzędna istnieje i jest aktywna
                    await ValidateParentUnitAsync(newParentUnitId, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas walidacji zmiany jednostki nadrzędnej");
                result.AddError("Nie można sprawdzić możliwości zmiany jednostki nadrzędnej");
            }
        }

        private async Task ValidateHierarchyDepthAsync(string? parentUnitId, ValidationResult result)
        {
            const int MaxHierarchyDepth = 10; // Maksymalna głębokość hierarchii

            if (parentUnitId == null)
                return; // Jednostka root - głębokość 0

            try
            {
                var hierarchyPath = await _organizationalUnitService.GetHierarchyPathAsync(parentUnitId);
                var currentDepth = hierarchyPath.Count();

                if (currentDepth >= MaxHierarchyDepth)
                {
                    result.AddError($"Maksymalna głębokość hierarchii ({MaxHierarchyDepth} poziomów) zostałaby przekroczona");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania głębokości hierarchii");
                result.AddError("Nie można sprawdzić głębokości hierarchii");
            }
        }

        private async Task ValidateOrganizationSpecificRulesAsync(OrganizationalUnit unit, ValidationResult result)
        {
            // Reguły specyficzne dla organizacji edukacyjnej

            // Sprawdź czy nazwa zawiera niedozwolone kombinacje
            var forbiddenNames = new[] { "DELETED", "REMOVED", "TEST", "TEMP" };
            if (forbiddenNames.Any(fn => unit.Name.ToUpperInvariant().Contains(fn)))
            {
                result.AddError("Nazwa jednostki organizacyjnej zawiera niedozwolone słowa");
            }

            // Walidacja kontekstu edukacyjnego
            if (unit.Name.ToLowerInvariant().Contains("klasa"))
            {
                // Klasy muszą mieć jednostkę nadrzędną
                if (string.IsNullOrEmpty(unit.ParentUnitId))
                {
                    result.AddError("Klasa musi być przypisana do jednostki nadrzędnej (np. semestru)");
                }
            }

            if (unit.Name.ToLowerInvariant().Contains("semestr"))
            {
                // Semestry muszą mieć jednostkę nadrzędną (szkołę)
                if (string.IsNullOrEmpty(unit.ParentUnitId))
                {
                    result.AddError("Semestr musi być przypisany do jednostki nadrzędnej (szkoły)");
                }
            }

            // Sprawdź limit jednostek na tym samym poziomie
            if (!string.IsNullOrEmpty(unit.ParentUnitId))
            {
                var siblings = await _organizationalUnitService.GetSubUnitsAsync(unit.ParentUnitId);
                if (siblings.Count() >= 50) // Maksymalnie 50 podjednostek
                {
                    result.AddError("Jednostka nadrzędna ma już maksymalną liczbę podjednostek (50)");
                }
            }
        }

        private async Task ValidateDeleteBusinessRulesAsync(OrganizationalUnit unit, ValidationResult result)
        {
            // Nie można usunąć jednostek systemowych
            var systemUnitNames = new[] { "Podstawowy", "Default", "System" };
            if (systemUnitNames.Contains(unit.Name, StringComparer.OrdinalIgnoreCase))
            {
                result.AddError("Nie można usunąć jednostki systemowej");
            }

            // Nie można usunąć jednostki, która była używana w historii
            // (tu można dodać sprawdzenie w OperationHistory)

            // Sprawdź czy jednostka nie jest oznaczona jako chroniona
            if (unit.Description?.ToUpperInvariant().Contains("PROTECTED") == true)
            {
                result.AddError("Jednostka jest oznaczona jako chroniona i nie może zostać usunięta");
            }

            await Task.CompletedTask; // Placeholder dla przyszłych asynchronicznych sprawdzeń
        }

        #endregion
    }

    /// <summary>
    /// Klasa reprezentująca wynik walidacji
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<string> Errors { get; } = new List<string>();

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Errors.Add(error);
            }
        }

        public static ValidationResult Success() => new ValidationResult();

        public static ValidationResult Failure(string error)
        {
            var result = new ValidationResult();
            result.AddError(error);
            return result;
        }

        public static ValidationResult Failure(IEnumerable<string> errors)
        {
            var result = new ValidationResult();
            foreach (var error in errors)
            {
                result.AddError(error);
            }
            return result;
        }

        public override string ToString()
        {
            return IsValid ? "Walidacja zakończona sukcesem" : string.Join("; ", Errors);
        }
    }
}