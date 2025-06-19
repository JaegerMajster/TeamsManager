using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TeamsManager.Api.Extensions
{
    /// <summary>
    /// Rozszerzenia dla HttpContext ułatwiające pracę z Bearer tokenami
    /// </summary>
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Klucz cache w HttpContext.Items dla Bearer tokenu
        /// </summary>
        private const string BearerTokenCacheKey = "_TeamsManager_BearerToken_Cache";

        /// <summary>
        /// Pobiera Bearer token z nagłówka Authorization z cache per-request
        /// </summary>
        /// <param name="httpContext">Kontekst HTTP</param>
        /// <returns>Bearer token bez prefiksu "Bearer " lub null jeśli nie znaleziono</returns>
        public static async Task<string?> GetBearerTokenAsync(this HttpContext httpContext)
        {
            if (httpContext == null)
                return null;

            // Sprawdzenie cache per-request
            if (httpContext.Items.TryGetValue(BearerTokenCacheKey, out var cachedToken))
            {
                return cachedToken as string;
            }

            string? token = null;

            try
            {
                // Próba użycia ASP.NET Core authentication system
                token = await httpContext.GetTokenAsync("access_token");
                
                if (!string.IsNullOrEmpty(token))
                {
                    // Zapisanie w cache i zwrócenie
                    httpContext.Items[BearerTokenCacheKey] = token;
                    return token;
                }
            }
            catch (InvalidOperationException)
            {
                // GetTokenAsync może rzucić wyjątek jeśli authentication scheme nie wspiera token storage
                // Ignorujemy i przechodzimy do fallback
            }
            catch (Exception)
            {
                // Każdy inny błąd też ignorujemy i używamy fallback
            }

            // Fallback - ręczne parsowanie nagłówka Authorization
            token = ParseBearerTokenFromHeader(httpContext);

            // Zapisanie w cache (nawet jeśli null)
            httpContext.Items[BearerTokenCacheKey] = token;

            return token;
        }

        /// <summary>
        /// Czyści cache Bearer tokenu w bieżącym żądaniu
        /// </summary>
        /// <param name="httpContext">Kontekst HTTP</param>
        public static void ClearBearerTokenCache(this HttpContext httpContext)
        {
            if (httpContext?.Items != null && httpContext.Items.ContainsKey(BearerTokenCacheKey))
            {
                httpContext.Items.Remove(BearerTokenCacheKey);
            }
        }

        /// <summary>
        /// Parsuje Bearer token z nagłówka Authorization
        /// </summary>
        /// <param name="httpContext">Kontekst HTTP</param>
        /// <returns>Bearer token bez prefiksu "Bearer " lub null jeśli nie znaleziono</returns>
        private static string? ParseBearerTokenFromHeader(HttpContext httpContext)
        {
            // Sprawdzenie czy nagłówek Authorization istnieje
            if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            {
                return null;
            }

            // Pobranie pierwszego nagłówka Authorization
            var authHeader = authHeaderValues.FirstOrDefault()?.ToString();
            
            if (string.IsNullOrEmpty(authHeader))
            {
                return null;
            }

            // Sprawdzenie czy zaczyna się od "Bearer " (case-insensitive)
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Wyciągnięcie tokenu i usunięcie spacji
            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            // Zwrócenie tokenu lub null jeśli pusty
            return string.IsNullOrEmpty(token) ? null : token;
        }
    }
} 