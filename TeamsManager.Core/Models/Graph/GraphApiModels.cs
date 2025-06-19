using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Informacje o dostępności endpointu Graph API.
    /// Konsoliduje GraphApiAvailability, GraphEndpointInfo i GraphEndpointTestResult.
    /// </summary>
    public class GraphApiAvailability
    {
        /// <summary>
        /// Nazwa endpointu.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// URL endpointu.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Opis endpointu.
        /// </summary>
        public string? Description { get; set; }

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
        /// Szczegóły błędu.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Komunikat o stanie endpointu.
        /// </summary>
        public string ResponseMessage => ErrorMessage ?? (IsAvailable ? "Endpoint dostępny" : "Endpoint niedostępny");

        /// <summary>
        /// Czas sprawdzenia.
        /// </summary>
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Wymagane uprawnienia dla endpointu.
        /// </summary>
        public List<string> RequiredPermissions { get; set; } = new List<string>();

        /// <summary>
        /// Czy ostatnie sprawdzenie było pomyślne.
        /// </summary>
        public bool IsSuccessful => IsAvailable && HttpStatusCode >= 200 && HttpStatusCode < 300;

        // Kompatybilność z poprzednim kodem
        /// <summary>
        /// Alias dla Name (kompatybilność wsteczna).
        /// </summary>
        public string Endpoint 
        { 
            get => Name; 
            set => Name = value; 
        }
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
        /// Nazwa główna użytkownika.
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
    /// Konsoliduje GraphRateLimitStatus, GraphRateLimitInfo i GraphQuotaInfo.
    /// </summary>
    public class GraphRateLimitStatus
    {
        /// <summary>
        /// Czy osiągnięto limit.
        /// </summary>
        public bool IsLimitReached { get; set; }

        /// <summary>
        /// Czy API jest obecnie throttled.
        /// </summary>
        public bool IsThrottled { get; set; }

        /// <summary>
        /// Pozostała liczba żądań.
        /// </summary>
        public int? RemainingRequests { get; set; }

        /// <summary>
        /// Maksymalna liczba żądań.
        /// </summary>
        public int? MaxRequests { get; set; }

        /// <summary>
        /// Maksymalna liczba żądań na minutę.
        /// </summary>
        public int? MaxRequestsPerMinute { get; set; }

        /// <summary>
        /// Aktualna liczba żądań w tej minucie.
        /// </summary>
        public int CurrentRequestsThisMinute { get; set; }

        /// <summary>
        /// Pozostała liczba żądań w tej minucie.
        /// </summary>
        public int? RemainingRequestsThisMinute { get; set; }

        /// <summary>
        /// Liczba żądań na minutę.
        /// </summary>
        public int RequestsPerMinute { get; set; }

        /// <summary>
        /// Czas resetowania limitu.
        /// </summary>
        public DateTime? ResetTime { get; set; }

        /// <summary>
        /// Czas do następnego resetu w sekundach.
        /// </summary>
        public int? RetryAfterSeconds { get; set; }

        /// <summary>
        /// Zalecany czas oczekiwania w sekundach.
        /// </summary>
        public int? RecommendedDelaySeconds { get; set; }

        /// <summary>
        /// Typ limitu.
        /// </summary>
        public string? LimitType { get; set; }

        /// <summary>
        /// Procent wykorzystania limitu.
        /// </summary>
        public double? UsagePercentage { get; set; }
    }



    /// <summary>
    /// Żądanie batch Graph API.
    /// Kontener dla listy pojedynczych żądań (Clean Architecture).
    /// </summary>
    public class GraphBatchRequest
    {
        /// <summary>
        /// Lista żądań w batchu.
        /// </summary>
        public List<GraphBatchRequestItem> Requests { get; set; } = new List<GraphBatchRequestItem>();
    }

    /// <summary>
    /// Element żądania batch Graph API.
    /// </summary>
    public class GraphBatchRequestItem
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

    // ===== GRAPH MAIL API MODELS =====

    /// <summary>
    /// Model żądania wysłania emaila przez Graph Mail API
    /// </summary>
    public class GraphSendMailRequest
    {
        /// <summary>
        /// Wiadomość do wysłania
        /// </summary>
        public GraphMessage Message { get; set; } = new();

        /// <summary>
        /// Czy zapisać kopię w folderze Wysłane
        /// </summary>
        public bool SaveToSentItems { get; set; } = true;
    }

    /// <summary>
    /// Model wiadomości email w Graph API
    /// </summary>
    public class GraphMessage
    {
        /// <summary>
        /// ID wiadomości
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Temat wiadomości
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// Treść wiadomości
        /// </summary>
        public GraphMessageBody Body { get; set; } = new();

        /// <summary>
        /// Nadawca wiadomości
        /// </summary>
        public GraphEmailAddress? From { get; set; }

        /// <summary>
        /// Lista odbiorców (To)
        /// </summary>
        public List<GraphEmailAddress> ToRecipients { get; set; } = new();

        /// <summary>
        /// Lista odbiorców w kopii (CC)
        /// </summary>
        public List<GraphEmailAddress> CcRecipients { get; set; } = new();

        /// <summary>
        /// Lista odbiorców w ukrytej kopii (BCC)
        /// </summary>
        public List<GraphEmailAddress> BccRecipients { get; set; } = new();

        /// <summary>
        /// Lista adresów odpowiedzi
        /// </summary>
        public List<GraphEmailAddress> ReplyTo { get; set; } = new();

        /// <summary>
        /// Ważność wiadomości
        /// </summary>
        public string? Importance { get; set; } = "normal";

        /// <summary>
        /// Data utworzenia
        /// </summary>
        public DateTime? CreatedDateTime { get; set; }

        /// <summary>
        /// Data wysłania
        /// </summary>
        public DateTime? SentDateTime { get; set; }

        /// <summary>
        /// Data otrzymania
        /// </summary>
        public DateTime? ReceivedDateTime { get; set; }

        /// <summary>
        /// Czy wiadomość została przeczytana
        /// </summary>
        public bool? IsRead { get; set; }

        /// <summary>
        /// Czy to szkic
        /// </summary>
        public bool? IsDraft { get; set; }

        /// <summary>
        /// Lista załączników
        /// </summary>
        public List<GraphAttachment>? Attachments { get; set; }
    }

    /// <summary>
    /// Treść wiadomości email
    /// </summary>
    public class GraphMessageBody
    {
        /// <summary>
        /// Typ treści (HTML, Text)
        /// </summary>
        public string ContentType { get; set; } = "HTML";

        /// <summary>
        /// Zawartość
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Adres email
    /// </summary>
    public class GraphEmailAddress
    {
        /// <summary>
        /// Nazwa wyświetlana
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Adres email
        /// </summary>
        public string Address { get; set; } = string.Empty;
    }

    /// <summary>
    /// Załącznik wiadomości
    /// </summary>
    public class GraphAttachment
    {
        /// <summary>
        /// ID załącznika
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Typ OData
        /// </summary>
        public string? ODataType { get; set; }

        /// <summary>
        /// Nazwa pliku
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Typ zawartości
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Rozmiar w bajtach
        /// </summary>
        public int? Size { get; set; }

        /// <summary>
        /// Czy załącznik jest inline
        /// </summary>
        public bool? IsInline { get; set; }

        /// <summary>
        /// Zawartość zakodowana w Base64
        /// </summary>
        public string? ContentBytes { get; set; }
    }

    /// <summary>
    /// Odpowiedź z listą wiadomości
    /// </summary>
    public class GraphMessagesResponse
    {
        /// <summary>
        /// Lista wiadomości
        /// </summary>
        public List<GraphMessage> Value { get; set; } = new();

        /// <summary>
        /// Link do kolejnej strony
        /// </summary>
        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }

        /// <summary>
        /// Liczba wszystkich elementów
        /// </summary>
        [JsonPropertyName("@odata.count")]
        public int? Count { get; set; }
    }



    /// <summary>
    /// Metryki Graph API (rozszerzone).
    /// </summary>
    public class GraphMetricsInfo
    {
        /// <summary>
        /// Czas odpowiedzi Graph API w milisekundach.
        /// </summary>
        public long GraphApiResponseTime { get; set; }

        /// <summary>
        /// Liczba wykonanych żądań.
        /// </summary>
        public int RequestCount { get; set; }

        /// <summary>
        /// Liczba błędów.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Średni czas odpowiedzi.
        /// </summary>
        public double AverageResponseTime { get; set; }

        /// <summary>
        /// Status rate limiting.
        /// </summary>
        public GraphRateLimitStatus? RateLimitStatus { get; set; }

        /// <summary>
        /// Czas ostatniego pomiaru.
        /// </summary>
        public DateTime LastMeasurement { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Procent błędów.
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Procent trafień cache.
        /// </summary>
        public double CacheHitRate { get; set; }

        /// <summary>
        /// Liczba operacji na Teams dzisiaj.
        /// </summary>
        public int TeamsOperationsCount { get; set; }

        /// <summary>
        /// Liczba operacji na użytkownikach dzisiaj.
        /// </summary>
        public int UsersOperationsCount { get; set; }

        /// <summary>
        /// Liczba operacji na kanałach dzisiaj.
        /// </summary>
        public int ChannelsOperationsCount { get; set; }

        /// <summary>
        /// Liczba żądań na minutę.
        /// </summary>
        public int RequestsPerMinute { get; set; }

        /// <summary>
        /// Liczba operacji batch.
        /// </summary>
        public int BatchOperationsCount { get; set; }

        /// <summary>
        /// Liczba błędów w ostatniej godzinie.
        /// </summary>
        public int ErrorsLastHour { get; set; }

        /// <summary>
        /// Liczba ostrzeżeń w ostatniej godzinie.
        /// </summary>
        public int WarningsLastHour { get; set; }
    }

    /// <summary>
    /// Informacje o cache Graph API.
    /// </summary>
    public class GraphCacheInfo
    {
        /// <summary>
        /// Rozmiar cache w bajtach.
        /// </summary>
        public long CacheSize { get; set; }

        /// <summary>
        /// Liczba elementów w cache.
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// Procent trafień cache.
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Czas ostatniego czyszczenia cache.
        /// </summary>
        public DateTime? LastCleared { get; set; }

        /// <summary>
        /// Status cache.
        /// </summary>
        public string Status { get; set; } = "Aktywny";
    }

    /// <summary>
    /// Informacje o zdrowiu połączenia z Graph API.
    /// Konsoliduje GraphConnectionStatus i GraphConnectionHealthInfo.
    /// </summary>
    public class GraphConnectionHealthInfo
    {
        /// <summary>
        /// Czy połączenie jest aktywne.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Czy token jest ważny.
        /// </summary>
        public bool IsTokenValid { get; set; }

        /// <summary>
        /// Status zdrowia połączenia.
        /// </summary>
        public GraphHealthStatus Status { get; set; } = GraphHealthStatus.Unknown;

        /// <summary>
        /// Czas ostatniego sprawdzenia.
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Czas odpowiedzi w milisekundach.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Wersja Graph API.
        /// </summary>
        public string? GraphVersion { get; set; }

        /// <summary>
        /// Komunikat błędu (jeśli wystąpił).
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Czas wygaśnięcia tokenu (używany przez kod API).
        /// </summary>
        public DateTime? TokenExpiresAt { get; set; }

        /// <summary>
        /// Czy połączenie jest zdrowe (IsConnected i IsTokenValid).
        /// </summary>
        public bool IsHealthy => IsConnected && IsTokenValid;
    }

    /// <summary>
    /// Szczegółowe informacje diagnostyczne Graph API.
    /// Konsoliduje wszystkie dane diagnostyczne.
    /// </summary>
    public class GraphDiagnosticInfo
    {
        /// <summary>
        /// Czy połączenie jest aktywne.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Czy użytkownik jest uwierzytelniony.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Status zdrowia diagnostyki.
        /// </summary>
        public GraphHealthStatus Status { get; set; } = GraphHealthStatus.Unknown;

        /// <summary>
        /// Czas odpowiedzi w milisekundach.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Czas ostatniego sprawdzenia.
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Lista błędów diagnostycznych.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Lista ostrzeżeń diagnostycznych.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Czy wszystkie testy przeszły pomyślnie.
        /// </summary>
        public bool AllTestsPassed { get; set; }

        /// <summary>
        /// Czy aplikacja ma wymagane uprawnienia.
        /// </summary>
        public bool HasRequiredPermissions { get; set; }

        /// <summary>
        /// Informacje o rate limiting.
        /// </summary>
        public GraphRateLimitStatus? RateLimitInfo { get; set; }

        /// <summary>
        /// Dodatkowe informacje diagnostyczne.
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Wersja Graph API.
        /// </summary>
        public string? GraphApiVersion { get; set; }

        /// <summary>
        /// ID dzierżawy.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// ID aplikacji.
        /// </summary>
        public string? ApplicationId { get; set; }

        /// <summary>
        /// Właściwości używane przez UI (kompatybilność).
        /// </summary>
        public TimeSpan? LastOperationDuration => TimeSpan.FromMilliseconds(ResponseTimeMs);
        public string GraphApiStatus => Status.ToString();
        public bool IsHealthy => IsConnected && HasRequiredPermissions;
        public bool HasGraphToken => IsAuthenticated;
        public bool HasUserCreationPermissions => HasRequiredPermissions;
        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";
    }

    /// <summary>
    /// Wyniki testów połączenia z Graph API.
    /// Konsoliduje GraphConnectionTestResult i GraphEndpointTestResult.
    /// </summary>
    public class GraphConnectionTestResult
    {
        /// <summary>
        /// Czy połączenie jest aktywne.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Czy użytkownik jest uwierzytelniony.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Czy aplikacja ma wymagane uprawnienia.
        /// </summary>
        public bool HasRequiredPermissions { get; set; }

        /// <summary>
        /// Ogólny wynik testów.
        /// </summary>
        public string OverallResult { get; set; } = string.Empty;

        /// <summary>
        /// Liczba testów które przeszły.
        /// </summary>
        public int PassedTestsCount { get; set; }

        /// <summary>
        /// Całkowita liczba testów.
        /// </summary>
        public int TotalTestsCount { get; set; }

        /// <summary>
        /// Procent pomyślnych testów.
        /// </summary>
        public double SuccessPercentage { get; set; }

        /// <summary>
        /// Czas odpowiedzi w milisekundach.
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// Czas trwania testów.
        /// </summary>
        public TimeSpan TestDuration { get; set; }

        /// <summary>
        /// Lista błędów z testów.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Lista ostrzeżeń z testów.
        /// </summary>
        public List<string> WarningMessages { get; set; } = new List<string>();

        /// <summary>
        /// Dodatkowe informacje o testach.
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Szczegółowe wyniki poszczególnych testów.
        /// </summary>
        public List<GraphTestResult> TestResults { get; set; } = new List<GraphTestResult>();

        /// <summary>
        /// Wyniki testów endpointów.
        /// </summary>
        public List<GraphApiAvailability> EndpointTestResults { get; set; } = new List<GraphApiAvailability>();

        /// <summary>
        /// Informacje o rate limiting.
        /// </summary>
        public GraphRateLimitStatus? RateLimitInfo { get; set; }

        /// <summary>
        /// Średni czas odpowiedzi w milisekundach.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Czy wszystkie testy przeszły pomyślnie.
        /// </summary>
        public bool AllTestsPassed { get; set; }

        /// <summary>
        /// Aliasy dla kompatybilności z kodem API.
        /// </summary>
        public int PassedTests => PassedTestsCount;
        public int TotalTests => TotalTestsCount;
    }

    /// <summary>
    /// Wynik pojedynczego testu Graph API.
    /// </summary>
    public class GraphTestResult
    {
        /// <summary>
        /// Nazwa testu.
        /// </summary>
        public string TestName { get; set; } = string.Empty;

        /// <summary>
        /// Czy test przeszedł pomyślnie.
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Komunikat o wyniku testu.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Czas trwania testu.
        /// </summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Informacje o uprawnieniach Graph API.
    /// Rozszerzone o brakujące właściwości.
    /// </summary>
    public class GraphPermissionInfo
    {
        /// <summary>
        /// Nazwa uprawnienia.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Czy uprawnienie jest przyznane.
        /// </summary>
        public bool IsGranted { get; set; }

        /// <summary>
        /// Typ uprawnienia (Application/Delegated).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Opis uprawnienia.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Czas ostatniego sprawdzenia.
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Lista przyznanych uprawnień.
        /// </summary>
        public List<string> AssignedPermissions { get; set; } = new List<string>();

        /// <summary>
        /// Lista brakujących uprawnień.
        /// </summary>
        public List<string> MissingPermissions { get; set; } = new List<string>();

        /// <summary>
        /// Czy ma wszystkie wymagane uprawnienia.
        /// </summary>
        public bool HasRequiredPermissions { get; set; }

        /// <summary>
        /// Typ uwierzytelnienia.
        /// </summary>
        public string? AuthenticationType { get; set; }

        /// <summary>
        /// Czas wygaśnięcia tokenu.
        /// </summary>
        public DateTime? TokenExpiresAt { get; set; }

        /// <summary>
        /// Nazwa dzierżawy.
        /// </summary>
        public string? TenantName { get; set; }

        /// <summary>
        /// ID aplikacji.
        /// </summary>
        public string? ApplicationId { get; set; }

        /// <summary>
        /// Status uprawnień.
        /// </summary>
        public GraphHealthStatus Status { get; set; } = GraphHealthStatus.Unknown;

        /// <summary>
        /// Czy ma uprawnienie do czytania użytkowników.
        /// </summary>
        public bool HasUserReadPermission { get; set; }

        /// <summary>
        /// Czy ma uprawnienie do czytania zespołów.
        /// </summary>
        public bool HasTeamReadPermission { get; set; }

        /// <summary>
        /// Czy ma uprawnienie do zarządzania użytkownikami.
        /// </summary>
        public bool HasUserManagePermission { get; set; }

        /// <summary>
        /// Czy ma uprawnienie do zarządzania zespołami.
        /// </summary>
        public bool HasTeamManagePermission { get; set; }



        /// <summary>
        /// Czy ma uprawnienie do czytania katalogu.
        /// </summary>
        public bool HasDirectoryReadPermission { get; set; }

        /// <summary>
        /// Czy ma uprawnienie do czytania grup.
        /// </summary>
        public bool HasGroupReadPermission { get; set; }

        /// <summary>
        /// Czy ma uprawnienie do zapisu grup.
        /// </summary>
        public bool HasGroupWritePermission { get; set; }

        /// <summary>
        /// Procent kompletności uprawnień (0-100).
        /// </summary>
        public double PermissionCompleteness
        {
            get
            {
                var requiredPermissions = new[] { "User.Read", "Team.ReadBasic.All", "Group.Read.All" };
                var grantedCount = requiredPermissions.Count(p => AssignedPermissions.Contains(p));
                return requiredPermissions.Length > 0 ? (double)grantedCount / requiredPermissions.Length * 100 : 0;
            }
        }

        /// <summary>
        /// Lista błędów związanych z uprawnieniami.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Czy wszystkie uprawnienia są prawidłowe.
        /// </summary>
        public bool IsValid => HasRequiredPermissions && Errors.Count == 0;

        /// <summary>
        /// Komunikat błędu (pierwszy błąd z listy).
        /// </summary>
        public string? ErrorMessage => Errors.Count > 0 ? Errors.First() : null;

        /// <summary>
        /// Lista dostępnych uprawnień (alias dla AssignedPermissions).
        /// </summary>
        public List<string> AvailablePermissions => AssignedPermissions;

        /// <summary>
        /// Sprawdza czy ma określone uprawnienie.
        /// </summary>
        public bool HasPermission(string permission) => AssignedPermissions.Contains(permission);
    }

    /// <summary>
    /// Standardowe zakresy uprawnień Graph API.
    /// </summary>
    public static class GraphPermissionScopes
    {
        /// <summary>
        /// Minimalne wymagane uprawnienia do działania aplikacji.
        /// </summary>
        public static readonly string[] RequiredPermissions = new[]
        {
            "User.Read",
            "Team.ReadBasic.All",
            "Group.Read.All"
        };

        /// <summary>
        /// Uprawnienia do zarządzania użytkownikami.
        /// </summary>
        public static readonly string[] UserManagementPermissions = new[]
        {
            "User.Read.All",
            "User.ReadWrite.All",
            "Directory.Read.All"
        };

        /// <summary>
        /// Uprawnienia do zarządzania zespołami.
        /// </summary>
        public static readonly string[] TeamManagementPermissions = new[]
        {
            "Team.ReadBasic.All",
            "Team.Create",
            "TeamSettings.ReadWrite.All",
            "Group.ReadWrite.All"
        };

        /// <summary>
        /// Wszystkie uprawnienia używane przez aplikację.
        /// </summary>
        public static readonly string[] AllPermissions = RequiredPermissions
            .Concat(UserManagementPermissions)
            .Concat(TeamManagementPermissions)
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Status zdrowia Graph API.
    /// </summary>
    public enum GraphHealthStatus
    {
        Healthy,
        Warning,
        Critical,
        Unknown
    }
}