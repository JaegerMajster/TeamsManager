using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Core.Services.PowerShellServices
{
    public class PowerShellUserResolverService : IPowerShellUserResolverService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly IPowerShellCacheService _cacheService;
        private readonly ILogger<PowerShellUserResolverService> _logger;

        public PowerShellUserResolverService(
            IPowerShellConnectionService connectionService,
            IPowerShellCacheService cacheService,
            ILogger<PowerShellUserResolverService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogWarning("Próba pobrania ID użytkownika z pustym UPN.");
                return null;
            }

            // Sprawdź cache
            if (!forceRefresh)
            {
                var cachedId = await GetCachedUserIdAsync(userUpn);
                if (!string.IsNullOrEmpty(cachedId))
                {
                    _logger.LogDebug("ID użytkownika {UserUpn} znalezione w cache.", userUpn);
                    return cachedId;
                }
            }

            _logger.LogDebug("ID użytkownika {UserUpn} nie znalezione w cache lub wymuszono odświeżenie.", userUpn);

            try
            {
                // Pobierz z Graph API
                var script = $@"
                    $user = Get-MgUser -UserId '{userUpn.Replace("'", "''")}' -ErrorAction Stop
                    if ($user) {{ $user.Id }} else {{ $null }}
                ";

                var results = await _connectionService.ExecuteScriptAsync(script);
                var userId = results?.FirstOrDefault()?.BaseObject?.ToString();

                if (!string.IsNullOrEmpty(userId))
                {
                    // Zapisz w cache
                    _cacheService.SetUserId(userUpn, userId);
                    _logger.LogDebug("ID użytkownika {UserUpn} zapisane w cache.", userUpn);
                }
                else if (forceRefresh)
                {
                    // Jeśli wymuszono odświeżenie i nie znaleziono, zapisz null na krótko
                    _cacheService.Set($"PowerShell_UserId_{userUpn}", (string?)null, TimeSpan.FromMinutes(1));
                }

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania ID użytkownika {UserUpn}", userUpn);
                
                // Cache negatywny wynik na krótko
                _cacheService.Set($"PowerShell_UserId_{userUpn}", (string?)null, TimeSpan.FromMinutes(1));
                
                return null;
            }
        }

        public async Task<string?> GetCachedUserIdAsync(string userUpn)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
                return null;

            return await _cacheService.GetUserIdAsync(userUpn, false);
        }
    }
} 