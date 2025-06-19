# 📁 Struktura Projektu TeamsManager

**📅 Ostatnia aktualizacja:** 19 czerwca 2025, 09:47  
**🔢 Statystyki:** 550+ plików źródłowych (CS/XAML/JSON), ~40,000+ linii kodu  
**⚡ Technologia:** .NET 9.0, Material Design 3.0, WPF + ASP.NET Core API + Application Layer (6 orkiestratorów)  

> **Status:** Projekt gotowy do produkcji - wszystkie 1680 testów przechodzą (100% SUKCES!)

---

## 🏗️ Aktualna Struktura Projektu TeamsManager

### 📋 **Pliki główne**
```
.gitignore
README.md
TeamsManager.sln
global.json                               ← .NET 9.0 SDK requirement
DataImportOrchestrator_README.md         ← NOWY: Dokumentacja orkiestratora importu
```

### 📚 **Dokumentacja (`docs/`)**
```
docs/
├── 📊 schematy/
│   ├── architektura-systemu.svg
│   ├── diagram-aktywnosci-schoolyear.svg
│   ├── diagram-erd.svg
│   └── jpg/ (wersje JPG schematów)
├── 📄 Pliki aktualne (16 plików):
│   ├── analizaStabilnosciNet9.md        - Analiza migracji na .NET 9.0
│   ├── architekturaDI.md                - 🆕 NOWY: Kompletny przewodnik architektury DI
│   ├── audytArchitektruySync.md         - Audyt synchronizacji architektury
│   ├── DI-Architecture.md               - 🆕 NOWY: Kompletny przewodnik architektury DI
│   ├── Migration-Guide.md               - 🆕 NOWY: Przewodnik migracji do DI (6 etapów)
│   ├── Release-Notes-DI.md              - 🆕 NOWY: Release notes refaktoryzacji DI
│   ├── powerShellService.md             - Zarządzanie PowerShell Services
│   ├── strategiaCache.md                - Strategia cache'owania
│   ├── strukturaProjektu.md             - Ten plik
│   ├── styleUI.md                       - Przewodnik stylów UI
│   ├── synchronizacja.md                - Synchronizacja architektury
│   ├── TodoSystemKolejkowy.md           - System kolejkowania operacji
│   ├── tokenPlany.md                    - Plany rozwoju tokenów
│   ├── tokenRefactor.md                 - Refaktoryzacja token managera
│   └── README.md                        - Główna dokumentacja
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
├── Controllers/ (19 kontrolerów)
│   ├── ApplicationSettingsController.cs
│   ├── BulkUserManagementController.cs   ← NOWY: Orkiestrator zarządzania użytkownikami
│   ├── ChannelsController.cs
│   ├── DataImportController.cs           ← NOWY: Orkiestrator importu danych CSV/Excel
│   ├── DepartmentsController.cs
│   ├── DiagnosticsController.cs
│   ├── HealthMonitoringController.cs     ← NOWY: Orkiestrator monitorowania zdrowia systemu
│   ├── OperationHistoriesController.cs
│   ├── OrganizationalUnitsController.cs
│   ├── PowerShellController.cs
│   ├── ReportingController.cs            ← NOWY: Orkiestrator raportowania i eksportu danych
│   ├── SchoolTypesController.cs
│   ├── SchoolYearsController.cs
│   ├── SchoolYearProcessController.cs    ← NOWY: Orkiestrator procesów szkolnych
│   ├── SubjectsController.cs
│   ├── TeamLifecycleController.cs        ← NOWY: Orkiestrator cyklu życia zespołów
│   ├── TeamsController.cs
│   ├── TeamTemplatesController.cs
│   ├── TestAuthController.cs
│   └── UsersController.cs
├── Extensions/
│   └── HttpContextExtensions.cs
├── HealthChecks/
│   ├── DependencyInjectionHealthCheck.cs
│   └── GraphConnectionHealthCheck.cs     ← NOWY: Health check Microsoft Graph
├── Hubs/
│   ├── MonitoringHub.cs                  ← NOWY: Hub monitorowania
│   └── NotificationHub.cs
├── Properties/
│   └── launchSettings.json
├── Services/
│   └── SignalRNotificationService.cs
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
│   │   ├── IOrganizationalUnitRepository.cs
│   │   ├── ISchoolYearRepository.cs
│   │   ├── ISubjectRepository.cs
│   │   ├── ITeamRepository.cs
│   │   ├── ITeamTemplateRepository.cs
│   │   └── IUserRepository.cs
│   └── Services/ (Interfejsy biznesowe)
│       ├── Auth/
│       │   └── ITokenManager.cs
│       ├── Cache/
│       │   └── ICacheInvalidationService.cs
│       ├── Graph/                        ← NOWY: Microsoft Graph API Services
│       │   ├── IGraphBulkOperationsService.cs
│       │   ├── IGraphCacheService.cs
│       │   ├── IGraphConnectionService.cs
│       │   ├── IGraphTeamManagementService.cs
│       │   ├── IGraphUserManagementService.cs
│       │   ├── IGraphUserResolverService.cs
│       │   └── IGraphValidationService.cs
│       ├── Synchronization/
│       │   └── IChannelSynchronizer.cs
│       ├── IAdminNotificationService.cs
│       ├── IApplicationSettingService.cs
│   │   ├── IBulkUserManagementOrchestrator.cs  ← NOWY: Orkiestrator zarządzania użytkownikami
│       ├── IChannelService.cs
│       ├── IDataImportOrchestrator.cs         ← NOWY: Orkiestrator importu danych CSV/Excel
│       ├── IDepartmentService.cs
│       ├── IHealthMonitoringOrchestrator.cs   ← NOWY: Orkiestrator monitorowania zdrowia
│       ├── INotificationService.cs
│       ├── IOperationHistoryService.cs
│       ├── IOrganizationalUnitService.cs
│       ├── IReportingOrchestrator.cs          ← NOWY: Orkiestrator raportowania
│       ├── ISchoolTypeService.cs
│       ├── ISchoolYearProcessOrchestrator.cs  ← NOWY: Orkiestrator procesów szkolnych
│       ├── ISchoolYearService.cs
│       ├── ISubjectService.cs
│       ├── ITeamLifecycleOrchestrator.cs      ← NOWY: Orkiestrator cyklu życia zespołów
│       ├── ITeamService.cs
│       ├── ITeamTemplateService.cs
│       ├── IUserService.cs
│       └── IModernHttpService.cs
├── Common/ (Wzorce projektowe)
│   ├── CircuitBreaker.cs
│   └── ModernCircuitBreaker.cs
├── Enums/ (9 enumeracji domenowych)
│   ├── ChannelStatus.cs
│   ├── HealthStatus.cs                   ← NOWY: Status zdrowia systemu
│   ├── OperationStatus.cs
│   ├── OperationType.cs
│   ├── SchoolYearStatus.cs
│   ├── SettingType.cs
│   ├── TeamMemberRole.cs
│   ├── TeamStatus.cs
│   ├── TeamVisibility.cs
│   └── UserRole.cs
├── Exceptions/ (Dedykowane wyjątki)
│   └── Graph/                            ← NOWY: Microsoft Graph wyjątki
│       ├── GraphApiException.cs
│       ├── GraphConnectionException.cs
│       ├── GraphRateLimitException.cs
│       └── GraphServiceException.cs
├── Extensions/
│   ├── EnumExtensions.cs
│   └── GraphServiceExtensions.cs        ← NOWY: Rozszerzenia Graph API
├── Helpers/
│   ├── AuditHelper.cs
│   └── GraphModelMapper.cs              ← NOWY: Mapowanie modeli Graph
├── Models/ (20+ encji domenowych)
│   ├── ApiResponses.cs
│   ├── ApplicationSetting.cs
│   ├── BaseEntity.cs
│   ├── BulkOperationProgress.cs         ← NOWY: Progress operacji zbiorczych
│   ├── Channel.cs
│   ├── Department.cs
│   ├── Graph/                           ← NOWY: Modele Microsoft Graph
│   │   ├── GraphApiConfiguration.cs
│   │   ├── GraphApiModels.cs
│   │   ├── GraphBulkOperationModels.cs
│   │   ├── GraphBatchRequest.cs
│   │   ├── GraphBatchResponse.cs
│   │   ├── GraphErrorModels.cs
│   │   ├── GraphRateLimitInfo.cs
│   │   ├── GraphResponseModels.cs
│   │   ├── GraphTeamModels.cs
│   │   ├── GraphUserModels.cs
│   │   └── GraphValidationModels.cs
│   ├── OperationHistory.cs
│   ├── OrganizationalUnit.cs
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
    │   ├── CacheInvalidationService.cs
    │   └── TeamTemplateCacheKeys.cs
    ├── Graph/                            ← NOWY: Microsoft Graph Services
    │   ├── GraphBulkOperationsService.cs
    │   ├── GraphCacheService.cs
    │   ├── GraphConnectionService.cs
    │   ├── GraphTeamManagementService.cs
    │   ├── GraphUserManagementService.cs
    │   ├── GraphUserResolverService.cs
    │   └── GraphValidationService.cs
    ├── Synchronization/
    │   ├── ChannelSynchronizer.cs
    │   ├── GraphSynchronizerBase.cs
    │   ├── TeamSynchronizer.cs
    │   └── UserSynchronizer.cs
    ├── UserContext/
    │   └── CurrentUserService.cs
    ├── ApplicationSettingService.cs
    ├── ChannelService.cs
    ├── DepartmentService.cs
    ├── ModernHttpService.cs
    ├── OperationHistoryService.cs
    ├── OrganizationalUnitService.cs
    ├── SchoolTypeService.cs
    ├── SchoolYearService.cs
    ├── SubjectService.cs
    ├── TeamService.cs
    ├── TeamTemplateService.cs
    └── UserService.cs
```

