namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Status zdrowia połączenia z Microsoft Graph API.
    /// </summary>
    public enum GraphHealthStatus
    {
        /// <summary>
        /// Połączenie jest w pełni sprawne.
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// Połączenie działa, ale występują ostrzeżenia.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Połączenie ma problemy, ale jest częściowo funkcjonalne.
        /// </summary>
        Degraded = 2,

        /// <summary>
        /// Połączenie nie działa.
        /// </summary>
        Critical = 3,

        /// <summary>
        /// Status nieznany.
        /// </summary>
        Unknown = 4
    }
} 