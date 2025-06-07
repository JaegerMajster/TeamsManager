# 📁 Struktura Projektu TeamsManager

**📅 Ostatnia aktualizacja:** 07 czerwca 2025, 08:46  
**🔢 Statystyki:** 255+ plików źródłowych (CS/XAML), ~63,200 linii kodu  
**⚡ Technologia:** .NET 9.0, Material Design 3.0, WPF + ASP.NET Core API + Application Layer  

> **Status:** Projekt gotowy do produkcji - wszystkie 1113 testów przechodzą

---

## 🏗️ Aktualna Struktura Projektu TeamsManager

### 📋 **Pliki główne**
```
.gitignore
README.md
TeamsManager.sln
DataImportOrchestrator_README.md     ← NOWY: Dokumentacja orkiestratora importu
```

### 📚 **Dokumentacja (`docs/`)**
```
docs/
├── 📊 schematy/
│   ├── architektura.md
│   └── modelDanych.md
├── 📄 Pliki aktualne (12 plików):
│   ├── analizaStabilnosciNet9.md    - Analiza migracji na .NET 9.0
│   ├── analizaTokenuBearer.md       - Dokumentacja Bearer Token
│   ├── audytArchitektruySync.md     - Audyt synchronizacji architektury
│   ├── powerShellService.md         - Zarządzanie PowerShell Services
│   ├── strategiaCache.md            - Strategia cache'owania
│   ├── strukturaProjektu.md         - Ten plik
│   ├── styleUI.md                   - Przewodnik stylów UI
│   ├── synchronizacja.md            - Synchronizacja architektury
│   ├── TodoSystemKolejkowy.md       - System kolejkowania operacji
│   ├── tokenPlany.md                - Plany rozwoju tokenów
│   ├── tokenRefactor.md             - Refaktoryzacja token managera
│   └── README.md                    - Główna dokumentacja
```

### 🌐 **API (`TeamsManager.Api/`)**
```
TeamsManager.Api/
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── TeamsManager.Api.csproj
├── TeamsManager.Api.http
├── teamsmanager.db
├── Configuration/
│   └── ApiAuthConfig.cs
├── Controllers/ (17 kontrolerów)
│   ├── ApplicationSettingsController.cs
│   ├── ChannelsController.cs
│   ├── DataImportController.cs           ← NOWY: Orkiestrator importu danych CSV/Excel
│   ├── DepartmentsController.cs
│   ├── DiagnosticsController.cs
│   ├── HealthMonitoringController.cs     ← NOWY: Orkiestrator monitorowania zdrowia systemu
│   ├── ReportingController.cs            ← NOWY: Orkiestrator raportowania i eksportu danych
│   ├── OperationHistoriesController.cs
│   ├── PowerShellController.cs
│   ├── SchoolTypesController.cs
│   ├── SchoolYearsController.cs
│   ├── SchoolYearProcessController.cs    ← NOWY: Orkiestrator procesów szkolnych
│   ├── SubjectsController.cs
│   ├── TeamsController.cs
│   ├── TeamLifecycleController.cs        ← NOWY: Orkiestrator cyklu życia zespołów
│   ├── BulkUserManagementController.cs    ← NOWY: Orkiestrator zarządzania użytkownikami
│   ├── TeamTemplatesController.cs
│   ├── TestAuthController.cs
│   └── UsersController.cs
├── HealthChecks/
│   ├── DependencyInjectionHealthCheck.cs
│   └── PowerShellConnectionHealthCheck.cs
├── Hubs/
│   └── NotificationHub.cs
├── Properties/
│   └── launchSettings.json
└── Swagger/
    ├── AuthorizationOperationFilter.cs
    ├── ExampleSchemaFilter.cs
    └── TagsDocumentFilter.cs
```

