# Plan Kompletnych Testów TeamsManager

## Pokrycie Testowe - Architektura Warstwowa

### 1. WARSTWA CORE (TeamsManager.Core)

#### 1.1 Models - Testy Jednostkowe Modeli
- [x] `BaseEntity` - testy audytu, soft delete
- [x] `User` - testy właściwości obliczanych, relacji, walidacji
- [x] `Department` - testy hierarchii, rekursji, właściwości obliczanych
- [x] `Team` - testy statusów, członków, kanałów, archiwizacji
- [x] `TeamMember` - testy ról, relacji
- [x] `Channel` - testy statusów, archiwizacji
- [x] `TeamTemplate` - testy placeholderów, generowania nazw
- [x] `SchoolType` - testy właściwości obliczanych
- [x] `SchoolYear` - testy dat, właściwości obliczanych
- [x] `Subject` - testy relacji z nauczycielami
- [x] `UserSchoolType` - testy dat aktywności
- [x] `UserSubject` - testy przypisań
- [x] `OrganizationalUnit` - testy hierarchii
- [x] `OperationHistory` - testy statusów, postępu
- [x] `ApplicationSetting` - testy typów, walidacji, konwersji
- [ ] `BulkOperationResult` - testy wyników operacji masowych
- [ ] `HealthOperationResult` - testy monitorowania zdrowia
- [ ] `ApiResponses` - testy modeli API

#### 1.2 Enums - Testy Enumów
- [x] `UserRole` - wartości, konwersje
- [x] `TeamStatus` - wartości, konwersje  
- [x] `TeamMemberRole` - wartości, konwersje
- [x] `OperationStatus` - wartości, konwersje
- [x] `OperationType` - wartości, konwersje
- [x] `ChannelStatus` - wartości, konwersje
- [x] `SettingType` - wartości, konwersje
- [ ] `TeamVisibility` - wartości, konwersje
- [ ] `HealthStatus` - wartości, konwersje

#### 1.3 Extensions - Testy Rozszerzeń
- [x] `EnumExtensions` - testy polskich tłumaczeń
- [ ] `GraphServiceExtensions` - testy rozszerzeń Graph API

#### 1.4 Services - Testy Serwisów Biznesowych
- [ ] `UserService` - CRUD, relacje, synchronizacja Graph
- [ ] `TeamService` - CRUD, członkowie, archiwizacja, synchronizacja
- [ ] `DepartmentService` - CRUD, hierarchia
- [ ] `ChannelService` - CRUD, archiwizacja
- [ ] `SchoolTypeService` - CRUD, przypisania
- [ ] `SchoolYearService` - CRUD, daty aktywności
- [ ] `SubjectService` - CRUD, nauczyciele
- [ ] `TeamTemplateService` - CRUD, generowanie, placeholdery
- [ ] `OrganizationalUnitService` - CRUD, hierarchia
- [ ] `OperationHistoryService` - CRUD, filtrowanie
- [ ] `ApplicationSettingService` - CRUD, walidacja, typy
- [ ] `ModernHttpService` - HTTP, Graph API, retry logic
- [ ] `GraphAdminNotificationService` - powiadomienia
- [ ] `SeedDataService` - dane inicjalne

#### 1.5 Graph Services
- [ ] `GraphConnectionService` - połączenia, autoryzacja
- [ ] `GraphUserService` - operacje na użytkownikach
- [ ] `GraphTeamService` - operacje na zespołach
- [ ] `GraphBulkOperationsService` - operacje masowe
- [ ] `GraphCacheService` - cache Graph API

#### 1.6 Helpers & Utilities
- [ ] `AuditHelper` - audyt operacji
- [ ] `GraphModelMapper` - mapowanie modeli
- [ ] `CircuitBreaker` - circuit breaker pattern
- [ ] `ModernCircuitBreaker` - nowoczesny circuit breaker

### 2. WARSTWA APPLICATION (TeamsManager.Application)

#### 2.1 Orchestrators - Testy Orkiestratorów
- [x] `ReportingOrchestrator` - generowanie raportów, procesy
- [ ] `BulkUserManagementOrchestrator` - operacje masowe użytkowników
- [ ] `DataImportOrchestrator` - import danych
- [ ] `HealthMonitoringOrchestrator` - monitorowanie zdrowia
- [ ] `SchoolYearProcessOrchestrator` - procesy roku szkolnego
- [ ] `TeamLifecycleOrchestrator` - cykl życia zespołów
- [ ] `TeamsManagerHealthOrchestrator` - ogólne monitorowanie

