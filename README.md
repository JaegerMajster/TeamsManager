# TeamsManager - Dokumentacja Projektu

## 1. Informacje Ogólne

**TeamsManager** to lokalnie uruchamiana aplikacja desktopowa (WPF) dla systemu Windows, zaprojektowana jako zaawansowana nakładka na skrypty PowerShell. Jej głównym celem jest usprawnienie i automatyzacja zarządzania zespołami, użytkownikami oraz powiązanymi zasobami w środowisku Microsoft Teams. Aplikacja eliminuje potrzebę bezpośredniego korzystania z licencjonowanego Microsoft Graph API dla wielu typowych operacji administracyjnych, oferując jednocześnie bogatszą funkcjonalność i bardziej przyjazny interfejs użytkownika niż standardowe narzędzia.

**Specjalizacja:** Aplikacja jest szczególnie dedykowana do zarządzania złożonymi środowiskami edukacyjnymi (np. szkoły, uczelnie). Oferuje zaawansowane funkcje, takie jak dynamiczne szablony nazw zespołów, zarządzanie cyklem życia zespołów (archiwizacja, przywracanie z modyfikacją nazw), obsługa hierarchii działów, typów szkół, lat szkolnych oraz szczegółowe powiązania nauczycieli z przedmiotami i typami szkół.

**Projekt realizowany jest jako praca zaliczeniowa na studiach informatycznych, obejmująca zagadnienia z przedmiotów takich jak "Programowanie aplikacji sieciowych", "Projektowanie zaawansowanych systemów informatycznych" oraz "Programowanie w technologii .NET".**

## 2. Architektura Aplikacji

### 2.1. Struktura Rozwiązania

Rozwiązanie `TeamsManager` zostało zaprojektowane zgodnie z zasadami czystej architektury (Clean Architecture), z wyraźnym podziałem na warstwy i jednoznacznie zdefiniowane odpowiedzialności poszczególnych komponentów. Składa się z pięciu głównych projektów:

```mermaid
graph TD;
    UI[TeamsManager.UI<br/>(Aplikacja WPF<br/>Interfejs Użytkownika<br/>Wzorzec MVVM, MaterialDesign)] --> API[TeamsManager.Api<br/>(Lokalne REST API<br/>ASP.NET Core, WebSockets - SignalR)];
    API --> Core[TeamsManager.Core<br/>(Logika Biznesowa<br/>Modele Domenowe, Serwisy Aplikacyjne<br/>Integracja z PowerShell)];
    API --> Data[TeamsManager.Data<br/>(Dostęp do Danych<br/>Entity Framework Core, SQLite<br/>Repozytoria)];
    Core --> Data;
    Tests[TeamsManager.Tests<br/>(Testy Jednostkowe i Integracyjne<br/>xUnit, FluentAssertions, Moq)] -.-> Core;
    Tests -.-> Data;
    Tests -.-> API;

    style UI fill:#cce5ff,stroke:#333,stroke-width:2px;
    style API fill:#e6ccff,stroke:#333,stroke-width:2px;
    style Core fill:#ccffcc,stroke:#333,stroke-width:2px;
    style Data fill:#ffe0cc,stroke:#333,stroke-width:2px;
    style Tests fill:#ffcccc,stroke:#333,stroke-width:2px;
```

**TeamsManager.Core**: Centralna biblioteka klas .NET stanowiąca serce aplikacji. Zawiera całą logikę biznesową, szczegółowe modele domenowe (encje, enumy, bogate właściwości obliczane i metody domenowe), serwisy aplikacyjne oraz interfejsy definiujące kontrakty dla innych warstw. Kluczowym elementem jest tu PowerShellService, odpowiedzialny za bezpośrednią interakcję ze skryptami PowerShell i Microsoft Teams.

**TeamsManager.Data**: Warstwa infrastruktury odpowiedzialna za trwałość danych i komunikację z lokalną bazą danych SQLite. Implementuje TeamsManagerDbContext (kontekst Entity Framework Core), szczegółowe konfiguracje mapowania encji przy użyciu Fluent API oraz implementacje wzorca Repository dla abstrakcji dostępu do danych.

