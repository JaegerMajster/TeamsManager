using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TeamsManager.UI.Services.Auth;

namespace TeamsManager.UI.Middleware
{
    /// <summary>
    /// Middleware do walidacji tokenów Authorization header w EmbeddedApiServer
    /// Wyciąga token Bearer i dodaje go do HttpContext.Items dla kontrolerów
    /// </summary>
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenValidationMiddleware> _logger;
        private readonly EmbeddedOboTokenManager _oboTokenManager;

        public TokenValidationMiddleware(
            RequestDelegate next,
            ILogger<TokenValidationMiddleware> logger,
            EmbeddedOboTokenManager oboTokenManager)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _oboTokenManager = oboTokenManager ?? throw new ArgumentNullException(nameof(oboTokenManager));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Sprawdź czy żądanie ma Authorization header
                if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    var authHeaderValue = authHeader.ToString();
                    
                    // Sprawdź czy to Bearer token
                    if (authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = authHeaderValue.Substring("Bearer ".Length).Trim();
                        
                        _logger.LogDebug("Znaleziono Bearer token w Authorization header");
                        
                        // Waliduj token
                        if (_oboTokenManager.ValidateUserToken(token))
                        {
                            _logger.LogDebug("Token użytkownika jest prawidłowy");
                            
                            // Dodaj token do HttpContext.Items dla kontrolerów
                            context.Items["UserAccessToken"] = token;
                            
                            // Uzyskaj token OBO dla Graph API
                            var oboToken = await _oboTokenManager.GetOboAccessTokenAsync(token);
                            if (!string.IsNullOrEmpty(oboToken))
                            {
                                context.Items["GraphAccessToken"] = oboToken;
                                _logger.LogDebug("Token OBO dla Graph API został uzyskany pomyślnie");
                            }
                            else
                            {
                                _logger.LogWarning("Nie udało się uzyskać tokenu OBO dla Graph API");
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Token użytkownika nie przeszedł walidacji");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Authorization header nie zawiera Bearer token");
                    }
                }
                else
                {
                    _logger.LogDebug("Brak Authorization header w żądaniu");
                }

                // Kontynuuj pipeline
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd w TokenValidationMiddleware");
                
                // W przypadku błędu, kontynuuj pipeline bez tokenów
                await _next(context);
            }
        }
    }
} 