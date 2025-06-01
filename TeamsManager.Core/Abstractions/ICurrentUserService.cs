namespace TeamsManager.Core.Abstractions
{
    /// <summary>
    /// Interfejs serwisu dostarczającego informacje o bieżącym użytkowniku.
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// Pobiera User Principal Name (UPN) aktualnie zalogowanego użytkownika.
        /// </summary>
        /// <returns>UPN użytkownika lub wartość domyślną/null, jeśli użytkownik nie jest zalogowany.</returns>
        string? GetCurrentUserUpn();

        /// <summary>
        /// Ustawia UPN aktualnie zalogowanego użytkownika.
        /// Wywoływane przez system logowania po pomyślnym uwierzytelnieniu.
        /// </summary>
        /// <param name="upn">UPN zalogowanego użytkownika.</param>
        void SetCurrentUserUpn(string? upn);

        /// <summary>
        /// Pobiera unikalny identyfikator (np. ObjectId z Azure AD) aktualnie zalogowanego użytkownika.
        /// </summary>
        /// <returns>ID użytkownika lub null, jeśli użytkownik nie jest zalogowany lub ID nie jest dostępne.</returns>
        string? GetCurrentUserId();

        /// <summary>
        /// Sprawdza czy użytkownik jest zalogowany
        /// </summary>
        bool IsAuthenticated => !string.IsNullOrWhiteSpace(GetCurrentUserUpn());
    }
}