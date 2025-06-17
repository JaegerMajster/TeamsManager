using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Abstractions.Services.Graph
{
    /// <summary>
    /// Serwis zarządzający operacjami masowymi w Microsoft 365 przez Graph API
    /// Implementacja operacji masowych z pełnym wsparciem dla Graph Batch API
    /// </summary>
    public interface IGraphBulkOperationsService
    {
        /// <summary>
        /// Masowo dodaje użytkowników do zespołu
        /// Graph API Endpoint: POST /v1.0/$batch (with POST /v1.0/teams/{team-id}/members requests)
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpns">Lista UPN użytkowników do dodania</param>
        /// <param name="role">Rola użytkowników (Member/Owner)</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Słownik z wynikami operacji dla każdego użytkownika</returns>
        Task<Dictionary<string, bool>> BulkAddUsersToTeamAsync(
            string teamId,
            List<string> userUpns,
            string role = "Member",
            IProgress<BulkOperationProgress>? progress = null);

        /// <summary>
        /// Masowo usuwa użytkowników z zespołu
        /// Graph API Endpoint: POST /v1.0/$batch (with DELETE /v1.0/teams/{team-id}/members/{membership-id} requests)
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpns">Lista UPN użytkowników do usunięcia</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Słownik z wynikami operacji dla każdego użytkownika</returns>
        Task<Dictionary<string, bool>> BulkRemoveUsersFromTeamAsync(
            string teamId,
            List<string> userUpns,
            IProgress<BulkOperationProgress>? progress = null);

        /// <summary>
        /// Masowo archiwizuje zespoły
        /// Graph API Endpoint: POST /v1.0/$batch (with POST /v1.0/teams/{team-id}/archive requests)
        /// </summary>
        /// <param name="teamIds">Lista ID zespołów do archiwizacji</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Słownik z wynikami operacji dla każdego zespołu</returns>
        Task<Dictionary<string, bool>> BulkArchiveTeamsAsync(
            List<string> teamIds,
            IProgress<BulkOperationProgress>? progress = null);

        /// <summary>
        /// Masowo archiwizuje zespoły (wersja z batch size dla orkiestratora)
        /// Graph API Endpoint: POST /v1.0/$batch (max 20 requests per batch - Graph API limit)
        /// </summary>
        /// <param name="teamIds">Lista ID zespołów do archiwizacji</param>
        /// <param name="accessToken">Token dostępu API</param>
        /// <param name="batchSize">Rozmiar partii (max 20 dla Graph API)</param>
        /// <returns>Wynik operacji masowej z Graph API specyfiką</returns>
        Task<GraphBulkResult> ArchiveTeamsAsync(string[] teamIds, string accessToken, int batchSize = 20);

        /// <summary>
        /// Masowo tworzy zespoły (dla orkiestratora)
        /// Graph API Endpoint: POST /v1.0/$batch (with POST /v1.0/teams requests)
        /// </summary>
        /// <param name="teamCreateRequests">Lista żądań tworzenia zespołów</param>
        /// <param name="accessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z Graph API specyfiką</returns>
        Task<GraphBulkResult> CreateTeamsAsync(GraphBatchOperation[] teamCreateRequests, string accessToken);

        /// <summary>
        /// Masowo aktualizuje właściwości użytkowników
        /// Graph API Endpoint: POST /v1.0/$batch (with PATCH /v1.0/users/{user-id} requests)
        /// </summary>
        /// <param name="userUpdates">Słownik gdzie klucz to UPN użytkownika, a wartość to słownik właściwości do aktualizacji</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Słownik z wynikami operacji dla każdego użytkownika</returns>
        Task<Dictionary<string, bool>> BulkUpdateUserPropertiesAsync(
            Dictionary<string, Dictionary<string, string>> userUpdates,
            IProgress<BulkOperationProgress>? progress = null);

        /// <summary>
        /// Archiwizuje zespół i dezaktywuje użytkowników, którzy są tylko w tym zespole
        /// Graph API Endpoints: POST /v1.0/teams/{team-id}/archive + PATCH /v1.0/users/{user-id}
        /// </summary>
        /// <param name="teamId">ID zespołu do archiwizacji</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Słownik z wynikiem operacji dla zespołu</returns>
        Task<Dictionary<string, bool>> ArchiveTeamAndDeactivateExclusiveUsersAsync(
            string teamId,
            IProgress<BulkOperationProgress>? progress = null);

        /// <summary>
        /// Synchronizuje członkostwo zespołu z docelową listą użytkowników (NOWA FUNKCJONALNOŚĆ)
        /// Graph API Endpoints: GET /v1.0/teams/{team-id}/members + batch operations for add/remove
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="targetUserUpns">Docelowa lista UPN użytkowników</param>
        /// <param name="defaultRole">Domyślna rola dla nowych członków</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Wynik synchronizacji członkostwa</returns>
        Task<GraphBulkResult> SynchronizeTeamMembershipAsync(
            string teamId,
            List<string> targetUserUpns,
            string defaultRole = "Member",
            IProgress<BulkOperationProgress>? progress = null);

        #region Enhanced V2 Methods with GraphBulkResult

        /// <summary>
        /// Masowo dodaje użytkowników do zespołu z zaawansowanym raportowaniem Graph API
        /// Graph API Endpoint: POST /v1.0/$batch z szczegółowym rate limiting
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpns">Lista UPN użytkowników do dodania</param>
        /// <param name="role">Rola użytkowników (Member/Owner)</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Słownik z szczegółowymi wynikami Graph API dla każdego użytkownika</returns>
        Task<Dictionary<string, GraphBulkResult>> BulkAddUsersToTeamV2Async(
            string teamId,
            List<string> userUpns,
            string role = "Member",
            IProgress<BulkOperationProgress>? progress = null);

        /// <summary>
        /// Masowo usuwa użytkowników z zespołu z zaawansowanym raportowaniem Graph API
        /// Graph API Endpoint: POST /v1.0/$batch z szczegółowym rate limiting
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpns">Lista UPN użytkowników do usunięcia</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Słownik z szczegółowymi wynikami Graph API dla każdego użytkownika</returns>
        Task<Dictionary<string, GraphBulkResult>> BulkRemoveUsersFromTeamV2Async(
            string teamId,
            List<string> userUpns,
            IProgress<BulkOperationProgress>? progress = null);

        /// <summary>
        /// Masowo archiwizuje zespoły z zaawansowanym raportowaniem Graph API
        /// Graph API Endpoint: POST /v1.0/$batch z szczegółowym rate limiting
        /// </summary>
        /// <param name="teamIds">Lista ID zespołów do archiwizacji</param>
        /// <param name="progress">Progress reporter (opcjonalny)</param>
        /// <returns>Słownik z szczegółowymi wynikami Graph API dla każdego zespołu</returns>
        Task<Dictionary<string, GraphBulkResult>> BulkArchiveTeamsV2Async(
            List<string> teamIds,
            IProgress<BulkOperationProgress>? progress = null);

        #endregion

        #region Rate Limiting & Batch Management

        /// <summary>
        /// Sprawdza aktualny stan rate limiting dla Graph API
        /// Graph API Headers: X-RateLimit-Remaining, Retry-After
        /// </summary>
        /// <returns>Informacje o rate limiting</returns>
        Task<GraphRateLimitStatus> GetRateLimitStatusAsync();

        /// <summary>
        /// Wykonuje operację batch z automatycznym rate limiting
        /// Graph API Endpoint: POST /v1.0/$batch
        /// </summary>
        /// <param name="batchOperations">Lista operacji do wykonania</param>
        /// <param name="accessToken">Token dostępu</param>
        /// <param name="respectRateLimit">Czy respektować rate limiting</param>
        /// <returns>Wyniki operacji batch</returns>
        Task<GraphBulkResult> ExecuteBatchOperationsAsync(
            List<GraphBatchOperation> batchOperations,
            string accessToken,
            bool respectRateLimit = true);

        #endregion
    }
} 