### 🏛️ **Core (`TeamsManager.Core/`) - Clean Architecture**
```
TeamsManager.Core/
├── TeamsManager.Core.csproj
├── Abstractions/ (Interfejsy - DDD Contracts)
│   ├── ICurrentUserService.cs
│   ├── Data/ (15 repozytoriów)
│   │   ├── IApplicationSettingRepository.cs
│   │   ├── IGenericRepository.cs
│   │   ├── IOperationHistoryRepository.cs
│   │   ├── ISchoolYearRepository.cs
│   │   ├── ISubjectRepository.cs
│   │   ├── ITeamRepository.cs
│   │   ├── ITeamTemplateRepository.cs
│   │   └── IUserRepository.cs
│   └── Services/ (Interfejsy biznesowe)
│       ├── Auth/
│       │   └── ITokenManager.cs
│       ├── PowerShell/ (6 specjalistycznych serwisów)
│       │   ├── IPowerShellBulkOperationsService.cs
│       │   ├── IPowerShellCacheService.cs
│       │   ├── IPowerShellConnectionService.cs
│       │   ├── IPowerShellTeamManagementService.cs
│       │   ├── IPowerShellUserManagementService.cs
│       │   └── IPowerShellUserResolverService.cs
│       ├── IApplicationSettingService.cs
│       ├── IChannelService.cs
│       ├── IDepartmentService.cs
│       ├── INotificationService.cs
│       ├── IOperationHistoryService.cs
│       ├── IPowerShellService.cs
│       ├── ISchoolTypeService.cs
│       ├── ISchoolYearService.cs
│       ├── ISchoolYearProcessOrchestrator.cs  ← NOWY: Orkiestrator procesów szkolnych
│       ├── IDataImportOrchestrator.cs         ← NOWY: Orkiestrator importu danych CSV/Excel
│       ├── ITeamLifecycleOrchestrator.cs      ← NOWY: Orkiestrator cyklu życia zespołów
│       ├── IBulkUserManagementOrchestrator.cs  ← NOWY: Orkiestrator zarządzania użytkownikami
│       ├── IHealthMonitoringOrchestrator.cs   ← NOWY: Orkiestrator monitorowania zdrowia
│       ├── IReportingOrchestrator.cs          ← NOWY: Orkiestrator raportowania
│       ├── ISubjectService.cs
│       ├── ITeamService.cs
│       ├── ITeamTemplateService.cs
│       ├── IUserService.cs
│       └── IModernHttpService.cs
├── Common/ (Wzorce projektowe)
│   ├── CircuitBreaker.cs
│   └── ModernCircuitBreaker.cs
├── Enums/ (8 enumeracji domenowych)
│   ├── ChannelStatus.cs
│   ├── OperationStatus.cs
│   ├── OperationType.cs
│   ├── SettingType.cs
│   ├── TeamMemberRole.cs
│   ├── TeamStatus.cs
│   ├── TeamVisibility.cs
│   └── UserRole.cs
├── Exceptions/ (Dedykowane wyjątki PowerShell)
│   └── PowerShell/
│       ├── PowerShellCommandExecutionException.cs
│       ├── PowerShellConnectionException.cs
│       ├── PowerShellException.cs
│       └── PowerShellExceptionBuilder.cs
├── Extensions/
│   └── PowerShellServiceExtensions.cs
├── Helpers/
│   ├── AuditHelper.cs
│   └── PowerShell/
│       ├── PSObjectMapper.cs
│       └── PSParameterValidator.cs
├── Models/ (13+ encji domenowych)
│   ├── ApplicationSetting.cs
│   ├── BaseEntity.cs
│   ├── BulkOperationResult.cs               ← ROZSZERZONY: Nowe właściwości dla orkiestracji
│   ├── Channel.cs
│   ├── Department.cs
│   ├── OperationHistory.cs
│   ├── SchoolType.cs
│   ├── SchoolYear.cs
│   ├── Subject.cs
│   ├── Team.cs
│   ├── TeamMember.cs
│   ├── TeamTemplate.cs
│   ├── User.cs
│   ├── UserSchoolType.cs
│   └── UserSubject.cs
└── Services/ (Implementacje biznesowe)
    ├── Auth/
    │   └── TokenManager.cs
    ├── Cache/
    │   └── TeamTemplateCacheKeys.cs
    ├── PowerShell/ (5 zaawansowanych serwisów)
    │   ├── PowerShellBulkOperationsService.cs
    │   ├── PowerShellCacheService.cs
    │   ├── PowerShellConnectionService.cs
    │   ├── PowerShellTeamManagementService.cs
    │   └── PowerShellUserManagementService.cs
    ├── PowerShellServices/
    │   └── PowerShellUserResolverService.cs
    ├── UserContext/
    │   └── CurrentUserService.cs
    ├── ApplicationSettingService.cs
    ├── ChannelService.cs
    ├── DepartmentService.cs
    ├── OperationHistoryService.cs
    ├── PowerShellService.cs
    ├── SchoolTypeService.cs
    ├── SchoolYearService.cs
    ├── StubNotificationService.cs
    ├── SubjectService.cs
    ├── TeamService.cs
    ├── TeamTemplateService.cs
    ├── UserService.cs
    └── ModernHttpService.cs
```

