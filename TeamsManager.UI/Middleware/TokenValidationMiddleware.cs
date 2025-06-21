using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TeamsManager.UI.Services.Auth;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.Core.Abstractions.Services;

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
                _logger.LogDebug("[TOKEN-VALIDATION] Sprawdzanie Authorization header...");
                
                // Sprawdź czy żądanie ma Authorization header
                if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    var authHeaderValue = authHeader.ToString();
                    if (authHeaderValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var userAccessToken = authHeaderValue.Substring("Bearer ".Length).Trim();
                        
                        if (!string.IsNullOrEmpty(userAccessToken))
                        {
                            _logger.LogDebug("[TOKEN-VALIDATION] Token użytkownika otrzymany, długość: {Length}", userAccessToken.Length);
                            
                            // Wykonaj przepływ OBO
                            var oboAccessToken = await _oboTokenManager.GetOboAccessTokenAsync(userAccessToken);
                            
                            if (!string.IsNullOrEmpty(oboAccessToken))
                            {
                                _logger.LogDebug("[TOKEN-VALIDATION] Token OBO otrzymany pomyślnie");
                                
                                // ✅ NAPRAWKA OBO: Dodaj tokeny do HttpContext.Items
                                context.Items["UserAccessToken"] = userAccessToken;
                                context.Items["GraphAccessToken"] = oboAccessToken;
                                
                                // ✅ NAPRAWKA OBO: Ustaw token OBO w ModernHttpService
                                try
                                {
                                    var modernHttpService = context.RequestServices.GetService<IModernHttpService>();
                                    if (modernHttpService != null)
                                    {
                                        modernHttpService.SetOboToken(oboAccessToken);
                                        _logger.LogDebug("[TOKEN-VALIDATION] Token OBO ustawiony w ModernHttpService");
                                    }
                                    else
                                    {
                                        _logger.LogWarning("[TOKEN-VALIDATION] ModernHttpService nie jest dostępny w DI");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "[TOKEN-VALIDATION] Błąd podczas ustawiania tokenu OBO w ModernHttpService");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("[TOKEN-VALIDATION] Nie udało się uzyskać tokenu OBO");
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("[TOKEN-VALIDATION] Brak Authorization header");
                }
                
                // Kontynuuj pipeline
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOKEN-VALIDATION] Błąd w TokenValidationMiddleware");
                // Kontynuuj pipeline mimo błędu
                await _next(context);
            }
        }
    }
} 