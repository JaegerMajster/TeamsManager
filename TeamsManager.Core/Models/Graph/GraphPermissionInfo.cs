using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Informacje o uprawnieniach aplikacji w Microsoft Graph API.
    /// Zachowuje kompatybilność z PowerShellPermissionInfo.
    /// </summary>
    public class GraphPermissionInfo
    {
        /// <summary>
        /// Czy aplikacja ma wystarczające uprawnienia.
        /// </summary>
        public bool HasRequiredPermissions { get; set; }

        /// <summary>
        /// Lista przypisanych uprawnień.
        /// </summary>
        public List<string> AssignedPermissions { get; set; } = new List<string>();

        /// <summary>
        /// Lista brakujących uprawnień.
        /// </summary>
        public List<string> MissingPermissions { get; set; } = new List<string>();

        /// <summary>
        /// Nazwa dzierżawy.
        /// </summary>
        public string? TenantName { get; set; }

        /// <summary>
        /// ID aplikacji.
        /// </summary>
        public string? ApplicationId { get; set; }

        /// <summary>
        /// Typ uwierzytelnienia.
        /// </summary>
        public string? AuthenticationType { get; set; }

        /// <summary>
        /// Data wygaśnięcia tokenu.
        /// </summary>
        public DateTime? TokenExpiresAt { get; set; }

        /// <summary>
        /// Kompletność uprawnień (0-100%).
        /// </summary>
        public double PermissionCompleteness
        {
            get
            {
                var totalRequired = GraphPermissionScopes.RequiredPermissions.Count;
                if (totalRequired == 0) return 100.0;

                var assigned = AssignedPermissions.Count(p => GraphPermissionScopes.RequiredPermissions.Contains(p));
                return (double)assigned / totalRequired * 100.0;
            }
        }

        /// <summary>
        /// Status uprawnień.
        /// </summary>
        public PermissionStatus Status
        {
            get
            {
                if (PermissionCompleteness >= 100.0) return PermissionStatus.Complete;
                if (PermissionCompleteness >= 80.0) return PermissionStatus.Sufficient;
                if (PermissionCompleteness >= 50.0) return PermissionStatus.Partial;
                return PermissionStatus.Insufficient;
            }
        }

        /// <summary>
        /// Czy token wygasł.
        /// </summary>
        public bool IsTokenExpired => TokenExpiresAt.HasValue && TokenExpiresAt.Value <= DateTime.UtcNow;

        /// <summary>
        /// Sprawdza czy aplikacja ma konkretne uprawnienie.
        /// </summary>
        /// <param name="permission">Nazwa uprawnienia</param>
        /// <returns>True jeśli ma uprawnienie</returns>
        public bool HasPermission(string permission)
        {
            return AssignedPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sprawdza czy aplikacja ma wszystkie podane uprawnienia.
        /// </summary>
        /// <param name="permissions">Lista uprawnień</param>
        /// <returns>True jeśli ma wszystkie uprawnienia</returns>
        public bool HasPermissions(IEnumerable<string> permissions)
        {
            return permissions.All(p => HasPermission(p));
        }

        /// <summary>
        /// Sprawdza czy aplikacja ma którekolwiek z podanych uprawnień.
        /// </summary>
        /// <param name="permissions">Lista uprawnień</param>
        /// <returns>True jeśli ma którekolwiek uprawnienie</returns>
        public bool HasAnyPermission(IEnumerable<string> permissions)
        {
            return permissions.Any(p => HasPermission(p));
        }

        /// <summary>
        /// Pobiera szczegółowy raport uprawnień.
        /// </summary>
        /// <returns>Szczegółowy raport</returns>
        public string GetPermissionReport()
        {
            var report = new List<string>
            {
                "=== RAPORT UPRAWNIEŃ MICROSOFT GRAPH API ===",
                $"Dzierżawa: {TenantName ?? "Nieznana"}",
                $"Aplikacja: {ApplicationId ?? "Nieznana"}",
                $"Typ uwierzytelnienia: {AuthenticationType ?? "Nieznany"}",
                $"Token wygasa: {TokenExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Nieznane"}",
                $"Status: {Status}",
                $"Kompletność: {PermissionCompleteness:F1}%",
                ""
            };

            if (AssignedPermissions.Count > 0)
            {
                report.Add("=== PRZYPISANE UPRAWNIENIA ===");
                foreach (var permission in AssignedPermissions.OrderBy(p => p))
                {
                    var isRequired = GraphPermissionScopes.RequiredPermissions.Contains(permission);
                    report.Add($"{(isRequired ? "✓" : "ℹ")} {permission}");
                }
                report.Add("");
            }

            if (MissingPermissions.Count > 0)
            {
                report.Add("=== BRAKUJĄCE UPRAWNIENIA ===");
                foreach (var permission in MissingPermissions.OrderBy(p => p))
                {
                    report.Add($"❌ {permission}");
                }
                report.Add("");
            }

            report.Add("=== KONIEC RAPORTU ===");
            return string.Join(Environment.NewLine, report);
        }
    }

    /// <summary>
    /// Status uprawnień aplikacji.
    /// </summary>
    public enum PermissionStatus
    {
        Insufficient = 0,
        Partial = 1,
        Sufficient = 2,
        Complete = 3
    }

    /// <summary>
    /// Definicje uprawnień wymaganych dla Microsoft Graph API.
    /// </summary>
    public static class GraphPermissionScopes
    {
        /// <summary>
        /// Uprawnienia wymagane dla pełnej funkcjonalności aplikacji.
        /// </summary>
        public static readonly List<string> RequiredPermissions = new List<string>
        {
            // Podstawowe uprawnienia użytkownika
            "User.Read",
            "User.ReadWrite.All",
            "User.ReadBasic.All",

            // Uprawnienia grup i zespołów
            "Group.Read.All",
            "Group.ReadWrite.All",
            "Team.ReadBasic.All",
            "Team.Create",
            "TeamMember.ReadWrite.All",
            "Channel.ReadBasic.All",
            "Channel.Create",
            "Channel.Delete.All",

            // Uprawnienia organizacji
            "Organization.Read.All",
            "Directory.Read.All",

            // Uprawnienia aplikacji
            "Application.Read.All"
        };

        /// <summary>
        /// Uprawnienia opcjonalne dla rozszerzonych funkcji.
        /// </summary>
        public static readonly List<string> OptionalPermissions = new List<string>
        {
            "Mail.Send",
            "Calendars.ReadWrite",
            "Files.ReadWrite.All",
            "Sites.ReadWrite.All"
        };

        /// <summary>
        /// Wszystkie uprawnienia (wymagane + opcjonalne).
        /// </summary>
        public static List<string> AllPermissions => RequiredPermissions.Concat(OptionalPermissions).ToList();
    }
} 