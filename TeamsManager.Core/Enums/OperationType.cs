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
        UserActivated = 34,     // Aktywacja użytkownika

        // Operacje wsadowe
        BulkTeamCreation = 40,  // Masowe tworzenie zespołów
        BulkUserImport = 41,    // Masowy import użytkowników
        BulkArchiving = 42,     // Masowa archiwizacja

        // Operacje systemowe
        SystemBackup = 50,      // Kopia zapasowa systemu
        SystemRestore = 51,     // Przywracanie z kopii zapasowej
        ConfigurationChanged = 52, // Zmiana konfiguracji systemu

        // Operacje na działach
        DepartmentCreated = 60, // Utworzenie działu
        DepartmentUpdated = 61, // Aktualizacja działu
        DepartmentDeleted = 62, // Usunięcie działu

        // Operacje na typach szkół
        SchoolTypeCreated = 70,                 // Utworzenie typu szkoły
        SchoolTypeUpdated = 71,                 // Aktualizacja typu szkoły
        SchoolTypeDeleted = 72,                 // Usunięcie typu szkoły
        UserAssignedToSchoolType = 73,          // Przypisanie użytkownika do typu szkoły
        UserRemovedFromSchoolType = 74,         // Usunięcie przypisania użytkownika z typu szkoły
        ViceDirectorAssignedToSchoolType = 75,  // Przypisanie wicedyrektora do nadzoru typu szkoły
        ViceDirectorRemovedFromSchoolType = 76, // Usunięcie przypisania wicedyrektora z nadzoru typu szkoły

        // Operacje na latach szkolnych
        SchoolYearCreated = 80,         // Utworzenie roku szkolnego
        SchoolYearUpdated = 81,         // Aktualizacja roku szkolnego
        SchoolYearDeleted = 82,         // Usunięcie roku szkolnego
        SchoolYearSetAsCurrent = 83,    // Ustawienie roku szkolnego jako bieżący

        // Operacje na przedmiotach
        SubjectCreated = 90,            // Utworzenie przedmiotu
        SubjectUpdated = 91,            // Aktualizacja przedmiotu
        SubjectDeleted = 92,            // Usunięcie przedmiotu
        TeacherAssignedToSubject = 93,  // Przypisanie nauczyciela do przedmiotu
        TeacherRemovedFromSubject = 94, // Usunięcie przypisania nauczyciela z przedmiotu

        // Operacje na szablonach zespołów
        TeamTemplateCreated = 100,      // Utworzenie szablonu zespołu
        TeamTemplateUpdated = 101,      // Aktualizacja szablonu zespołu
        TeamTemplateDeleted = 102,      // Usunięcie szablonu zespołu
        TeamTemplateCloned = 103,       // Sklonowanie szablonu zespołu

        // Operacje na ustawieniach aplikacji
        ApplicationSettingUpdated = 110,  // Aktualizacja ustawienia aplikacji
        ApplicationSettingCreated = 111,  // Utworzenie ustawienia aplikacji
        ApplicationSettingDeleted = 112,  // Usunięcie ustawienia aplikacji

        // Generyczne/Inne
        GenericCreated = 200,           // Generyczne utworzenie (jeśli brak specyficznego typu)
        GenericUpdated = 201,           // Generyczna aktualizacja
        GenericDeleted = 202,           // Generyczne usunięcie
        GenericOperation = 203          // Inna, niesklasyfikowana operacja
    }
}