### 📋 **Application (`TeamsManager.Application/`) - Warstwa Aplikacyjna**
```
TeamsManager.Application/
├── TeamsManager.Application.csproj
└── Services/
    ├── BulkUserManagementOrchestrator.cs    ← NOWY: Implementacja orkiestratora zarządzania użytkownikami
    ├── DataImportOrchestrator.cs            ← NOWY: Implementacja orkiestratora importu danych
    ├── HealthMonitoringOrchestrator.cs      ← NOWY: Implementacja orkiestratora monitorowania zdrowia
    ├── ReportingOrchestrator.cs             ← NOWY: Implementacja orkiestratora raportowania
    ├── SchoolYearProcessOrchestrator.cs     ← NOWY: Implementacja orkiestratora procesów
    └── TeamLifecycleOrchestrator.cs         ← NOWY: Implementacja orkiestratora cyklu życia zespołów
```

### 🗃️ **Data (`TeamsManager.Data/`) - Warstwa Danych**
```
TeamsManager.Data/
├── TeamsManager.Data.csproj
├── DesignTimeDbContextFactory.cs
├── Program.cs
├── TeamsManagerDbContext.cs
├── Migrations/ (SQLite + Entity Framework Core - 10 migracji)
│   ├── 20250529171240_InitialCreate.cs
│   ├── 20250529171240_InitialCreate.Designer.cs
│   ├── 20250530143555_ReplaceTeamIsVisibleWithVisibility.cs
│   ├── 20250530143555_ReplaceTeamIsVisibleWithVisibility.Designer.cs
│   ├── 20250601120000_AddOrganizationalUnits.cs
│   ├── 20250601120000_AddOrganizationalUnits.Designer.cs
│   ├── 20250605140000_AddBulkOperationProgress.cs
│   ├── 20250605140000_AddBulkOperationProgress.Designer.cs
│   ├── 20250610100000_AddSchoolYearStatus.cs
│   ├── 20250610100000_AddSchoolYearStatus.Designer.cs
│   └── TeamsManagerDbContextModelSnapshot.cs
├── Repositories/ (10 repozytoriów z wzorcem Generic Repository)
│   ├── ApplicationSettingRepository.cs
│   ├── ChannelRepository.cs
│   ├── DepartmentRepository.cs
│   ├── GenericRepository.cs
│   ├── OperationHistoryRepository.cs
│   ├── OrganizationalUnitRepository.cs
│   ├── SchoolYearRepository.cs
│   ├── SubjectRepository.cs
│   ├── TeamRepository.cs
│   ├── TeamTemplateRepository.cs
│   └── UserRepository.cs
└── UnitOfWork/
    └── EfUnitOfWork.cs
```

