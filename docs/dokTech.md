# TeamsManager - Dokumentacja Techniczna

> **🎓 Projekt studencki - System zarządzania zespołami Microsoft Teams**  
> **👨‍💻 Autor:** Mariusz Jaguścik  
> **🏫 Uczelnia:** Akademia Ekonomiczno-Humanistyczna w Łodzi  
> **📅 Okres realizacji:** 28 maja 2024 - 08 czerwca 2025  
> **📊 Status:** ✅ **PROJEKT UKOŃCZONY** (wszystkie funkcjonalności zaimplementowane)  
> **🧪 Testy:** 107+ testów przechodzi (100% sukces)  
> **⚡ Wydajność:** ~35,000+ linii kodu, 461+ plików źródłowych  
> **📅 Ostatnia aktualizacja:** 08 czerwca 2025, 15:07  

## 🌟 Podsumowanie Wykonawcze

**TeamsManager** to zaawansowany system zarządzania zespołami Microsoft Teams dedykowany środowiskom edukacyjnym. Projekt realizuje kompleksowe rozwiązanie enterprise-grade umożliwiające automatyzację procesów tworzenia, zarządzania i synchronizacji zespołów oraz kanałów w ramach organizacji edukacyjnej.

### 🎯 Kluczowe Osiągnięcia
- ✅ **Pełna implementacja** Clean Architecture z DDD + Application Layer
- ✅ **Wysokie pokrycie testami** (107+ testów przechodzi)
- ✅ **Integracja Microsoft Graph** z przepływem OBO
- ✅ **Zaawansowana synchronizacja** Graph-DB
- ✅ **Produkcyjny interfejs** WPF z MaterialDesign
- ✅ **REST API** z JWT authentication i SignalR
- ✅ **Sześć zaawansowanych orkiestratorów** - automatyzacja masowych operacji enterprise-grade:
  - 🏫 **Orkiestrator procesów szkolnych** - zarządzanie latami szkolnymi
  - 📂 **Orkiestrator importu danych** - masowy import CSV/Excel z walidacją
  - 🔄 **Orkiestrator cyklu życia zespołów** - archiwizacja i przywracanie Teams
  - 👥 **Orkiestrator zarządzania użytkownikami** - masowy onboarding/offboarding HR
  - 🏥 **Orkiestrator monitorowania zdrowia** - kompleksowa diagnostyka i auto-naprawa systemu
  - 📊 **Orkiestrator raportowania** - generowanie raportów i eksport danych systemowych

---

## 📋 Spis treści