### 📋 **Application (`TeamsManager.Application/`) - Warstwa Aplikacyjna**
```
TeamsManager.Application/
├── TeamsManager.Application.csproj
└── Services/
    ├── SchoolYearProcessOrchestrator.cs     ← NOWY: Implementacja orkiestratora procesów
    ├── DataImportOrchestrator.cs            ← NOWY: Implementacja orkiestratora importu danych
    ├── TeamLifecycleOrchestrator.cs         ← NOWY: Implementacja orkiestratora cyklu życia zespołów
    ├── BulkUserManagementOrchestrator.cs    ← NOWY: Implementacja orkiestratora zarządzania użytkownikami
    ├── HealthMonitoringOrchestrator.cs      ← NOWY: Implementacja orkiestratora monitorowania zdrowia
    ├── ReportingOrchestrator.cs             ← NOWY: Implementacja orkiestratora raportowania
    └── Models/
        ├── SchoolYearProcessOptions.cs      ← NOWY: Opcje konfiguracji procesów
        ├── SchoolYearProcessStatus.cs       ← NOWY: Status i postęp procesów
        ├── TeamCreationPlan.cs              ← NOWY: Plan tworzenia zespołów
        ├── ImportOptions.cs                 ← NOWY: Opcje konfiguracji importu
        ├── ImportProcessStatus.cs           ← NOWY: Status procesów importu
        ├── ArchiveOptions.cs                ← NOWY: Opcje archiwizacji zespołów
        ├── RestoreOptions.cs                ← NOWY: Opcje przywracania zespołów
        ├── TeamMigrationPlan.cs             ← NOWY: Plan migracji zespołów
        ├── ConsolidationOptions.cs          ← NOWY: Opcje konsolidacji zespołów
        └── TeamLifecycleProcessStatus.cs    ← NOWY: Status procesów cyklu życia zespołów
```

### 🗃️ **Data (`TeamsManager.Data/`) - Warstwa Danych**
```
TeamsManager.Data/
├── TeamsManager.Data.csproj
├── TeamsManagerDbContext.cs
├── Migrations/ (SQLite + Entity Framework Core)
│   ├── 20250529171240_InitialCreate.cs
│   ├── 20250529171240_InitialCreate.Designer.cs
│   ├── 20250530143555_ReplaceTeamIsVisibleWithVisibility.cs
│   ├── 20250530143555_ReplaceTeamIsVisibleWithVisibility.Designer.cs
│   └── TeamsManagerDbContextModelSnapshot.cs
└── Repositories/ (8 repozytoriów z wzorcem Generic Repository)
    ├── ApplicationSettingRepository.cs
    ├── GenericRepository.cs
    ├── OperationHistoryRepository.cs
    ├── SchoolYearRepository.cs
    ├── SubjectRepository.cs
    ├── TeamRepository.cs
    ├── TeamTemplateRepository.cs
    └── UserRepository.cs
```