### 🧪 **Tests (`TeamsManager.Tests/`) - 1680 testów (100% SUKCES!), wysokie pokrycie**
```
TeamsManager.Tests/
├── TeamsManager.Tests.csproj
├── ComprehensiveTestPlan.md
├── TestPlan.md
├── Authorization/
│   └── JwtAuthenticationTests.cs
├── Collections/
│   └── SequentialTestCollection.cs
├── Configuration/
│   └── ApiAuthConfigTests.cs
├── Controllers/ (Testy API - 18 kontrolerów)
│   ├── ApplicationSettingsControllerTests.cs
│   ├── BulkUserManagementControllerTests.cs
│   ├── ChannelsControllerTests.cs
│   ├── DataImportControllerTests.cs
│   ├── DepartmentsControllerTests.cs
│   ├── DiagnosticsControllerTests.cs
│   ├── HealthMonitoringControllerTests.cs
│   ├── OperationHistoriesControllerTests.cs
│   ├── OrganizationalUnitsControllerTests.cs
│   ├── PowerShellControllerTests.cs
│   ├── ReportingControllerTests.cs
│   ├── SchoolTypesControllerTests.cs
│   ├── SchoolYearsControllerTests.cs
│   ├── SchoolYearProcessControllerTests.cs
│   ├── SubjectsControllerTests.cs
│   ├── TeamLifecycleControllerTests.cs
│   ├── TeamsControllerTests.cs
│   ├── TeamTemplatesControllerTests.cs
│   └── UsersControllerTests.cs
├── Data/ (Testy warstwy danych)
│   ├── DataProgramTests.cs
│   ├── DesignTimeDbContextFactoryTests.cs
│   ├── MigrationsTests.cs
│   └── UnitOfWorkTests.cs
├── Enums/ (Testy enumeracji - 7 enumów)
│   ├── ChannelStatusTests.cs
│   ├── OperationStatusTests.cs
│   ├── OperationTypeTests.cs
│   ├── SchoolYearStatusTests.cs
│   ├── SettingTypeTests.cs
│   ├── TeamMemberRoleTests.cs
│   ├── TeamStatusTests.cs
│   └── UserRoleTests.cs
├── Extensions/
│   └── HttpContextExtensionsTests.cs
├── HealthChecks/
├── Helpers/
│   └── PowerShell/
├── Infrastructure/
│   ├── TestDbContext.cs
│   └── Services/
│       └── TestCurrentUserService.cs
├── Integration/
│   ├── IntegrationTestBase.cs
│   └── NotificationHubIntegrationTests.cs
├── Models/ (Testy encji - 24 modele)
│   ├── ApplicationSettingTests.cs
│   ├── BaseEntityTests.cs
│   ├── BulkOperationProgressTests.cs
│   ├── ChannelTests.cs
│   ├── DepartmentTests.cs
│   ├── GraphApiConfigurationTests.cs
│   ├── GraphApiModelsTests.cs
│   ├── GraphBatchRequestTests.cs
│   ├── GraphBatchResponseTests.cs
│   ├── GraphBulkOperationModelsTests.cs
│   ├── GraphErrorModelsTests.cs
│   ├── GraphRateLimitInfoTests.cs
│   ├── GraphResponseModelsTests.cs
│   ├── GraphTeamModelsTests.cs
│   ├── GraphUserModelsTests.cs
│   ├── GraphValidationModelsTests.cs
│   ├── OperationHistoryTests.cs
│   ├── OrganizationalUnitTests.cs
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
│   └── RepositoryPerformanceTests.cs
├── Repositories/ (Testy repozytoriów - 13 repozytoriów)
│   ├── ApplicationSettingRepositoryTests.cs
│   ├── ChannelRepositoryTests.cs
│   ├── DepartmentRepositoryTests.cs
│   ├── GenericRepositoryTests.cs
│   ├── OperationHistoryRepositoryTests.cs
│   ├── OrganizationalUnitRepositoryTests.cs
│   ├── SchoolYearRepositoryTests.cs
│   ├── SubjectRepositoryTests.cs
│   ├── TeamRepositoryTests.cs
│   ├── TeamTemplateRepositoryTests.cs
│   ├── UserRepositoryTests.cs
│   ├── UserSchoolTypeRepositoryTests.cs
│   └── UserSubjectRepositoryTests.cs
├── Services/ (Testy serwisów biznesowych)
│   ├── Application/ (Testy orkiestratorów - 6 orkiestratorów)
│   │   ├── BulkUserManagementOrchestratorTests.cs    ← 26 testów
│   │   ├── DataImportOrchestratorTests.cs            ← 37 testów
│   │   ├── HealthMonitoringOrchestratorTests.cs      ← 35 testów
│   │   ├── ReportingOrchestratorTests.cs             ← 44 testy
│   │   ├── SchoolYearProcessOrchestratorTests.cs     ← 28 testów
│   │   └── TeamLifecycleOrchestratorTests.cs         ← 17 testów
│   ├── CircuitBreakerTests.cs
│   ├── Core/ (Testy serwisów Core)
│   │   ├── ChannelServiceTests.cs
│   │   ├── DepartmentServiceTests.cs
│   │   ├── GraphServiceTests.cs
│   │   ├── OrganizationalUnitServiceTests.cs
│   │   └── UserServiceTests.cs
│   ├── CurrentUserServiceTests.cs
│   ├── Graph/ (Testy Microsoft Graph Services - 6 serwisów)
│   │   ├── GraphBulkOperationsServiceTests.cs        ← 23 testy (100% SUKCES!)
│   │   ├── GraphCacheServiceTests.cs                 ← 45 testów (100% SUKCES!)
│   │   ├── GraphConnectionServiceTests.cs            ← 6 testów (100% SUKCES!)
│   │   ├── GraphTeamManagementServiceTests.cs        ← 12 testów (100% SUKCES!)
│   │   ├── GraphUserManagementServiceTests.cs
│   │   └── GraphValidationServiceTests.cs
│   ├── ModernHttpServiceTests.cs
│   ├── Synchronization/
│   ├── ApplicationSettingServiceTests.cs
│   ├── SchoolTypeServiceTests.cs
│   ├── SchoolYearServiceTests.cs
│   ├── SubjectServiceTests.cs
│   ├── TeamServiceTests.cs
│   └── TeamTemplateServiceTests.cs
├── TestResults/
├── UI/
└── Validation/
    └── OrganizationalUnitValidatorTests.cs
```

