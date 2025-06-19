using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Model reprezentujący licencję Microsoft 365 z Graph API
    /// </summary>
    public class License
    {
        /// <summary>
        /// Unikalny identyfikator SKU licencji
        /// </summary>
        public string? SkuId { get; set; }

        /// <summary>
        /// Numer części SKU (np. "ENTERPRISEPACK", "POWER_BI_PRO")
        /// </summary>
        public string? SkuPartNumber { get; set; }

        /// <summary>
        /// Data przypisania licencji
        /// </summary>
        public DateTime? AssignedDateTime { get; set; }

        /// <summary>
        /// Stan licencji (np. "Active", "Suspended", "Warning")
        /// </summary>
        public string? State { get; set; }

        /// <summary>
        /// Lista wyłączonych planów w ramach licencji
        /// </summary>
        public List<string> DisabledPlans { get; set; } = new List<string>();

        /// <summary>
        /// Nazwa wyświetlana licencji (opcjonalna)
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Czy licencja jest aktywna
        /// </summary>
        public bool IsActive => string.Equals(State, "Active", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Liczba wyłączonych planów
        /// </summary>
        public int DisabledPlansCount => DisabledPlans?.Count ?? 0;

        /// <summary>
        /// Zwraca czytelną reprezentację licencji
        /// </summary>
        /// <returns>String reprezentujący licencję</returns>
        public override string ToString()
        {
            var displayName = !string.IsNullOrEmpty(DisplayName) ? DisplayName : SkuPartNumber ?? "Nieznana licencja";
            var status = IsActive ? "Aktywna" : State ?? "Nieznany";
            return $"{displayName} ({status})";
        }
    }
} 