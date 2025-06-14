using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Abstractions.Services.Graph
{
    /// <summary>
    /// Interfejs serwisu zarządzania połączeniem z Microsoft Graph API.
    /// Zapewnia funkcjonalności diagnostyki, walidacji uprawnień i monitorowania zdrowia połączenia.
    /// </summary>
    public interface IGraphConnectionService
    {
        /// <summary>
        /// Sprawdza status połączenia z Microsoft Graph API.
        /// Endpoint: GET /v1.0/me
        /// </summary>
        /// <returns>Informacje o stanie połączenia</returns>
        Task<GraphConnectionHealthInfo> GetConnectionHealthAsync();

        /// <summary>
        /// Wykonuje pełną diagnostykę połączenia z Graph API.
        /// Endpoints: GET /v1.0/me, GET /v1.0/organization, GET /v1.0/applications/{id}
        /// </summary>
        /// <returns>Szczegółowe informacje diagnostyczne</returns>
        Task<GraphDiagnosticInfo> GetDiagnosticInfoAsync();

        /// <summary>
        /// Sprawdza uprawnienia aplikacji w Graph API.
        /// Endpoint: GET /v1.0/me/appRoleAssignments
        /// </summary>
        /// <returns>Informacje o uprawnieniach</returns>
        Task<GraphPermissionInfo> GetPermissionInfoAsync();

        /// <summary>
        /// Wykonuje test połączenia z różnymi endpointami Graph API.
        /// Endpoints: GET /v1.0/me, GET /v1.0/users, GET /v1.0/groups, GET /v1.0/teams
        /// </summary>
        /// <returns>Wyniki testów połączenia</returns>
        Task<GraphConnectionTestResult> TestConnectionAsync();

        /// <summary>
        /// Sprawdza dostępność konkretnego endpointu Graph API.
        /// </summary>
        /// <param name="endpoint">Endpoint do sprawdzenia (np. "/v1.0/users")</param>
        /// <returns>Informacje o dostępności endpointu</returns>
        Task<GraphApiAvailability> CheckEndpointAvailabilityAsync(string endpoint);

        /// <summary>
        /// Pobiera kontekst użytkownika z Graph API.
        /// Endpoint: GET /v1.0/me
        /// </summary>
        /// <returns>Kontekst użytkownika</returns>
        Task<GraphUserContext> GetUserContextAsync();

        /// <summary>
        /// Sprawdza status rate limiting dla Graph API.
        /// Analizuje nagłówki odpowiedzi HTTP.
        /// </summary>
        /// <returns>Status rate limiting</returns>
        Task<GraphRateLimitStatus> GetRateLimitStatusAsync();

        /// <summary>
        /// Wykonuje żądanie batch do Graph API.
        /// Endpoint: POST /v1.0/$batch
        /// </summary>
        /// <param name="requests">Lista żądań do wykonania</param>
        /// <returns>Odpowiedzi batch</returns>
        Task<GraphBatchResponse> ExecuteBatchRequestAsync(IEnumerable<GraphBatchRequest> requests);

        /// <summary>
        /// Sprawdza czy token dostępu jest ważny i nie wygasł.
        /// </summary>
        /// <returns>True jeśli token jest ważny</returns>
        Task<bool> IsTokenValidAsync();

        /// <summary>
        /// Odświeża token dostępu jeśli to konieczne.
        /// </summary>
        /// <returns>True jeśli token został odświeżony</returns>
        Task<bool> RefreshTokenIfNeededAsync();

        /// <summary>
        /// Pobiera szczegółowe informacje o błędzie Graph API.
        /// </summary>
        /// <param name="exception">Wyjątek do analizy</param>
        /// <returns>Szczegółowe informacje o błędzie</returns>
        GraphApiError AnalyzeGraphError(Exception exception);
    }
} 