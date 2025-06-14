# Plan Refaktoryzacji PowerShell → Graph API

## 📋 **Analiza Obecnego Stanu**

### **🔍 Zidentyfikowane Komponenty PowerShell**

#### **1. Serwisy PowerShell (Core)**
- `PowerShellConnectionService` - zarządzanie połączeniem i runspace
- `PowerShellTeamManagementService` - operacje na zespołach Teams
- `PowerShellUserManagementService` - zarządzanie użytkownikami
- `PowerShellBulkOperationsService` - operacje masowe
- `PowerShellCacheService` - cache dla PowerShell
- `PowerShellUserResolverService` - rozwiązywanie ID użytkowników
- `PowerShellService` - fasada główna

#### **2. Interfejsy PowerShell**
- `IPowerShellConnectionService`
- `IPowerShellTeamManagementService` 
- `IPowerShellUserManagementService`
- `IPowerShellBulkOperationsService`
- `IPowerShellCacheService`
- `IPowerShellUserResolverService`
- `IPowerShellService`

#### **3. Modele PowerShell**
- `PowerShellDiagnosticInfo`
- `PowerShellPermissionInfo`
- `PowerShellModuleStatus`
- `PowerShellModuleInstallationResult`
- `PowerShellConnectionTestResult`
- `ConnectionHealthInfo`

#### **4. Wyjątki PowerShell**
- `PowerShellConnectionException`
- `PowerShellCommandExecutionException`

#### **5. Helpery PowerShell**
- `PowerShellCommandBuilder`
- `PSParameterValidator`
- `PSObjectMapper`

#### **6. Serwisy Używające PowerShell**
- `ChannelService` - używa `IPowerShellService`
- `GraphAdminNotificationService` - używa `IPowerShellService`
- `OrganizationalUnitService` - używa `IPowerShellCacheService`

#### **7. UI/API Integracje**
- `TeamsManagerApiService` - endpointy diagnostyczne PowerShell
- `TeamsManagerMonitoringService` - monitoring PowerShell
- `DiagnosticsController` - API endpointy PowerShell

### **🎯 Istniejące Komponenty Graph API**

#### **Gotowe do Wykorzystania:**
- `IModernHttpService` - HTTP client z resilience dla Graph API
- `TokenManager` - zarządzanie tokenami OBO flow
- `GraphUserProfileService` - przykład implementacji Graph API
- `IConfidentialClientApplication` - MSAL dla Graph API
- Konfiguracja HttpClient z resilience patterns

---

## 🚀 **TASKI REFAKTORYZACJI**

### **ETAP 1: Przygotowanie Infrastruktury Graph API** ⏱️ **2 dni**

#### **1.1 Stworzenie Interfejsów Graph**
- [x] **TASK 1.1.1:** Utworzyć folder `TeamsManager.Core/Abstractions/Services/Graph/`
- [x] **TASK 1.1.2:** Utworzyć `IGraphTeamManagementService.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Interfejs używa modeli GraphTeam, GraphUser, GraphChannel, GraphDiagnosticInfo, GraphTeamMember (do utworzenia w ETAP 1.2)
  - Wszystkie metody mają dokumentację z endpointami Graph API
  - Zachowano kompatybilność sygnatur z IPowerShellTeamManagementService ale zastąpiono PSObject → GraphTeam/GraphChannel/GraphTeamMember
  - Dodano metody AddTeamMemberAsync i RemoveTeamMemberAsync (nie było w PowerShell interface)
  - Zastąpiono Collection<PSObject> → List<GraphTeam> dla lepszej type safety
  - Wszystkie Graph API endpoints są udokumentowane w komentarzach metod
  - Dodano GetGraphVersionAsync zamiast GetPowerShellVersionAsync
- [x] **TASK 1.1.3:** Utworzyć `IGraphUserManagementService.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Interfejs używa modeli GraphUser, GraphLicense, GraphTeamMember (do utworzenia w ETAP 1.2)
  - Wszystkie metody mają dokumentację z endpointami Graph API
  - Zachowano kompatybilność sygnatur z IPowerShellUserManagementService ale zastąpiono PSObject → GraphUser/GraphLicense/GraphTeamMember
  - Dodano metodę RevokeUserSignInSessionsAsync (nowa funkcjonalność Graph API)
  - Zastąpiono Collection<PSObject> → List<GraphUser> dla lepszej type safety
  - Wszystkie Graph API endpoints są udokumentowane w komentarzach metod
  - Dodano zaawansowane filtry OData dla operacji wyszukiwania
- [x] **TASK 1.1.4:** Utworzyć `IGraphBulkOperationsService.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Interfejs używa modeli GraphBulkResult, GraphBatchOperation, BulkOperationProgress, GraphRateLimitStatus (do utworzenia w ETAP 1.2)
  - Wszystkie metody mają dokumentację z endpointami Graph Batch API (POST /v1.0/$batch)
  - Zachowano kompatybilność sygnatur z IPowerShellBulkOperationsService ale dodano IProgress<BulkOperationProgress>
  - Dodano nowe funkcjonalności: rate limiting, progress tracking, synchronizację członkostwa (SynchronizeTeamMembershipAsync)
  - Batch size ograniczony do 20 (limit Graph API) zamiast 50 (PowerShell)
  - Dodano sekcję Rate Limiting & Batch Management z GetRateLimitStatusAsync i ExecuteBatchOperationsAsync
  - Wszystkie Graph API endpoints są udokumentowane w komentarzach metod
  - Zastąpiono BulkOperationResult → GraphBulkResult dla Graph API specyfiki
- [x] **TASK 1.1.5:** Utworzyć `IGraphConnectionService.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Interfejs używa modeli GraphConnectionHealthInfo, GraphPermissionInfo, GraphDiagnosticInfo, GraphApiAvailability, GraphUserContext, GraphConnectionTestResult, GraphRateLimitStatus, GraphBatchResponse, GraphBatchRequest, GraphApiError (do utworzenia w ETAP 1.2)
  - Wszystkie metody mają dokumentację z endpointami Graph API
  - Zachowano kompatybilność sygnatur z IPowerShellConnectionService
  - Dodano nowe funkcjonalności: batch requests, rate limiting monitoring, endpoint availability checking
  - Usunięto PowerShell-specific metody (moduły, runspace)
