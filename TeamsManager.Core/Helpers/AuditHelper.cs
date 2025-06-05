using System;

namespace TeamsManager.Core.Helpers
{
    /// <summary>
    /// Klasa pomocnicza dla operacji audytu w domenach biznesowych.
    /// Zapewnia spójne wartości fallback dla sytuacji gdy użytkownik nie jest dostępny.
    /// </summary>
    public static class AuditHelper
    {
        /// <summary>
        /// Użytkownik systemowy - podstawowa wartość fallback
        /// </summary>
        public const string SystemUser = "system";
        
        /// <summary>
        /// Automatyczne aktualizacje statystyk aktywności
        /// </summary>
        public const string SystemActivityUpdate = "system_activity_update";
        
        /// <summary>
        /// Operacje masowe/bulk
        /// </summary>
        public const string SystemBulkOperation = "system_bulk_operation";
        
        /// <summary>
        /// Operacje migracji danych
        /// </summary>
        public const string SystemMigration = "system_migration";
        
        /// <summary>
        /// Automatyczne statystyki użycia (np. IncrementUsage w TeamTemplate)
        /// </summary>
        public const string SystemUsageStats = "system_usage_stats";
        
        /// <summary>
        /// Aktualizacje logowania użytkowników
        /// </summary>
        public const string SystemLoginUpdate = "system_login_update";

        /// <summary>
        /// Pobiera wartość audytu dla użytkownika z kontekstem fallback.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika (może być null/empty)</param>
        /// <param name="fallbackContext">Kontekst dla systemu (np. "activity_update", "bulk_operation")</param>
        /// <returns>UPN użytkownika lub system_{fallbackContext}</returns>
        public static string GetAuditUser(string? userUpn, string fallbackContext)
        {
            return string.IsNullOrWhiteSpace(userUpn) 
                ? $"{SystemUser}_{fallbackContext}" 
                : userUpn;
        }

        /// <summary>
        /// Pobiera wartość audytu dla automatycznych aktualizacji aktywności.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika (może być null/empty)</param>
        /// <returns>UPN użytkownika lub system_activity_update</returns>
        public static string GetActivityUpdateAuditUser(string? userUpn)
        {
            return GetAuditUser(userUpn, "activity_update");
        }

        /// <summary>
        /// Pobiera wartość audytu dla aktualizacji statystyk użycia.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika (może być null/empty)</param>
        /// <returns>UPN użytkownika lub system_usage_stats</returns>
        public static string GetUsageStatsAuditUser(string? userUpn)
        {
            return GetAuditUser(userUpn, "usage_stats");
        }

        /// <summary>
        /// Pobiera wartość audytu dla aktualizacji logowania.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika (może być null/empty)</param>
        /// <returns>UPN użytkownika lub system_login_update</returns>
        public static string GetLoginUpdateAuditUser(string? userUpn)
        {
            return GetAuditUser(userUpn, "login_update");
        }
    }
} 