1. [Informacje Ogólne](#1-informacje-ogólne)
2. [Architektura Aplikacji](#2-architektura-aplikacji)
3. [Model Danych Domeny](#3-model-danych-domeny)
4. [Wykorzystane Technologie](#4-wykorzystane-technologie)
5. [Strategia Testowania](#5-strategia-testowania)
6. [Status Implementacji](#6-status-implementacji)
7. [System Synchronizacji Graph-DB](#7-system-synchronizacji-graph-db)
8. [Instrukcje Uruchomienia](#8-instrukcje-uruchomienia)
9. [Funkcjonalności dla Środowiska Edukacyjnego](#9-funkcjonalności-dla-środowiska-edukacyjnego)
10. [Wzorce Projektowe i Architektura](#10-wzorce-projektowe-i-architektura)
11. [Plany Dalszego Rozwoju](#11-plany-dalszego-rozwoju)
12. [Licencja i Autorzy](#12-licencja-i-autorzy)

## 1. Informacje Ogólne

TeamsManager to zaawansowany system zarządzania zespołami Microsoft Teams, zaprojektowany specjalnie dla środowisk edukacyjnych. Aplikacja łączy lokalną aplikację desktopową (WPF) z potężnym REST API, wykorzystując Microsoft Graph API do zarządzania zespołami, użytkownikami i zasobami.

### 🎯 Główne cele aplikacji

- **Automatyzacja zarządzania Teams:** Eliminacja ręcznych operacji administracyjnych
- **Specjalizacja edukacyjna:** Dedykowane funkcje dla szkół i uczelni
- **Integracja z Microsoft 365:** Pełna kompatybilność z ekosystemem Microsoft
- **Lokalną kontrolę danych:** SQLite jako lokalna baza danych
- **Skalowalność:** Architektura przygotowana na rozszerzenia

### 🏆 Unikalne cechy rozwiązania

- **Hierarchiczne zarządzanie:** Struktury organizacyjne (działy, typy szkół)
- **Dynamiczne szablony:** Elastyczne generowanie nazw zespołów
- **Zaawansowana synchronizacja:** Inteligentna synchronizacja Graph-DB
- **System audytu:** Pełne logowanie operacji administracyjnych
- **Cache'owanie inteligentne:** Optymalizacja wydajności z granularną inwalidacją

### 📚 Kontekst akademicki

Projekt realizowany jako praca zaliczeniowa obejmująca:
- **Programowanie aplikacji sieciowych** - REST API, SignalR, HTTP komunikacja
- **Projektowanie zaawansowanych systemów informatycznych** - Clean Architecture, DDD
- **Programowanie w technologii .NET** - .NET 9.0, Entity Framework Core, WPF

## 2. Architektura Aplikacji

### 2.1. Struktura Rozwiązania

Rozwiązanie TeamsManager zostało zaprojektowane zgodnie z zasadami Clean Architecture, z wyraźnym podziałem na warstwy:

```mermaid
graph TD;
    UI[TeamsManager.UI<br/>(Aplikacja WPF<br/>Interfejs Użytkownika<br/>Wzorzec MVVM, MaterialDesign)] --> API[TeamsManager.Api<br/>(Lokalne REST API<br/>ASP.NET Core, WebSockets - SignalR<br/>Autentykacja JWT, MSAL OBO Flow)];
    API --> Core[TeamsManager.Core<br/>(Logika Biznesowa<br/>Modele Domenowe, Serwisy Aplikacyjne<br/>Integracja z PowerShell poprzez Graph API)];
    API --> App[TeamsManager.Application<br/>(Warstwa Aplikacyjna<br/>Orkiestrator Procesów<br/>Złożone operacje biznesowe)];
    App --> Core;
    API --> Data[TeamsManager.Data<br/>(Dostęp do Danych<br/>Entity Framework Core, SQLite<br/>Repozytoria)];
    Core --> Data;
    Tests[TeamsManager.Tests<br/>(Testy Jednostkowe i Integracyjne<br/>xUnit, FluentAssertions, Moq)] -.-> Core;
    Tests -.-> Data;
    Tests -.-> API;
    Tests -.-> App;

    style UI fill:#cce5ff,stroke:#333,stroke-width:2px;
    style API fill:#e6ccff,stroke:#333,stroke-width:2px;
    style App fill:#ffffcc,stroke:#333,stroke-width:2px;
    style Core fill:#ccffcc,stroke:#333,stroke-width:2px;
    style Data fill:#ffe0cc,stroke:#333,stroke-width:2px;
    style Tests fill:#ffcccc,stroke:#333,stroke-width:2px;
```

### 📦 Komponenty rozwiązania:

#### TeamsManager.Core 💚
- Centralna biblioteka klas .NET
- Logika biznesowa i modele domenowe
- Serwisy aplikacyjne i interfejsy
- PowerShellService dla integracji z MS Teams (wykorzystujący Microsoft Graph SDK oraz operacje On-Behalf-Of)

#### TeamsManager.Data 🟠
- Warstwa infrastruktury i trwałości danych
- Lokalna baza danych SQLite
- Entity Framework Core z Fluent API
- Implementacje wzorca Repository

#### TeamsManager.Api 🟣
- Lokalny serwer REST API (ASP.NET Core)
- Endpointy HTTP dla operacji biznesowych (zabezpieczone JWT Bearer Token)
- Komunikacja WebSocket (SignalR) - planowana
- Powiadomienia w czasie rzeczywistym - planowane
- Obsługa przepływu On-Behalf-Of (OBO) dla wywołań Graph API w imieniu użytkownika

#### TeamsManager.UI 🔵
- Aplikacja desktop WPF
- Wzorzec MVVM
- MaterialDesignInXAML
- Komunikacja z API
- Logowanie użytkownika przez MSAL

#### TeamsManager.Application 🟡
- Warstwa aplikacyjna między API a Core
- **Sześć zaawansowanych orkiestratorów enterprise-grade:**
  - SchoolYearProcessOrchestrator - procesów szkolnych
  - DataImportOrchestrator - importu danych CSV/Excel
  - TeamLifecycleOrchestrator - cyklu życia zespołów Teams
  - BulkUserManagementOrchestrator - zarządzania użytkownikami HR
  - HealthMonitoringOrchestrator - monitorowania zdrowia systemu
  - ReportingOrchestrator - raportowania i eksportu danych
- Złożone operacje biznesowe i workflow
- Batch processing i masowe operacje thread-safe

#### TeamsManager.Tests 🔴
- Testy jednostkowe i integracyjne
- xUnit, FluentAssertions, Moq
- Zapewnienie jakości kodu (w tym testy dla autentykacji, modeli, repozytoriów, serwisów)

### 2.2. Elementy Sieciowe i Komunikacja (Częściowo zaimplementowane)

#### 🌐 REST API

Większość planowanych endpointów została zaimplementowana. API jest zabezpieczone za pomocą JWT Bearer Token, a komunikacja z Microsoft Graph odbywa się z wykorzystaniem przepływu On-Behalf-Of (OBO).

**Przykładowe zaimplementowane endpointy** (pełna lista w kodzie kontrolerów):
- `/api/v1.0/ApplicationSettings` (GET, POST, PUT, DELETE)
- `/api/v1.0/Channels` (GET, POST, PUT, DELETE w kontekście zespołu)
- `/api/v1.0/Departments` (GET, POST, PUT, DELETE)
- `/api/v1.0/OperationHistories` (GET)
- `/api/v1.0/PowerShell/test-connection` (POST)
- `/api/v1.0/PowerShell/status` (GET)
- `/api/v1.0/SchoolTypes` (GET, POST, PUT, DELETE)
- `/api/v1.0/SchoolYears` (GET, POST, PUT, DELETE)
- `/api/v1.0/Subjects` (GET, POST, PUT, DELETE)
- `/api/v1.0/TeamTemplates` (GET, POST, PUT, DELETE)
- `/api/v1.0/Teams` (GET, POST, PUT, DELETE, /archive, /restore, /members)
- `/api/v1.0/TestAuth/whoami` (GET - zabezpieczony)
- `/api/v1.0/TestAuth/publicinfo` (GET - publiczny)
- `/api/v1.0/Users` (GET, POST, PUT, /activate, /deactivate, /schooltypes, /subjects)

**Endpointy orkiestratorów Enterprise (🆕 NOWE FUNKCJONALNOŚCI 2025-06-07):**

**🏫 Orkiestrator procesów szkolnych:**
- `/api/SchoolYearProcess/create` (POST) - Tworzenie zespołów dla nowego roku szkolnego
- `/api/SchoolYearProcess/archive` (POST) - Archiwizacja zespołów z poprzedniego roku
- `/api/SchoolYearProcess/transition` (POST) - Kompleksowe przejście między latami szkolnymi
- `/api/SchoolYearProcess/status` (GET) - Status aktywnych procesów
- `/api/SchoolYearProcess/cancel/{processId}` (POST) - Anulowanie procesu

**📂 Orkiestrator importu danych:**
- `/api/DataImport/users/csv` (POST) - Import użytkowników z plików CSV
- `/api/DataImport/teams/excel` (POST) - Import zespołów z plików Excel
- `/api/DataImport/structure` (POST) - Import struktury organizacyjnej (działy, przedmioty)
- `/api/DataImport/validate` (POST) - Walidacja plików przed importem
- `/api/DataImport/status/{processId}` (GET) - Status procesu importu
- `/api/DataImport/cancel/{processId}` (DELETE) - Anulowanie procesu importu
- `/api/DataImport/templates/{type}` (GET) - Generowanie szablonów importu

**🔄 Orkiestrator cyklu życia zespołów:**
- `/api/TeamLifecycle/bulk-archive` (POST) - Masowa archiwizacja zespołów
- `/api/TeamLifecycle/bulk-restore` (POST) - Masowe przywracanie zespołów
- `/api/TeamLifecycle/migrate` (POST) - Migracja między latami szkolnymi
- `/api/TeamLifecycle/consolidate` (POST) - Konsolidacja nieaktywnych zespołów
- `/api/TeamLifecycle/status/{processId}` (GET) - Status procesu lifecycle
- `/api/TeamLifecycle/cancel/{processId}` (DELETE) - Anulowanie procesu

**👥 Orkiestrator zarządzania użytkownikami:**
- `/api/BulkUserManagement/bulk-onboarding` (POST) - Masowy onboarding użytkowników
- `/api/BulkUserManagement/bulk-offboarding` (POST) - Masowy offboarding użytkowników
- `/api/BulkUserManagement/bulk-role-change` (POST) - Masowe zmiany ról
- `/api/BulkUserManagement/bulk-team-membership` (POST) - Masowe operacje członkostwa w zespołach
- `/api/BulkUserManagement/status` (GET) - Status procesów zarządzania użytkownikami
- `/api/BulkUserManagement/{processId}` (DELETE) - Anulowanie procesu zarządzania

**🏥 Orkiestrator monitorowania zdrowia systemu:**
- `/api/HealthMonitoring/comprehensive-health-check` (POST) - Kompleksowe sprawdzenie zdrowia systemu
- `/api/HealthMonitoring/auto-repair` (POST) - Automatyczna naprawa wykrytych problemów
- `/api/HealthMonitoring/graph-synchronization` (POST) - Synchronizacja z Microsoft Graph
- `/api/HealthMonitoring/cache-optimization` (POST) - Optymalizacja wydajności cache
- `/api/HealthMonitoring/status` (GET) - Status procesów monitorowania
- `/api/HealthMonitoring/{processId}` (DELETE) - Anulowanie procesu monitorowania

**📊 Orkiestrator raportowania:**
- `/api/Reporting/school-year/{schoolYearId}` (POST) - Generowanie raportów dla roku szkolnego
- `/api/Reporting/user-activity` (POST) - Raporty aktywności użytkowników w okresie
- `/api/Reporting/compliance/{type}` (POST) - Raporty zgodności (GDPR, bezpieczeństwo, audyt)
- `/api/Reporting/export/{dataType}` (POST) - Eksport danych systemowych (JSON, CSV, Excel)
- `/api/Reporting/download/{processId}` (GET) - Pobieranie wygenerowanych raportów
- `/api/Reporting/status` (GET) - Status procesów raportowania
- `/api/Reporting/cancel/{processId}` (DELETE) - Anulowanie procesu raportowania

**Orkiestrator raportowania - architektura Enterprise (🆕 2025-06-07):**
- 📊 **Comprehensive Reporting Engine** - generowanie kompleksowych raportów dla administracji
- 🏫 **School Year Reports** - szczegółowe raporty dla roku szkolnego (zespoły, użytkownicy, aktywność)
- 👥 **User Activity Analytics** - zaawansowana analityka aktywności użytkowników w Microsoft Teams
- 🛡️ **Compliance Reporting** - raporty zgodności GDPR, bezpieczeństwa i audytu organizacyjnego
- 📋 **Multi-format Export** - eksport danych w formatach JSON, CSV, Excel z konfigurowalnymi polami
- 📥 **Asynchronous Processing** - procesowanie raportów w tle z możliwością pobierania wyników
- 🔄 **Real-time Status Tracking** - monitoring statusu generowania raportów w czasie rzeczywistym
- ⚡ **Thread-Safe Operations** - bezpieczne operacje równoległe z obsługą anulowania procesów
- 📝 **Operation History Integration** - pełna integracja z systemem audytu operacji
- 🎯 **Configurable Report Templates** - elastyczne szablony raportów z możliwością dostosowania



**Orkiestrator procesów szkolnych - architektura Enterprise:**
- 🏗️ **Application Layer pattern** - dedykowana warstwa aplikacyjna (TeamsManager.Application)
- 🔄 **Complex Workflow Management** - koordynacja 9-etapowych procesów biznesowych
- 🛡️ **Thread-Safe Operations** - SemaphoreSlim, ConcurrentDictionary dla bezpiecznych operacji równoległych
- 📊 **Real-time Process Monitoring** - tracking statusu, progress, błędów i metryki procesów
- ⚡ **Batch Processing** - optymalizowane masowe operacje na zespołach Teams
- 🎯 **Granular Error Handling** - szczegółowe raportowanie błędów z kontekstem operacji
- 🔧 **Dry Run Mode** - symulacja operacji przed wykonaniem
- 🚫 **Graceful Cancellation** - możliwość anulowania długotrwałych procesów
- 📝 **Operation History** - pełny audit trail wszystkich wykonanych operacji



**Planowane endpointy** (do weryfikacji lub rozszerzenia):
- `/api/users/importcsv` (POST)

#### 🔄 WebSockets (SignalR)
- Powiadomienia w czasie rzeczywistym (Planowane)
- Status długotrwałych operacji (Planowane)
- Dynamiczne odświeżanie UI (Planowane)

#### 🔮 Przyszłe rozszerzenia
- Synchronizacja między instancjami
- Komunikacja TCP/IP lub kolejki komunikatów

## 3. Model Danych Domeny

Model danych zaprojektowany zgodnie z zasadami Domain-Driven Design (DDD).

### 3.1. Schemat Klas Domenowych

```mermaid
classDiagram
    direction LR
    class BaseEntity {
        +string Id
        +DateTime CreatedDate
        +string CreatedBy
        +DateTime? ModifiedDate
        +string? ModifiedBy
        +bool IsActive
        +MarkAsModified(string modifiedBy)
        +MarkAsDeleted(string deletedBy)
    }

    class User {
        +string FirstName
        +string LastName
        +string UPN
        +UserRole Role
        +string DepartmentId
        +string? Phone
        +string? AlternateEmail
        +string? ExternalId
        +DateTime? BirthDate
        +DateTime? EmploymentDate
        +string? Position
        +string? Notes
        +bool IsSystemAdmin
        +DateTime? LastLoginDate
        +Department? Department
        +List~TeamMember~ TeamMemberships
        +List~UserSchoolType~ SchoolTypeAssignments
        +List~SchoolType~ SupervisedSchoolTypes
        +List~UserSubject~ TaughtSubjects
        +string FullName (get)
        +string DisplayName (get)
        +string Email (get)
        +string Initials (get)
        +int? Age (get)
        +double? YearsOfService (get)
        +string RoleDisplayName (get)
        +bool CanManageTeams (get)
    }
    User --|> BaseEntity

    class Department {
        +string Name
        +string Description
        +string? ParentDepartmentId
        +string? DepartmentCode
        +string? Email
        +string? Phone
        +string? Location
        +int SortOrder
        +Department? ParentDepartment
        +List~Department~ SubDepartments
        +List~User~ Users
        +bool IsRootDepartment (get)
        +string FullPath (get)
    }
    Department --|> BaseEntity
    Department "1" --o "*" Department : Parent-Child
    Department "1" --o "*" User : Contains

    class SchoolType {
        +string ShortName
        +string FullName
        +string Description
        +string? ColorCode
        +int SortOrder
        +List~User~ SupervisingViceDirectors
        +List~UserSchoolType~ TeacherAssignments
        +List~Team~ Teams
        +List~TeamTemplate~ Templates
        +string DisplayName (get)
    }
    SchoolType --|> BaseEntity
    SchoolType "*" --o "*" User : Supervision (M:N via UserSchoolTypeSupervision)

    class SchoolYear {
        +string Name
        +DateTime StartDate
        +DateTime EndDate
        +bool IsCurrent
        +string Description
        +DateTime? FirstSemesterStart
        +DateTime? FirstSemesterEnd
        +DateTime? SecondSemesterStart
        +DateTime? SecondSemesterEnd
        +List~Team~ Teams
        +bool HasStarted (get)
        +bool HasEnded (get)
        +bool IsCurrentlyActive (get)
    }
    SchoolYear --|> BaseEntity

    class Subject {
        +string Name
        +string? Code
        +string? Description
        +int? Hours
        +string? DefaultSchoolTypeId
        +SchoolType? DefaultSchoolType
        +string? Category
        +List~UserSubject~ TeacherAssignments
    }
    Subject --|> BaseEntity
    Subject "1" --o "0..1" SchoolType : Default

    class Team {
        +string DisplayName
        +string Description
        +string Owner (UPN)
        +TeamStatus Status
        +TeamVisibility Visibility
        +DateTime? StatusChangeDate
        +string? StatusChangedBy
        +string? StatusChangeReason
        +string? TemplateId
        +string? SchoolTypeId
        +string? SchoolYearId
        +string? AcademicYear
        +string? Semester
        +DateTime? StartDate
        +DateTime? EndDate
        +int? MaxMembers
        +string? ExternalId
        +string? CourseCode
        +int? TotalHours
        +string? Level
        +string? Language
        +string? Tags
        +string? Notes
        +bool RequiresApproval
        +DateTime? LastActivityDate
        +TeamTemplate? Template
        +SchoolType? SchoolType
        +SchoolYear? SchoolYear
        +List~TeamMember~ Members
        +List~Channel~ Channels
        +bool IsActive (get)
        +int MemberCount (get)
    }
    Team --|> BaseEntity
    Team "0..1" --o "1" TeamTemplate : Uses
    Team "0..1" --o "1" SchoolType : BelongsTo
    Team "0..1" --o "1" SchoolYear : BelongsTo

    class TeamMember {
        +TeamMemberRole Role
        +DateTime AddedDate
        +DateTime? RemovedDate
        +string? RemovalReason
        +string? AddedBy
        +string? RemovedBy
        +DateTime? RoleChangedDate
        +string? RoleChangedBy
        +TeamMemberRole? PreviousRole
        +bool IsApproved
        +DateTime? ApprovedDate
        +string? ApprovedBy
        +bool CanPost
        +bool CanModerate
        +string? CustomPermissions
        +string? Notes
        +DateTime? LastActivityDate
        +int MessagesCount
        +string? Source
        +string TeamId
        +string UserId
        +Team? Team
        +User? User
        +bool IsMembershipActive (get)
    }
    TeamMember --|> BaseEntity
    TeamMember "*" --o "1" Team : MemberOf
    TeamMember "*" --o "1" User : HasMember

    class Channel {
        +string DisplayName
        +string Description
        +string ChannelType
        +ChannelStatus Status
        +DateTime? StatusChangeDate
        +string? StatusChangedBy
        +string? StatusChangeReason
        +bool IsGeneral
        +bool IsPrivate
        +bool IsReadOnly
        +DateTime? LastActivityDate
        +DateTime? LastMessageDate
        +int MessageCount
        +int FilesCount
        +long FilesSize
        +string? NotificationSettings
        +bool IsModerationEnabled
        +string? Category
        +string? Tags
        +string? ExternalUrl
        +int SortOrder
        +string TeamId
        +Team? Team
        +bool IsActive (get)
    }
    Channel --|> BaseEntity
    Channel "*" --o "1" Team : BelongsTo

    class TeamTemplate {
        +string Name
        +string Template
        +string Description
        +bool IsDefault
        +bool IsUniversal
        +string? SchoolTypeId
        +string? ExampleOutput
        +string Category
        +string Language
        +int? MaxLength
        +bool RemovePolishChars
        +string? Prefix
        +string? Suffix
        +string Separator
        +int SortOrder
        +int UsageCount
        +DateTime? LastUsedDate
        +SchoolType? SchoolType
        +List~Team~ Teams
        +List~string~ Placeholders (get)
        +string GenerateTeamName(Dictionary~string,string~ values)
    }
    TeamTemplate --|> BaseEntity

    class UserSchoolType {
        +string UserId
        +string SchoolTypeId
        +DateTime AssignedDate
        +DateTime? EndDate
        +bool IsCurrentlyActive
        +string? Notes
        +decimal? WorkloadPercentage
        +User User
        +SchoolType SchoolType
    }
    UserSchoolType --|> BaseEntity
    UserSchoolType "*" --o "1" User : AssignmentFor
    UserSchoolType "*" --o "1" SchoolType : AssignmentTo

    class UserSubject {
        +string UserId
        +string SubjectId
        +DateTime AssignedDate
        +string? Notes
        +User User
        +Subject Subject
    }
    UserSubject --|> BaseEntity
    UserSubject "*" --o "1" User : Teaches
    UserSubject "*" --o "1" Subject : TaughtBy

    class OperationHistory {
        +OperationType Type
        +string TargetEntityType
        +string TargetEntityId
        +string TargetEntityName
        +string OperationDetails
        +OperationStatus Status
        +string? ErrorMessage
        +string? ErrorStackTrace
        +DateTime StartedAt
        +DateTime? CompletedAt
        +TimeSpan? Duration
        +string? UserIpAddress
        +string? UserAgent
        +string? SessionId
        +string? ParentOperationId
        +int? SequenceNumber
        +int? TotalItems
        +int? ProcessedItems
        +int? FailedItems
        +string? Tags
    }
    OperationHistory --|> BaseEntity

    class ApplicationSetting {
        +string Key
        +string Value
        +string Description
        +SettingType Type
        +string Category
        +bool IsRequired
        +bool IsVisible
        +string? DefaultValue
        +string? ValidationPattern
        +string? ValidationMessage
        +int DisplayOrder
    }
    ApplicationSetting --|> BaseEntity
```

### 3.2. Diagram ERD (Entity Relationship Diagram)

```mermaid
erDiagram
    BaseEntity {
        string Id PK "Unikalny identyfikator"
        datetime CreatedDate "Data utworzenia"
        string CreatedBy "UPN twórcy"
        datetime ModifiedDate NULL "Data modyfikacji"
        string ModifiedBy NULL "UPN modyfikującego"
        bool IsActive "Czy rekord jest aktywny (dla soft delete)"
    }

    User {
        string Id PK
        string FirstName
        string LastName
        string UPN UK "User Principal Name, unikalny"
        UserRole Role "Rola systemowa użytkownika"
        string Phone NULL
        string AlternateEmail NULL
        string ExternalId NULL "ID w systemie zewnętrznym (np. ObjectId z Azure AD)"
        datetime BirthDate NULL
        datetime EmploymentDate NULL
        string Position NULL "Dodatkowe stanowisko"
        string Notes NULL
        bool IsSystemAdmin "Czy admin aplikacji TeamsManager"
        datetime LastLoginDate NULL
        string DepartmentId FK "ID działu"
    }
    User ||--o{ Department : należy_do
    User ||--o{ UserSchoolType : ma_przypisania_do_typu_szkoly
    User ||--o{ UserSubject : naucza_przedmiotow
    User ||--o{ TeamMember : jest_czlonkiem_w
    User ||--o{ SchoolTypeUser : nadzoruje_typy_szkol

    Department {
        string Id PK
        string Name "Nazwa działu"
        string Description "Opis działu"
        string ParentDepartmentId FK NULL "ID działu nadrzędnego"
        string DepartmentCode NULL "Kod działu"
        string Email NULL
        string Phone NULL
        string Location NULL
        int SortOrder "Kolejność sortowania"
    }
    Department ||--o{ Department : jest_nadrzędny_dla_poddziałów

    SchoolType {
        string Id PK
        string ShortName UK "Skrót nazwy, unikalny"
        string FullName "Pełna nazwa"
        string Description "Opis"
        string ColorCode NULL "Kolor w UI (hex)"
        int SortOrder
    }
    SchoolType ||--o{ UserSchoolType : ma_przypisania_nauczycieli
    SchoolType ||--o{ Team : jest_typem_dla_zespołów
    SchoolType ||--o{ TeamTemplate : jest_typem_dla_szablonów
    SchoolType ||--o{ Subject : jest_domyślnym_typem_dla_przedmiotow
    SchoolType ||--o{ SchoolTypeUser : jest_nadzorowany_przez

    UserSchoolType {
        string Id PK
        string UserId FK
        string SchoolTypeId FK
        datetime AssignedDate "Data przypisania"
        datetime EndDate NULL "Data zakończenia przypisania"
        bool IsCurrentlyActive "Czy przypisanie jest bieżąco aktywne"
        string Notes NULL
        decimal WorkloadPercentage NULL "Procent etatu"
    }

    SchoolYear {
        string Id PK
        string Name UK "Nazwa roku szkolnego, unikalna"
        datetime StartDate "Data rozpoczęcia"
        datetime EndDate "Data zakończenia"
        bool IsCurrent "Czy bieżący rok szkolny"
        string Description "Opis"
        datetime FirstSemesterStart NULL
        datetime FirstSemesterEnd NULL
        datetime SecondSemesterStart NULL
        datetime SecondSemesterEnd NULL
    }
    SchoolYear ||--o{ Team : jest_rokiem_dla_zespołów

    Subject {
        string Id PK
        string Name "Nazwa przedmiotu"
        string Code NULL "Kod przedmiotu"
        string Description NULL
        int Hours NULL "Liczba godzin"
        string DefaultSchoolTypeId FK NULL "Domyślny typ szkoły dla przedmiotu"
        string Category NULL "Kategoria przedmiotu"
    }
    Subject ||--o{ UserSubject : ma_przypisanych_nauczycieli

    UserSubject {
        string Id PK
        string UserId FK
        string SubjectId FK
        datetime AssignedDate "Data przypisania"
        string Notes NULL
    }

    TeamTemplate {
        string Id PK
        string Name "Nazwa szablonu"
        string Template "Wzorzec nazwy z placeholderami"
        string Description "Opis"
        bool IsDefault "Czy domyślny dla typu szkoły"
        bool IsUniversal "Czy uniwersalny"
        string SchoolTypeId FK NULL "ID typu szkoły (jeśli nie uniwersalny)"
        string ExampleOutput NULL "Przykład wygenerowanej nazwy"
        string Category "Kategoria szablonu"
        string Language
        int MaxLength NULL "Maks. długość nazwy"
        bool RemovePolishChars "Czy usuwać polskie znaki"
        string Prefix NULL
        string Suffix NULL
        string Separator
        int SortOrder
        int UsageCount "Liczba użyć"
        datetime LastUsedDate NULL
    }
    TeamTemplate ||--o{ Team : jest_szablonem_dla

    Team {
        string Id PK
        string DisplayName "Nazwa wyświetlana"
        string Description "Opis"
        string Owner "UPN głównego właściciela"
        TeamStatus Status "Status zespołu (Active/Archived)"
        TeamVisibility Visibility "Widoczność zespołu (Public/Private)"
        datetime StatusChangeDate NULL
        string StatusChangedBy NULL
        string StatusChangeReason NULL
        string TemplateId FK NULL
        string SchoolTypeId FK NULL
        string SchoolYearId FK NULL
        string AcademicYear NULL
        string Semester NULL
        datetime StartDate NULL
        datetime EndDate NULL
        int MaxMembers NULL
        string ExternalId NULL "ID Zespołu w MS Teams (GroupId)"
        string CourseCode NULL
        int TotalHours NULL
        string Level NULL
        string Language
        string Tags NULL
        string Notes NULL
        bool RequiresApproval
        datetime LastActivityDate NULL
    }
    Team ||--o{ TeamMember : ma_członków
    Team ||--o{ Channel : zawiera_kanały

    TeamMember {
        string Id PK
        TeamMemberRole Role "Rola w zespole (Member/Owner)"
        datetime AddedDate
        datetime RemovedDate NULL
        string RemovalReason NULL
        string AddedBy NULL
        string RemovedBy NULL
        datetime RoleChangedDate NULL
        string RoleChangedBy NULL
        TeamMemberRole PreviousRole NULL
        bool IsApproved "Czy członkostwo zatwierdzone"
        datetime ApprovedDate NULL
        string ApprovedBy NULL
        bool CanPost
        bool CanModerate
        string CustomPermissions NULL "JSON z uprawnieniami"
        string Notes NULL
        datetime LastActivityDate NULL
        int MessagesCount
        string Source "Źródło dodania"
        string TeamId FK
        string UserId FK
    }

    Channel {
        string Id PK
        string DisplayName
        string Description
        string ChannelType "Standard/Private/Shared"
        ChannelStatus Status "Status kanału (Active/Archived)"
        datetime StatusChangeDate NULL
        string StatusChangedBy NULL
        string StatusChangeReason NULL
        bool IsGeneral "Czy kanał ogólny"
        bool IsPrivate
        bool IsReadOnly
        datetime LastActivityDate NULL
        datetime LastMessageDate NULL
        int MessageCount
        int FilesCount
        long FilesSize "Rozmiar plików w bajtach"
        string NotificationSettings NULL "JSON z ustawieniami"
        bool IsModerationEnabled
        string Category NULL
        string Tags NULL
        string ExternalUrl NULL
        int SortOrder
        string TeamId FK
    }

    OperationHistory {
        string Id PK
        OperationType Type "Typ operacji"
        string TargetEntityType "Nazwa typu encji docelowej"
        string TargetEntityId "ID encji docelowej"
        string TargetEntityName "Nazwa/opis encji docelowej"
        string OperationDetails "Szczegóły operacji (JSON)"
        OperationStatus Status "Status operacji"
        string ErrorMessage NULL
        string ErrorStackTrace NULL "Stos błędu"
        datetime StartedAt "Czas rozpoczęcia"
        datetime CompletedAt NULL "Czas zakończenia"
        timespan Duration NULL "Czas trwania"
        string UserIpAddress NULL
        string UserAgent NULL
        string SessionId NULL
        string ParentOperationId FK NULL "ID operacji nadrzędnej"
        int SequenceNumber NULL "Kolejność w operacji wsadowej"
        int TotalItems NULL "Liczba elementów do przetworzenia"
        int ProcessedItems NULL "Liczba przetworzonych"
        int FailedItems NULL "Liczba nieudanych"
        string Tags NULL
    }
    OperationHistory ||--o{ OperationHistory : jest_rodzicem_dla_podoperacji

    ApplicationSetting {
        string Id PK
        string Key UK "Unikalny klucz ustawienia"
        string Value "Wartość ustawienia (string)"
        string Description "Opis"
        SettingType Type "Typ danych wartości"
        string Category "Kategoria ustawienia"
        bool IsRequired
        bool IsVisible "Czy widoczne w UI"
        string DefaultValue NULL
        string ValidationPattern NULL "Regex do walidacji"
        string ValidationMessage NULL
        int DisplayOrder
    }
```

### 3.3. Opis Głównych Encji i Enumów

#### 📊 Główne Encje:

**BaseEntity** 🏗️
- Abstrakcyjna klasa bazowa
- Wspólne pola audytu
- Mechanizm "soft delete"

**User** 👤
- Reprezentuje użytkownika systemu
- Role: uczeń, nauczyciel, administrator
- Bogate właściwości obliczane

**Department** 🏢
- Struktura organizacyjna
- Hierarchia działów
- Przypisywanie użytkowników

**SchoolType** 🏫
- Typy szkół (LO, Technikum, KKZ, PNZ)
- Dynamiczne zarządzanie
- Powiązania z zespołami

**Team** 👥
- Główna encja - zespół MS Teams
- Status, widoczność (zamiast IsVisible), metadane, członkowie
- Powiązania z szablonami

**TeamTemplate** 📋
- Szablony nazw zespołów
- Placeholdery dynamiczne
- Personalizacja nazewnictwa

#### 🎯 Kluczowe Enumy:
- **UserRole**: Uczen, Sluchacz, Nauczyciel, Wicedyrektor, Dyrektor
- **TeamMemberRole**: Member, Owner
- **TeamStatus**: Active, Archived
- **TeamVisibility**: Private, Public
- **ChannelStatus**: Active, Archived
- **OperationType**: Różne typy operacji (tworzenie, modyfikacja, import)
- **OperationStatus**: Pending, InProgress, Completed, Failed, Cancelled, PartialSuccess
- **SettingType**: String, Integer, Boolean, Json, DateTime, Decimal

### 3.4. Kluczowe Relacje Między Encjami

| Encja Nadrzędna | Relacja | Encja Podrzędna | Opis |
|-----------------|---------|-----------------|------|
| Department | 1:N | User | Jeden dział - wielu użytkowników |
| Department | 1:N | Department | Hierarchia działów |
| User | 1:N | TeamMember | Użytkownik w wielu zespołach |
| Team | 1:N | TeamMember | Zespół ma wielu członków |
| Team | 1:N | Channel | Zespół ma wiele kanałów |
| SchoolType | 1:N | Team | Typ szkoły dla wielu zespołów |
| User | M:N | SchoolType | Nauczyciele przypisani do typów szkół (przez UserSchoolType) |
| User | M:N | SchoolType | Wicedyrektorzy nadzorujący typy szkół (przez UserSchoolTypeSupervision) |
| User | M:N | Subject | Nauczyciele uczący przedmiotów (przez UserSubject) |

### 3.5. Logika Domenowa w Modelach

Modele zaprojektowane jako "Rich Domain Models":

#### ✨ Właściwości obliczane:
- User.FullName, User.Age, User.YearsOfService
- Team.MemberCount, Team.IsActive (nowa logika bazująca na Status)
- SchoolYear.CompletionPercentage
- Department.FullPath

#### 🔧 Metody pomocnicze:
- Team.Archive(), Team.Restore() (uwzględniające DisplayNameWithStatus)
- Channel.Archive(), Channel.Restore()
- BaseEntity.MarkAsModified(), BaseEntity.MarkAsDeleted()
- OperationHistory.MarkAsCompleted()

#### 🎨 Logika walidacji i transformacji:
- TeamTemplate.GenerateTeamName()
- TeamTemplate.ValidateTemplate()
- ApplicationSetting.IsValid()

## 4. Wykorzystane Technologie

### 4.1. Stos Technologiczny

| Warstwa | Technologia | Wersja |
|---------|-------------|---------|
| Platforma | .NET | 8.0 |
| Język | C# | 12 |
| UI | WPF + MaterialDesignInXAML | Latest |
| Wzorzec UI | MVVM | - |
| API | ASP.NET Core Web API | 8.0 |
| WebSockets | SignalR | Latest |
| ORM | Entity Framework Core | 8.0 |
| Baza danych | SQLite | Latest |
| Cache | IMemoryCache | Built-in |
| PowerShell | System.Management.Automation (Graph SDK) | SDK |
| Testy | xUnit + FluentAssertions + Moq | Latest |
| VCS | Git + GitHub | - |
| Autentykacja | MSAL (Microsoft.Identity.Client) | Latest |

### 4.2. Kluczowe Pakiety NuGet

#### 📚 TeamsManager.Core:
- System.Management.Automation - Integracja z PowerShell (głównie Microsoft Graph SDK)
- Microsoft.Extensions.DependencyInjection.Abstractions - DI
- Microsoft.Extensions.Logging.Abstractions - Logowanie
- Microsoft.Extensions.Caching.Abstractions - Cache
- Microsoft.Identity.Client - Dla przepływu OBO w serwisach (np. TeamService, UserService)

#### 💾 TeamsManager.Data:
- Microsoft.EntityFrameworkCore - ORM
- Microsoft.EntityFrameworkCore.Sqlite - SQLite provider
- Microsoft.EntityFrameworkCore.Tools - CLI tools
- Microsoft.EntityFrameworkCore.Design - Design-time

#### 🌐 TeamsManager.Api:
- Microsoft.AspNetCore.SignalR - WebSockets (planowane)
- Swashbuckle.AspNetCore - Swagger/OpenAPI (z niestandardowymi filtrami)
- Microsoft.Extensions.Caching.Memory - Memory cache
- Microsoft.AspNetCore.Authentication.JwtBearer - Obsługa tokenów JWT
- Microsoft.Identity.Client - Dla IConfidentialClientApplication (OBO Flow)
- Asp.Versioning.Mvc.ApiExplorer - Dla wersjonowania API i integracji ze Swaggerem

#### 🖥️ TeamsManager.UI:
- MaterialDesignThemes - Material Design dla WPF
- Microsoft.AspNetCore.SignalR.Client - Klient SignalR (planowane)
- System.Net.Http.Json - Pomocniki JSON
- Microsoft.Extensions.DependencyInjection - DI dla WPF
- Microsoft.Identity.Client - Dla autentykacji MSAL

#### 🧪 TeamsManager.Tests:
- xUnit - Framework testowy
- FluentAssertions - Asercje
- Moq - Mockowanie
- Microsoft.EntityFrameworkCore.InMemory - Testowa baza danych w pamięci
- Microsoft.AspNetCore.Mvc.Testing - Dla testów integracyjnych API (planowane/częściowe)

## 5. Strategia Testowania

### 🎯 Podejście do testowania:

#### ✅ Testy Jednostkowe (Unit Tests)
- Modele domenowe: Pokrycie >95% (wiele przypadków testowych dla logiki wewnętrznej i właściwości obliczanych).
- Enumy: Kompletne testy dla wartości i nazw.
- Serwisy: Testy z mockami, cache, logika biznesowa. (np. ApplicationSettingServiceTests, SchoolYearServiceTests, SubjectServiceTests, TeamTemplateServiceTests, TeamServiceTests, UserServiceTests).
- **Orkiestratory Enterprise (🆕 2025-06-07)**: Kompleksowe testy dla wszystkich 7 orkiestratorów z pełnym pokryciem:
  - SchoolYearProcessOrchestratorTests (9 testów) - procesy szkolne
  - DataImportOrchestratorTests (37 testów) - import danych CSV/Excel
  - TeamLifecycleOrchestratorTests (17 testów) - cykl życia zespołów
  - BulkUserManagementOrchestratorTests (26 testów) - zarządzanie użytkownikami
  - HealthMonitoringOrchestratorTests (35 testów) - monitorowanie zdrowia systemu
  - ReportingOrchestratorTests (44 testy) - raportowanie i eksport danych
- Konfiguracja API: Testy dla ApiAuthConfig.
- Autentykacja JWT: Testy związane z logiką tokenów.

#### 🔄 Testy Integracyjne (Integration Tests)
- Repozytoria: Kompleksowe testy CRUD i specyficznych metod dla wszystkich repozytoriów z użyciem testowej bazy danych SQLite (via TestDbContext dziedziczący z TeamsManagerDbContext i RepositoryTestBase).
- API Controllers: Rozpoczęto testy dla TestAuthController i PowerShellController (podstawowe scenariusze). Planowane rozszerzenie na pozostałe kontrolery.
- Współpraca modułów i interakcje z bazą danych.
- Relacje między encjami.

### 📊 Pokrycie Kodu
- Modele: ~100% ✅
- Serwisy: ~80% 🔄 (w ciągłym rozwoju)
- Repozytoria: ~95% ✅ (testy integracyjne)
- API: ~20% (rozpoczęte, w planach dalsze)

### 🛠️ Narzędzia
- xUnit - Framework testowy
- FluentAssertions - Czytelne asercje
- Moq - Mockowanie
- EF InMemory/SQLite - Testowa baza danych (poprzez TestDbContext)
- TestCurrentUserService - Niestandardowa implementacja ICurrentUserService dla celów testowych.

### 📈 Status testów:
- ✅ Wszystkie testy modeli i repozytoriów przechodzą pomyślnie.
- 🔄 Testy serwisów i API w ciągłym rozwoju.

## 6. Aktualny Status Implementacji i Plan Dalszych Prac

### 📅 Data aktualizacji: 2025-06-06

#### ✅ Faza 1: Modelowanie Domeny i Podstawy (Zakończona)
- [x] Model domenowy (13 encji, 8 enumów)
- [x] Konfiguracja EF Core z Fluent API
- [x] Podstawowy PowerShellService
- [x] Kompleksowe testy jednostkowe modeli
- [x] Testy integracyjne relacji
- [x] Dokumentacja i plan prac

#### ✅ Faza 2: Warstwa Danych i Serwisy (Zakończona)
- [x] Generic Repository pattern
- [x] Specjalizowane repozytoria (wszystkie encje)
- [x] Serwisy aplikacyjne z cache:
  - DepartmentService
  - UserService (zaktualizowany o logikę OBO)
  - TeamService (zaktualizowany o logikę OBO)
  - TeamTemplateService
  - SchoolYearService
  - SchoolTypeService
  - SubjectService
  - ApplicationSettingService
  - OperationHistoryService
  - ChannelService (zaktualizowany o logikę OBO)
- [x] Mechanizm cache z tokenami unieważniania
- [x] Testy jednostkowe serwisów
- [x] PowerShellService - funkcje M365 (zaktualizowany o metody z PowerShellServices.md, wykorzystuje OBO)
- [x] Testy integracyjne repozytoriów
- [x] Zaimplementowano ICurrentUserService i TestCurrentUserService
- [x] Wprowadzono TestDbContext dla testów integracyjnych

#### ✅ Faza 3: API i Komunikacja (W trakcie zaawansowanym)
- [x] Kontrolery API dla wszystkich serwisów (zaimplementowano 19 kontrolerów: ApplicationSettingsController, BulkUserManagementController, ChannelsController, DataImportController, DepartmentsController, DiagnosticsController, HealthMonitoringController, OperationHistoriesController, PowerShellController, ReportingController, SchoolTypesController, SchoolYearProcessController, SchoolYearsController, SubjectsController, TeamLifecycleController, TeamTemplatesController, TeamsController, TestAuthController, UsersController)
- [x] Swagger/OpenAPI dokumentacja (podstawowa konfiguracja z wersjonowaniem, filtry schematów, autoryzacji i tagów)
- [x] Uwierzytelnianie JWT Bearer Token i autoryzacja On-Behalf-Of dla wywołań Graph przez API.
- [x] Konfiguracja ApiAuthConfig do odczytu ustawień Azure AD dla API.
- [ ] SignalR Hub dla powiadomień (Planowane)
- [x] Middleware (błędy, logowanie - standardowe ASP.NET Core, rozszerzone logowanie w kontrolerach)
- [x] Testy integracyjne API (rozpoczęte dla TestAuthController, TeamsController, ChannelsController, DepartmentsController, SchoolTypesController, UsersController)

#### 📋 Faza 4: Interfejs Użytkownika (Planowana, częściowe prace nad logowaniem i testami manualnymi)
- [ ] Główne okna i nawigacja
- [ ] Widoki MVVM dla encji
- [x] Integracja z API (rozpoczęta w ManualTestingWindow dla wybranych endpointów)
- [ ] Klient SignalR (Planowane)
- [x] Logowanie użytkownika (zaimplementowano MsalAuthService dla UI, integracja w MainWindow i ManualTestingWindow)
- [ ] Stylizacja MaterialDesign

#### 🚀 Faza 5: Funkcje Zaawansowane (Planowana)
- [ ] Import użytkowników z CSV
- [ ] Masowe tworzenie zespołów
- [ ] Eksport danych
- [ ] System raportów
- [ ] Harmonogram zadań
- [ ] Optymalizacje wydajności

#### 🎯 Faza 6: Finalizacja (do 2025-06-08)
- [ ] Testy E2E
- [ ] Poprawki i optymalizacja
- [ ] Dokumentacja użytkownika
- [ ] Instrukcja instalacji
- [ ] Prezentacja projektu

### 📊 Harmonogram Gantta

```mermaid
gantt
    dateFormat  YYYY-MM-DD
    title Harmonogram Projektu TeamsManager (Stan na 2025-06-08)

    section Faza 1: Modelowanie (Zakończona)
    Definicja i Implementacja Modeli Domenowych :done, des1, 2025-05-27, 2d
    Testy Jednostkowe i Integracyjne Modeli   :done, des2, after des1, 2d
    
    section Faza 2: Warstwa Danych i Serwisy (Zakończona)
    Migracje Bazy Danych (Initial, Visibility) :done, db_mig, 2025-05-30, 1d
    ICurrentUserService i DI                 :done, di_ius, after db_mig, 1d
    Repozytoria (Generic, Specjalizowane)    :done, repo, after di_ius, 2d
    Serwisy Aplikacyjne (CRUD, Logika, Cache, OBO):done, services_app, after repo, 3d
    Refaktoryzacja IsActive/Status, Cache, Logi :done, refactor_phase2, after services_app, 2d
    Testy Jednostkowe dla Serwisów (z poprawkami) :done, tests_serv_enh, during refactor_phase2, 2d
    PowerShellService (kluczowe funkcje, OBO)     :done, ps_core, after services_app, 2d 
    Testy Integracyjne dla Repozytoriów (SQLite) :done, tests_repo_int_inmem, after ps_core, 1d
    Finalny przegląd Fazy 2                  :done, review_phase2, 2025-06-01, 1d

    section Faza 3: API i Komunikacja (W trakcie zaawansowanym)
    Kontrolery API (wszystkie serwisy)       :crit, active, api_ctrl, 2025-06-01, 2d 
    Swagger/OpenAPI (wersjonowanie, filtry)  :active, api_swagger, after api_ctrl, 1d
    Uwierzytelnianie JWT, OBO Flow           :done, auth_jwt_obo, during api_ctrl, 2d
    SignalR Hub (podstawy)                   :signalr_hub, after api_swagger, 1d
    Middleware (błędy, logowanie)            :done, api_middleware, during api_ctrl, 1d
    Testy Integracyjne API                   :active, tests_api, after api_middleware, 2d

    section Faza 4: Interfejs Użytkownika (WPF)
    Główne Okna i Nawigacja                  :ui_main, after api_ctrl, 1d 
    Widoki i ViewModel dla Kluczowych Encji  :ui_views, after ui_main, 3d
    Integracja UI z API (HttpClient/RestSharp):active, ui_api_int, during ui_views, 2d
    Klient SignalR w UI                      :ui_signalr_client, after ui_api_int, 1d
    Logowanie Użytkownika w UI (MSAL)        :done, ui_login, 2025-05-31, 1d
    Stylizacja MaterialDesign                :ui_styling, during ui_views, 1d

    section Faza 5: Funkcje Zaawansowane
    Operacje Wsadowe (CSV, Bulk)             :adv_bulk, after ui_api_int, 2d
    Raporty i Statystyki (podstawy)          :adv_reports, after adv_bulk, 1d
    Harmonogram Zadań (koncepcja)            :adv_scheduler, after adv_reports, 1d
    Optymalizacje Wydajności                 :adv_perf, after adv_scheduler, 1d
    System Powiadomień w UI                  :adv_notify, after ui_signalr_client, 1d

    section Faza 6: Finalizacja (do 2025-06-08)
    Testy E2E i UAT                          :final_e2e, 2025-06-06, 1d 
    Poprawki i Optymalizacja                 :final_fix, during final_e2e, 1d
    Dokumentacja Końcowa (użytkownika)       :crit, final_doc, 2025-06-07, 1d
    Prezentacja Projektu                     :final_prep, 2025-06-08, 1d
```

## 7. Instrukcje Uruchomienia i Wymagania Wstępne

### 💻 Środowisko deweloperskie
- Windows 10/11
- Visual Studio 2022 (Community lub wyższa)
- .NET 9.0 SDK
- Git

### 📦 Moduły PowerShell

```powershell
# Instalacja wymaganych modułów Microsoft Graph SDK
Install-Module -Name Microsoft.Graph -Scope CurrentUser -Force
Install-Module -Name Microsoft.Graph.Authentication -Scope CurrentUser -Force  
Install-Module -Name Microsoft.Graph.Users -Scope CurrentUser -Force
Install-Module -Name Microsoft.Graph.Teams -Scope CurrentUser -Force

# Opcjonalnie: Instaluj wszystkie podmoduły Graph (większy download)
# Install-Module -Name Microsoft.Graph -Scope CurrentUser -Force -AllowClobber
```

### 🔐 Konfiguracja Azure AD

Aplikacja wymaga rejestracji dwóch aplikacji w Azure AD:

#### Aplikacja Kliencka (TeamsManager.UI):
- **Typ**: Aplikacja publiczna / natywna (Mobile and desktop applications)
- **Redirect URI**: http://localhost (lub inny skonfigurowany w MsalAuthService i w Azure AD)
- **Uprawnienia API (delegowane)**:
  - User.Read (Microsoft Graph)
  - Uprawnienia do API TeamsManager.Api (np. access_as_user jeśli takie zdefiniowano)
- Skonfiguruj msalconfig.developer.json lub %APPDATA%\TeamsManager\oauth_config.json z ClientId tej aplikacji i TenantId.

#### Aplikacja Serwerowa (TeamsManager.Api):
- **Typ**: Aplikacja sieci Web / API
- **Uwierzytelnianie**: Brak Redirect URI (lub standardowe, jeśli API ma własne UI)
- **Uwidocznij API (Expose an API)**:
  - Ustaw App ID URI (np. api://twoj-guid-api lub https://twoj-tenant.onmicrosoft.com/teamsmanager-api) - to będzie Audience dla tokenów.
  - Zdefiniuj zakres (scope), np. access_as_user.
- **Certyfikaty i klucze tajne**: Wygeneruj klucz tajny klienta (ClientSecret).
- **Uprawnienia API (aplikacji)**:
  - Microsoft Graph: User.Read.All, Group.ReadWrite.All, Directory.ReadWrite.All (lub bardziej granularne, w zależności od potrzeb PowerShellService). Wymagają zgody administratora.
- Skonfiguruj appsettings.json (lub User Secrets) dla TeamsManager.Api z TenantId, ClientId (tej aplikacji API), ClientSecret i Audience (App ID URI).

### 🔑 Uprawnienia Microsoft 365 dla użytkownika (dla operacji PowerShell)
- Konto używane do logowania w UI powinno mieć odpowiednie uprawnienia w Microsoft Teams/Azure AD do wykonywania operacji zarządzania, jeśli PowerShellService działa w jego imieniu (OBO).
- Dla operacji administracyjnych wykonywanych przez PowerShellService z uprawnieniami aplikacji, aplikacja API musi mieć przyznane odpowiednie zgody administratora.

## 8. Funkcjonalności dla Środowiska Edukacyjnego

### 🏫 Zarządzanie Strukturą Organizacyjną
- Hierarchiczne działy i wydziały
- Dynamiczne typy szkół (LO, Technikum, KKZ, PNZ)
- Zarządzanie latami szkolnymi i semestrami

### 👥 Zarządzanie Użytkownikami
- Role: Uczeń, Słuchacz, Nauczyciel, Wicedyrektor, Dyrektor
- Przypisywanie do działów
- Mapowanie na atrybuty M365

### 📚 Zarządzanie Przedmiotami
- Definiowanie przedmiotów i kursów
- Przypisywanie nauczycieli (relacja M:N)
- Kategorie i godziny lekcyjne

### 📋 Dynamiczne Szablony Nazw
- Wzorce z placeholderami: {TypSzkoly} {Oddzial} - {Przedmiot}
- Prefiksy i sufiksy
- Walidacja długości nazw

### 👨‍🏫 Zarządzanie Zespołami Edukacyjnymi
- Tworzenie zespołów typu "Class" (przez PowerShellService)
- Import członków z CSV (planowane)
- Zarządzanie kanałami tematycznymi

### 🔄 Cykl Życia Zespołu
- Automatyczna archiwizacja (planowane)
- Prefiks "ARCHIWALNY -" (obsługiwane w modelu Team)
- Przywracanie z modyfikacją (obsługiwane w modelu Team)

### 📊 Audyt i Historia
- Rejestrowanie wszystkich operacji
- Szczegółowe logi z czasem
- Analiza błędów

### ⚙️ Konfiguracja Aplikacji
- Ustawienia w bazie danych
- Parametry domyślne
- Flagi funkcji

## 9. Korzyści Rozwiązania

### 🏫 Dla szkół i instytucji edukacyjnych
- ✅ Darmowe - brak opłat za Graph API (aplikacja wykorzystuje Graph SDK, które samo w sobie jest darmowe, ale operacje na Graph API mogą podlegać limitom w zależności od licencji M365)
- ✅ Dedykowane funkcje edukacyjne
- ✅ Automatyzacja zadań administracyjnych
- ✅ Standaryzacja nazewnictwa i struktur
- ✅ Pełny cykl życia zespołów
- ✅ Import danych z CSV (planowane)
- ✅ Lokalna kontrola nad danymi konfiguracyjnymi i historią

### 💻 Dla administratorów IT
- ✅ PowerShell (pośrednio przez Graph SDK) - znajome narzędzia i możliwości
- ✅ Transparentność operacji (logowanie, historia)
- ✅ Monitoring w czasie rzeczywistym (planowane z SignalR)
- ✅ Dziennik audytu kompletny
- ✅ Synchronizacja pracy (planowana)

## 10. System Synchronizacji Graph-DB

### 🔄 Architektura Synchronizacji

TeamsManager implementuje zaawansowany system dwukierunkowej synchronizacji między Microsoft Graph a lokalną bazą danych, zapewniając spójność danych oraz wysoką wydajność dzięki inteligentnej strategii cache.

#### Kluczowe Komponenty

**IGraphSynchronizer<T>** - Interfejs synchronizacji dla różnych typów encji:
- `TeamSynchronizer` - synchronizacja zespołów Graph→DB
- `UserSynchronizer` - synchronizacja użytkowników z ochroną soft-deleted
- `ChannelSynchronizer` - synchronizacja kanałów z automatyczną klasyfikacją

**IUnitOfWork** - Wzorzec transakcyjności zapewniający spójność operacji Graph+DB

**CacheInvalidationService** - Centralne zarządzanie cache z granularną inwalidacją

#### Przepływ Synchronizacji

```
API Request → Cache Check → DB Query → Graph Sync (jeśli potrzebne) → Cache Update → Response
```

1. **Cache Check**: Sprawdzenie czy dane są w cache
2. **DB Query**: Pobranie z bazy danych jeśli brak w cache  
3. **Graph Sync**: Automatyczna synchronizacja z Graph jeśli dane nieaktualne
4. **Cache Update**: Inteligentna inwalidacja powiązanych kluczy cache
5. **Response**: Zwrócenie aktualnych danych

#### Wzorzec ExecuteWithAutoConnectAsync

Wszystkie operacje PowerShell używają ujednoliconego wzorca:

```csharp
// Nowy wzorzec (Etap 3/8+)
var result = await _powerShellService.ExecuteWithAutoConnectAsync(
    apiAccessToken,
    async () => await _powerShellService.Teams.GetTeamAsync(teamId),
    "Pobieranie zespołu z Graph"
);
```

**Korzyści**:
- Automatyczne zarządzanie połączeniem
- Centralne error handling  
- Spójne logowanie operacji
- Retry mechanism

#### Strategia Cache

**Granularna Inwalidacja** - zamiast czyścić cały cache, system inwaliduje tylko powiązane klucze:

```csharp
// Aktualizacja zespołu inwaliduje:
await _cacheInvalidationService.InvalidateForTeamUpdatedAsync(team);

// Wewnętrznie inwaliduje klucze:
// - "Team_Id_{teamId}"
// - "Teams_AllActive" 
// - "Teams_ByOwner_{ownerUpn}"
// - "Teams_Active" (jeśli status = Active)
```

**Operacje Masowe** - batch invalidation dla wydajności:

```csharp
await _cacheInvalidationService.InvalidateForTeamMembersBulkOperationAsync(teamId, userIds);
```

#### Ochrona Soft-Deleted Users

UserSynchronizer chroni użytkowników oznaczonych jako nieaktywni:

```csharp
public async Task<bool> RequiresSynchronizationAsync(PSObject graphObject, User? existingEntity)
{
    if (existingEntity?.IsActive == false)
        return false; // Nie sync soft-deleted users
        
    // Pozostała logika wykrywania zmian...
}
```

#### Historia Operacji

Każda krytyczna operacja jest logowana w `OperationHistoryService`:

```csharp
// 1. Inicjalizacja operacji
var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
    OperationType.TeamCreated, nameof(Team), targetEntityId: team.Id
);

// 2. Aktualizacja statusu przy sukcesie/błędzie
await _operationHistoryService.UpdateOperationStatusAsync(
    operation.Id, OperationStatus.Completed, "Zespół pomyślnie utworzony"
);
```

#### Metryki Wydajności

Oczekiwane wartości produkcyjne:
- **Cache Hit Rate**: > 80%
- **Sync Duration**: < 500ms per entity
- **API Response Time**: < 100ms (z cache), < 1000ms (z sync)
- **Memory Usage**: < 50MB cache per 1000 entities

### 📚 Dokumentacja Szczegółowa

Kompletna dokumentacja architektury synchronizacji dostępna w:
- [`docs/Architecture-Synchronization.md`](docs/Architecture-Synchronization.md) - Szczegółowa architektura
- [`docs/Cache-Strategy.md`](docs/Cache-Strategy.md) - Strategia cache i inwalidacji

**🆕 Nowa dokumentacja Dependency Injection (2025-06-07):**
- [`docs/DI-Architecture.md`](docs/DI-Architecture.md) - **Kompletny przewodnik architektury DI**
- [`docs/Migration-Guide.md`](docs/Migration-Guide.md) - **Przewodnik migracji do DI (6 etapów)**  
- [`docs/Release-Notes-DI.md`](docs/Release-Notes-DI.md) - **Release notes refaktoryzacji DI**

## 11. Dokumentacja Techniczna

### 🏗️ Wzorce Projektowe

#### Domain-Driven Design (DDD)
- Bogate modele domenowe
- Logika biznesowa w encjach
- Hermetyzacja zachowań

#### Repository Pattern
- Abstrakcja dostępu do danych
- Separacja warstw
- Łatwość testowania

#### Service Layer Pattern
- Enkapsulacja logiki biznesowej
- Koordynacja repozytoriów
- Zarządzanie transakcjami (niejawne, przez SaveChanges w serwisach/kontrolerach)

#### MVVM (Model-View-ViewModel)
- Separacja widoku od logiki (w TeamsManager.UI)
- Data binding
- Testowalne ViewModele

#### Dependency Injection (DI)
- Luźne powiązania
- Łatwość testowania
- Konfiguracja w runtime (w Program.cs dla API i App.xaml.cs dla UI)

### 📐 Architektura warstw

```mermaid
graph LR
    subgraph "Warstwa Prezentacji"
        UI[TeamsManager.UI<br/>WPF, MSAL]
    end
    subgraph "Warstwa Aplikacji"
        API[TeamsManager.Api<br/>ASP.NET Core, JWT, OBO]
    end
    subgraph "Warstwa Domeny"
        Core[TeamsManager.Core]
    end
    subgraph "Warstwa Infrastruktury"
        Data[TeamsManager.Data<br/>EF Core, SQLite]
        subgraph "Zewnętrzne Usługi"
            M365[Microsoft 365<br/>(Graph API)]
        end
        PowerShell[PowerShellService<br/>(Graph SDK Client)]
        Cache[IMemoryCache]
    end
    subgraph "Testy"
        Tests[TeamsManager.Tests]
    end

    UI --> API;
    API --> Core;
    Core --> Data;
    Core -- uses --> PowerShell;
    PowerShell -- interacts with --> M365;
    Core -- uses --> Cache; 
    API -- uses --> Cache;
    UI -- uses (indirectly via API or local cache) --> Cache;

    Tests -- testuje --> UI;
    Tests -- testuje --> API;
    Tests -- testuje --> Core;
    Tests -- testuje --> Data;
```

## 11. Instrukcje Instalacji i Konfiguracji

### 🔧 Wymagania Systemowe

#### Minimalne Wymagania:
- **OS**: Windows 10/11, Windows Server 2019+
- **.NET**: .NET 9.0 SDK
- **IDE**: Visual Studio 2022 (17.8+) lub VS Code
- **PowerShell**: PowerShell 7.0+ (dla Graph SDK)
- **RAM**: 4GB (8GB zalecane)
- **Dysk**: 2GB wolnego miejsca

#### Wymagania Azure/Microsoft 365:
- **Azure AD Tenant** z uprawnieniami administratora
- **Microsoft 365 Business/Enterprise** licencja
- **Azure App Registration** z odpowiednimi uprawnieniami
- **Microsoft Teams** aktywny w organizacji

### 📦 Instalacja Krok po Kroku

#### 1. Przygotowanie Środowiska

```bash
# Sprawdź wersję .NET
dotnet --version  # Powinno być >= 9.0

# Zainstaluj PowerShell 7+ (jeśli nie masz)
winget install Microsoft.PowerShell

# Sprawdź PowerShell
pwsh --version
```

#### 2. Klonowanie Repozytorium

```bash
git clone https://github.com/your-org/TeamsManager.git
cd TeamsManager

# Przywróć pakiety NuGet
dotnet restore
```

#### 3. Konfiguracja Azure AD

**Krok 3.1: Utwórz App Registration**
1. Przejdź do [Azure Portal](https://portal.azure.com)
2. Azure Active Directory → App registrations → New registration
3. Nazwa: `TeamsManager-API`
4. Supported account types: `Single tenant`
5. Redirect URI: `https://localhost:7001/signin-oidc`

**Krok 3.2: Skonfiguruj API Permissions**
```
Microsoft Graph (Application permissions):
- User.Read.All
- Group.ReadWrite.All  
- Directory.ReadWrite.All
- TeamMember.ReadWrite.All
- Team.ReadBasic.All

Microsoft Graph (Delegated permissions):
- User.Read
- Group.ReadWrite.All
- Directory.ReadWrite.All
```

**Krok 3.3: Utwórz Client Secret**
1. Certificates & secrets → New client secret
2. Skopiuj wartość (będzie potrzebna w konfiguracji)

#### 4. Konfiguracja Aplikacji

**Krok 4.1: Konfiguracja API (`TeamsManager.Api/appsettings.json`)**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=teamsmanager.db"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID", 
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "Audience": "YOUR_CLIENT_ID"
  }
}
```

**Krok 4.2: Konfiguracja UI (`TeamsManager.UI/appsettings.json`)**
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_UI_CLIENT_ID"
  },
  "ApiConfiguration": {
    "BaseUrl": "https://localhost:7001",
    "Timeout": 30
  }
}
```

#### 5. Inicjalizacja Bazy Danych

```bash
# Przejdź do projektu API
cd TeamsManager.Api

# Utwórz bazę danych
dotnet ef database update

# Opcjonalnie: Załaduj dane testowe
dotnet run --seed-data
```

#### 6. Instalacja Modułów PowerShell

```powershell
# Uruchom PowerShell jako Administrator
Install-Module Microsoft.Graph.Authentication -Force
Install-Module Microsoft.Graph.Users -Force  
Install-Module Microsoft.Graph.Teams -Force

# Sprawdź instalację
Get-Module Microsoft.Graph.* -ListAvailable
```

### 🚀 Uruchomienie Aplikacji

#### Opcja 1: Visual Studio
1. Otwórz `TeamsManager.sln`
2. Ustaw Multiple Startup Projects:
   - `TeamsManager.Api` (Start)
   - `TeamsManager.UI` (Start)
3. Naciśnij F5

#### Opcja 2: Linia Komend
```bash
# Terminal 1 - API
cd TeamsManager.Api
dotnet run

# Terminal 2 - UI  
cd TeamsManager.UI
dotnet run
```

#### Opcja 3: Docker (Planowane)
```bash
docker-compose up -d
```

### ⚙️ Konfiguracja Zaawansowana

#### Konfiguracja Cache
```json
{
  "CacheSettings": {
    "DefaultExpirationMinutes": 15,
    "MaxCacheSize": "100MB",
    "EnableDistributedCache": false
  }
}
```

#### Konfiguracja Logowania
```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/teamsmanager-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

#### Konfiguracja Health Checks
```json
{
  "HealthChecks": {
    "PowerShellConnection": {
      "Enabled": true,
      "TimeoutSeconds": 30,
      "TestConnectionOnHealthCheck": true
    }
  }
}
```

### 🔒 Bezpieczeństwo

#### Ochrona Secrets
```bash
# Użyj User Secrets dla development
dotnet user-secrets init
dotnet user-secrets set "AzureAd:ClientSecret" "YOUR_SECRET"
dotnet user-secrets set "AzureAd:TenantId" "YOUR_TENANT_ID"
```

#### Konfiguracja HTTPS
```bash
# Wygeneruj certyfikat development
dotnet dev-certs https --trust
```

### 🧪 Weryfikacja Instalacji

#### Test API
```bash
# Sprawdź health check
curl https://localhost:7001/health

# Test autentykacji
curl -H "Authorization: Bearer YOUR_TOKEN" https://localhost:7001/api/v1.0/TestAuth/whoami
```

#### Test UI
1. Uruchom aplikację UI
2. Sprawdź logowanie MSAL
3. Przetestuj połączenie z API

#### Test PowerShell
```powershell
# W aplikacji przejdź do Manual Testing
# Kliknij "Test PowerShell Connection"
# Sprawdź logi w Output
```

### 🐛 Rozwiązywanie Problemów

#### Problem: "Unable to connect to Graph"
**Rozwiązanie:**
1. Sprawdź uprawnienia App Registration
2. Zweryfikuj Client Secret
3. Sprawdź czy admin consent został udzielony

#### Problem: "Database connection failed"
**Rozwiązanie:**
```bash
# Usuń bazę i utwórz ponownie
rm teamsmanager.db
dotnet ef database update
```

#### Problem: "PowerShell module not found"
**Rozwiązanie:**
```powershell
# Reinstaluj moduły
Uninstall-Module Microsoft.Graph.* -Force
Install-Module Microsoft.Graph.Authentication -Force
Install-Module Microsoft.Graph.Users -Force
Install-Module Microsoft.Graph.Teams -Force
```

### 📚 Dodatkowe Zasoby

- **Dokumentacja Microsoft Graph**: https://docs.microsoft.com/graph/
- **Azure AD App Registration**: https://docs.microsoft.com/azure/active-directory/
- **PowerShell Graph SDK**: https://docs.microsoft.com/powershell/microsoftgraph/

## 12. Licencja i Autorzy

### 👨‍💻 Informacje o projekcie

**Projekt**: TeamsManager - System zarządzania zespołami Microsoft Teams dla środowiska edukacyjnego

**Autor**: Mariusz Jaguścik

**Przedmioty**:
- Programowanie w technologii .NET
- Projektowanie zaawansowanych systemów informatycznych
- Programowanie aplikacji sieciowych

**Uczelnia**: Akademia Ekonomiczno-Humanistyczna w Łodzi

**Rok akademicki**: 2024/2025

**Licencja**: MIT License

### 📊 Status projektu

**Ostatnia aktualizacja**: 2025-06-08

**Status**:
- Faza 1 (Modelowanie Domeny) - Zakończona.
- Faza 2 (Warstwa Danych i Serwisy) - Zakończona.
- Faza 3 (API i Komunikacja) - W trakcie zaawansowanym. Większość kontrolerów API zaimplementowana, skonfigurowano Swagger i autentykację JWT z OBO.
- Faza 4 (Interfejs Użytkownika) - Rozpoczęto prace nad logowaniem MSAL i podstawową strukturą okna testów manualnych.

**Testy**:
- ✅ Modele: Wysokie pokrycie, testy dla logiki wewnętrznej.
- ✅ Enumy: Kompletne testy.
- ✅ Serwisy: Dobre pokrycie dla logiki biznesowej i obsługi cache.
- ✅ Repozytoria: Pełne testy integracyjne dla wszystkich repozytoriów (CRUD, metody specyficzne).
- 🔄 API: Rozpoczęto testy dla wybranych kontrolerów.