### 🧪 **Tests (`TeamsManager.Tests/`) - 961 testów, 100% coverage**
```
TeamsManager.Tests/
├── TeamsManager.Tests.csproj
├── Authorization/
│   └── JwtAuthenticationTests.cs
├── Collections/
│   └── SequentialTestCollection.cs
├── Configuration/
│   └── ApiAuthConfigTests.cs
├── Controllers/ (Testy API)
│   ├── ChannelsControllerTests.cs
│   ├── DepartmentsControllerTests.cs
│   ├── SchoolTypesControllerTests.cs
│   ├── TeamsControllerTests.cs
│   └── UsersControllerTests.cs
├── Enums/ (Testy enumeracji)
│   ├── ChannelStatusTests.cs
│   ├── OperationStatusTests.cs
│   ├── OperationTypeTests.cs
│   ├── SettingTypeTests.cs
│   ├── TeamMemberRoleTests.cs
│   ├── TeamStatusTests.cs
│   └── UserRoleTests.cs
├── HealthChecks/
│   └── PowerShellConnectionHealthCheckTests.cs
├── Infrastructure/
│   ├── TestDbContext.cs
│   └── Services/
│       └── TestCurrentUserService.cs
├── Integration/
│   └── IntegrationTestBase.cs
├── Models/ (Testy encji)
│   ├── ApplicationSettingTests.cs
│   ├── BaseEntityTests.cs
│   ├── ChannelTests.cs
│   ├── DepartmentTests.cs
│   ├── OperationHistoryTests.cs
│   ├── SchoolTypeTests.cs
│   ├── SchoolYearTests.cs
│   ├── SubjectTests.cs
│   ├── TeamMemberTests.cs
│   ├── TeamTemplateTests.cs
│   ├── TeamTests.cs
│   ├── UserSchoolTypeTests.cs
│   ├── UserSubjectTests.cs
│   └── UserTests.cs
├── Performance/ (Testy wydajności)
│   ├── RepositoryPerformanceTests.cs
│   └── ServicePerformanceTests.cs
├── PowerShell/ (Testy PowerShell Services)
│   ├── PowerShellBulkOperationsServiceTests.cs
│   ├── PowerShellCacheServiceTests.cs
│   ├── PowerShellConnectionServiceTests.cs
│   ├── PowerShellServiceTests.cs
│   ├── PowerShellTeamManagementServiceTests.cs
│   ├── PowerShellUserManagementServiceTests.cs
│   └── PowerShellUserResolverServiceTests.cs
├── Repositories/ (Testy repozytoriów)
│   ├── ApplicationSettingRepositoryTests.cs
│   ├── GenericRepositoryTests.cs
│   ├── OperationHistoryRepositoryTests.cs
│   ├── SchoolYearRepositoryTests.cs
│   ├── SubjectRepositoryTests.cs
│   ├── TeamRepositoryTests.cs
│   ├── TeamTemplateRepositoryTests.cs
│   └── UserRepositoryTests.cs
├── Security/ (Testy bezpieczeństwa)
│   ├── AuthControllerTests.cs
│   ├── AuthorizationTests.cs
│   ├── JwtSecurityTests.cs
│   └── TokenManagerTests.cs
└── Services/ (Testy serwisów biznesowych)
    ├── ApplicationSettingServiceTests.cs
    ├── ChannelServiceTests.cs
    ├── CurrentUserServiceTests.cs
    ├── DepartmentServiceTests.cs
    ├── ModernHttpServiceTests.cs
    ├── OperationHistoryServiceTests.cs
    ├── SchoolTypeServiceTests.cs
    ├── SchoolYearServiceTests.cs
    ├── SchoolYearProcessOrchestratorTests.cs  ← NOWY: Testy orkiestratora procesów
    ├── DataImportOrchestratorTests.cs         ← NOWY: Testy orkiestratora importu danych (37 testów)
    ├── TeamLifecycleOrchestratorTests.cs      ← NOWY: Testy orkiestratora cyklu życia zespołów (17 testów)
    ├── BulkUserManagementOrchestratorTests.cs ← NOWY: Testy orkiestratora zarządzania użytkownikami (26 testów)
    ├── HealthMonitoringOrchestratorTests.cs   ← NOWY: Testy orkiestratora monitorowania zdrowia (35 testów)
    ├── ReportingOrchestratorTests.cs          ← NOWY: Testy orkiestratora raportowania (44 testy)
    ├── SubjectServiceTests.cs
    ├── TeamServiceTests.cs
    ├── TeamTemplateServiceTests.cs
    └── UserServiceTests.cs
```