- [x] **TASK 1.1.6:** Utworzyć `IGraphCacheService.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Interfejs używa modeli GraphCacheMetadata, GraphCacheMetrics, GraphCacheValidationResult, GraphCacheRateLimitInfo, GraphRateLimitInfo (do utworzenia w ETAP 1.2)
  - Wszystkie metody mają dokumentację z endpointami Graph API
  - Zachowano kompatybilność sygnatur z IPowerShellCacheService ale dodano Graph API specyfikę
  - Dodano nowe funkcjonalności: ETag support, rate limiting integration, Graph API specific cache patterns
  - Dodano długie opcje cache dla danych rzadko zmieniających się w Graph API (GetShortTermCacheOptions, GetMediumTermCacheOptions, GetLongTermCacheOptions)
  - Dodano sekcję Rate Limiting Integration z CanMakeGraphRequest, SetRateLimitInfo, GetRateLimitInfo
  - Dodano sekcję Cache Validation & ETag Support z ValidateCache, UpdateETag, IsCacheExpired
  - Rozszerzone metody cache z Graph API metadanymi: TryGetValueWithMetadata, Set z etag i rateLimitInfo
- [x] **TASK 1.1.7:** Utworzyć `IGraphService.cs` (fasada)
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Interfejs używa modeli GraphOperationResult, GraphServiceMetrics, GraphCacheWarmupResult, GraphCacheWarmupOptions, GraphServiceConfiguration (do utworzenia w ETAP 1.2)
  - Wszystkie metody mają dokumentację z endpointami Graph API
  - Zachowano kompatybilność sygnatur z IPowerShellService ale zastąpiono PowerShellDiagnosticInfo → GraphDiagnosticInfo
  - Dodano nowe funkcjonalności: batch requests, rate limiting, cache warming, performance metrics
  - Fasada agreguje wszystkie Graph API services: Teams, Users, BulkOperations, Connection, Cache
  - Dodano sekcje: Performance & Monitoring, Cache Management, Diagnostics & Health Check, Configuration & Settings
  - ExecuteWithAutoConnectAsync zwraca GraphOperationResult<T> zamiast T? dla lepszej obsługi błędów
  - Dodano ExecuteBatchOperationAsync dla operacji Graph Batch API
  - Dodano zaawansowane zarządzanie konfiguracją: UpdateConfiguration, GetConfiguration, IsConfigurationValid

#### **1.2 Stworzenie Modeli Graph**
- [x] **TASK 1.2.1:** Utworzyć folder `TeamsManager.Core/Models/Graph/`
- [x] **TASK 1.2.2:** Utworzyć `GraphDiagnosticInfo.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Model zawiera GraphDiagnosticInfo, GraphRateLimitInfo, GraphHealthStatus
  - Zachowano kompatybilność z PowerShellDiagnosticInfo (te same właściwości)
  - Dodano Graph API specific properties: GraphApiVersion, TenantId, ApplicationId, RateLimitInfo, ResponseTimeMs
  - Dodano metodę GetDetailedReport() dla szczegółowej diagnostyki
  - Usunięto PowerShell-specific properties (RunspaceState, RunspaceReady, BasicCommandTest)
  - ZAIMPLEMENTOWANO: Kompletny model z wszystkimi wymaganymi właściwościami, GraphRateLimitInfo reference, GraphHealthStatus enum, szczegółowy GetDetailedReport() z sekcjami błędów, ostrzeżeń i informacji dodatkowych
- [x] **TASK 1.2.3:** Utworzyć `GraphPermissionInfo.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Model zawiera GraphPermissionInfo i GraphPermissionScopes (static class)
  - Zachowano kompatybilność z PowerShellPermissionInfo
  - Dodano Graph API specific properties: TenantName, ApplicationId, AuthenticationType, TokenExpiresAt
  - Dodano zaawansowane funkcje: PermissionCompleteness, PermissionStatus, token expiry checks
  - GraphPermissionScopes zawiera wszystkie wymagane uprawnienia Graph API dla aplikacji
  - Dodano metody pomocnicze: HasPermission(), HasPermissions(), HasAnyPermission()
  - Dodano szczegółowy raport uprawnień GetPermissionReport()
  - ZAIMPLEMENTOWANO: Kompletny model z enum PermissionStatus, GraphPermissionScopes z RequiredPermissions i OptionalPermissions, właściwości obliczane PermissionCompleteness i Status, metody weryfikacji uprawnień, szczegółowy GetPermissionReport() z sekcjami przypisanych i brakujących uprawnień
- [x] **TASK 1.2.4:** Utworzyć `GraphConnectionTestResult.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Model zawiera GraphConnectionTestResult i GraphEndpointTestResult
  - Zachowano kompatybilność z PowerShellConnectionTestResult
  - Dodano Graph API specific tests: GraphApiAvailabilityTest, GraphAuthenticationTest, TeamReadTest, RateLimitTest
  - Usunięto PowerShell-specific test: RunspaceTest
  - Dodano zaawansowane funkcje: EndpointTestResults, RateLimitInfo, AverageResponseTimeMs, WarningMessages
  - Dodano performance i rate limit monitoring: HasPerformanceIssues, HasRateLimitIssues
  - Rozbudowano GetDetailedResult() o szczegółowe sekcje diagnostyczne z rekomendacjami
  - ZAIMPLEMENTOWANO: Kompletny model z GraphEndpointTestResult, właściwości obliczane HasPerformanceIssues i HasRateLimitIssues, SuccessRate, szczegółowy GetDetailedResult() z sekcjami wydajności, rate limiting, wyników testów endpointów, ostrzeżeń, błędów i rekomendacji
- [x] **TASK 1.2.5:** Utworzyć `GraphOperationResult.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Model zawiera GraphOperationResult, GraphOperationSuccess, GraphOperationError, GraphServiceMetrics, GraphCacheWarmupOptions, GraphCacheWarmupResult
  - Zachowano kompatybilność z BulkOperationResult (Success, IsSuccess, ErrorMessage, SuccessfulOperations, Errors)
  - Dodano Graph API specific properties: GraphEndpoint, HttpMethod, HttpStatusCode, RequestId, ErrorCode, ErrorDetails
  - Dodano zaawansowane funkcje: batch operations, cache support (FromCache, ETag), retry logic (WasRetried, RetryCount)
  - Dodano performance monitoring: HasPerformanceIssues, HasRateLimitIssues, ShouldRetry
  - Dodano static factory methods: CreateSuccess(), CreateError(), CreateFromCache(), CreateBatchResult()
  - Dodano utility methods: GetDetailedResult(), GetSummary(), AddMetadata(), GetMetadata()
  - Implicit operator bool dla kompatybilności z istniejącym kodem
  - ZAIMPLEMENTOWANO: Kompletny generyczny model (19.5KB, 535 linii) z GraphOperationResult<T>, GraphOperationSuccess, GraphOperationError, GraphServiceMetrics, GraphCacheWarmupOptions, GraphCacheWarmupResult, właściwości obliczane HasPerformanceIssues/HasRateLimitIssues/ShouldRetry, static factory methods, szczegółowy GetDetailedResult() z sekcjami cache, retry, błędów, metryk, implicit operator bool
- [x] **TASK 1.2.6:** Utworzyć `GraphTeam.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Model zawiera GraphTeam, GraphTeamSettings, GraphTeamGuestSettings, GraphTeamMemberSettings, GraphTeamMessagingSettings, GraphTeamFunSettings, GraphTeamDiscoverySettings, GraphTeamMember, GraphSyncInfo
  - Zachowano kompatybilność z Team.cs (DisplayName, Description, IsActive, MemberCount, OwnerCount)
  - Dodano Graph API specific properties: Id (Group ID), Mail, MailNickname, WebUrl, PhotoUrl, Classification, ETag
  - Dodano pełne ustawienia zespołu zgodne z Graph API: Settings, GuestSettings, MemberSettings, MessagingSettings, FunSettings, DiscoverySettings
  - Dodano metody konwersji: ToLocalTeam(), FromLocalTeam() dla integracji z istniejącym kodem
  - Dodano metody pomocnicze: HasMember(), HasOwner(), GetMember(), GetChannel(), GetSummary()
  - Dodano GraphSyncInfo dla śledzenia synchronizacji z Graph API
  - Wszystkie właściwości nullable dla elastyczności Graph API responses
  - ZAIMPLEMENTOWANO: Kompletny model (17.4KB, 460 linii) z GraphTeam i wszystkimi klasami pomocniczymi, zachowano kompatybilność z lokalnym Team.cs, dodano konwersje ToLocalTeam()/FromLocalTeam(), metody pomocnicze, pełne ustawienia zespołu Graph API, GraphSyncInfo z IsSynchronized property, wszystkie właściwości nullable