**TeamsManager.Api**: Lokalny serwer REST API, zbudowany w technologii ASP.NET Core. Stanowi bramę dla interfejsu użytkownika, udostępniając endpointy HTTP dla wszystkich operacji biznesowych. Odpowiada również za implementację komunikacji WebSocket (za pomocą SignalR) na potrzeby powiadomień w czasie rzeczywistym.

**TeamsManager.UI**: Aplikacja kliencka typu desktop, zrealizowana w technologii WPF (Windows Presentation Foundation). Stanowi graficzny interfejs użytkownika (GUI), umożliwiając interakcję z systemem. Została zaprojektowana zgodnie ze wzorcem architektonicznym MVVM (Model-View-ViewModel) i wykorzystuje bibliotekę MaterialDesignInXAML do zapewnienia nowoczesnego wyglądu (w tym ciemnego motywu). Komunikuje się z TeamsManager.Api.

**TeamsManager.Tests**: Projekt zawierający kompleksowy zestaw testów jednostkowych i integracyjnych dla wszystkich pozostałych komponentów aplikacji. Celem jest zapewnienie wysokiej jakości, niezawodności i łatwości utrzymania kodu.

### 2.2. Elementy Sieciowe i Komunikacja (Planowane)

Aplikacja będzie wykorzystywać lokalną komunikację sieciową między swoimi komponentami:

**REST API**: Interfejs użytkownika (TeamsManager.UI) będzie komunikował się z logiką aplikacji udostępnianą przez TeamsManager.Api za pomocą standardowych żądań HTTP/HTTPS (GET, POST, PUT, DELETE) do zarządzania zespołami, użytkownikami, szablonami itp. Przykładowe planowane endpointy:

- `/api/teams` (GET, POST)
- `/api/teams/{id}` (GET, PUT, DELETE)
- `/api/teams/{id}/archive` (POST)
- `/api/teams/{id}/restore` (POST)
- `/api/teams/{id}/members` (GET, POST)
- `/api/users` (GET, POST)
- `/api/users/importcsv` (POST)
- `/api/schooltypes` (GET, POST, PUT, DELETE)
- `/api/schoolyears` (GET, POST, PUT, DELETE)
- `/api/subjects` (GET, POST, PUT, DELETE)
- `/api/teamtemplates` (GET, POST, PUT, DELETE)
- `/api/settings` (GET, PUT)

**WebSockets (SignalR)**: TeamsManager.Api wykorzysta SignalR do wysyłania powiadomień w czasie rzeczywistym do klienta TeamsManager.UI o statusie długotrwałych operacji (np. masowe tworzenie zespołów, import użytkowników), zakończonych zadaniach lub innych ważnych zdarzeniach systemowych, co zapewni dynamiczne odświeżanie interfejsu użytkownika.

**Synchronizacja między instancjami (Rozważane na przyszłość)**: W przyszłości może zostać dodany mechanizm komunikacji (np. oparty na TCP/IP lub kolejkach komunikatów) umożliwiający synchronizację stanu i operacji między wieloma instancjami aplikacji TeamsManager uruchomionymi na różnych stanowiskach.

## 3. Model Danych Domeny

Model danych został starannie zaprojektowany zgodnie z zasadami Domain-Driven Design (DDD), aby precyzyjnie odzwierciedlić złożoność i specyfikę zarządzania środowiskiem edukacyjnym w Microsoft Teams. Encje są bogate w logikę biznesową, właściwości obliczane i metody pomocnicze.

