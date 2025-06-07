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
            try
            {
                // Pobierz token dla Graph API
                var token = await _authService.AcquireGraphTokenAsync();
                
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    _logger.LogDebug("TokenAuthorizationHandler: Added Bearer token to request for {Uri}", 
                        request.RequestUri);
                }
                else
                {
                    _logger.LogWarning("TokenAuthorizationHandler: No token available for {Uri}", 
                        request.RequestUri);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TokenAuthorizationHandler: Failed to acquire token for {Uri}", 
                    request.RequestUri);
                // Nie przerywamy żądania - może endpoint nie wymaga autoryzacji
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
} 