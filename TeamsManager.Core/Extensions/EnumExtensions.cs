using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Extensions
{
    /// <summary>
    /// Rozszerzenia dla enumów z polskimi tłumaczeniami
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Zwraca polską nazwę statusu operacji
        /// </summary>
        public static string ToPolishString(this OperationStatus status) => status switch
        {
            OperationStatus.Pending => "Oczekująca",
            OperationStatus.InProgress => "W trakcie",
            OperationStatus.Completed => "Zakończona sukcesem",
            OperationStatus.Failed => "Nieudana",
            OperationStatus.Cancelled => "Anulowana",
            OperationStatus.PartialSuccess => "Częściowy sukces",
            _ => status.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę typu operacji
        /// </summary>
        public static string ToPolishString(this OperationType operationType) => operationType switch
        {
            OperationType.None => "Nieznana operacja",
            OperationType.TeamCreated => "Utworzenie zespołu",
            OperationType.TeamUpdated => "Aktualizacja zespołu",
            OperationType.TeamArchived => "Archiwizacja zespołu",
            OperationType.TeamUnarchived => "Przywrócenie zespołu",
            OperationType.TeamDeleted => "Usunięcie zespołu",
            OperationType.MemberAdded => "Dodanie członka",
            OperationType.MemberRemoved => "Usunięcie członka",
            OperationType.MemberRoleChanged => "Zmiana roli członka",
            OperationType.TeamMembersAdded => "Masowe dodawanie członków",
            OperationType.TeamMembersRemoved => "Masowe usuwanie członków",
            OperationType.ChannelCreated => "Utworzenie kanału",
            OperationType.ChannelUpdated => "Aktualizacja kanału",
            OperationType.ChannelDeleted => "Usunięcie kanału",
            OperationType.UserCreated => "Utworzenie użytkownika",
            OperationType.UserUpdated => "Aktualizacja użytkownika",
            OperationType.UserImported => "Import użytkownika",
            OperationType.UserDeactivated => "Dezaktywacja użytkownika",
            OperationType.UserActivated => "Aktywacja użytkownika",
            OperationType.DepartmentCreated => "Utworzenie działu",
            OperationType.DepartmentUpdated => "Aktualizacja działu",
            OperationType.DepartmentDeleted => "Usunięcie działu",
            OperationType.GenericCreated => "Utworzenie jednostki organizacyjnej",
            OperationType.GenericUpdated => "Aktualizacja jednostki organizacyjnej",
            OperationType.GenericDeleted => "Usunięcie jednostki organizacyjnej",
            OperationType.SchoolTypeCreated => "Utworzenie typu szkoły",
            OperationType.SchoolTypeUpdated => "Aktualizacja typu szkoły",
            OperationType.SchoolTypeDeleted => "Usunięcie typu szkoły",
            OperationType.SubjectCreated => "Utworzenie przedmiotu",
            OperationType.SubjectUpdated => "Aktualizacja przedmiotu",
            OperationType.SubjectDeleted => "Usunięcie przedmiotu",
            OperationType.TeamTemplateCreated => "Utworzenie szablonu zespołu",
            OperationType.TeamTemplateUpdated => "Aktualizacja szablonu zespołu",
            OperationType.TeamTemplateDeleted => "Usunięcie szablonu zespołu",
            OperationType.BulkTeamCreation => "Masowe tworzenie zespołów",
            OperationType.BulkUserImport => "Masowy import użytkowników",
            OperationType.BulkArchiving => "Masowa archiwizacja",
            OperationType.SystemBackup => "Kopia zapasowa systemu",
            OperationType.SystemRestore => "Przywracanie systemu",
            _ => operationType.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę statusu zespołu
        /// </summary>
        public static string ToPolishString(this TeamStatus status) => status switch
        {
            TeamStatus.Active => "Aktywny",
            TeamStatus.Archived => "Zarchiwizowany",
            _ => status.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę widoczności zespołu
        /// </summary>
        public static string ToPolishString(this TeamVisibility visibility) => visibility switch
        {
            TeamVisibility.Public => "Publiczny",
            TeamVisibility.Private => "Prywatny",
            _ => visibility.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę roli członka zespołu
        /// </summary>
        public static string ToPolishString(this TeamMemberRole role) => role switch
        {
            TeamMemberRole.Owner => "Właściciel",
            TeamMemberRole.Member => "Członek",
            _ => role.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę roli użytkownika
        /// </summary>
        public static string ToPolishString(this UserRole role) => role switch
        {
            UserRole.Uczen => "Uczeń",
            UserRole.Sluchacz => "Słuchacz",
            UserRole.Nauczyciel => "Nauczyciel",
            UserRole.PracownikAdministracyjny => "Pracownik administracyjny",
            UserRole.Wicedyrektor => "Wicedyrektor",
            UserRole.Dyrektor => "Dyrektor",
            UserRole.Administrator => "Administrator",
            _ => role.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę statusu kanału
        /// </summary>
        public static string ToPolishString(this ChannelStatus status) => status switch
        {
            ChannelStatus.Active => "Aktywny",
            ChannelStatus.Archived => "Zarchiwizowany",
            _ => status.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę typu ustawienia
        /// </summary>
        public static string ToPolishString(this SettingType type) => type switch
        {
            SettingType.String => "Tekst",
            SettingType.Integer => "Liczba całkowita",
            SettingType.Boolean => "Wartość logiczna",
            SettingType.Json => "Obiekt JSON",
            SettingType.DateTime => "Data i czas",
            SettingType.Decimal => "Liczba dziesiętna",
            _ => type.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę statusu zdrowia
        /// </summary>
        public static string ToPolishString(this HealthStatus status) => status switch
        {
            HealthStatus.Healthy => "Sprawny",
            HealthStatus.Degraded => "Ograniczony",
            HealthStatus.Unhealthy => "Niesprawny",
            _ => status.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę poziomu krytyczności błędu
        /// </summary>
        public static string ToPolishString(this HealthErrorSeverity severity) => severity switch
        {
            HealthErrorSeverity.Info => "Informacja",
            HealthErrorSeverity.Warning => "Ostrzeżenie",
            HealthErrorSeverity.Error => "Błąd",
            HealthErrorSeverity.Critical => "Krytyczny",
            _ => severity.ToString()
        };

        /// <summary>
        /// Zwraca polską nazwę typu encji
        /// </summary>
        public static string ToPolishEntityType(this string entityType) => entityType switch
        {
            "Team" => "Zespół",
            "User" => "Użytkownik",
            "Department" => "Dział",
            "OrganizationalUnit" => "Jednostka organizacyjna",
            "SchoolType" => "Typ szkoły",
            "Subject" => "Przedmiot",
            "TeamTemplate" => "Szablon zespołu",
            "Bulk" => "Operacja wsadowa",
            "System" => "System",
            "Generic" => "Ogólny",
            _ => entityType // Fallback do oryginalnej wartości
        };
    }
} 