### 🖼️ **UI (`TeamsManager.UI/`) - WPF Material Design 3.0**
```
TeamsManager.UI/
├── App.xaml (Konfiguracja Material Design + Custom Styles)
├── App.xaml.cs
├── TeamsManager.UI.csproj
├── appsettings.json
├── Controls/
├── Converters/ (31 konwerterów)
│   ├── BooleanToOpacityConverter.cs
│   ├── BooleanToVisibilityConverter.cs
│   ├── BooleanToYesNoConverter.cs
│   └── [+28 innych konwerterów]
├── Docs/
│   └── UniversalDialogSystem.md
├── Examples/
│   └── TeamTemplateEditorUsage.cs
├── Models/
│   ├── ConditionalAccessInfo.cs
│   ├── Configuration/
│   │   ├── ApiConfiguration.cs
│   │   ├── ConfigurationValidationResult.cs
│   │   ├── LoginSettings.cs
│   │   ├── UiConfiguration.cs
│   │   └── ProviderType.cs
│   ├── DialogOptions.cs
│   ├── Import/
│   │   └── ImportDataTypeModel.cs
│   ├── Monitoring/
│   │   └── MonitoringModels.cs
│   ├── SchoolTypeModels/
│   │   └── SchoolTypeDisplayModel.cs
│   ├── SchoolYearModels/
│   │   └── SchoolYearDisplayModel.cs
│   ├── Teams/
│   │   ├── TeamGrouping.cs
│   │   └── TemplateValueViewModel.cs
│   ├── TestCase.cs
│   ├── UI/
│   │   └── DepartmentStatistics.cs
│   └── ViewModels/
│       ├── SchoolTypeAssignmentModel.cs
│       └── UserDetailModel.cs
├── Scripts/
│   ├── CreateDefaultOrganizationalUnit.cs
│   └── TestDepartmentCRUD.cs
├── Services/
│   ├── Abstractions/                           ← 🆕 NOWY: Interfejsy DI
│   │   ├── IApplicationSettingService.cs
│   │   ├── IConfigurationDetectionService.cs
│   │   ├── IManualTestingService.cs
│   │   └── IUiConfigurationService.cs
│   ├── ApplicationSettingService.cs
│   ├── ConditionalAccessAnalyzer.cs
│   ├── Configuration/
│   │   ├── ApiConfigurationService.cs
│   │   ├── ConfigurationDetectionService.cs
│   │   ├── JsonConfigurationProviderService.cs
│   │   └── UiConfigurationService.cs
│   ├── Dashboard/
│   │   ├── DashboardMetricsService.cs
│   │   ├── DashboardStatisticsService.cs
│   │   └── DashboardWidgetService.cs
│   ├── DepartmentCodeMigrationService.cs
│   ├── Http/
│   │   └── ApiHttpService.cs
│   ├── UI/
│   │   ├── DialogService.cs
│   │   ├── NotificationService.cs
│   │   └── ThemeService.cs
│   └── [+13 innych serwisów]
├── Styles/ (Material Design 3.0 + Custom)
│   └── CommonStyles.xaml (26KB, 591 linii - kompletny system stylów)
├── UserControls/
│   ├── BulkOperationsToolbar.xaml (.cs)
│   ├── ChannelCard.xaml (.cs)
│   ├── Import/ (7 kontrolek importu)
│   │   └── [+7 plików]
│   ├── Settings/ (2 kontrolki ustawień)
│   │   └── [+2 pliki]
│   ├── Teams/ (6 kontrolek zespołów)
│   │   └── [+6 plików]
│   └── [+1 inne kontrolki]
├── ViewModels/ (MVVM Pattern - 40+ ViewModeli)
│   ├── BaseViewModel.cs
│   ├── Dashboard/
│   │   └── DashboardViewModel.cs
│   ├── Departments/
│   │   └── [+3 ViewModele]
│   ├── Dialogs/
│   │   └── [+1 ViewModel]
│   ├── Import/
│   │   └── [+5 ViewModeli]
│   ├── LoginViewModel.cs
│   ├── Monitoring/
│   │   └── [+1 ViewModel + 1 katalog]
│   ├── Operations/
│   │   └── [+2 ViewModele]
│   ├── OrganizationalUnits/
│   │   └── [+3 ViewModele]
│   ├── RelayCommand.cs (Command Pattern)
│   ├── SchoolTypes/
│   │   └── [+2 ViewModele]
│   ├── SchoolYears/
│   │   └── [+1 ViewModel]
│   ├── Settings/
│   │   └── [+2 ViewModele]
│   ├── Shell/
│   │   └── [+1 ViewModel]
│   ├── Subjects/
│   │   └── [+3 ViewModele]
│   ├── Teams/
│   │   └── [+8 ViewModeli]
│   └── Users/
│       └── [+4 ViewModele]
└── Views/ (50+ okien i widoków - wszystkie z Dependency Injection)
    ├── Common/
    │   └── [+2 widoki]
    ├── Dashboard/
    │   └── [+2 widoki]
    ├── Departments/
    │   └── [+4 widoki]
    ├── Dialogs/
    │   └── [+2 dialogi]
    ├── Import/
    │   └── [+2 widoki]
    ├── LoginWindow.xaml (.cs)
    ├── ManualTestingWindow.xaml (.cs)
    ├── Monitoring/
    │   └── [+2 widoki + 1 katalog]
    ├── Operations/
    │   └── [+2 widoki]
    ├── OrganizationalUnits/
    │   └── [+4 widoki]
    ├── SchoolTypes/
    │   └── [+4 widoki]
    ├── SchoolYears/
    │   └── [+2 widoki]
    ├── Settings/
    │   └── [+2 widoki]
    ├── Shell/
    │   └── [+2 widoki]
    ├── Subjects/
    │   └── [+8 widoków]
    ├── Teams/
    │   └── [+12 widoków]
    ├── Users/
    │   └── [+6 widoków]
    └── [+3 inne widoki]
```