### 3. WARSTWA DATA (TeamsManager.Data)

#### 3.1 Repositories - Testy Repozytoriów
- [x] `GenericRepository<T>` - CRUD, filtrowanie, paginacja
- [x] `UserRepository` - specjalizowane zapytania użytkowników
- [x] `TeamRepository` - specjalizowane zapytania zespołów
- [x] `DepartmentRepository` - hierarchia działów
- [x] `ApplicationSettingRepository` - ustawienia
- [x] `OperationHistoryRepository` - historia operacji
- [x] `SchoolYearRepository` - lata szkolne
- [x] `SubjectRepository` - przedmioty i nauczyciele
- [x] `TeamTemplateRepository` - szablony zespołów
- [x] `TeamMemberRepository` - członkowie zespołów
- [x] `ChannelRepository` - kanały

#### 3.2 DbContext - Testy Kontekstu Bazy
- [ ] `TeamsManagerDbContext` - konfiguracja, relacje, audyt
- [ ] `EfUnitOfWork` - transakcje, rollback

#### 3.3 Migrations - Testy Migracji
- [ ] Testy integralności migracji
- [ ] Testy kompatybilności wstecznej

### 4. WARSTWA API (TeamsManager.Api)

#### 4.1 Controllers - Testy Kontrolerów
- [x] `UsersController` - CRUD, autoryzacja, tokeny
- [x] `TeamsController` - CRUD, członkowie, autoryzacja
- [x] `DepartmentsController` - CRUD, hierarchia
- [x] `ChannelsController` - CRUD, autoryzacja
- [x] `SchoolTypesController` - CRUD
- [ ] `SchoolYearsController` - CRUD, aktywność
- [ ] `SubjectsController` - CRUD, nauczyciele
- [ ] `TeamTemplatesController` - CRUD, generowanie
- [ ] `ApplicationSettingsController` - CRUD, walidacja
- [ ] `OperationHistoriesController` - historia, filtrowanie
- [ ] `BulkUserManagementController` - operacje masowe
- [ ] `DataImportController` - import danych
- [ ] `HealthMonitoringController` - monitorowanie
- [ ] `ReportingController` - raporty
- [ ] `TeamLifecycleController` - cykl życia zespołów
- [ ] `SchoolYearProcessController` - procesy roku szkolnego
- [ ] `DiagnosticsController` - diagnostyka
- [x] `TestAuthController` - testy autoryzacji

#### 4.2 Authorization - Testy Autoryzacji
- [x] `JwtAuthenticationTests` - JWT, tokeny, claims
- [ ] `ApiAuthConfig` - konfiguracja OAuth
- [ ] Middleware autoryzacji
- [ ] Policy-based authorization

#### 4.3 Extensions & Middleware
- [x] `HttpContextExtensions` - ekstraktowanie tokenów
- [ ] Error handling middleware
- [ ] Request/Response logging

#### 4.4 Health Checks
- [ ] `DependencyInjectionHealthCheck` - DI container
- [ ] `GraphConnectionHealthCheck` - połączenie Graph API
- [ ] Health check endpoints

#### 4.5 Hubs - SignalR
- [x] `NotificationHub` - powiadomienia real-time
- [ ] `MonitoringHub` - monitorowanie real-time

### 5. WARSTWA UI (TeamsManager.UI)

#### 5.1 Services - Testy Serwisów UI
- [x] `MsalAuthService` - autoryzacja MSAL
- [ ] `TeamsManagerApiService` - komunikacja z API
- [ ] `GraphUserProfileService` - profile użytkowników
- [ ] `MonitoringDataService` - dane monitorowania
- [ ] `TeamsManagerMonitoringService` - serwis monitorowania
- [ ] `ManualTestingService` - testy manualne
- [ ] `ApplicationSettingService` - ustawienia UI
- [ ] `ConditionalAccessAnalyzer` - analiza dostępu
- [ ] `SignalRService` - SignalR w UI
- [ ] `SimpleUserService` - prosty serwis użytkowników
- [ ] `UserSynchronizationService` - synchronizacja użytkowników
- [ ] `UIDialogService` - dialogi UI

