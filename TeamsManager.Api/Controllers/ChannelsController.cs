// Plik: TeamsManager.Api/Controllers/ChannelsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using TeamsManager.Core.Abstractions; // Dla ICurrentUserService
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Api.Extensions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic; // Dodane dla List w DTO
using TeamsManager.Core.Models;

namespace TeamsManager.Api.Controllers
{
    public class CreateChannelRequestDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPrivate { get; set; } = false;
        // Można dodać np. Owner UPN jeśli tworzymy kanał prywatny i chcemy od razu dodać właściciela
    }

    public class UpdateChannelRequestDto
    {
        public string? NewDisplayName { get; set; }
        public string? NewDescription { get; set; }
        // Inne właściwości, które można aktualizować, np. IsFavoriteByDefault (jeśli Graph API na to pozwala przez to polecenie)
    }


    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/teams/{teamId}/[controller]")]
    [Authorize]
    public class ChannelsController : ControllerBase
    {
        private readonly IChannelService _channelService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ChannelsController> _logger;

        public ChannelsController(
            IChannelService channelService,
            ICurrentUserService currentUserService,
            ILogger<ChannelsController> logger)
        {
            _channelService = channelService ?? throw new ArgumentNullException(nameof(channelService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }



        [HttpGet]
        public async Task<IActionResult> GetTeamChannels(string teamId, [FromQuery] bool forceRefresh = false)
        {
            _logger.LogInformation("API: Pobieranie kanałów dla lokalnego zespołu ID: {TeamId}", teamId);
            var accessToken = await HttpContext.GetBearerTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Nie znaleziono tokenu dostępu w nagłówku Authorization.");
                return Unauthorized(new { Message = "Brak tokenu dostępu." });
            }

            var channels = await _channelService.GetTeamChannelsAsync(teamId, accessToken, forceRefresh);
            if (channels == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { Message = "Nie można pobrać kanałów. Problem z usługą zależną lub zespołem." });
            }
            return Ok(channels);
        }

        [HttpGet("{channelGraphId}")]
        public async Task<IActionResult> GetTeamChannelById(string teamId, string channelGraphId, [FromQuery] bool forceRefresh = false)
        {
            _logger.LogInformation("API: Pobieranie kanału GraphID: {ChannelGraphId} dla lokalnego zespołu ID: {TeamId}", channelGraphId, teamId);
            var accessToken = await HttpContext.GetBearerTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Nie znaleziono tokenu dostępu w nagłówku Authorization.");
                return Unauthorized(new { Message = "Brak tokenu dostępu." });
            }

            var channel = await _channelService.GetTeamChannelByIdAsync(teamId, channelGraphId, accessToken, forceRefresh);
            if (channel == null)
            {
                _logger.LogInformation("API: Kanał GraphID: {ChannelGraphId} w zespole ID: {TeamId} nie został znaleziony.", channelGraphId, teamId);
                return NotFound(new { Message = $"Kanał o GraphID '{channelGraphId}' w zespole ID '{teamId}' nie został znaleziony." });
            }
            return Ok(channel);
        }

        [HttpGet("byName/{channelDisplayName}")]
        public async Task<IActionResult> GetTeamChannelByDisplayName(string teamId, string channelDisplayName, [FromQuery] bool forceRefresh = false)
        {
            var decodedChannelDisplayName = System.Net.WebUtility.UrlDecode(channelDisplayName);
            _logger.LogInformation("API: Pobieranie kanału '{ChannelDisplayName}' dla zespołu ID: {TeamId} (metoda po nazwie)", decodedChannelDisplayName, teamId);
            var accessToken = await HttpContext.GetBearerTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Nie znaleziono tokenu dostępu w nagłówku Authorization.");
                return Unauthorized(new { Message = "Brak tokenu dostępu." });
            }

            var channel = await _channelService.GetTeamChannelByDisplayNameAsync(teamId, decodedChannelDisplayName, accessToken, forceRefresh);
            if (channel == null)
            {
                _logger.LogInformation("API: Kanał '{ChannelDisplayName}' w zespole ID: {TeamId} nie został znaleziony.", decodedChannelDisplayName, teamId);
                return NotFound(new { Message = $"Kanał '{decodedChannelDisplayName}' w zespole ID '{teamId}' nie został znaleziony." });
            }
            return Ok(channel);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeamChannel(string teamId, [FromBody] CreateChannelRequestDto requestDto)
        {
            _logger.LogInformation("API: Żądanie utworzenia kanału '{DisplayName}' w zespole ID: {TeamId}", requestDto.DisplayName, teamId);
            var accessToken = await HttpContext.GetBearerTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Nie znaleziono tokenu dostępu w nagłówku Authorization.");
                return Unauthorized(new { Message = "Brak tokenu dostępu." });
            }

            var createdChannel = await _channelService.CreateTeamChannelAsync(teamId, requestDto.DisplayName, accessToken, requestDto.Description, requestDto.IsPrivate);
            if (createdChannel == null)
            {
                _logger.LogWarning("API: Nie udało się utworzyć kanału '{DisplayName}' w zespole ID: {TeamId}.", requestDto.DisplayName, teamId);
                return BadRequest(new { Message = "Nie udało się utworzyć kanału. Sprawdź logi serwera." });
            }

            _logger.LogInformation("API: Kanał '{DisplayName}' (GraphID: {ChannelGraphId}) utworzony pomyślnie w zespole ID: {TeamId}.", createdChannel.DisplayName, createdChannel.Id, teamId);
            // Zwracamy nowy obiekt Channel z lokalnej bazy (który powinien mieć GraphID jako Id)
            return CreatedAtAction(nameof(GetTeamChannelById), new { teamId = teamId, channelGraphId = createdChannel.Id }, createdChannel);
        }

        [HttpPut("{channelId}")] // Używamy channelId (GraphID)
        public async Task<IActionResult> UpdateTeamChannel(string teamId, string channelId, [FromBody] UpdateChannelRequestDto requestDto)
        {
            _logger.LogInformation("API: Żądanie aktualizacji kanału GraphID: {ChannelId} w zespole ID: {TeamId}", channelId, teamId);
            var accessToken = await HttpContext.GetBearerTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Nie znaleziono tokenu dostępu w nagłówku Authorization.");
                return Unauthorized(new { Message = "Brak tokenu dostępu." });
            }

            if (string.IsNullOrWhiteSpace(requestDto.NewDisplayName) && requestDto.NewDescription == null) // Sprawdzamy czy NewDescription jest null, bo może być ""
            {
                return BadRequest(new { Message = "Należy podać przynajmniej nową nazwę lub nowy opis." });
            }

            // Serwis UpdateTeamChannelAsync przyjmuje channelId.
            // currentDisplayName nie jest już potrzebne w sygnaturze serwisu, jeśli operujemy na ID.
            var updatedChannel = await _channelService.UpdateTeamChannelAsync(teamId, channelId, accessToken, requestDto.NewDisplayName, requestDto.NewDescription);

            if (updatedChannel != null)
            {
                _logger.LogInformation("API: Kanał GraphID: {ChannelId} w zespole ID: {TeamId} zaktualizowany pomyślnie.", channelId, teamId);
                return Ok(updatedChannel);
            }
            _logger.LogWarning("API: Nie udało się zaktualizować kanału GraphID: {ChannelId} w zespole ID: {TeamId}.", channelId, teamId);
            return BadRequest(new { Message = "Nie udało się zaktualizować kanału." });
        }

        [HttpDelete("{channelId}")] // Używamy channelId (GraphID)
        public async Task<IActionResult> RemoveTeamChannel(string teamId, string channelId)
        {
            _logger.LogInformation("API: Żądanie usunięcia kanału GraphID: {ChannelId} z zespołu ID: {TeamId}", channelId, teamId);
            var accessToken = await HttpContext.GetBearerTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Nie znaleziono tokenu dostępu w nagłówku Authorization.");
                return Unauthorized(new { Message = "Brak tokenu dostępu." });
            }

            // ChannelService.RemoveTeamChannelAsync teraz sam weryfikuje czy kanał nie jest "Generalny" na podstawie danych z DB
            var success = await _channelService.RemoveTeamChannelAsync(teamId, channelId, accessToken);
            if (success)
            {
                _logger.LogInformation("API: Kanał GraphID: {ChannelId} usunięty pomyślnie z zespołu ID: {TeamId}.", channelId, teamId);
                return Ok(new { Message = "Kanał usunięty pomyślnie." });
            }
            _logger.LogWarning("API: Nie udało się usunąć kanału GraphID: {ChannelId} z zespołu ID: {TeamId}.", channelId, teamId);
            // Serwis powinien zalogować dokładniejszy błąd (np. kanał nie znaleziony, próba usunięcia Generalnego, błąd Graph)
            // Możemy też zwrócić NotFound jeśli serwis by to sygnalizował.
            var channelExists = await _channelService.GetTeamChannelByIdAsync(teamId, channelId, accessToken, forceRefresh: true);
            if (channelExists == null)
            {
                return NotFound(new { Message = $"Kanał o GraphID '{channelId}' w zespole '{teamId}' nie został znaleziony." });
            }
            return BadRequest(new { Message = "Nie udało się usunąć kanału. Sprawdź, czy kanał nie jest kanałem 'General/Ogólny' lub czy nie wystąpił inny błąd." });
        }
    }
}