### 3.1. Diagram ERD (Entity Relationship Diagram)

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
        string ExternalId NULL "ID w systemie zewnętrznym"
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
    User ||--|{ SchoolType : nadzoruje_typy_szkol (M:N przez UserSchoolTypeSupervision - niejawna tabela łącząca)

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
        string ExternalId NULL
        string CourseCode NULL
        int TotalHours NULL
        string Level NULL
        string Language
        string Tags NULL
        string Notes NULL
        bool IsVisible
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
    OperationHistory ||--o{ OperationHistory : jest_rodzicem_dla_podoperacji (self-referencing)

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

### 3.2. Opis Głównych Encji i Enumów

**BaseEntity**: Abstrakcyjna klasa bazowa dostarczająca wspólne pola audytu (Id, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy) oraz flagę IsActive dla mechanizmu "soft delete".

**User**: Reprezentuje użytkownika systemu (ucznia, nauczyciela, administratora). Przechowuje dane osobowe, rolę systemową, przypisanie do działu oraz informacje o członkostwach w zespołach i przypisaniach do typów szkół/przedmiotów.

**Department**: Modeluje dział, wydział lub inną jednostkę organizacyjną, z możliwością tworzenia struktur hierarchicznych.

**SchoolType**: Definiuje typ szkoły lub jednostki edukacyjnej (np. LO, Technikum, KKZ), umożliwiając ich dynamiczne zarządzanie i kategoryzację.

**SchoolYear**: Reprezentuje rok szkolny/akademicki z datami rozpoczęcia, zakończenia oraz opcjonalnymi definicjami semestrów.

**Subject**: Definiuje przedmiot nauczania lub kurs, z możliwością przypisania go do domyślnego typu szkoły.

**Team**: Główna encja reprezentująca zespół Microsoft Teams, z jego statusem, metadanymi, powiązaniami z szablonem, typem szkoły, rokiem szkolnym, członkami i kanałami.

**TeamMember**: Encja pośrednicząca w relacji wiele-do-wielu między User a Team, przechowująca szczegóły członkostwa (rola, data dodania, status zatwierdzenia itp.).

**Channel**: Modeluje kanał komunikacyjny wewnątrz zespołu Teams, z jego statusem (Aktywny/Zarchiwizowany), typem i metadanymi aktywności.

**TeamTemplate**: Umożliwia definiowanie szablonów nazw dla nowo tworzonych zespołów, wspierając placeholdery, prefiksy, sufiksy i inne opcje personalizacji.

**UserSchoolType**: Encja pośrednicząca w relacji wiele-do-wielu między User (nauczycielami) a SchoolType, określająca szczegóły przypisania (np. data, procent etatu).

**UserSubject**: Encja pośrednicząca w relacji wiele-do-wielu między User (nauczycielami) a Subject, określająca, który nauczyciel naucza jakiego przedmiotu.

**OperationHistory**: Rejestruje wszystkie istotne operacje wykonywane w systemie, służąc jako dziennik audytu, monitorowania postępu operacji wsadowych i diagnostyki błędów.

**ApplicationSetting**: Pozwala na dynamiczną konfigurację aplikacji poprzez przechowywanie różnych ustawień (np. kluczy API, wartości domyślnych, flag funkcji) w bazie danych.

**Kluczowe Enumy**:

- **UserRole**: (Uczen, Sluchacz, Nauczyciel, Wicedyrektor, Dyrektor) - Definiuje role użytkowników w systemie edukacyjnym.
- **TeamMemberRole**: (Member, Owner) - Określa rolę użytkownika w konkretnym zespole Microsoft Teams.
- **TeamStatus**: (Active, Archived) - Definiuje status zespołu.
- **ChannelStatus**: (Active, Archived) - Definiuje status kanału.
- **OperationType**: (np. TeamCreated, MemberAdded, BulkUserImport) - Kategoryzuje typy operacji logowanych w OperationHistory.
- **OperationStatus**: (Pending, InProgress, Completed, Failed, Cancelled, PartialSuccess) - Określa status wykonywanej operacji.
- **SettingType**: (String, Integer, Boolean, Json, DateTime, Decimal) - Definiuje typ danych przechowywanych w ApplicationSetting.Value.

### 3.3. Kluczowe Relacje Między Encjami

| Encja Nadrzędna | Typ Relacji | Encja Podrzędna/Powiązana | Tabela Pośrednicząca (dla M:N) | Kluczowe Właściwości Nawigacyjne | Opis |
|-----------------|-------------|---------------------------|--------------------------------|----------------------------------|------|
| Department | 1 : N | User | - | Department.Users, User.Department | Jeden dział może mieć wielu użytkowników; użytkownik należy do jednego działu. |
| Department | 1 : N | Department | - | Department.SubDepartments, Department.ParentDepartment | Jeden dział może mieć wiele poddziałów; poddział ma jeden dział nadrzędny (hierarchia). |
| User | 1 : N | TeamMember | - | User.TeamMemberships, TeamMember.User | Jeden użytkownik może mieć wiele członkostw w różnych zespołach. |
| Team | 1 : N | TeamMember | - | Team.Members, TeamMember.Team | Jeden zespół może mieć wielu członków. |
| Team | 1 : N | Channel | - | Team.Channels, Channel.Team | Jeden zespół może mieć wiele kanałów. |
| SchoolType | 1 : N | Team | - | SchoolType.Teams, Team.SchoolType | Jeden typ szkoły może być powiązany z wieloma zespołami. |
| SchoolYear | 1 : N | Team | - | SchoolYear.Teams, Team.SchoolYear | Jeden rok szkolny może obejmować wiele zespołów. |
| TeamTemplate | 1 : N | Team | - | TeamTemplate.Teams, Team.Template | Jeden szablon może być użyty do stworzenia wielu zespołów. |
| SchoolType | 1 : N | TeamTemplate | - | SchoolType.Templates, TeamTemplate.SchoolType | Jeden typ szkoły może mieć wiele dedykowanych szablonów. |
| User | M : N | SchoolType | UserSchoolType | User.SchoolTypeAssignments, SchoolType.TeacherAssignments | Nauczyciel może być przypisany do wielu typów szkół; typ szkoły może mieć wielu przypisanych nauczycieli. |
| User | M : N | SchoolType | UserSchoolTypeSupervision (niejawna) | User.SupervisedSchoolTypes, SchoolType.SupervisingViceDirectors | Wicedyrektor może nadzorować wiele typów szkół; typ szkoły może być nadzorowany przez wielu wicedyrektorów. |
| User | M : N | Subject | UserSubject | User.TaughtSubjects, Subject.TeacherAssignments | Nauczyciel może nauczać wielu przedmiotów; przedmiot może być nauczany przez wielu nauczycieli. |
| OperationHistory | 1 : N | OperationHistory | - | OperationHistory.SubOperations (planowane), OperationHistory.ParentOperation | Operacja może mieć wiele podoperacji. |
| SchoolType | 1 : N | Subject | - | (brak bezpośredniej kolekcji w SchoolType), Subject.DefaultSchoolType | Jeden typ szkoły może być domyślnym dla wielu przedmiotów. |

### 3.4. Logika Domenowa w Modelach

Modele zostały zaprojektowane jako "bogate modele domenowe" (Rich Domain Models), co oznacza, że zawierają nie tylko dane, ale również logikę biznesową bezpośrednio w encjach. Przykłady:

- **Właściwości obliczane**: (np. User.FullName, Team.MemberCount, SchoolYear.CompletionPercentage, Department.FullPath, Channel.StatusDescription, TeamTemplate.Placeholders, OperationHistory.ProgressPercentage, UserSchoolType.AssignmentDescription).
- **Metody pomocnicze modyfikujące stan encji**: (np. Team.Archive(), Team.Restore(), OperationHistory.MarkAsCompleted(), ApplicationSetting.IsValid(), Channel.Archive(), BaseEntity.MarkAsModified()).
- **Logika walidacji i transformacji danych**: (np. TeamTemplate.GenerateTeamName(), TeamTemplate.ValidateTemplate()).

## 4. Wykorzystane Technologie

### 4.1. Stos Technologiczny

- **Platforma**: .NET 8.0
- **Język programowania**: C# 12
- **Interfejs użytkownika (UI)**: WPF (Windows Presentation Foundation)
- **Stylizacja**: MaterialDesignInXAML
- **Wzorzec architektoniczny**: MVVM (Model-View-ViewModel)
- **Logika biznesowa i interakcja z PowerShell**: Biblioteka klas .NET
- **PowerShell**: System.Management.Automation (SDK)
- **API**: ASP.NET Core Web API (dla lokalnego serwera)
- **Komunikacja w czasie rzeczywistym**: SignalR (WebSockets)
- **Dostęp do danych (ORM)**: Entity Framework Core 8
- **Baza danych**: SQLite (lokalna, plikowa)
- **Testowanie**:
  - Framework: xUnit
  - Asercje: FluentAssertions
  - Mockowanie: Moq (planowane dla testów serwisów)
- **Kontrola wersji**: Git, GitHub

### 4.2. Kluczowe Pakiety NuGet

**TeamsManager.Core**
- System.Management.Automation: Integracja z PowerShell.
- Microsoft.Extensions.DependencyInjection.Abstractions: Podstawa dla wstrzykiwania zależności.
- Microsoft.Extensions.Logging.Abstractions: Podstawa dla systemu logowania.

**TeamsManager.Data**
- Microsoft.EntityFrameworkCore: Główny pakiet Entity Framework Core.
- Microsoft.EntityFrameworkCore.Sqlite: Dostawca bazy danych SQLite dla EF Core.
- Microsoft.EntityFrameworkCore.Tools: Narzędzia wiersza poleceń dla EF Core (np. do migracji).
- Microsoft.EntityFrameworkCore.Design: Narzędzia czasu projektowania dla EF Core.

**TeamsManager.Api**
- Microsoft.AspNetCore.SignalR: Implementacja WebSockets w ASP.NET Core.
- Swashbuckle.AspNetCore: Automatyczne generowanie dokumentacji API (Swagger/OpenAPI).
- Microsoft.EntityFrameworkCore.Sqlite: (pośrednio przez TeamsManager.Data lub do konfiguracji DbContext w DI).

**TeamsManager.UI**
- MaterialDesignThemes: Biblioteka kontrolek i stylów Material Design dla WPF.
- Microsoft.AspNetCore.SignalR.Client: Klient SignalR do komunikacji WebSocket z API.
- System.Net.Http.Json: Ułatwienia do pracy z JSON przez HTTP.
- Microsoft.Extensions.DependencyInjection: Implementacja wstrzykiwania zależności w aplikacjach WPF.

**TeamsManager.Tests**
- xUnit: Popularny framework do testów jednostkowych.
- FluentAssertions: Biblioteka do tworzenia bardziej czytelnych i ekspresyjnych asercji w testach.
- Moq: Biblioteka do tworzenia obiektów mock (zaślepek) na potrzeby testów jednostkowych.
- Microsoft.EntityFrameworkCore.InMemory: Dostawca bazy danych w pamięci dla EF Core, użyteczny do szybkich testów integracyjnych warstwy danych.

## 5. Strategia Testowania

Projekt kładzie duży nacisk na jakość kodu poprzez rozbudowaną strategię testowania:

**Testy Jednostkowe (Unit Tests)**: Dla wszystkich klas modeli (weryfikacja wartości domyślnych, logiki właściwości, metod pomocniczych), wszystkich enumów oraz kluczowych komponentów logiki biznesowej (serwisów) w izolacji.

**Testy Integracyjne (Integration Tests)**: Sprawdzają poprawność współpracy między różnymi modułami, np. interakcję modeli z DbContext i bazą danych (przy użyciu InMemory lub TestContainers), działanie relacji, współpracę serwisów z repozytoriami. TeamIntegrationTests.cs jest przykładem.

**Pokrycie Kodu**: Dążenie do jak najwyższego pokrycia kodu testami. Obecnie modele domenowe i ich wewnętrzna logika są w pełni pokryte.

**Narzędzia**: xUnit jako framework testowy, FluentAssertions dla czytelnych i ekspresyjnych asercji, Moq do mockowania zależności w testach jednostkowych serwisów.

**Aktualny status testów**: Wszystkie zaimplementowane testy jednostkowe i integracyjne dla modeli danych (ponad 100 metody testowe, w tym wiele parametryzowanych) przechodzą pomyślnie (100% sukcesu).

## 6. Aktualny Status Implementacji i Plan Dalszych Prac

**Data aktualizacji**: 2025-05-29

### ✅ Ukończono

**Faza 1**: Modelowanie Domeny i Podstawy

- Zdefiniowanie i implementacja kompletnego, rozbudowanego modelu domenowego (13 encji, 7 enumów) z uwzględnieniem zasad Domain-Driven Design.
- Pełna konfiguracja TeamsManagerDbContext dla wszystkich encji i ich relacji przy użyciu Entity Framework Core Fluent API.
- Implementacja podstawowej wersji PowerShellService do interakcji z PowerShell.
- Stworzenie kompleksowego zestawu testów jednostkowych dla wszystkich klas modeli i enumów.
- Implementacja kluczowych testów integracyjnych weryfikujących współpracę między modelami.
- Ustalenie szczegółowego planu dalszych prac, strategii testowania i zasad dokumentacji.
- Wszystkie testy dla modeli przechodzą pomyślnie.

### 🔄 W Trakcie Realizacji / Następne Kroki

**Faza 2**: Warstwa Danych i Pierwsze Operacje

- Utworzenie pierwszej migracji bazy danych na podstawie finalnego modelu i DbContext.
- Implementacja mechanizmu ICurrentUserService i jego wstrzykiwania do DbContext w celu poprawnego zapisu pól audytu CreatedBy i ModifiedBy (wymaga konfiguracji Dependency Injection w projektach startowych).
- Implementacja wzorca Repository dla wszystkich encji w TeamsManager.Data.
- Stworzenie pierwszych Serwisów Aplikacyjnych w TeamsManager.Core (np. TeamService, UserService) implementujących podstawowe operacje CRUD i logikę biznesową, wykorzystujących repozytoria oraz PowerShellService.
- Rozbudowa PowerShellService o kolejne metody do zarządzania Teams.
- Testy jednostkowe dla repozytoriów i serwisów.

**Faza 3**: API i Komunikacja

- Implementacja kontrolerów w TeamsManager.Api udostępniających funkcjonalność serwisów poprzez endpointy REST.
- Konfiguracja Swagger/OpenAPI dla dokumentacji API.
- Implementacja SignalR Hub w TeamsManager.Api dla powiadomień w czasie rzeczywistym.
- Testy integracyjne dla API.

**Faza 4**: Interfejs Użytkownika (WPF)

- Budowa głównych okien i nawigacji w aplikacji TeamsManager.UI zgodnie z MVVM.
- Implementacja widoków i ViewModeli dla zarządzania kluczowymi encjami (Zespoły, Użytkownicy, Działy, Typy Szkół, Szablony).
- Integracja UI z TeamsManager.Api (wywołania REST, obsługa odpowiedzi).
- Implementacja klienta SignalR w UI do odbierania powiadomień.
- Implementacja mechanizmu logowania użytkownika i przekazywania jego tożsamości do ICurrentUserService.

**Faza 5**: Funkcje Zaawansowane i Usprawnienia

- Implementacja logiki dla SchoolYear.IsCurrent (w serwisie).
- Optymalizacje w PowerShellService (ładowanie modułów).
- Implementacja funkcjonalności "Przedmioty i Nauczyciele" (serwisy, UI).
- Mechanizm cache'owania dla ApplicationSetting (zgodnie z "Podejściem 1").
- Obsługa operacji wsadowych (np. import CSV użytkowników, masowe tworzenie zespołów).
- Rozbudowa systemu szablonów TeamTemplate.

**Faza 6**: Finalizacja (do 2025-06-08)

- Kompleksowe testy E2E (End-to-End).
- Poprawki błędów i optymalizacja wydajności.
- Finalizacja dokumentacji użytkownika i technicznej.
- Przygotowanie do prezentacji i obrony projektu.

### Harmonogram (Główne Etapy - Orientacyjny)

```mermaid
gantt
    dateFormat  YYYY-MM-DD
    title Harmonogram Projektu TeamsManager (Stan na 2025-05-29)

    section Faza 1: Modelowanie (Zakończona)
    Definicja i Implementacja Modeli Domenowych :done, des1, 2025-05-27, 2d
    Testy Jednostkowe i Integracyjne Modeli   :done, des2, after des1, 2d
    
    section Faza 2: Warstwa Danych i Podst. Logika (Rozpoczęta)
    Migracje Bazy Danych                     :crit, active, db_mig, 2025-05-29, 1d
    ICurrentUserService i DI                 :crit, di_ius, after db_mig, 1d
    Repozytoria                              :repo, after di_ius, 1d
    Podstawowe Serwisy Aplikacyjne (CRUD)    :services_crud, after repo, 2d
    Rozbudowa PowerShellService              :ps_enh, after services_crud, 1d
    Testy dla Repozytoriów i Serwisów        :tests_serv, after services_crud, 1d

    section Faza 3: API i Komunikacja
    Kontrolery API                           :api_ctrl, after tests_serv, 2d
    SignalR Hub i Klient (podstawy)          :signalr, after api_ctrl, 1d
    Testy API                                :tests_api, after signalr, 1d

    section Faza 4: Interfejs Użytkownika (WPF)
    Główne Okna i Nawigacja                  :ui_main, after api_ctrl, 1d 
    Widoki i ViewModel dla Kluczowych Encji  :ui_views, after ui_main, 3d
    Integracja UI z API                      :ui_api_int, during ui_views, 2d
    Logowanie Użytkownika w UI               :ui_login, after ui_main, 1d

    section Faza 5: Funkcje Zaawansowane
    Logika SchoolYear.IsCurrent              :adv_sy_curr, after services_crud, 1d
    Optymalizacje PowerShellService          :adv_ps_opt, after ps_enh, 1d
    Funkcjonalność Przedmioty i Nauczyciele  :adv_subj, after services_crud, 2d
    Cache dla ApplicationSetting             :adv_cache, after services_crud, 1d
    Operacje Wsadowe (CSV, Bulk)             :adv_bulk, after ui_api_int, 2d

    section Faza 6: Finalizacja (do 2025-06-08)
    Testy E2E i UAT                          :final_e2e, after adv_bulk, 1d
    Poprawki i Optymalizacja                 :final_fix, during final_e2e, 1d
    Dokumentacja Końcowa                     :crit, final_doc, 2025-06-07, 1d
    Przygotowanie do Prezentacji             :final_prep, 2025-06-08, 1d
```

## 7. Instrukcje Uruchomienia i Wymagania Wstępne

*(Sekcja do uzupełnienia, gdy aplikacja będzie w pełni uruchamialna – na razie pozostaje jak w pierwotnej wersji, zaktualizujemy ją później)*

### Środowisko deweloperskie

- Windows 10/11
- Visual Studio 2022 (Community lub wyższa)
- .NET 8.0 SDK
- Git do kontroli wersji

### Moduły PowerShell

```powershell
# Instalacja wymaganych modułów (jeśli jeszcze nie zainstalowane)
# Install-Module -Name MicrosoftTeams -Force -AllowClobber
# Install-Module -Name ExchangeOnlineManagement -Force -AllowClobber
```

**Uwaga**: Upewnij się, że używasz kompatybilnych wersji modułów PowerShell z Twoim systemem i uprawnieniami.

### Uprawnienia Microsoft 365

Konto z uprawnieniami do zarządzania zespołami Microsoft Teams (np. Administrator Teams, Właściciel zespołu dla niektórych operacji).

## 8. Funkcjonalności dla Środowiska Edukacyjnego

- **Zarządzanie Strukturą Organizacyjną**: Definiowanie działów (z hierarchią), typów szkół (np. LO, Technikum, KKZ, PNZ – dynamicznie konfigurowalne), lat szkolnych.
- **Zarządzanie Użytkownikami**: Tworzenie kont dla uczniów, słuchaczy, nauczycieli, wicedyrektorów, dyrektorów z przypisaniem do działów i ról systemowych. Mapowanie na atrybuty M365 (planowane).
- **Zarządzanie Przedmiotami**: Możliwość definiowania przedmiotów i przypisywania do nich nauczycieli (relacja M:N).
- **Dynamiczne Szablony Nazw Zespołów**: Tworzenie zaawansowanych szablonów (np. {TypSzkoly} {Oddzial} - {Przedmiot} - {Nauczyciel}) do automatycznego i spójnego nazywania zespołów, z możliwością przypisania szablonów do konkretnych typów szkół oraz stosowania prefiksów, sufiksów i walidacji.
- **Zarządzanie Zespołami Edukacyjnymi**: Tworzenie zespołów (w tym typu "Class" dla funkcji edukacyjnych) na podstawie szablonów lub ręcznie, przypisywanie do typu szkoły i roku szkolnego, zarządzanie członkami (dodawanie z CSV, usuwanie, zmiana ról), zarządzanie kanałami.
- **Cykl Życia Zespołu**: Archiwizacja zespołów po zakończeniu roku szkolnego/kursu (z automatyczną zmianą nazwy przez dodanie prefiksu "ARCHIWALNY - ") oraz ich przywracanie.
- **Audyt i Historia**: Śledzenie wszystkich kluczowych operacji wykonywanych w systemie (tworzenie, modyfikacja, usuwanie, operacje wsadowe) z możliwością przeglądania szczegółów.
- **Konfiguracja Aplikacji**: Możliwość dostosowania parametrów działania aplikacji (np. domyślne wartości, limity, flagi funkcji) poprzez ustawienia przechowywane w bazie.

## 9. Korzyści Rozwiązania

### Dla szkół i instytucji edukacyjnych

✅ Darmowe (w kontekście braku opłat za Graph API dla wielu operacji) i oparte na istniejącej infrastrukturze Microsoft 365.

✅ Dedykowane funkcje dla specyficznych potrzeb środowiska edukacyjnego, z możliwością dostosowania.

✅ Automatyzacja czasochłonnych zadań administracyjnych związanych z zarządzaniem zespołami i użytkownikami.

✅ Standaryzacja dzięki zaawansowanym szablonom nazw i predefiniowanym strukturom.

✅ Pełne zarządzanie cyklem życia zespołów i użytkowników.

✅ Możliwość importu danych (np. użytkowników z plików CSV).

✅ Lokalne działanie z pełną kontrolą nad danymi konfiguracyjnymi i historią operacji przechowywanymi w lokalnej bazie SQLite.

### Dla administratorów IT

✅ Wykorzystanie istniejących uprawnień i skryptów PowerShell, co ułatwia wdrożenie.

✅ Pełna kontrola i transparentność wykonywanych operacji dzięki szczegółowemu logowaniu.

✅ Możliwość monitorowania działań w czasie rzeczywistym (planowane przez WebSockets).

✅ Kompletny dziennik historii operacji dla celów audytu, diagnostyki i raportowania.

✅ Potencjalna synchronizacja pracy między wieloma administratorami (planowane).

## 10. Dokumentacja Techniczna

### Wzorce Projektowe

**Domain-Driven Design (DDD)**: Modele encji są bogate w logikę biznesową, odzwierciedlają rzeczywiste koncepty domeny i hermetyzują swoje zachowania.

**Repository Pattern**: Abstrakcja nad warstwą dostępu do danych, zapewniająca separację logiki biznesowej od technologii utrwalania danych (planowane dla TeamsManager.Data).

**MVVM (Model-View-ViewModel)**: Wzorzec architektoniczny dla aplikacji WPF (TeamsManager.UI), zapewniający separację logiki prezentacji od widoku.

**Dependency Injection (DI)**: Szeroko stosowane we wszystkich warstwach do zarządzania zależnościami, promowania luźnych powiązań i ułatwiania testowania.

**Test-Oriented Development**: Nacisk na tworzenie testów jednostkowych i integracyjnych na każdym etapie rozwoju w celu zapewnienia jakości i regresji.

### Architektura (koncepcja warstw)

```mermaid
graph LR
    subgraph Warstwa Prezentacji
        UI[TeamsManager.UI (WPF)]
    end
    subgraph Warstwa Aplikacji / Brama
        API[TeamsManager.Api (ASP.NET Core)]
    end
    subgraph Warstwa Domeny i Logiki Aplikacji
        Core[TeamsManager.Core]
    end
    subgraph Warstwa Infrastruktury
        Data[TeamsManager.Data (EF Core, SQLite)]
        PowerShell[PowerShell Engine]
    end
    subgraph Testy
        Tests[TeamsManager.Tests]
    end

    UI --> API;
    API --> Core;
    Core --> Data;
    Core --> PowerShell;
    Tests -- testuje --> UI;
    Tests -- testuje --> API;
    Tests -- testuje --> Core;
    Tests -- testuje --> Data;
```

## 11. Licencja i Autorzy

### **Projekt**: TeamsManager - System zarządzania zespołami Microsoft Teams dla środowiska edukacyjnego

- **Autor**: Mariusz Jaguścik
- **Przedmiot**: Programowanie w technologii .NET, Projektowanie zaawansowanych systemów informatycznych, Programowanie aplikacji sieciowych
- **Uczelnia**: Akademia Ekonomiczno-Humanistyczna
- **Rok akademicki**: 2024/2025

**Licencja**: MIT License

**Ostatnia aktualizacja**: 2025-05-29  
**Status**: Zakończono fazę modelowania danych oraz implementację i weryfikację testów jednostkowych i integracyjnych dla modeli. Rozpoczynanie Fazy 2: Warstwa Danych.  
**Testy**: Wszystkie testy modeli (ponad 100 metod testowych) przechodzą pomyślnie (100% sukcesu). ✅