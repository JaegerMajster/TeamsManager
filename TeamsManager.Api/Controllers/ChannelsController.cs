using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamsManager.Core.Abstractions.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Management.Automation; // Dla PSObject
using TeamsManager.Core.Models; // Dla Channel, jeśli będziemy mapować
using TeamsManager.Core.Enums; // Dla ChannelStatus

namespace TeamsManager.Api.Controllers
{
    // --- Data Transfer Objects (DTO) ---
    // W docelowym projekcie te klasy powinny znaleźć się w osobnym projekcie/folderze

    public class CreateChannelRequestDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPrivate { get; set; } = false;
        // Inne właściwości jak Owner UPN dla kanałów prywatnych można dodać
    }

    public class UpdateChannelRequestDto
    {
        public string? NewDisplayName { get; set; } // Nowa nazwa kanału
        public string? NewDescription { get; set; } // Nowy opis kanału
        // Uwaga: Zmiana MembershipType (np. z publicznego na prywatny) nie jest trywialna i może wymagać utworzenia nowego kanału.
    }


    // --- Kontroler ---

    [ApiController]
    [Route("api/teams/{teamId}/[controller]")] // Trasa bazowa: /api/teams/{teamId}/Channels
    [Authorize] // Wszystkie operacje na kanałach domyślnie wymagają autoryzacji
    public class ChannelsController : ControllerBase
    {
        private readonly IPowerShellService _powerShellService;
        private readonly ITeamService _teamService; // Potrzebny do logiki związanej z Team (np. aktualizacji w lokalnej bazie)
        private readonly ILogger<ChannelsController> _logger;

        public ChannelsController(
            IPowerShellService powerShellService,
            ITeamService teamService,
            ILogger<ChannelsController> logger)
        {
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private string? GetAccessTokenFromHeader()
        {
            if (Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader.Substring("Bearer ".Length).Trim();
                }
            }
            _logger.LogWarning("Nie znaleziono tokenu dostępu w nagłówku Authorization.");
            return null;
        }

        private async Task<bool> ConnectToGraphWithToken(string? accessToken, string[]? scopes = null)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Próba połączenia z Graph bez tokenu dostępu.");
                return false;
            }
            // Domyślne scopes dla operacji na kanałach, jeśli nie podano inaczej
            var defaultScopes = scopes ?? new[] { "Group.ReadWrite.All", "Channel.ReadBasic.All", "Channel.ReadWrite.All" };
            bool isConnected = await _powerShellService.ConnectWithAccessTokenAsync(accessToken, defaultScopes);
            if (!isConnected)
            {
                _logger.LogError("Nie udało się nawiązać połączenia z Microsoft Graph API.");
            }
            return isConnected;
        }

        [HttpGet]
        public async Task<IActionResult> GetTeamChannels(string teamId)
        {
            _logger.LogInformation("Pobieranie kanałów dla zespołu ID: {TeamId}", teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (!await ConnectToGraphWithToken(accessToken))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Message = "Nie udało się połączyć z usługą Microsoft Graph." });
            }

            var psObjects = await _powerShellService.GetTeamChannelsAsync(teamId);
            if (psObjects == null)
            {
                // Błąd został już zalogowany przez PowerShellService
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Wystąpił błąd podczas pobierania kanałów." });
            }

            // TODO: Zmapować PSObject na Channel DTO lub model Channel, jeśli potrzebne
            var channels = psObjects.Select(pso => new
            {
                Id = pso.Properties["Id"]?.Value?.ToString(),
                DisplayName = pso.Properties["DisplayName"]?.Value?.ToString(),
                Description = pso.Properties["Description"]?.Value?.ToString(),
                MembershipType = pso.Properties["MembershipType"]?.Value?.ToString(), // Np. Standard, Private
                WebUrl = pso.Properties["WebUrl"]?.Value?.ToString()
            }).ToList();

            return Ok(channels);
        }

        [HttpGet("{channelDisplayName}")]
        public async Task<IActionResult> GetTeamChannel(string teamId, string channelDisplayName)
        {
            var decodedChannelDisplayName = System.Net.WebUtility.UrlDecode(channelDisplayName);
            _logger.LogInformation("Pobieranie kanału '{ChannelDisplayName}' dla zespołu ID: {TeamId}", decodedChannelDisplayName, teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (!await ConnectToGraphWithToken(accessToken))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Message = "Nie udało się połączyć z usługą Microsoft Graph." });
            }

            var psObject = await _powerShellService.GetTeamChannelAsync(teamId, decodedChannelDisplayName);
            if (psObject == null)
            {
                _logger.LogInformation("Kanał '{ChannelDisplayName}' w zespole ID: {TeamId} nie został znaleziony lub wystąpił błąd.", decodedChannelDisplayName, teamId);
                return NotFound(new { Message = $"Kanał '{decodedChannelDisplayName}' w zespole ID '{teamId}' nie został znaleziony." });
            }

            // TODO: Zmapować PSObject na Channel DTO lub model Channel
            var channel = new
            {
                Id = psObject.Properties["Id"]?.Value?.ToString(),
                DisplayName = psObject.Properties["DisplayName"]?.Value?.ToString(),
                Description = psObject.Properties["Description"]?.Value?.ToString(),
                MembershipType = psObject.Properties["MembershipType"]?.Value?.ToString(),
                WebUrl = psObject.Properties["WebUrl"]?.Value?.ToString()
            };
            return Ok(channel);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeamChannel(string teamId, [FromBody] CreateChannelRequestDto requestDto)
        {
            _logger.LogInformation("Żądanie utworzenia kanału '{DisplayName}' w zespole ID: {TeamId}", requestDto.DisplayName, teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (!await ConnectToGraphWithToken(accessToken))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Message = "Nie udało się połączyć z usługą Microsoft Graph." });
            }

            var psObject = await _powerShellService.CreateTeamChannelAsync(teamId, requestDto.DisplayName, requestDto.IsPrivate, requestDto.Description);
            if (psObject == null)
            {
                _logger.LogWarning("Nie udało się utworzyć kanału '{DisplayName}' w zespole ID: {TeamId}.", requestDto.DisplayName, teamId);
                return BadRequest(new { Message = "Nie udało się utworzyć kanału. Sprawdź logi serwera." });
            }

            // TODO: Zmapować PSObject na Channel DTO lub model Channel
            var createdChannel = new
            {
                Id = psObject.Properties["Id"]?.Value?.ToString(),
                DisplayName = psObject.Properties["DisplayName"]?.Value?.ToString(),
                Description = psObject.Properties["Description"]?.Value?.ToString(),
                MembershipType = psObject.Properties["MembershipType"]?.Value?.ToString()
            };
            _logger.LogInformation("Kanał '{DisplayName}' (ID: {ChannelId}) utworzony pomyślnie w zespole ID: {TeamId}.", createdChannel.DisplayName, createdChannel.Id, teamId);

            // TODO: Rozważyć aktualizację lokalnej bazy danych o nowo utworzony kanał,
            // np. poprzez wywołanie metody w ITeamService, która by to obsłużyła.

            return CreatedAtAction(nameof(GetTeamChannel), new { teamId = teamId, channelDisplayName = createdChannel.DisplayName }, createdChannel);
        }

        [HttpPut("{currentChannelDisplayName}")]
        public async Task<IActionResult> UpdateTeamChannel(string teamId, string currentChannelDisplayName, [FromBody] UpdateChannelRequestDto requestDto)
        {
            var decodedCurrentChannelDisplayName = System.Net.WebUtility.UrlDecode(currentChannelDisplayName);
            _logger.LogInformation("Żądanie aktualizacji kanału '{CurrentChannelDisplayName}' w zespole ID: {TeamId}", decodedCurrentChannelDisplayName, teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (!await ConnectToGraphWithToken(accessToken))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Message = "Nie udało się połączyć z usługą Microsoft Graph." });
            }

            if (string.IsNullOrWhiteSpace(requestDto.NewDisplayName) && string.IsNullOrWhiteSpace(requestDto.NewDescription))
            {
                return BadRequest(new { Message = "Należy podać przynajmniej nową nazwę lub nowy opis." });
            }

            var success = await _powerShellService.UpdateTeamChannelAsync(teamId, decodedCurrentChannelDisplayName, requestDto.NewDisplayName, requestDto.NewDescription);
            if (success)
            {
                _logger.LogInformation("Kanał '{CurrentChannelDisplayName}' w zespole ID: {TeamId} zaktualizowany pomyślnie.", decodedCurrentChannelDisplayName, teamId);
                // TODO: Rozważyć aktualizację lokalnej bazy danych.
                return NoContent(); // 204 No Content
            }
            _logger.LogWarning("Nie udało się zaktualizować kanału '{CurrentChannelDisplayName}' w zespole ID: {TeamId}.", decodedCurrentChannelDisplayName, teamId);
            return BadRequest(new { Message = "Nie udało się zaktualizować kanału." });
        }

        [HttpDelete("{channelDisplayName}")]
        public async Task<IActionResult> RemoveTeamChannel(string teamId, string channelDisplayName)
        {
            var decodedChannelDisplayName = System.Net.WebUtility.UrlDecode(channelDisplayName);
            _logger.LogInformation("Żądanie usunięcia kanału '{ChannelDisplayName}' z zespołu ID: {TeamId}", decodedChannelDisplayName, teamId);
            var accessToken = GetAccessTokenFromHeader();
            if (!await ConnectToGraphWithToken(accessToken))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Message = "Nie udało się połączyć z usługą Microsoft Graph." });
            }

            if (decodedChannelDisplayName.Equals("General", StringComparison.OrdinalIgnoreCase) || decodedChannelDisplayName.Equals("Ogólny", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Próba usunięcia kanału General/Ogólny dla zespołu {TeamId}, co jest niedozwolone.", teamId);
                return BadRequest(new { Message = "Nie można usunąć kanału General/Ogólny." });
            }

            var success = await _powerShellService.RemoveTeamChannelAsync(teamId, decodedChannelDisplayName);
            if (success)
            {
                _logger.LogInformation("Kanał '{ChannelDisplayName}' usunięty pomyślnie z zespołu ID: {TeamId}.", decodedChannelDisplayName, teamId);
                // TODO: Rozważyć aktualizację lokalnej bazy danych (oznaczenie kanału jako usunięty/nieaktywny).
                return Ok(new { Message = "Kanał usunięty pomyślnie." });
            }
            _logger.LogWarning("Nie udało się usunąć kanału '{ChannelDisplayName}' z zespołu ID: {TeamId}.", decodedChannelDisplayName, teamId);
            // PowerShellService powinien zalogować dokładniejszy błąd.
            return BadRequest(new { Message = "Nie udało się usunąć kanału." });
        }
    }
}