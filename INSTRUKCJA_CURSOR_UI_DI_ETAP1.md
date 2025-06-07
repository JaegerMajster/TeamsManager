# Szczegółowa Instrukcja dla Cursora: UI DI Refactoring (Etap 1/6 - Utworzenie abstrakcji i fundamentów)

## ⚠️ KRYTYCZNA ANALIZA PRZED ROZPOCZĘCIEM

**PRZED JAKĄKOLWIEK ZMIANĄ MUSISZ:**

1. **Przeanalizować CAŁĄ strukturę projektu UI**
2. **Sprawdzić czy w projekcie API są podobne rozwiązania** 
3. **Zidentyfikować istniejące konwencje i wzorce**
4. **Upewnić się że rozumiesz obecną architekturę**
5. **Zaplanować zmiany tak aby NIE złamać istniejącej funkcjonalności**

---

## 📋 Analiza istniejących rozwiązań

### 1. Wzorce z TeamsManager.Core do skopiowania:

**Lokalizacja interfejsów:** `TeamsManager.Core/Abstractions/Services/` - wszystkie interfejsy serwisów są w Core, NIE w Api

**Konwencja nazewnictwa:** 
- Interfejsy zaczynają się od `I` (np. `ITeamService`, `IUserService`)
- Pełne komentarze XML dla każdego publicznego członka
- Namespace zgodny z lokalizacją pliku

**Wzorzec interfejsu z Core:**
```csharp
namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z [nazwa].
    /// </summary>
    public interface IExampleService
    {
        /// <summary>
        /// Asynchronicznie wykonuje operację.
        /// </summary>
        /// <param name="parameter">Opis parametru.</param>
        /// <returns>Opis zwracanej wartości.</returns>
        Task<Result> MethodAsync(string parameter);
    }
}
```

**Rejestracja DI w App.xaml.cs:** 
- Używane są metody `AddScoped`, `AddSingleton`, `AddTransient`
- Wzorzec: `services.AddSingleton<IService, ServiceImplementation>();`

### 2. Struktura folderów do zachowania:
```
TeamsManager.UI/
├── Services/          # Istniejący folder - tu umieść implementacje
│   ├── Abstractions/  # NOWY folder - tu umieść interfejsy
│   ├── Configuration/ # Istniejący folder z konfiguracją
│   └── Http/          # NOWY folder - tu umieść HTTP handlers
├── Models/
│   └── Configuration/ # NOWY folder - tu umieść modele konfiguracji
```

### 3. Istniejące serwisy UI do analizy:

**MsalAuthService (TeamsManager.UI/MsalAuthService.cs):** 
- Obecnie bez interfejsu, tworzy HttpClient lokalnie
- Ma metody: `AcquireTokenInteractiveAsync`, `SignOutAsync`, `AcquireGraphTokenAsync`, `AcquireGraphTokenInteractiveAsync`
- **UWAGA:** Plik jest w głównym katalogu UI, nie w Services/

**GraphUserProfileService (Services/GraphUserProfileService.cs):**
- Obecnie bez interfejsu, ma własny HttpClient
- Ma metody: `GetUserProfileAsync`, `GetUserPhotoAsync`, `TestGraphAccessAsync`
- Implementuje `IDisposable`

**Serwisy konfiguracji w Services/Configuration/:** 
- Już używają wzorców DI i są zarejestrowane w `App.xaml.cs`

---

## 🎯 Kontekst

To jest pierwszy etap refaktoryzacji. Aplikacja obecnie działa ale:

- Serwisy są tworzone ręcznie w konstruktorach okien
- Brak interfejsów dla `MsalAuthService` i `GraphUserProfileService`  
- HttpClient tworzony jest bezpośrednio zamiast przez `IHttpClientFactory`
- Brak mechanizmu automatycznego dodawania tokenów

---

## 📚 Wymagania wstępne