- [x] **TASK 1.2.7:** Utworzyć `GraphUser.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Model zawiera GraphUser, GraphLicense, GraphServicePlan
  - Zachowano kompatybilność z User.cs (FirstName/GivenName, LastName/Surname, UPN/UserPrincipalName, IsActive, FullName)
  - Dodano Graph API specific properties: Id (Object ID), Mail, MailNickname, UserType, AccountEnabled, CreatedDateTime, LastSignInDateTime
  - Dodano pełne informacje organizacyjne: JobTitle, Department, CompanyName, OfficeLocation, Manager, DirectReports
  - Dodano zarządzanie licencjami: AssignedLicenses, ServicePlans, LicenseType
  - Dodano metody konwersji: ToLocalUser(), FromLocalUser() dla integracji z istniejącym kodem
  - Dodano metody pomocnicze: HasLicense(), HasAdminRole(), IsMemberOfGroup(), GetLicense()
  - Dodano zaawansowane właściwości obliczane: ActivityStatus, DaysSinceLastSignIn, IsRecentlyActive
  - Dodano utility methods: GetSummary(), GetDetailedInfo()
  - Wszystkie właściwości nullable dla elastyczności Graph API responses
  - ZAIMPLEMENTOWANO: Kompletny model (18.3KB, 400 linii) z GraphUser, GraphLicense, GraphServicePlan, zachowano kompatybilność z lokalnym User.cs poprzez właściwości obliczane FirstName/LastName/UPN/IsActive, pełne informacje organizacyjne z Manager/DirectReports, zarządzanie licencjami, metody konwersji ToLocalUser()/FromLocalUser(), zaawansowane właściwości obliczane ActivityStatus/DaysSinceLastSignIn/IsRecentlyActive, szczegółowy GetDetailedInfo() z informacjami o licencjach
- [x] **TASK 1.2.8:** Utworzyć `GraphChannel.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Model zawiera GraphChannel, GraphChannelSettings, GraphChannelStats, GraphChannelMember, GraphChannelTab
  - Zachowano kompatybilność z Channel.cs (DisplayName, Description, IsActive, IsPrivate, IsGeneral, IsReadOnly)
  - Dodano Graph API specific properties: Id (Channel ID), TeamId, Email, WebUrl, ETag, CreatedDateTime, TenantId
  - Dodano pełne ustawienia kanału: Settings (GraphChannelSettings), Stats (GraphChannelStats)
  - Dodano członków kanału prywatnego: Members (GraphChannelMember) z endpoint GET /v1.0/teams/{team-id}/channels/{channel-id}/members
  - Dodano karty kanału: Tabs (GraphChannelTab) z endpoint GET /v1.0/teams/{team-id}/channels/{channel-id}/tabs
  - Dodano metody konwersji: ToLocalChannel(), FromLocalChannel() dla integracji z istniejącym kodem
  - Dodano metody pomocnicze: HasMember(), GetMember(), GetTab(), CanBeDeleted(), GetDeletionBlockReason()
  - Dodano utility methods: GetSummary(), GetDetailedInfo()
  - Wszystkie właściwości nullable dla elastyczności Graph API responses
  - MembershipType zgodny z Graph API: standard, private, unknownFutureValue
  - ZAIMPLEMENTOWANO: Kompletny model (18.7KB, 488 linii) z GraphChannel, GraphChannelSettings, GraphChannelStats, GraphChannelMember, GraphChannelTab, zachowano kompatybilność z lokalnym Channel.cs poprzez właściwości obliczane IsPrivate z MembershipType, konwersje ToLocalChannel()/FromLocalChannel() z mapowaniem statystyk, metody CanBeDeleted()/GetDeletionBlockReason() z regułami biznesowymi, szczegółowy GetDetailedInfo() z sekcjami statystyk, członków i kart