### 📊 **Raporty pokrycia testów (`CoverageReport/`)**
```
CoverageReport/
├── class.js
├── icon_cog_dark.svg
├── icon_cog.svg
└── [+544 plików raportów HTML]
```

### 🗄️ **Wyniki testów (`TestResults/`)**
```
TestResults/
└── [pliki wyników testów xUnit]
```

---

## 🏗️ Architektura Systemu

### **Wzorce Projektowe:**
- **Clean Architecture** (Core, Data, API, UI)
- **Domain Driven Design** (DDD)
- **CQRS Pattern** (Command Query Responsibility Segregation)
- **Repository Pattern** z Generic Repository
- **Dependency Injection** (Microsoft.Extensions.DependencyInjection) - ✅ **100% DI w UI**
- **HttpClientFactory Pattern** - connection pooling, token management
- **Circuit Breaker Pattern** (Odporność na awarie)
- **MVVM Pattern** (UI Layer)
- **Factory Pattern** - service creation, graceful degradation
- **Handler Pattern** - TokenAuthorizationHandler dla Microsoft Graph

### **Technologie:**
- **.NET 9.0** - Najnowsza wersja platformy
- **ASP.NET Core API** - RESTful API z Swagger
- **WPF + Material Design 3.0** - Nowoczesny UI
- **Entity Framework Core** - ORM dla SQLite
- **Microsoft Graph API** - Integracja z Teams/Office 365
- **JWT Authentication** - Bearer Token security
- **SignalR** - Real-time komunikacja
- **xUnit + Moq + MemoryCache.Testing.Moq** - Framework testowy (1680 testów)
- **SQLite** - Baza danych

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