#### 5.2 ViewModels - Testy ViewModels
- [ ] `BaseViewModel` - bazowa funkcjonalność
- [ ] `LoginViewModel` - logowanie
- [ ] `DashboardViewModel` - dashboard
- [ ] `DepartmentsManagementViewModel` - zarządzanie działami
- [ ] `MonitoringDashboardViewModel` - dashboard monitorowania
- [ ] `BulkImportWizardViewModel` - kreator importu
- [ ] Wszystkie inne ViewModels

#### 5.3 Converters - Testy Konwerterów
- [ ] `BooleanToVisibilityConverter`
- [ ] `BooleanToOpacityConverter`
- [ ] `BooleanToYesNoConverter`
- [ ] Wszystkie inne konwertery (28+ plików)

#### 5.4 UserControls - Testy Kontrolek
- [ ] `BulkOperationsToolbar`
- [ ] `ChannelCard`
- [ ] `SettingEditorControl`
- [ ] `TemplatePreviewControl`
- [ ] Import controls
- [ ] Teams controls

### 6. TESTY INTEGRACYJNE

#### 6.1 End-to-End Workflows
- [ ] Kompletny cykl życia użytkownika
- [ ] Kompletny cykl życia zespołu
- [ ] Import i synchronizacja danych
- [ ] Operacje masowe
- [ ] Procesy roku szkolnego

#### 6.2 API Integration Tests
- [x] `NotificationHubIntegrationTests` - SignalR
- [ ] Testy całych kontrolerów z bazą danych
- [ ] Testy autoryzacji end-to-end
- [ ] Testy Graph API integration

#### 6.3 Database Integration Tests
- [ ] Testy migracji
- [ ] Testy wydajności zapytań
- [ ] Testy integralności danych
- [ ] Testy transakcji

### 7. TESTY WYDAJNOŚCIOWE

#### 7.1 Performance Tests
- [x] `RepositoryPerformanceTests` - wydajność repozytoriów
- [ ] Testy wydajności serwisów
- [ ] Testy wydajności API
- [ ] Testy obciążeniowe Graph API
- [ ] Testy pamięci i GC

#### 7.2 Load Tests
- [ ] Testy obciążeniowe API
- [ ] Testy równoległości
- [ ] Testy cache'owania
- [ ] Testy circuit breaker

### 8. TESTY BEZPIECZEŃSTWA

#### 8.1 Security Tests
- [ ] Testy autoryzacji i uwierzytelniania
- [ ] Testy injection attacks
- [ ] Testy CSRF protection
- [ ] Testy rate limiting
- [ ] Testy token validation

### 9. TESTY KONFIGURACJI

#### 9.1 Configuration Tests
- [x] `ApiAuthConfigTests` - konfiguracja OAuth
- [ ] Testy walidacji konfiguracji
- [ ] Testy różnych środowisk
- [ ] Testy User Secrets
- [ ] Testy Azure Key Vault

### 10. MOCK & TEST INFRASTRUCTURE

#### 10.1 Test Helpers
- [x] `TestCurrentUserService` - mock current user
- [x] `TestDbContext` - test database context
- [x] `IntegrationTestBase` - baza testów integracyjnych
- [ ] Mock Graph API services
- [ ] Test data builders
- [ ] Custom assertions

## Metryki Pokrycia

### Obecny Stan (z analizy)
- **Modele**: ~80% pokryte
- **Enumy**: ~70% pokryte  
- **Repozytoria**: ~90% pokryte
- **Kontrolery**: ~30% pokryte
- **Serwisy Core**: ~10% pokryte
- **Serwisy UI**: ~5% pokryte
- **Orkiestratorzy**: ~15% pokryte

### Cel Docelowy
- **Ogólne pokrycie**: 90%+
- **Krytyczne ścieżki**: 100%
- **Modele biznesowe**: 95%+
- **API endpoints**: 90%+
- **Serwisy**: 85%+

## Priorytety Implementacji

### Faza 1 (Krytyczne)
1. Kompletne testy serwisów Core
2. Brakujące testy kontrolerów API
3. Testy orkiestratorów Application
4. Testy autoryzacji i bezpieczeństwa

### Faza 2 (Ważne)
1. Testy serwisów UI
2. Testy ViewModels
3. Testy integracyjne end-to-end
4. Testy wydajnościowe

### Faza 3 (Uzupełniające)
1. Testy kontrolek UI
2. Testy konwerterów
3. Testy konfiguracji
4. Testy bezpieczeństwa zaawansowane 