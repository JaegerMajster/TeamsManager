# Dodanie Testów Integracyjnych dla Repozytoriów

- [x] Krok 1: Konfiguracja Bazy Danych InMemory/SQLite dla Testów
    - [x] Utworzyć w `TeamsManager.Tests` klasę pomocniczą (np. `DbContextTestFixture`) dostarczającą skonfigurowaną instancję `TeamsManagerDbContext`.
    - [x] Zapewnić tworzenie schematu (`EnsureCreatedAsync`) i czyszczenie bazy danych między testami.

- [x] Krok 2: Testy Integracyjne dla `UserRepository`** (np. w `TeamsManager.Tests/Repositories/UserRepositoryTests.cs`)
    - [x] Test `AddAsync_ShouldAddUserToDatabase`.
    - [x] Test `GetByIdAsync_ShouldReturnCorrectUser`.
    - [x] Test `GetUserByUpnAsync_ShouldReturnCorrectUser`.
    - [x] Test `GetUsersByRoleAsync_ShouldReturnMatchingUsers`.
    - [x] Test `SearchUsersAsync_ShouldReturnMatchingUsers`.
    - [x] Test `Update_ShouldModifyUserData`.
    - [x] Test `Delete_ShouldMarkUserAsInactive` (lub `Delete_ShouldRemoveUserPhysically` w zależności od implementacji `GenericRepository.Delete`).

- [x] Krok 3: Testy Integracyjne dla `TeamRepository`
    - [x] Test `AddAsync_ShouldAddTeamToDatabase`.
    - [x] Test `GetByIdAsync_ShouldReturnCorrectTeam_WithIncludes` (członkowie, kanały).
    - [x] Test `GetTeamByNameAsync_ShouldReturnCorrectTeam`.
    - [x] Test `GetTeamsByOwnerAsync_ShouldReturnMatchingTeams`.
    - [x] Test `GetActiveTeamsAsync_ShouldReturnOnlyActiveStatusTeams`.
    - [x] Test `GetArchivedTeamsAsync_ShouldReturnOnlyArchivedStatusTeams`.
    - [x] Test `Update_ShouldModifyTeamData`.

- [x] Krok 4: Testy Integracyjne dla `TeamTemplateRepository`
    - [x] Testy CRUD.
    - [x] Test `GetDefaultTemplateForSchoolTypeAsync`.
    - [x] Test `GetUniversalTemplatesAsync`.
    - [x] Test `GetTemplatesBySchoolTypeAsync`.
    - [x] Test `SearchTemplatesAsync`.

- [x] Krok 5: Testy Integracyjne dla `SchoolYearRepository`
    - [x] Testy CRUD.
    - [x] Test `GetCurrentSchoolYearAsync`.
    - [x] Test `GetSchoolYearByNameAsync`.
    - [x] Test `GetSchoolYearsActiveOnDateAsync`.

- [x] Krok 6: Testy Integracyjne dla `OperationHistoryRepository`
    - [x] Testy CRUD.
    - [x] Test `GetHistoryForEntityAsync`.
    - [x] Test `GetHistoryByUserAsync`.
    - [x] Test `GetHistoryByDateRangeAsync`.

- [x] Krok 7: Testy Integracyjne dla `ApplicationSettingRepository`
    - [x] Testy CRUD.
    - [x] Test `GetSettingByKeyAsync`.
    - [x] Test `GetSettingsByCategoryAsync`.

- [x] Krok 8: Testy integracyjne dla `SubjectRepositoryTests.cs`
    - [x] 6.1. Utwórz nowy plik testowy `TeamsManager.Tests/Repositories/SubjectRepositoryTests.cs`.
    - [x] 6.2. Napisz testy integracyjne dla nowych, specyficznych metod (`GetByCodeAsync`, `GetTeachersAsync`, `GetByIdWithDetailsAsync`, `GetAllActiveWithDetailsAsync`) używając bazy danych InMemory lub testowej SQLite.

- [x] Krok 9: Testy Integracyjne dla pozostałych repozytoriów (Department, Channel, TeamMember - jeśli używają `GenericRepository`)
    - [x] Utworzyć odpowiednie pliki testowe.
    - [x] Przetestować podstawowe operacje CRUD.
    - [x] Przetestować `FindAsync` z różnymi predykatami.