**Pliki do analizy PRZED zmianami:**
- `TeamsManager.Core/Abstractions/Services/ITeamService.cs` - wzorzec interfejsów
- `TeamsManager.UI/App.xaml.cs` - obecna konfiguracja DI
- `TeamsManager.UI/MsalAuthService.cs` - obecna implementacja
- `TeamsManager.UI/Services/GraphUserProfileService.cs` - obecna implementacja

**Stan testów do zachowania:**
- ✅ Aplikacja musi się uruchamiać
- ✅ Logowanie MSAL musi działać  
- ✅ Pobieranie profilu użytkownika musi działać

---

## 🚀 Kroki implementacji

### Krok 1: Utworzenie struktury folderów

```bash
mkdir TeamsManager.UI/Services/Abstractions/
mkdir TeamsManager.UI/Services/Http/
mkdir TeamsManager.UI/Models/Configuration/
```

### Krok 2: Utworzenie interfejsu IMsalAuthService

**Plik:** `TeamsManager.UI/Services/Abstractions/IMsalAuthService.cs`

```csharp
using Microsoft.Identity.Client;
using System.Threading.Tasks;
using System.Windows;

namespace TeamsManager.UI.Services.Abstractions
{
    /// <summary>
    /// Interfejs serwisu zarządzającego autentykacją MSAL.
    /// Wzorowany na konwencjach z TeamsManager.Core.Abstractions.Services
    /// </summary>
    public interface IMsalAuthService
    {
        /// <summary>
        /// Pobiera token w trybie interaktywnym (z oknem logowania)
        /// </summary>
        /// <param name="window">Okno rodzica dla dialogu logowania</param>
        /// <returns>Rezultat autentykacji lub null w przypadku błędu</returns>
        Task<AuthenticationResult?> AcquireTokenInteractiveAsync(Window window);
        
        /// <summary>
        /// Wylogowuje użytkownika
        /// </summary>
        /// <returns>Task reprezentujący operację asynchroniczną</returns>
        Task SignOutAsync();
        
        /// <summary>
        /// Pobiera token dla Microsoft Graph w trybie cichym
        /// </summary>
        /// <returns>Token dostępu lub null w przypadku błędu</returns>
        Task<string?> AcquireGraphTokenAsync();
        
        /// <summary>
        /// Pobiera token dla Microsoft Graph w trybie interaktywnym
        /// </summary>
        /// <param name="window">Okno rodzica dla dialogu logowania</param>
        /// <returns>Token dostępu lub null w przypadku błędu</returns>
        Task<string?> AcquireGraphTokenInteractiveAsync(Window window);
    }
}
```

### Krok 3: Utworzenie interfejsu IGraphUserProfileService

**Plik:** `TeamsManager.UI/Services/Abstractions/IGraphUserProfileService.cs`

```csharp
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TeamsManager.UI.Services; // Dla UserProfile i GraphTestResult

namespace TeamsManager.UI.Services.Abstractions
{
    /// <summary>
    /// Interfejs serwisu do pobierania profilu użytkownika z Microsoft Graph.
    /// Wzorowany na konwencjach z TeamsManager.Core.Abstractions.Services
    /// </summary>
    public interface IGraphUserProfileService
    {
        /// <summary>
        /// Pobiera profil użytkownika z Microsoft Graph
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <returns>Profil użytkownika lub null w przypadku błędu</returns>
        Task<UserProfile?> GetUserProfileAsync(string accessToken);
        
        /// <summary>
        /// Pobiera zdjęcie profilowe użytkownika
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <returns>Zdjęcie profilowe lub null w przypadku braku lub błędu</returns>
        Task<BitmapImage?> GetUserPhotoAsync(string accessToken);
        
        /// <summary>
        /// Testuje dostęp do Microsoft Graph API
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <returns>Wynik testów dostępu do Graph API</returns>
        Task<GraphTestResult> TestGraphAccessAsync(string accessToken);
    }
}
```

