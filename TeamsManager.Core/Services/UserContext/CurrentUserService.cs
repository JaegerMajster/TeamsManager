using TeamsManager.Core.Abstractions;

namespace TeamsManager.Core.Services.UserContext
{
    public class CurrentUserService : ICurrentUserService
    {
        private string? _currentUserUpn;

        public string? GetCurrentUserUpn()
        {
            // Dla działania narzędzi EF Core i początkowych testów,
            // zwracamy placeholder, jeśli _currentUserUpn nie został jeszcze ustawiony.
            // W aplikacji WPF ta wartość będzie dynamicznie ustawiana po zalogowaniu.
            return _currentUserUpn ?? "system@teamsmanager.local";
        }

        public void SetCurrentUserUpn(string? upn)
        {
            _currentUserUpn = upn;
        }
    }
}