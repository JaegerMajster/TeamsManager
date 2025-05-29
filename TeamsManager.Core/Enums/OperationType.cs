namespace TeamsManager.Core.Enums
{
    /// <summary>
    /// Typy operacji wykonywanych w systemie
    /// Używane do logowania w tabeli historii operacji
    /// </summary>
    public enum OperationType
    {
        // Operacje na zespołach
        TeamCreated = 1,        // Utworzenie zespołu
        TeamUpdated = 2,        // Aktualizacja zespołu
        TeamArchived = 3,       // Archiwizacja zespołu
        TeamUnarchived = 4,     // Przywrócenie zespołu z archiwum
        TeamDeleted = 5,        // Usunięcie zespołu

        // Operacje na członkach zespołów
        MemberAdded = 10,       // Dodanie członka do zespołu
        MemberRemoved = 11,     // Usunięcie członka z zespołu
        MemberRoleChanged = 12, // Zmiana roli członka w zespole

        // Operacje na kanałach
        ChannelCreated = 20,    // Utworzenie kanału
        ChannelUpdated = 21,    // Aktualizacja kanału
        ChannelDeleted = 22,    // Usunięcie kanału

        // Operacje na użytkownikach
        UserCreated = 30,       // Utworzenie użytkownika
        UserUpdated = 31,       // Aktualizacja użytkownika
        UserImported = 32,      // Import użytkownika z CSV
        UserDeactivated = 33,   // Dezaktywacja użytkownika

        // Operacje wsadowe
        BulkTeamCreation = 40,  // Masowe tworzenie zespołów
        BulkUserImport = 41,    // Masowy import użytkowników
        BulkArchiving = 42,     // Masowa archiwizacja

        // Operacje systemowe
        SystemBackup = 50,      // Kopia zapasowa systemu
        SystemRestore = 51,     // Przywracanie z kopii zapasowej
        ConfigurationChanged = 52 // Zmiana konfiguracji systemu
    }
}