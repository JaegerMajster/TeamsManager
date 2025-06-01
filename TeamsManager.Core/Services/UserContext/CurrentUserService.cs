// Plik: TeamsManager.Core/Services/UserContext/CurrentUserService.cs
using Microsoft.AspNetCore.Http; // Potrzebne dla IHttpContextAccessor i HttpContext
using System.Security.Claims;    // Potrzebne dla ClaimsPrincipal i standardowych typów oświadczeń
using TeamsManager.Core.Abstractions;
using System; // Dla ArgumentNullException

namespace TeamsManager.Core.Services.UserContext
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor? _httpContextAccessor; // Może być null, jeśli serwis jest używany poza kontekstem HTTP
        private string? _manualUserUpn; // Do użytku w testach lub zadaniach w tle

        // Konstruktor dla scenariuszy z DI (np. w API)
        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        // Konstruktor bez parametrów, aby umożliwić użycie w scenariuszach bez IHttpContextAccessor
        // (np. testy jednostkowe serwisów, które nie mockują pełnego kontekstu HTTP,
        // lub jeśli UI potrzebuje instancji przed pełną konfiguracją DI dla API).
        // W TeamsManager.UI (App.xaml.cs) prawdopodobnie rejestrujesz go bez IHttpContextAccessor.
        public CurrentUserService()
        {
            _httpContextAccessor = null;
            // Ustawienie domyślnego użytkownika dla scenariuszy nie-HTTP lub przed zalogowaniem
            _manualUserUpn = "system@teamsmanager.local";
            System.Diagnostics.Debug.WriteLine("CurrentUserService: Instancja utworzona bez IHttpContextAccessor (np. dla UI lub testów). Użyje _manualUserUpn lub domyślnego.");
        }

        public string? GetCurrentUserUpn()
        {
            // Priorytet 1: Użytkownik z kontekstu HTTP (jeśli dostępny i uwierzytelniony)
            if (_httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                // Standardowe oświadczenia dla UPN/nazwy użytkownika od Microsoft Identity Platform:
                // 1. "preferred_username" - często zawiera UPN.
                // 2. ClaimTypes.Name (http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name) - MSAL często mapuje tu UPN.
                // 3. "upn" (http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn)
                // Kolejność sprawdzania może zależeć od konfiguracji tokenu.
                var upnClaim = _httpContextAccessor.HttpContext.User.FindFirst("preferred_username")?.Value ??
                               _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value ??
                               _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.Upn)?.Value;

                if (!string.IsNullOrEmpty(upnClaim))
                {
                    System.Diagnostics.Debug.WriteLine($"CurrentUserService (HTTP Context): Zwracam UPN z oświadczenia: {upnClaim}");
                    return upnClaim;
                }

                // Fallback na User.Identity.Name, jeśli specyficzne oświadczenia UPN nie zostały znalezione,
                // ale użytkownik jest uwierzytelniony.
                var identityName = _httpContextAccessor.HttpContext.User.Identity.Name;
                if (!string.IsNullOrEmpty(identityName))
                {
                    System.Diagnostics.Debug.WriteLine($"CurrentUserService (HTTP Context): Zwracam UPN z User.Identity.Name: {identityName}");
                    return identityName;
                }
                System.Diagnostics.Debug.WriteLine("CurrentUserService (HTTP Context): Użytkownik uwierzytelniony, ale nie znaleziono oświadczenia UPN.");
            }

            // Priorytet 2: Użytkownik ustawiony ręcznie (np. w testach lub w UI przed pełnym DI dla API)
            if (!string.IsNullOrEmpty(_manualUserUpn))
            {
                System.Diagnostics.Debug.WriteLine($"CurrentUserService (Manual): Zwracam ręcznie ustawiony UPN: {_manualUserUpn}");
                return _manualUserUpn;
            }

            // Priorytet 3 (lub domyślny, jeśli konstruktor bez parametrów został użyty i nic nie ustawiono):
            // Wcześniej zwracałeś tu "system@teamsmanager.local" jeśli _manualUserUpn był null.
            // Jeśli konstruktor bez parametrów ustawia _manualUserUpn na "system@teamsmanager.local", to ten fallback jest już obsłużony.
            // Dla pewności, jeśli nic innego nie pasuje:
            System.Diagnostics.Debug.WriteLine("CurrentUserService: Nie udało się ustalić UPN z kontekstu HTTP ani ręcznie. Zwracam wartość domyślną 'unknown@teamsmanager.local'.");
            return "unknown@teamsmanager.local"; // Ostateczna wartość domyślna
        }

        public void SetCurrentUserUpn(string? upn)
        {
            // Ta metoda jest głównie dla:
            // 1. Testów jednostkowych serwisów, gdzie mockujemy ICurrentUserService.
            // 2. Aplikacji UI (App.xaml.cs), gdzie IHttpContextAccessor nie jest dostępny,
            //    i chcemy ustawić użytkownika "globalnie" dla sesji UI po zalogowaniu przez MSAL.
            // 3. Potencjalnych zadań w tle/konsolowych używających Core.
            _manualUserUpn = upn;
            System.Diagnostics.Debug.WriteLine($"CurrentUserService (Manual): UPN ustawiony na: {upn ?? "null"}");

            if (_httpContextAccessor?.HttpContext != null)
            {
                System.Diagnostics.Debug.WriteLine("CurrentUserService (Manual): Ostrzeżenie - SetCurrentUserUpn wywołane w obecności HttpContext. " +
                                                   "GetCurrentUserUpn() dla tego żądania HTTP nadal będzie próbował odczytać użytkownika z tokenu.");
            }
        }

        // Można dodać inne metody, np. do pobierania ID użytkownika, ról itp. z claims:
        public string? GetCurrentUserId()
        {
            if (_httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                // Claim 'oid' (object identifier) lub 'sub' (subject) jest często używany jako ID użytkownika
                // W tokenach od Microsoft Identity Platform, object ID użytkownika jest w "oid"
                return _httpContextAccessor.HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value ??
                       _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value; // NameIdentifier to często 'sub'
            }
            return null;
        }

        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(GetCurrentUserUpn()) && GetCurrentUserUpn() != "system@teamsmanager.local" && GetCurrentUserUpn() != "unknown@teamsmanager.local";
    }
}