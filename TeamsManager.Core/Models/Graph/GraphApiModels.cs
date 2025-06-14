using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Informacje o dostępności endpointu Graph API.
    /// </summary>
    public class GraphApiAvailability
    {
        /// <summary>
        /// Endpoint Graph API.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Czy endpoint jest dostępny.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Czas odpowiedzi w milisekundach.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// Komunikat błędu (jeśli wystąpił).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Czas sprawdzenia.
        /// </summary>
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Kontekst użytkownika z Graph API.
    /// </summary>
    public class GraphUserContext
    {
        /// <summary>
        /// ID użytkownika.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Nazwa użytkownika (UPN).
        /// </summary>
        public string? UserPrincipalName { get; set; }

        /// <summary>
        /// Nazwa wyświetlana.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Adres email.
        /// </summary>
        public string? Mail { get; set; }

        /// <summary>
        /// ID dzierżawy.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Czy użytkownik jest uwierzytelniony.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Role użytkownika.
        /// </summary>
        public List<string> Roles { get; set; } = new List<string>();

        /// <summary>
        /// Uprawnienia użytkownika.
        /// </summary>
        public List<string> Permissions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Status rate limiting Graph API.
    /// </summary>
    public class GraphRateLimitStatus
    {
        /// <summary>
        /// Czy osiągnięto limit.
        /// </summary>
        public bool IsLimitReached { get; set; }

        /// <summary>
        /// Pozostała liczba żądań.
        /// </summary>
        public int? RemainingRequests { get; set; }

        /// <summary>
        /// Maksymalna liczba żądań.
        /// </summary>
        public int? MaxRequests { get; set; }

        /// <summary>
        /// Czas resetowania limitu.
        /// </summary>
        public DateTime? ResetTime { get; set; }

        /// <summary>
        /// Czas do następnego resetu w sekundach.
        /// </summary>
        public int? RetryAfterSeconds { get; set; }

        /// <summary>
        /// Typ limitu.
        /// </summary>
        public string? LimitType { get; set; }

        /// <summary>
        /// Procent wykorzystania limitu.
        /// </summary>
        public double? UsagePercentage
        {
            get
            {
                if (!RemainingRequests.HasValue || !MaxRequests.HasValue || MaxRequests.Value == 0)
                    return null;

                return ((double)(MaxRequests.Value - RemainingRequests.Value) / MaxRequests.Value) * 100;
            }
        }
    }

    /// <summary>
    /// Żądanie batch Graph API.
    /// </summary>
    public class GraphBatchRequest
    {
        /// <summary>
        /// ID żądania.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Metoda HTTP.
        /// </summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>
        /// URL endpointu.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Nagłówki żądania.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Treść żądania.
        /// </summary>
        public object? Body { get; set; }
    }

    /// <summary>
    /// Odpowiedź batch Graph API.
    /// </summary>
    public class GraphBatchResponse
    {
        /// <summary>
        /// Lista odpowiedzi.
        /// </summary>
        public List<GraphBatchResponseItem> Responses { get; set; } = new List<GraphBatchResponseItem>();

        /// <summary>
        /// Czy wszystkie żądania zakończyły się sukcesem.
        /// </summary>
        public bool AllSuccessful => Responses.All(r => r.Status >= 200 && r.Status < 300);

        /// <summary>
        /// Liczba udanych żądań.
        /// </summary>
        public int SuccessfulCount => Responses.Count(r => r.Status >= 200 && r.Status < 300);

        /// <summary>
        /// Liczba nieudanych żądań.
        /// </summary>
        public int FailedCount => Responses.Count - SuccessfulCount;
    }

    /// <summary>
    /// Element odpowiedzi batch Graph API.
    /// </summary>
    public class GraphBatchResponseItem
    {
        /// <summary>
        /// ID żądania.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Nagłówki odpowiedzi.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Treść odpowiedzi.
        /// </summary>
        public object? Body { get; set; }
    }

    /// <summary>
    /// Informacje o błędzie Graph API.
    /// </summary>
    public class GraphApiError
    {
        /// <summary>
        /// Kod błędu.
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// Komunikat błędu.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Szczegóły błędu.
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// ID żądania.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Czas wystąpienia błędu.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Endpoint, na którym wystąpił błąd.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Kod statusu HTTP.
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// Czy błąd można ponowić.
        /// </summary>
        public bool CanRetry { get; set; }

        /// <summary>
        /// Zalecany czas oczekiwania przed ponowieniem (w sekundach).
        /// </summary>
        public int? RetryAfterSeconds { get; set; }
    }
} 