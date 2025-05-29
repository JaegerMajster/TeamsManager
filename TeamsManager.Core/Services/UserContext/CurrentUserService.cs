using TeamsManager.Core.Abstractions; // Dodaj ten using

namespace TeamsManager.Core.Services.UserContext
{
    /// <summary>
    /// Prosta implementacja serwisu dostarczającego informacje o bieżącym użytkowniku.
    /// W aplikacji WPF, wartość UPN będzie musiała być ustawiana po procesie logowania.
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private string? _currentUserUpn;

        /// <summary>
        /// Pobiera UPN aktualnie zalogowanego użytkownika.
        /// </summary>
        /// <returns>UPN użytkownika lub tymczasową wartość, jeśli żaden użytkownik nie jest ustawiony.</returns>
        public string? GetCurrentUserUpn()
        {
            // TODO: W aplikacji WPF, ta wartość powinna być dynamicznie ustawiana po zalogowaniu.
            // Dla celów działania narzędzi EF Core i początkowych testów,
            // zwracamy placeholder, jeśli _currentUserUpn nie został jeszcze ustawiony.
            return _currentUserUpn ?? "system@teamsmanager.local"; // Tymczasowa wartość dla audytu
        }

        /// <summary>
        /// Ustawia UPN aktualnie zalogowanego użytkownika.
        /// Ta metoda będzie wywoływana przez system logowania w aplikacji WPF.
        /// </summary>
        /// <param name="upn">UPN zalogowanego użytkownika.</param>
        public void SetCurrentUserUpn(string? upn)
        {
            _currentUserUpn = upn;
        }
    }
}