### Krok 4: Utworzenie modeli konfiguracji dla IOptions

**Plik:** `TeamsManager.UI/Models/Configuration/MsalConfiguration.cs`

```csharp
namespace TeamsManager.UI.Models.Configuration
{
    /// <summary>
    /// Konfiguracja MSAL dla aplikacji UI.
    /// Wzorowana na wzorcach konfiguracji z TeamsManager.Core
    /// </summary>
    public class MsalConfiguration
    {
        /// <summary>
        /// Ustawienia Azure AD
        /// </summary>
        public AzureAdSettings AzureAd { get; set; } = new();
        
        /// <summary>
        /// Scopes wymagane przez aplikację
        /// </summary>
        public string[] Scopes { get; set; } = new[] { "User.Read" };
    }

    /// <summary>
    /// Ustawienia Azure AD dla aplikacji UI
    /// </summary>
    public class AzureAdSettings
    {
        /// <summary>
        /// Instancja Azure AD (domyślnie: https://login.microsoftonline.com/)
        /// </summary>
        public string Instance { get; set; } = "https://login.microsoftonline.com/";
        
        /// <summary>
        /// Identyfikator tenanta Azure AD
        /// </summary>
        public string? TenantId { get; set; }
        
        /// <summary>
        /// Identyfikator klienta aplikacji
        /// </summary>
        public string? ClientId { get; set; }
        
        /// <summary>
        /// URI przekierowania po autentykacji
        /// </summary>
        public string? RedirectUri { get; set; }
        
        /// <summary>
        /// Scope API dla komunikacji z backendem
        /// </summary>
        public string? ApiScope { get; set; }
        
        /// <summary>
        /// Bazowy URL API
        /// </summary>
        public string? ApiBaseUrl { get; set; }
    }
}
```

### Krok 5: Przeniesienie MsalAuthService i dodanie implementacji interfejsu

**UWAGA:** Minimalne zmiany! Tylko przenieś plik i dodaj implementację interfejsu, NIE zmieniaj logiki!

1. **Przenieś plik** `TeamsManager.UI/MsalAuthService.cs` do `TeamsManager.UI/Services/MsalAuthService.cs`

2. **W pliku** `TeamsManager.UI/Services/MsalAuthService.cs`:
   - Dodaj `using TeamsManager.UI.Services.Abstractions;`
   - Zmień namespace na `TeamsManager.UI.Services`
   - Zmień deklarację klasy na: `public class MsalAuthService : IMsalAuthService`
   - **NIE ZMIENIAJ** żadnej logiki wewnętrznej na tym etapie

### Krok 6: Aktualizacja GraphUserProfileService - dodanie implementacji interfejsu

**W pliku** `TeamsManager.UI/Services/GraphUserProfileService.cs`:

- Dodaj `using TeamsManager.UI.Services.Abstractions;`
- Zmień deklarację klasy na: `public class GraphUserProfileService : IGraphUserProfileService, IDisposable`
- **NIE ZMIENIAJ** żadnej logiki wewnętrznej na tym etapie

### Krok 7: Przygotowanie szkieletu TokenAuthorizationHandler