- [x] **TASK 1.2.9:** Utworzyć `GraphBulkResult.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Model zawiera GraphBulkResult, GraphBulkOperationSuccess, GraphBulkOperationError, GraphBatchOperationResult
  - Zachowano kompatybilność z BulkOperationResult (Success, IsSuccess, ErrorMessage, SuccessfulOperations, Errors)
  - Dodano Graph API specific properties: RequestId, BatchId, GraphEndpoint, HttpMethod, HttpStatusCode, RateLimitInfo
  - Dodano zaawansowane funkcje: batch operations (BatchResults), cache support (FromCache, ETag), retry logic (WasRetried, RetryCount)
  - Dodano performance monitoring: HasPerformanceIssues, HasRateLimitIssues, ShouldRetry
  - Dodano static factory methods: CreateSuccess(), CreateError(), CreateFromCache(), CreateBatchResult()
  - Dodano utility methods: AddSuccess(), AddError(), AddMetadata(), GetMetadata(), GetDetailedResult(), GetSummary()
  - Implicit operator bool dla kompatybilności z istniejącym kodem
  - Pełne wsparcie dla Graph Batch API (POST /v1.0/$batch) z GraphBatchOperationResult
  - Wszystkie właściwości nullable dla elastyczności Graph API responses
  - Rozbudowane błędy z ErrorCode, ErrorDetails, RequestId dla lepszego debugowania
  - ZAIMPLEMENTOWANO: Kompletny model (15.9KB, 432 linie) z GraphBulkResult, GraphBulkOperationSuccess, GraphBulkOperationError, GraphBatchOperationResult, zachowano kompatybilność z BulkOperationResult, pełne wsparcie Graph Batch API z CreateBatchResult(), właściwości obliczane HasPerformanceIssues/HasRateLimitIssues/ShouldRetry, static factory methods, szczegółowy GetDetailedResult() z sekcjami cache, retry, błędów, batch results, implicit operator bool

#### **1.3 Rozszerzenie ModernHttpService**
- [x] **TASK 1.3.1:** Dodać metody Teams API do `IModernHttpService`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Dodano metody PATCH i DELETE do podstawowego interfejsu IModernHttpService
  - Dodano kompletny zestaw metod Teams API: CreateTeamAsync, UpdateTeamAsync, GetTeamAsync, GetAllTeamsAsync, ArchiveTeamAsync, UnarchiveTeamAsync, DeleteTeamAsync
  - Dodano metody zarządzania członkami zespołu: GetTeamMembersAsync, AddTeamMemberAsync, RemoveTeamMemberAsync
  - Dodano metody zarządzania kanałami: GetTeamChannelsAsync, CreateTeamChannelAsync, UpdateTeamChannelAsync, DeleteTeamChannelAsync, GetTeamChannelAsync
  - Wszystkie metody używają odpowiednich endpointów Graph API (v1.0/teams, v1.0/groups)
  - Implementacja wykorzystuje istniejące resilience patterns z Microsoft.Extensions.Http.Resilience
  - Dodano walidację argumentów i szczegółowe logowanie dla wszystkich operacji
  - Metody są generyczne (TRequest, TResponse) dla elastyczności z różnymi modelami danych
  - ZAIMPLEMENTOWANO: Rozszerzony interfejs IModernHttpService o PatchToGraphAsync(), DeleteFromGraphAsync() oraz 13 metod Teams API (zespoły, członkowie, kanały), wszystkie z generycznymi typami TRequest/TResponse, specyficznymi endpointami Graph API, szczegółową dokumentacją endpoint
- [x] **TASK 1.3.2:** Dodać metody Users API do `IModernHttpService`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Dodano kompletny zestaw metod Users API: CreateUserAsync, UpdateUserAsync, GetUserAsync, GetAllUsersAsync, DeleteUserAsync
  - Dodano metody zarządzania licencjami: AssignUserLicenseAsync, GetUserLicensesAsync
  - Dodano metody bezpieczeństwa: RevokeUserSignInSessionsAsync
  - Dodano metody filtrowania: GetUsersByDepartmentAsync, GetInactiveUsersAsync
  - Dodano metody relacji: GetUserTeamsAsync (członkostwo w zespołach)
  - Wszystkie metody używają odpowiednich endpointów Graph API (v1.0/users)
  - Implementacja obsługuje filtry OData z Uri.EscapeDataString dla bezpieczeństwa
  - Dodano walidację argumentów (userId, department, daysInactive > 0)
  - Metody GetInactiveUsersAsync używają ISO 8601 format daty dla Graph API
  - Wszystkie metody są generyczne dla elastyczności z różnymi modelami danych
  - ZAIMPLEMENTOWANO: Dodano 10 metod Users API (CRUD, licencje, bezpieczeństwo, filtrowanie, relacje), wszystkie z pełną dokumentacją endpointów Graph API, walidację parametrów, obsługę filtrów OData
- [x] **TASK 1.3.3:** Dodać metody Groups API do `IModernHttpService`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Dodano kompletny zestaw metod Groups API: CreateGroupAsync, UpdateGroupAsync, GetGroupAsync, GetAllGroupsAsync, DeleteGroupAsync
  - Dodano metody zarządzania członkami: GetGroupMembersAsync, AddGroupMemberAsync, RemoveGroupMemberAsync
  - Dodano metody zarządzania właścicielami: GetGroupOwnersAsync, AddGroupOwnerAsync, RemoveGroupOwnerAsync
  - Dodano metody filtrowania grup: GetMicrosoft365GroupsAsync, GetSecurityGroupsAsync, GetDistributionGroupsAsync
  - Dodano metodę sprawdzania Teams: GroupHasTeamAsync (używa try-catch dla 404 response)
  - Wszystkie metody używają odpowiednich endpointów Graph API (v1.0/groups)
  - Implementacja używa /$ref endpoints dla dodawania/usuwania członków i właścicieli
  - Filtry OData używają poprawnych wyrażeń: groupTypes/any(c:c eq 'Unified'), securityEnabled eq true
  - Dodano walidację argumentów (groupId, userId nie mogą być null/empty)
  - Wszystkie metody są generyczne dla elastyczności z różnymi modelami danych
  - ZAIMPLEMENTOWANO: Dodano 14 metod Groups API (CRUD, członkowie, właściciele, filtrowanie, Teams relationship), wszystkie z endpointami /$ref dla zarządzania, filtrami OData, szczegółową dokumentacją
- [x] **TASK 1.3.4:** Implementować batch operations w `ModernHttpService`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Dodano kompletną implementację Graph Batch API (POST /v1.0/$batch)
  - Dodano metody parallel operations: ExecuteParallelGetRequestsAsync, ExecuteParallelPostRequestsAsync, ExecuteParallelPatchRequestsAsync, ExecuteParallelDeleteRequestsAsync
  - Implementacja automatycznie dzieli żądania na batche (domyślnie max 20 na batch)
  - Dodano bulk operations z progress reporting: ExecuteBulkUserOperationsAsync, ExecuteBulkTeamOperationsAsync
  - Bulk operations używają SemaphoreSlim do kontroli współbieżności (5 dla users, 3 dla teams)
  - Dodano specjalne operacje Teams: ARCHIVE, UNARCHIVE z automatyczną ekstrakcją Team ID
  - Implementacja obsługuje rate limiting z opóźnieniami (500ms dla Teams operations)
  - Wszystkie batch operations mają szczegółowe logowanie i error handling
  - Progress reporting używa IProgress<(int completed, int total, string currentOperation)>
  - Wyniki bulk operations zawierają: TotalOperations, SuccessfulOperations, FailedOperations, Results, Errors, CompletedAt
  - Dodano pomocniczą metodę ExtractTeamIdFromEndpoint dla operacji Teams
  - ZAIMPLEMENTOWANO: Dodano 6 metod batch operations (4 parallel requests, 2 bulk operations), wszystkie z konfigurowalnymi batch sizes, progress reporting, kontrolą współbieżności, szczegółowymi statystykami

### ✅ **ETAP 1.3 UKOŃCZONY** - Rozszerzenie ModernHttpService (4/4 tasków)

**Podsumowanie ETAPU 1.3:**
- ✅ TASK 1.3.1: Dodano metody PATCH/DELETE + 13 metod Teams API
- ✅ TASK 1.3.2: Dodano 10 metod Users API (CRUD, licencje, bezpieczeństwo, filtrowanie) 
- ✅ TASK 1.3.3: Dodano 14 metod Groups API (CRUD, członkowie, właściciele, filtrowanie)
- ✅ TASK 1.3.4: Dodano 6 metod batch operations (parallel requests + bulk operations)

**Łącznie zaimplementowano 43 nowe metody API w IModernHttpService** z pełną obsługą Graph API, generycznymi typami, szczegółową dokumentacją endpointów, walidacją parametrów, batch operations, progress reporting i rate limiting.

#### **1.4 Stworzenie Graph Exceptions**
- [x] **TASK 1.4.1:** Utworzyć folder `TeamsManager.Core/Exceptions/Graph/`
- [x] **TASK 1.4.2:** Utworzyć `GraphConnectionException.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Utworzono GraphConnectionException z pełną obsługą błędów Graph API
  - Automatyczne wykrywanie typów błędów: IsAuthenticationError, IsRateLimitError
  - Wsparcie dla retry logic z RetryAfter i GetRecommendedRetryDelay()
  - Szczegółowe właściwości: Endpoint, HttpStatusCode, GraphErrorCode, GraphErrorDetails, RequestId
  - Static factory methods: CreateAuthenticationError(), CreateRateLimitError(), CreateTimeoutError(), CreateNetworkError()
  - Metody pomocnicze: CanRetry(), GetDetailedErrorMessage()
  - Pełna obsługa Graph API error responses z Microsoft Graph
  - ZAIMPLEMENTOWANO: GraphConnectionException (8.5KB, 237 linii) z automatycznym wykrywaniem typów błędów, retry logic, szczegółowymi właściwościami, 4 static factory methods, metodami pomocnicznymi CanRetry()/GetRecommendedRetryDelay()/GetDetailedErrorMessage(), pełną obsługą różnych scenariuszy błędów połączenia Graph API
