using Microsoft.AspNetCore.Authorization; // Dla atrybutu [Authorize]
using Microsoft.AspNetCore.Mvc;         // Dla ControllerBase, ApiController, Route, HttpGet, IActionResult, OkObjectResult, StatusCode
using TeamsManager.Core.Abstractions;     // Dla ICurrentUserService
// using Microsoft.Extensions.Logging; // Już powinno być globalnie przez ImplicitUsings, ale można dodać dla jasności
using System; // Dla ArgumentNullException
using System.Linq; // Dla User.Claims.Select

namespace TeamsManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Definiuje bazową trasę jako /api/TestAuth
    public class TestAuthController : ControllerBase
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TestAuthController> _logger;

        // Konstruktor do wstrzykiwania zależności
        public TestAuthController(ICurrentUserService currentUserService, ILogger<TestAuthController> logger)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Endpoint do testowania uwierzytelniania i pobierania informacji o zalogowanym użytkowniku
        [HttpGet("whoami")] // Trasa: GET /api/TestAuth/whoami
        [Authorize]         // Ten endpoint wymaga, aby użytkownik był uwierzytelniony
        public IActionResult WhoAmI()
        {
            _logger.LogInformation("Wywołano zabezpieczony endpoint /api/TestAuth/whoami");

            // Sprawdzenie, czy tożsamość użytkownika została poprawnie ustalona przez middleware
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                // Ten scenariusz nie powinien normalnie wystąpić z powodu atrybutu [Authorize],
                // który powinien zwrócić 401 zanim kod metody zostanie wykonany.
                // Dodajemy to jako dodatkowe zabezpieczenie lub do celów diagnostycznych.
                _logger.LogWarning("/api/TestAuth/whoami: User.Identity.IsAuthenticated jest false, mimo atrybutu [Authorize]. To nie powinno się zdarzyć.");
                return Unauthorized(new { Message = "Użytkownik nie jest uwierzytelniony (sprawdzenie po User.Identity)." });
            }

            // Logowanie informacji z HttpContext.User dla celów diagnostycznych
            _logger.LogInformation("HttpContext.User.Identity.IsAuthenticated: {IsAuth}", User.Identity.IsAuthenticated);
            _logger.LogInformation("HttpContext.User.Identity.Name (może to być UPN lub inne oświadczenie 'name'): {IdentityName}", User.Identity.Name);
            _logger.LogInformation("HttpContext.User.Identity.AuthenticationType: {AuthType}", User.Identity.AuthenticationType);

            _logger.LogDebug("Dostępne oświadczenia (claims) dla użytkownika:");
            foreach (var claim in User.Claims)
            {
                _logger.LogDebug("Claim: Typ = {ClaimType}, Wartość = {ClaimValue}, Wystawca = {ClaimIssuer}", claim.Type, claim.Value, claim.Issuer);
            }

            // Pobranie UPN i ID użytkownika za pomocą ICurrentUserService
            var userUpn = _currentUserService.GetCurrentUserUpn();
            var userId = _currentUserService.GetCurrentUserId(); // Zakładając, że masz tę metodę w ICurrentUserService

            // Sprawdzenie, czy ICurrentUserService poprawnie odczytał UPN
            // (nie powinien być wartością domyślną, jeśli użytkownik jest poprawnie uwierzytelniony przez token)
            if (string.IsNullOrEmpty(userUpn) || userUpn.Equals("system@teamsmanager.local", StringComparison.OrdinalIgnoreCase) || userUpn.Equals("unknown@teamsmanager.local", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("/api/TestAuth/whoami: Użytkownik jest uwierzytelniony (wg User.Identity), ale ICurrentUserService zwrócił UPN: '{UserUpn}'. Oświadczenia mogły nie zostać poprawnie zmapowane lub odczytane w CurrentUserService.", userUpn);

                // Zwróć szczegóły oświadczeń, aby pomóc w debugowaniu
                var claimsDetails = User.Claims.Select(c => new { Type = c.Type, Value = c.Value, Issuer = c.Issuer }).ToList();
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new
                    {
                        Message = "Nie udało się poprawnie zidentyfikować użytkownika na podstawie tokenu (ICurrentUserService zwrócił nieoczekiwany UPN). Sprawdź logi serwera i implementację CurrentUserService.",
                        AuthenticatedIdentityName = User.Identity.Name,
                        CurrentUserServiceUpn = userUpn,
                        Claims = claimsDetails
                    });
            }

            _logger.LogInformation("/api/TestAuth/whoami: Pomyślnie zidentyfikowano użytkownika. UPN z ICurrentUserService: '{UserUpn}', ID Obiektu (z ICurrentUserService): '{UserId}'", userUpn, userId ?? "N/A");

            return Ok(new
            {
                Message = "Jesteś pomyślnie uwierzytelniony!",
                UserPrincipalName = userUpn,
                ObjectId = userId,
                AuthenticationType = User.Identity?.AuthenticationType, // np. "Bearer"
                Claims = User.Claims.Select(c => new { c.Type, c.Value, c.Issuer }).ToList() // Zwróć oświadczenia do analizy po stronie klienta
            });
        }

        // Endpoint publiczny, nie wymagający autoryzacji, do testowania czy API w ogóle działa
        [HttpGet("publicinfo")] // Trasa: GET /api/TestAuth/publicinfo
        public IActionResult PublicInfo()
        {
            _logger.LogInformation("Wywołano publiczny endpoint /api/TestAuth/publicinfo");
            return Ok(new { Message = "To jest publiczny endpoint, dostępny bez logowania." });
        }
    }
}