**Plik:** `TeamsManager.UI/Services/Http/TokenAuthorizationHandler.cs`

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.Services.Http
{
    /// <summary>
    /// DelegatingHandler automatycznie dodający token Bearer do żądań HTTP.
    /// Wzorowany na podobnych handlerach z projektów .NET
    /// </summary>
    public class TokenAuthorizationHandler : DelegatingHandler
    {
        private readonly IMsalAuthService _authService;
        private readonly ILogger<TokenAuthorizationHandler> _logger;

        /// <summary>
        /// Inicjalizuje nową instancję TokenAuthorizationHandler
        /// </summary>
        /// <param name="authService">Serwis autentykacji MSAL</param>
        /// <param name="logger">Logger do zapisywania informacji diagnostycznych</param>
        public TokenAuthorizationHandler(
            IMsalAuthService authService,
            ILogger<TokenAuthorizationHandler> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Przetwarza żądanie HTTP, dodając token autoryzacyjny
        /// </summary>
        /// <param name="request">Żądanie HTTP</param>
        /// <param name="cancellationToken">Token anulowania</param>
        /// <returns>Odpowiedź HTTP</returns>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // TODO: W następnym etapie tutaj będzie logika pobierania i dodawania tokenu
            // Na razie tylko przepuszczamy żądanie dalej
            _logger.LogDebug("TokenAuthorizationHandler: Passing through request to {Uri}", 
                request.RequestUri);
            
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
```

---

## ✅ Punkty weryfikacji

**Po każdej zmianie sprawdź:**

### 1. Kompilacja
```bash
dotnet build TeamsManager.UI/TeamsManager.UI.csproj
```

### 2. Struktura folderów
- ✅ Czy folder `Services/Abstractions/` został utworzony?
- ✅ Czy interfejsy są w odpowiednim namespace?
- ✅ Czy `MsalAuthService.cs` został przeniesiony do `Services/`?

### 3. Konwencje nazewnictwa
- ✅ Interfejsy zaczynają się od `I`
- ✅ Namespace zgodny z lokalizacją pliku
- ✅ Komentarze XML dla publicznych członków

### 4. Aplikacja nadal działa
- ✅ Uruchom aplikację
- ✅ Sprawdź czy okno główne się otwiera
- ✅ Sprawdź czy logowanie MSAL działa (jeśli jest skonfigurowane)

---

## 📝 Podsumowanie zmian

### Dodane pliki:
- `Services/Abstractions/IMsalAuthService.cs` - wzorowany na konwencjach z Core
- `Services/Abstractions/IGraphUserProfileService.cs` - wzorowany na konwencjach z Core  
- `Models/Configuration/MsalConfiguration.cs` - modele konfiguracji dla IOptions
- `Services/Http/TokenAuthorizationHandler.cs` - szkielet handlera HTTP

### Przeniesione pliki:
- `MsalAuthService.cs` → `Services/MsalAuthService.cs` - przeniesiony i zaktualizowany namespace

### Zmodyfikowane pliki:
- `Services/MsalAuthService.cs` - dodana implementacja `IMsalAuthService`, zmieniony namespace
- `Services/GraphUserProfileService.cs` - dodana implementacja `IGraphUserProfileService`

### Punkty uwagi:
- ✅ Interfejsy umieszczone w folderze Abstractions zgodnie z konwencją z Core
- ✅ Minimalne zmiany w istniejących klasach - tylko implementacja interfejsów
- ✅ Przygotowana struktura dla kolejnych etapów
- ✅ Zachowane wszystkie istniejące funkcjonalności

### Zweryfikowane działanie:
- ✅ Aplikacja uruchamia się
- ✅ Kompilacja przechodzi bez błędów  
- ✅ Nie ma regresji w funkcjonalności

---

## ⏭️ Następne kroki

**W kolejnym etapie (Etap 2) będziemy:**
- Rejestrować `HttpClientFactory` w `App.xaml.cs`
- Implementować logikę w `TokenAuthorizationHandler`  
- Konfigurować named clients dla Graph API
- Rejestrować nowe interfejsy w DI container

**⚠️ WAŻNE:** Nie przechodź do następnego etapu bez weryfikacji że obecne zmiany działają!

---

## 🔄 Rollback Plan

**Jeśli coś pójdzie nie tak:**

1. **Przywróć MsalAuthService:** Przenieś `Services/MsalAuthService.cs` z powrotem do głównego katalogu UI
2. **Usuń nowe foldery:** `Services/Abstractions/`, `Services/Http/`, `Models/Configuration/`
3. **Przywróć oryginalne deklaracje klas** w `MsalAuthService` i `GraphUserProfileService`
4. **Zweryfikuj że aplikacja działa** jak przed zmianami 