- [x] **TASK 1.4.3:** Utworzyć `GraphApiException.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Utworzono GraphApiException z pełną obsługą błędów operacji Graph API
  - Szczegółowe właściwości: Endpoint, HttpMethod, HttpStatusCode, GraphErrorCode, GraphErrorMessage, GraphErrorDetails, RequestId, CorrelationId
  - Automatyczne wykrywanie typów błędów: IsPermissionError, IsValidationError, IsNotFoundError, IsConflictError
  - Static factory methods: CreatePermissionError(), CreateValidationError(), CreateNotFoundError(), CreateConflictError(), CreateBulkOperationError()
  - System metadanych z AddMetadata() i GetMetadata<T>()
  - Metody pomocnicze: GetDetailedErrorMessage(), CanRetry(), GetRecommendedRetryDelay()
  - Wsparcie dla operacji bulk z szczegółowymi statystykami
  - Pełna obsługa różnych scenariuszy błędów Graph API
  - ZAIMPLEMENTOWANO: GraphApiException (rozszerzony do 13.2KB, 323 linie) z wszystkimi wymaganymi właściwościami (HttpMethod, CorrelationId), 6 typów wykrywania błędów, 5 static factory methods, systemem metadanych (AddMetadata/GetMetadata), metodami pomocnicznymi, obsługą bulk operations z szczegółowymi statystykami (TotalOperations, FailedOperations, SuccessfulOperations, FailureRate)
- [x] **TASK 1.4.4:** Utworzyć `GraphRateLimitException.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Utworzono GraphRateLimitException dziedziczący po GraphApiException
  - Szczegółowe właściwości rate limiting: RetryAfterSeconds, RetryAfterTimestamp, LimitType, CurrentRequestCount, MaxRequestCount, WindowSizeSeconds, WindowResetSeconds
  - Enum RateLimitType: Unknown, Standard, ServiceSpecific, ResourceSpecific, TenantLevel, ApplicationLevel, UserLevel
  - Static factory methods: CreateStandardRateLimit(), CreateServiceSpecificRateLimit(), CreateResourceSpecificRateLimit(), CreateTenantLevelRateLimit(), CreateApplicationLevelRateLimit(), CreateFromHeaders()
  - Metody pomocnicze: CanRetryNow(), GetTimeUntilRetry(), GetRecommendedRetryDelay(), GetDetailedErrorMessage()
  - Automatyczne parsowanie nagłówków HTTP rate limiting
  - Pełna obsługa różnych typów limitów Microsoft Graph API
  - ZAIMPLEMENTOWANO: GraphRateLimitException (rozszerzony do 16.8KB, 382 linie) z enum RateLimitType (7 wartości), wszystkimi wymaganymi właściwościami rate limiting, 9 static factory methods, metodami pomocnicznymi CanRetryNow()/GetTimeUntilRetry(), automatycznym obliczaniem RemainingRequests/UsagePercentage/ResetTime, szczegółowym GetDetailedErrorMessage() z sekcją rate limiting
- [x] **TASK 1.4.5:** Utworzyć `GraphValidationException.cs`
  **Ważne!!! Do zapamiętania w przyszłej implementacji:**
  - Utworzono GraphValidationException dziedziczący po GraphApiException
  - Klasa ValidationError z szczegółowymi informacjami o błędach walidacji
  - Enum ValidationType: Unknown, Required, Format, Length, Range, Unique, Reference, DataType, Pattern, BusinessRule, Multiple
  - Static factory methods: CreateRequiredFieldError(), CreateFormatError(), CreateLengthError(), CreateRangeError(), CreateUniqueError(), CreateReferenceError(), CreateMultipleErrors()
  - Metody pomocnicze: HasErrorForField(), GetErrorsForField(), GetErrorsByType(), GetDetailedErrorMessage(), GetValidationSummary()
  - Pełna obsługa walidacji danych Graph API z szczegółowymi informacjami o błędach
  - Wsparcie dla wielu błędów walidacji jednocześnie
  - ZAIMPLEMENTOWANO: GraphValidationException (22.3KB, 518 linii) z klasą ValidationError (fluent interface WithMetadata), enum ValidationType (11 wartości), 6 static factory methods, 6 metodami pomocnicznymi (HasErrorForField/GetErrorsForField/GetErrorsByType/GetDetailedErrorMessage/GetValidationSummary), obsługą wielu błędów jednocześnie, szczegółowymi metadanymi dla każdego błędu (ExpectedFormat, MinLength, MaxLength, MinValue, MaxValue, ConflictingResource, ReferencedResource)

### ✅ **ETAP 1.4 ZAKOŃCZONY** - Utworzenie wyjątków Graph API (5/5 tasków)

**Podsumowanie ETAPU 1.4:**
- ✅ Utworzono folder TeamsManager.Core/Exceptions/Graph/
- ✅ GraphConnectionException - obsługa błędów połączenia z Graph API
- ✅ GraphApiException - bazowy wyjątek dla operacji Graph API  
- ✅ GraphRateLimitException - obsługa rate limiting z pełnymi szczegółami
- ✅ GraphValidationException - obsługa błędów walidacji danych

**Wszystkie wyjątki zawierają:**
- Szczegółowe właściwości specyficzne dla Graph API
- Static factory methods dla łatwego tworzenia
- Metody pomocnicze do analizy błędów
- Pełną obsługę retry logic
- Automatyczne wykrywanie typów błędów
- System metadanych dla dodatkowych informacji

---

### **ETAP 2: Implementacja Graph Services** ⏱️ **4 dni**

#### **2.1 GraphConnectionService**
- [ ] **TASK 2.1.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphConnectionService.cs`
- [ ] **TASK 2.1.2:** Implementować zarządzanie tokenami Graph API
- [ ] **TASK 2.1.3:** Implementować diagnostykę połączenia Graph
- [ ] **TASK 2.1.4:** Implementować walidację uprawnień Graph
- [ ] **TASK 2.1.5:** Implementować health check Graph API
- [ ] **TASK 2.1.6:** Napisać testy jednostkowe dla `GraphConnectionService`

#### **2.2 GraphTeamManagementService**
- [ ] **TASK 2.2.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphTeamManagementService.cs`
- [ ] **TASK 2.2.2:** Implementować `POST /v1.0/teams` - tworzenie zespołów
- [ ] **TASK 2.2.3:** Implementować `PATCH /v1.0/teams/{id}` - aktualizacja zespołów
- [ ] **TASK 2.2.4:** Implementować `GET /v1.0/teams` - pobieranie zespołów
- [ ] **TASK 2.2.5:** Implementować `POST /v1.0/teams/{id}/channels` - tworzenie kanałów
- [ ] **TASK 2.2.6:** Implementować `POST /v1.0/teams/{id}/members` - dodawanie członków
- [ ] **TASK 2.2.7:** Implementować `DELETE /v1.0/teams/{id}/members/{userId}` - usuwanie członków
- [ ] **TASK 2.2.8:** Napisać testy jednostkowe dla `GraphTeamManagementService`

