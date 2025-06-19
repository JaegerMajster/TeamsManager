using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using TeamsManager.Core.Abstractions;
using System;
using System.Linq;

namespace TeamsManager.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class TestAuthController : ControllerBase
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<TestAuthController> _logger;

        public TestAuthController(ICurrentUserService currentUserService, ILogger<TestAuthController> logger)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Endpoint do testowania uwierzytelniania i pobierania informacji o zalogowanym użytkowniku
        /// </summary>
        [HttpGet("whoami")]
        [Authorize]
        public IActionResult WhoAmI()
        {
            _logger.LogInformation("Wywołano zabezpieczony endpoint /api/TestAuth/whoami");

            // Sprawdzenie uwierzytelnienia użytkownika
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                _logger.LogWarning("/api/TestAuth/whoami: User.Identity.IsAuthenticated jest false, mimo atrybutu [Authorize]. To nie powinno się zdarzyć.");
                return Unauthorized(new { Message = "Użytkownik nie jest uwierzytelniony." });
            }

            // Logowanie informacji diagnostycznych
            _logger.LogInformation("HttpContext.User.Identity.IsAuthenticated: {IsAuth}", User.Identity.IsAuthenticated);
            _logger.LogInformation("HttpContext.User.Identity.Name: {IdentityName}", User.Identity.Name);
            _logger.LogInformation("HttpContext.User.Identity.AuthenticationType: {AuthType}", User.Identity.AuthenticationType);

            _logger.LogDebug("Dostępne oświadczenia (claims) dla użytkownika:");
            foreach (var claim in User.Claims)
            {
                _logger.LogDebug("Claim: Typ = {ClaimType}, Wartość = {ClaimValue}, Wystawca = {ClaimIssuer}", claim.Type, claim.Value, claim.Issuer);
            }

            // Pobranie UPN i ID użytkownika
            var userUpn = _currentUserService.GetCurrentUserUpn();
            var userId = _currentUserService.GetCurrentUserId();

            // Sprawdzenie poprawności UPN
            if (string.IsNullOrEmpty(userUpn) || userUpn.Equals("system@teamsmanager.local", StringComparison.OrdinalIgnoreCase) || userUpn.Equals("unknown@teamsmanager.local", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("/api/TestAuth/whoami: Użytkownik jest uwierzytelniony, ale ICurrentUserService zwrócił UPN: '{UserUpn}'.", userUpn);

                var claimsDetails = User.Claims.Select(c => new { Type = c.Type, Value = c.Value, Issuer = c.Issuer }).ToList();
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new
                    {
                        Message = "Nie udało się poprawnie zidentyfikować użytkownika na podstawie tokenu. Sprawdź logi serwera.",
                        AuthenticatedIdentityName = User.Identity.Name,
                        CurrentUserServiceUpn = userUpn,
                        Claims = claimsDetails
                    });
            }

            _logger.LogInformation("/api/TestAuth/whoami: Pomyślnie zidentyfikowano użytkownika. UPN: '{UserUpn}', ID: '{UserId}'", userUpn, userId ?? "N/A");

            return Ok(new
            {
                Message = "Jesteś pomyślnie uwierzytelniony!",
                UserPrincipalName = userUpn,
                ObjectId = userId,
                AuthenticationType = User.Identity?.AuthenticationType,
                Claims = User.Claims.Select(c => new { c.Type, c.Value, c.Issuer }).ToList()
            });
        }

        /// <summary>
        /// Endpoint publiczny do testowania dostępności API
        /// </summary>
        [HttpGet("publicinfo")]
        public IActionResult PublicInfo()
        {
            _logger.LogInformation("Wywołano publiczny endpoint /api/TestAuth/publicinfo");
            return Ok(new { Message = "To jest publiczny endpoint, dostępny bez logowania." });
        }
    }
}