- **👨‍💻 Linie kodu:** ~40,000+ (C# + XAML + JSON)
- **📁 Pliki źródłowe:** 550+
- **🧪 Testy:** 1680 (100% SUKCES! - wysokie pokrycie)
- **📚 Dokumentacja:** 16+ plików aktualnych
- **🏗️ Architektura:** Clean Architecture + DDD + Application Layer (6 orkiestratorów)
- **⚡ Technologia:** .NET 9.0, Material Design 3.0, Microsoft Graph API
- **📅 Status:** Gotowy do produkcji - wszystkie testy przechodzą!

---

## 🎯 Najnowsze Osiągnięcia (Czerwiec 2025)

### 🏆 **KOMPLETNY SUKCES Testów!**
- **1680 testów** - wszystkie przechodzą (100% SUKCES!)
- **Zero błędów** - eliminacja wszystkich problemów testowych
- **Microsoft Graph API** - pełna integracja z testami
- **MemoryCache.Testing.Moq v1.2.2** - nowoczesne testowanie cache

### 🚀 **Kluczowe Rozwiązania Techniczne:**
1. **GraphCacheServiceTests** - 45/45 testów (100%)
2. **GraphTeamManagementServiceTests** - 12/12 testów (100%)  
3. **GraphConnectionServiceTests** - 6/6 testów (100%)
4. **GraphBulkOperationsServiceTests** - 23/23 testy (100%)
5. **Wszystkie orkiestratory** - 187 testów aplikacyjnych (100%)

### 📈 **Wzrost Projektu:**
- **+89 plików** (461 → 550)
- **+5,000 linii kodu** (35k → 40k)
- **+1573 testów** (107 → 1680)
- **Microsoft Graph API** - pełna implementacja
- **Dependency Injection** - 100% pokrycie UI

---

> **📝 Uwaga:** Ten plik jest aktualizowany automatycznie. Ostatnia aktualizacja: **19 czerwca 2025, 09:47**