#### **2.3 GraphUserManagementService**
- [ ] **TASK 2.3.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphUserManagementService.cs`
- [ ] **TASK 2.3.2:** Implementować `POST /v1.0/users` - tworzenie użytkowników
- [ ] **TASK 2.3.3:** Implementować `PATCH /v1.0/users/{id}` - aktualizacja użytkowników
- [ ] **TASK 2.3.4:** Implementować `GET /v1.0/users` - pobieranie użytkowników
- [ ] **TASK 2.3.5:** Implementować `POST /v1.0/users/{id}/assignLicense` - przypisywanie licencji
- [ ] **TASK 2.3.6:** Implementować `POST /v1.0/users/{id}/revokeSignInSessions` - wylogowanie
- [ ] **TASK 2.3.7:** Napisać testy jednostkowe dla `GraphUserManagementService`

#### **2.4 GraphBulkOperationsService**
- [ ] **TASK 2.4.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphBulkOperationsService.cs`
- [ ] **TASK 2.4.2:** Implementować batch requests Graph API (`POST /v1.0/$batch`)
- [ ] **TASK 2.4.3:** Implementować parallel processing z rate limiting
- [ ] **TASK 2.4.4:** Implementować retry logic dla bulk operations
- [ ] **TASK 2.4.5:** Implementować progress reporting
- [ ] **TASK 2.4.6:** Napisać testy jednostkowe dla `GraphBulkOperationsService`

#### **2.5 GraphCacheService**
- [ ] **TASK 2.5.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphCacheService.cs`
- [ ] **TASK 2.5.2:** Implementować cache dla Graph API responses
- [ ] **TASK 2.5.3:** Implementować User ID resolution cache
- [ ] **TASK 2.5.4:** Implementować Team/Group metadata cache
- [ ] **TASK 2.5.5:** Implementować TTL management
- [ ] **TASK 2.5.6:** Napisać testy jednostkowe dla `GraphCacheService`

#### **2.6 GraphService (Fasada)**
- [ ] **TASK 2.6.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphService.cs`
- [ ] **TASK 2.6.2:** Implementować fasadę łączącą wszystkie Graph services
- [ ] **TASK 2.6.3:** Napisać testy jednostkowe dla `GraphService`

---

### **ETAP 3: Migracja Serwisów Domenowych** ⏱️ **3 dni**

#### **3.1 Aktualizacja ChannelService**
- [ ] **TASK 3.1.1:** Zastąpić `IPowerShellService` → `IGraphService` w `ChannelService.cs`
- [ ] **TASK 3.1.2:** Zaktualizować metody synchronizacji z Graph API
- [ ] **TASK 3.1.3:** Zmigrować cache logic na Graph
- [ ] **TASK 3.1.4:** Przetestować `ChannelService` z Graph API

#### **3.2 Aktualizacja GraphAdminNotificationService**
- [ ] **TASK 3.2.1:** Zastąpić PowerShell calls → Graph API calls w `GraphAdminNotificationService.cs`
- [ ] **TASK 3.2.2:** Użyć `IModernHttpService` dla Mail API
- [ ] **TASK 3.2.3:** Przetestować `GraphAdminNotificationService` z Graph API

#### **3.3 Aktualizacja OrganizationalUnitService**
- [ ] **TASK 3.3.1:** Zastąpić `IPowerShellCacheService` → `IGraphCacheService` w `OrganizationalUnitService.cs`
- [ ] **TASK 3.3.2:** Zaktualizować cache keys i logic
- [ ] **TASK 3.3.3:** Przetestować `OrganizationalUnitService` z Graph cache

---

### **ETAP 4: Migracja API Controllers** ⏱️ **2 dni**

#### **4.1 Aktualizacja DiagnosticsController**
- [ ] **TASK 4.1.1:** Zastąpić PowerShell diagnostics → Graph diagnostics w `DiagnosticsController.cs`
- [ ] **TASK 4.1.2:** Utworzyć endpoint `GET /api/diagnostics/graph/status`
- [ ] **TASK 4.1.3:** Utworzyć endpoint `POST /api/diagnostics/graph/test`
- [ ] **TASK 4.1.4:** Utworzyć endpoint `GET /api/diagnostics/graph/permissions`
- [ ] **TASK 4.1.5:** Przetestować nowe endpointy diagnostyczne

#### **4.2 Aktualizacja TeamLifecycleController**
- [ ] **TASK 4.2.1:** Zastąpić PowerShell operations → Graph operations w `TeamLifecycleController.cs`
- [ ] **TASK 4.2.2:** Zachować istniejące endpointy API (kompatybilność wsteczna)
- [ ] **TASK 4.2.3:** Przetestować wszystkie endpointy lifecycle

#### **4.3 Aktualizacja BulkUserManagementController**
- [ ] **TASK 4.3.1:** Zastąpić PowerShell bulk ops → Graph bulk ops w `BulkUserManagementController.cs`
- [ ] **TASK 4.3.2:** Wykorzystać Graph batch API
- [ ] **TASK 4.3.3:** Przetestować bulk operations

---

### **ETAP 5: Migracja UI Services** ⏱️ **1 dzień**

#### **5.1 Aktualizacja TeamsManagerApiService**
- [ ] **TASK 5.1.1:** Zaktualizować interfejsy diagnostyczne w `TeamsManagerApiService.cs`
- [ ] **TASK 5.1.2:** Dodać nowe metody Graph API
- [ ] **TASK 5.1.3:** Usunąć PowerShell-specific methods

#### **5.2 Aktualizacja MonitoringServices**
- [ ] **TASK 5.2.1:** Zastąpić PowerShell metrics → Graph metrics w `TeamsManagerMonitoringService.cs`
- [ ] **TASK 5.2.2:** Zaktualizować health checks w `MonitoringDataService.cs`

#### **5.3 Aktualizacja ViewModels**
- [ ] **TASK 5.3.1:** Zaktualizować button actions w `TeamsManagerHealthWidgetViewModel.cs`
- [ ] **TASK 5.3.2:** Dodać Graph-specific notifications w `TeamsManagerMetricsWidgetViewModel.cs`

---

### **ETAP 6: Aktualizacja Dependency Injection** ⏱️ **0.5 dnia**

#### **6.1 Nowa Rejestracja Serwisów**
- [ ] **TASK 6.1.1:** Utworzyć `TeamsManager.Core/Extensions/GraphServiceExtensions.cs`
- [ ] **TASK 6.1.2:** Implementować `AddGraphServices()` extension method
- [ ] **TASK 6.1.3:** Zarejestrować wszystkie Graph services w DI