### 🖼️ **UI (`TeamsManager.UI/`) - WPF Material Design 3.0**
```
TeamsManager.UI/
├── App.xaml (Konfiguracja Material Design + Custom Styles)
├── App.xaml.cs
├── AssemblyInfo.cs
├── TeamsManager.UI.csproj
├── Models/
│   └── Configuration/
│       ├── ApiConfiguration.cs
│       ├── UiConfiguration.cs
│       └── ProviderType.cs
├── Services/
│   └── Configuration/
│       ├── ApiConfigurationService.cs
│       ├── ConfigurationDetectionService.cs
│       ├── UiConfigurationService.cs
│       └── JsonConfigurationProviderService.cs
├── Styles/ (Material Design 3.0 + Custom)
│   └── CommonStyles.xaml (26KB, 591 linii - kompletny system stylów)
├── ViewModels/ (MVVM Pattern)
│   ├── Configuration/
│   │   ├── ApiConfigurationViewModel.cs
│   │   ├── ConfigurationDetectionViewModel.cs
│   │   ├── ConfigurationViewModelBase.cs
│   │   ├── TestConnectionViewModel.cs
│   │   └── UiConfigurationViewModel.cs
│   ├── DashboardViewModel.cs (Główny dashboard)
│   └── RelayCommand.cs (Command Pattern)
└── Views/ (6 okien aplikacji)
    ├── Configuration/ (4 okna konfiguracyjne)
    │   ├── ApiConfigurationWindow.xaml (.cs)
    │   ├── ConfigurationDetectionWindow.xaml (.cs)
    │   ├── TestConnectionWindow.xaml (.cs)
    │   └── UiConfigurationWindow.xaml (.cs)
    ├── DashboardWindow.xaml (.cs) (Główne okno)
    └── ManualTestingWindow.xaml (.cs) (Okno testów)
```

### 🕷️ **Legacy API (`TeamsApiApp/`) - Wycofywany**
```
TeamsApiApp/
├── Program.cs
├── TeamsApiApp.csproj
├── appsettings.json
├── appsettings.Development.json
└── Swagger/
    ├── ExampleSchemaFilter.cs
    └── TagsDocumentFilter.cs
```

---

## 🏗️ Architektura Systemu

### **Wzorce Projektowe:**
- **Clean Architecture** (Core, Data, API, UI)
- **Domain Driven Design** (DDD)
- **CQRS Pattern** (Command Query Responsibility Segregation)
- **Repository Pattern** z Generic Repository
- **Dependency Injection** (Microsoft.Extensions.DependencyInjection)
- **Circuit Breaker Pattern** (Odporność na awarie)
- **MVVM Pattern** (UI Layer)

### **Technologie:**
- **.NET 9.0** - Najnowsza wersja platformy
- **ASP.NET Core API** - RESTful API z Swagger
- **WPF + Material Design 3.0** - Nowoczesny UI
- **Entity Framework Core** - ORM dla SQLite
- **Microsoft Graph API** - Integracja z Teams
- **JWT Authentication** - Bearer Token security
- **SignalR** - Real-time komunikacja
- **xUnit + Moq** - Framework testowy
- **PowerShell Core** - Zarządzanie Teams

### **Bezpieczeństwo:**
- **OAuth 2.0 + On-Behalf-Of Flow** (OBO)
- **JWT Token Management** - Automatyczne odświeżanie
- **Circuit Breaker** - Ochrona przed przeciążeniem
- **Input Validation** - Walidacja wszystkich danych
- **Error Handling** - Dedykowane wyjątki

### **Wydajność:**
- **Cache Strategy** - Inteligentne cache'owanie
- **Bulk Operations** - Operacje zbiorcze PowerShell
- **Async/Await** - Programowanie asynchroniczne
- **Connection Pooling** - Optymalizacja połączeń
- **Memory Management** - Zarządzanie pamięcią

---

## 📊 Metryki Projektu

- **👨‍�� Linie kodu:** ~63,200 (C# + XAML)
- **📁 Pliki źródłowe:** 255
- **🧪 Testy:** 1113 (100% pass rate)
- **📚 Dokumentacja:** 13 plików aktualnych
- **🏗️ Architektura:** Clean Architecture + DDD
- **⚡ Technologia:** .NET 9.0, Material Design 3.0
- **📅 Status:** Gotowy do produkcji

---

> **📝 Uwaga:** Ten plik jest aktualizowany automatycznie. Ostatnia aktualizacja: **07 czerwca 2025, 08:46**
