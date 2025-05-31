using TeamsManager.Core.Abstractions;

namespace TeamsManager.Tests.Infrastructure.Services
{
    /// <summary>
    /// Implementacja ICurrentUserService dla testów
    /// Pozwala łatwo kontrolować kto jest "zalogowany" w teście
    /// </summary>
    public class TestCurrentUserService : ICurrentUserService
    {
        private string? _currentUserUpn = "test@teamsmanager.local";

        /// <summary>
        /// Konstruktor z opcjonalnym domyślnym użytkownikiem
        /// </summary>
        public TestCurrentUserService(string? defaultUser = null)
        {
            if (!string.IsNullOrWhiteSpace(defaultUser))
            {
                _currentUserUpn = defaultUser;
            }
        }

        public string? GetCurrentUserUpn() => _currentUserUpn;

        public void SetCurrentUserUpn(string? upn) => _currentUserUpn = upn;

        /// <summary>
        /// Resetuje do domyślnego użytkownika testowego
        /// </summary>
        public void Reset()
        {
            _currentUserUpn = "test@teamsmanager.local";
        }

        /// <summary>
        /// Symuluje brak zalogowanego użytkownika
        /// </summary>
        public void ClearUser()
        {
            _currentUserUpn = null;
        }
    }
}