#### **6.2 Aktualizacja Program.cs**
- [ ] **TASK 6.2.1:** Zastąpić `AddPowerShellServices()` → `AddGraphServices()` w `TeamsManager.Api/Program.cs`
- [ ] **TASK 6.2.2:** Zastąpić `AddPowerShellServices()` → `AddGraphServices()` w `TeamsManager.UI/App.xaml.cs`
- [ ] **TASK 6.2.3:** Zachować istniejące HttpClient configurations

---

### **ETAP 7: Testy i Walidacja** ⏱️ **3 dni**

#### **7.1 Testy Jednostkowe Graph Services**
- [ ] **TASK 7.1.1:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphConnectionServiceTests.cs`
- [ ] **TASK 7.1.2:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphTeamManagementServiceTests.cs`
- [ ] **TASK 7.1.3:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphUserManagementServiceTests.cs`
- [ ] **TASK 7.1.4:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphBulkOperationsServiceTests.cs`
- [ ] **TASK 7.1.5:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphCacheServiceTests.cs`

#### **7.2 Testy Integracyjne**
- [ ] **TASK 7.2.1:** Napisać testy z prawdziwym Graph API (dev tenant)
- [ ] **TASK 7.2.2:** Napisać testy batch operations
- [ ] **TASK 7.2.3:** Napisać testy rate limiting
- [ ] **TASK 7.2.4:** Napisać testy error handling

#### **7.3 Testy UI**
- [ ] **TASK 7.3.1:** Przetestować nowe funkcje diagnostyczne w UI
- [ ] **TASK 7.3.2:** Przetestować monitoring widgets
- [ ] **TASK 7.3.3:** Przetestować manual testing window

#### **7.4 Testy Performance**
- [ ] **TASK 7.4.1:** Zmierzyć performance przed migracją (baseline)
- [ ] **TASK 7.4.2:** Zmierzyć performance po migracji
- [ ] **TASK 7.4.3:** Porównać wyniki i zoptymalizować jeśli potrzeba

---

### **ETAP 8: Sprzątanie Kodu** ⏱️ **1 dzień**

#### **8.1 Usunięcie PowerShell Components**
- [ ] **TASK 8.1.1:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellConnectionService.cs`
- [ ] **TASK 8.1.2:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellTeamManagementService.cs`
- [ ] **TASK 8.1.3:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellUserManagementService.cs`
- [ ] **TASK 8.1.4:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellBulkOperationsService.cs`
- [ ] **TASK 8.1.5:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellCacheService.cs`
- [ ] **TASK 8.1.6:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellUserResolverService.cs`
- [ ] **TASK 8.1.7:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellService.cs`
- [ ] **TASK 8.1.8:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellServiceBase.cs`
- [ ] **TASK 8.1.9:** Usunąć cały folder `TeamsManager.Core/Abstractions/Services/PowerShell/`
- [ ] **TASK 8.1.10:** Usunąć cały folder `TeamsManager.Core/Exceptions/PowerShell/`
- [ ] **TASK 8.1.11:** Usunąć cały folder `TeamsManager.Core/Helpers/PowerShell/`
- [ ] **TASK 8.1.12:** Usunąć `TeamsManager.Core/Extensions/PowerShellServiceExtensions.cs`

#### **8.2 Aktualizacja Dependencies**
- [ ] **TASK 8.2.1:** Usunąć `System.Management.Automation` z wszystkich .csproj
- [ ] **TASK 8.2.2:** Usunąć `Microsoft.PowerShell.SDK` z wszystkich .csproj
- [ ] **TASK 8.2.3:** Usunąć inne PowerShell-related packages
- [ ] **TASK 8.2.4:** Sprawdzić czy aplikacja kompiluje się bez PowerShell dependencies

#### **8.3 Aktualizacja Dokumentacji**
- [ ] **TASK 8.3.1:** Zaktualizować README.md
- [ ] **TASK 8.3.2:** Zaktualizować docs/ folder
- [ ] **TASK 8.3.3:** Zaktualizować komentarze XML w kodzie
- [ ] **TASK 8.3.4:** Utworzyć migration guide dla deweloperów

#### **8.4 Finalne Testy**
- [ ] **TASK 8.4.1:** Uruchomić wszystkie testy jednostkowe
- [ ] **TASK 8.4.2:** Uruchomić wszystkie testy integracyjne
- [ ] **TASK 8.4.3:** Przetestować aplikację end-to-end
- [ ] **TASK 8.4.4:** Sprawdzić czy wszystkie funkcje działają poprawnie

---

## 📊 **Mapowanie Funkcjonalności**

### **PowerShell → Graph API Mapping**

| PowerShell Method | Graph API Endpoint | HTTP Method | Status |
|-------------------|-------------------|-------------|---------|
| `New-Team` | `/v1.0/teams` | POST | ⏳ |
| `Set-Team` | `/v1.0/teams/{id}` | PATCH | ⏳ |
| `Get-Team` | `/v1.0/teams` | GET | ⏳ |
| `Add-TeamUser` | `/v1.0/teams/{id}/members` | POST | ⏳ |
| `Remove-TeamUser` | `/v1.0/teams/{id}/members/{userId}` | DELETE | ⏳ |
| `New-TeamChannel` | `/v1.0/teams/{id}/channels` | POST | ⏳ |
| `New-MgUser` | `/v1.0/users` | POST | ⏳ |
| `Update-MgUser` | `/v1.0/users/{id}` | PATCH | ⏳ |
| `Get-MgUser` | `/v1.0/users` | GET | ⏳ |
| `Set-MgUserLicense` | `/v1.0/users/{id}/assignLicense` | POST | ⏳ |

### **Batch Operations Mapping**

| PowerShell Bulk | Graph Batch | Endpoint | Status |
|------------------|-------------|----------|---------|
| `ForEach-Object -Parallel` | `POST /v1.0/$batch` | Batch API | ⏳ |
| Multiple `Add-TeamUser` | Batch members add | `/v1.0/$batch` | ⏳ |
| Multiple `Remove-TeamUser` | Batch members remove | `/v1.0/$batch` | ⏳ |

---

## ⚠️ **Potencjalne Wyzwania i Rozwiązania**

### **1. Rate Limiting**
**Problem:** Graph API ma limity 10,000 requests/10min
**Rozwiązanie:** 
- Implementacja exponential backoff
- Batch operations gdzie możliwe
- Request queuing z throttling

### **2. Permissions**
**Problem:** Graph API wymaga specific permissions
**Rozwiązanie:**
- Audit obecnych PowerShell permissions
- Mapowanie na Graph scopes
- Aktualizacja app registration

### **3. Error Handling**
**Problem:** Różne error patterns Graph vs PowerShell
**Rozwiązanie:**
- Unified exception handling
- Retry logic dla transient errors
- Detailed error logging

### **4. Performance**
**Problem:** HTTP calls mogą być wolniejsze niż PowerShell
**Rozwiązanie:**
- Aggressive caching
- Parallel requests
- Connection pooling

### **5. Testing**
**Problem:** Testy z prawdziwym Graph API
**Rozwiązanie:**
- Mock Graph responses
- Dev tenant dla integration tests
- Circuit breaker patterns

---

## 🎯 **Kryteria Sukcesu**

### **Funkcjonalne:**
- [ ] Wszystkie istniejące funkcje działają z Graph API
- [ ] Performance nie gorsze niż PowerShell
- [ ] Error handling na tym samym poziomie
- [ ] Monitoring i diagnostyka działają

### **Techniczne:**
- [ ] Zero PowerShell dependencies
- [ ] Wszystkie testy przechodzą
- [ ] Kod kompiluje się bez ostrzeżeń
- [ ] Memory usage nie wzrósł znacząco

### **Operacyjne:**
- [ ] Deployment bez downtime
- [ ] Rollback plan gotowy
- [ ] Dokumentacja zaktualizowana
- [ ] Team przeszkolony

---

## 📅 **Timeline i Progress Tracking**

| Etap | Czas | Zależności | Postęp | Status |
|------|------|------------|---------|---------|
| **ETAP 1** | 2 dni | - | 0/16 tasków | ⏳ Oczekuje |
| **ETAP 2** | 4 dni | ETAP 1 | 0/23 tasków | ⏳ Oczekuje |
| **ETAP 3** | 3 dni | ETAP 2 | 0/7 tasków | ⏳ Oczekuje |
| **ETAP 4** | 2 dni | ETAP 3 | 0/8 tasków | ⏳ Oczekuje |
| **ETAP 5** | 1 dzień | ETAP 4 | 0/5 tasków | ⏳ Oczekuje |
| **ETAP 6** | 0.5 dnia | ETAP 5 | 0/3 taski | ⏳ Oczekuje |
| **ETAP 7** | 3 dni | ETAP 6 | 0/12 tasków | ⏳ Oczekuje |
| **ETAP 8** | 1 dzień | ETAP 7 | 0/16 tasków | ⏳ Oczekuje |

**Całkowity postęp: 0/90 tasków (0%)**
**Całkowity czas: 16.5 dnia (3.5 tygodnia)**

---

## 🔄 **Plan Rollback**

### **Jeśli Refaktoryzacja Nie Powiedzie Się:**

1. **Przywrócenie PowerShell Services**
   - [ ] Git revert do ostatniego working commit
   - [ ] Przywrócenie PowerShell dependencies
   - [ ] Przywrócenie DI configuration

2. **Hybrid Approach**
   - [ ] Zachowanie PowerShell dla krytycznych operacji
   - [ ] Graph API dla nowych funkcji
   - [ ] Stopniowa migracja

3. **Fallback Strategy**
   - [ ] Feature flags dla Graph vs PowerShell
   - [ ] Runtime switching
   - [ ] A/B testing approach

---

## 📝 **Notatki Implementacyjne**

### **Zachowanie Kompatybilności:**
- Wszystkie publiczne API endpoints pozostają bez zmian
- UI zachowuje tę samą funkcjonalność
- Database schema bez zmian
- Configuration format bez zmian

### **Monitoring Migracji:**
- Metryki performance przed/po
- Error rates monitoring
- User experience metrics
- Resource usage tracking

### **Dokumentacja:**
- Update wszystkich README
- API documentation refresh
- Architecture diagrams update
- Troubleshooting guides

---

**Status:** 📋 **PLAN GOTOWY DO REALIZACJI**
**Ostatnia aktualizacja:** 2024-12-19
**Autor:** AI Assistant
**Review:** Wymagany przed rozpoczęciem implementacji

---

## 📈 **Daily Progress Tracking**

### **Dzień 1:**
- [ ] Rozpoczęcie ETAP 1
- [ ] Ukończenie tasków 1.1.1 - 1.1.7
- [ ] Ukończenie tasków 1.2.1 - 1.2.9

### **Dzień 2:**
- [ ] Ukończenie ETAP 1
- [ ] Rozpoczęcie ETAP 2
- [ ] Ukończenie tasków 2.1.1 - 2.1.6

### **Dzień 3:**
- [ ] Ukończenie tasków 2.2.1 - 2.2.8

### **Dzień 4:**
- [ ] Ukończenie tasków 2.3.1 - 2.3.7

### **Dzień 5:**
- [ ] Ukończenie tasków 2.4.1 - 2.4.6

### **Dzień 6:**
- [ ] Ukończenie ETAP 2
- [ ] Rozpoczęcie ETAP 3

**...i tak dalej dla każdego dnia** 

## TASK 2.1.1: Utworzenie GraphConnectionService ✅ WYKONANE

**Cel**: Utworzenie podstawowej klasy GraphConnectionService implementującej IGraphConnectionService

**Implementacja**:
- ✅ Utworzono klasę `TeamsManager.Core/Services/Graph/GraphConnectionService.cs`
- ✅ Zaimplementowano konstruktor z dependency injection (IModernHttpService, IConfidentialClientApplication, ILogger)
- ✅ Utworzono podstawowe metody zarządzania tokenami (IsTokenValidAsync, RefreshTokenIfNeededAsync)
- ✅ Zaimplementowano GetConnectionHealthAsync z diagnostyką połączenia
- ✅ Dodano szkielety metod dla kolejnych tasków z NotImplementedException

**Modele utworzone**:
- ✅ `GraphConnectionHealthInfo` - informacje o zdrowiu połączenia
- ✅ `GraphDiagnosticInfo` - szczegółowe informacje diagnostyczne  
- ✅ `GraphPermissionInfo` - informacje o uprawnieniach
- ✅ `GraphConnectionTestResult` - wyniki testów połączenia
- ✅ `GraphApiModels` - dodatkowe modele (GraphApiAvailability, GraphUserContext, etc.)
- ✅ `GraphHealthStatus` - enumeracja statusów zdrowia
- ✅ `GraphRateLimitInfo` - informacje o rate limiting

**Wyjątki utworzone**:
- ✅ `GraphConnectionException` - błędy połączenia z Graph API
- ✅ `GraphApiException` - ogólne błędy Graph API (szkielet)
- ✅ `GraphRateLimitException` - błędy rate limiting (szkielet)

**Ważne!!! Do zapamiętania w przyszłej implementacji**:
- GraphConnectionService używa IModernHttpService do komunikacji z Graph API
- Zarządzanie tokenami oparte na Microsoft.Identity.Client (MSAL)
- Wszystkie metody mają pełne logowanie dla diagnostyki
- Implementacja GetConnectionHealthAsync testuje podstawowe połączenie przez endpoint /v1.0/me
- Struktura folderów: Services/Graph/ dla implementacji, Models/Graph/ dla modeli, Exceptions/Graph/ dla wyjątków
- Metody zwracają szczegółowe informacje diagnostyczne z możliwością generowania raportów
- Kompatybilność z istniejącymi PowerShell-based serwisami przez podobne nazwy właściwości w modelach

// ... existing code ... 