// Plik: TeamsManager.Core/Abstractions/Services/IChannelService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models; // Dla Channel

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową kanałów w zespołach Microsoft Teams.
    /// </summary>
    public interface IChannelService
    {
        /// <summary>
        /// Asynchronicznie pobiera wszystkie kanały dla określonego zespołu.
        /// Synchronizuje pobrane dane z lokalną bazą danych.
        /// </summary>
        /// <param name="teamId">Identyfikator lokalnego zespołu (Team.Id).</param>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO).</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja obiektów Channel z lokalnej bazy (zaktualizowana o dane z Graph) lub null w przypadku błędu połączenia z Graph.</returns>
        Task<IEnumerable<Channel>?> GetTeamChannelsAsync(string teamId, string apiAccessToken, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera konkretny kanał zespołu na podstawie jego identyfikatora Graph.
        /// Synchronizuje pobrane dane z lokalną bazą danych.
        /// </summary>
        /// <param name="teamId">Identyfikator lokalnego zespołu (Team.Id).</param>
        /// <param name="channelGraphId">ID kanału w Microsoft Graph.</param>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO).</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Obiekt Channel z lokalnej bazy (zaktualizowany o dane z Graph) lub null, jeśli nie znaleziono lub w przypadku błędu.</returns>
        Task<Channel?> GetTeamChannelByIdAsync(string teamId, string channelGraphId, string apiAccessToken, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera konkretny kanał zespołu na podstawie jego nazwy wyświetlanej.
        /// Ta metoda może być mniej precyzyjna niż GetTeamChannelByIdAsync, jeśli nazwy kanałów nie są unikalne.
        /// Synchronizuje pobrane dane z lokalną bazą danych.
        /// </summary>
        /// <param name="teamId">Identyfikator lokalnego zespołu (Team.Id).</param>
        /// <param name="channelDisplayName">Nazwa wyświetlana kanału.</param>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO).</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Obiekt Channel z lokalnej bazy (zaktualizowany o dane z Graph) lub null, jeśli nie znaleziono lub w przypadku błędu.</returns>
        Task<Channel?> GetTeamChannelByDisplayNameAsync(string teamId, string channelDisplayName, string apiAccessToken, bool forceRefresh = false);


        /// <summary>
        /// Asynchronicznie tworzy nowy kanał w zespole Microsoft Teams i zapisuje jego reprezentację w lokalnej bazie danych.
        /// </summary>
        /// <param name="teamId">Identyfikator lokalnego zespołu (Team.Id).</param>
        /// <param name="displayName">Nazwa wyświetlana nowego kanału.</param>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO).</param>
        /// <param name="description">Opcjonalny opis kanału.</param>
        /// <param name="isPrivate">Czy kanał ma być prywatny.</param>
        /// <returns>Utworzony i zapisany lokalnie obiekt Channel lub null w przypadku błędu.</returns>
        Task<Channel?> CreateTeamChannelAsync(string teamId, string displayName, string apiAccessToken, string? description = null, bool isPrivate = false);

        /// <summary>
        /// Asynchronicznie aktualizuje właściwości istniejącego kanału w Microsoft Teams i w lokalnej bazie danych.
        /// </summary>
        /// <param name="teamId">Identyfikator lokalnego zespołu (Team.Id).</param>
        /// <param name="channelId">Identyfikator kanału w Microsoft Graph (Channel.Id, które jest Graph ID).</param>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO).</param>
        /// <param name="newDisplayName">Nowa nazwa wyświetlana kanału (opcjonalna).</param>
        /// <param name="newDescription">Nowy opis kanału (opcjonalny).</param>
        /// <returns>Zaktualizowany obiekt Channel z lokalnej bazy lub null, jeśli operacja się nie powiodła.</returns>
        Task<Channel?> UpdateTeamChannelAsync(string teamId, string channelId, string apiAccessToken, string? newDisplayName = null, string? newDescription = null);

        /// <summary>
        /// Asynchronicznie usuwa kanał z Microsoft Teams i oznacza go jako nieaktywny w lokalnej bazie danych.
        /// Kanał "General" ("Ogólny") nie może być usunięty.
        /// </summary>
        /// <param name="teamId">Identyfikator lokalnego zespołu (Team.Id).</param>
        /// <param name="channelId">Identyfikator kanału w Microsoft Graph (Channel.Id).</param>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO).</param>
        /// <returns>True, jeśli operacja usunięcia z Microsoft Teams się powiodła i lokalny rekord został zaktualizowany.</returns>
        Task<bool> RemoveTeamChannelAsync(string teamId, string channelId, string apiAccessToken);

        /// <summary>
        /// Odświeża cache kanałów dla określonego zespołu (jeśli jest używany).
        /// </summary>
        /// <param name="teamId">Identyfikator lokalnego zespołu (Team.Id).</param>
        Task RefreshChannelCacheAsync(string teamId);
    }
}