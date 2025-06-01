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
        private string? _currentUserId = "test-user-id-123"; // Dodatkowe pole na potrzeby testów

        /// <summary>
        /// Konstruktor z opcjonalnym domyślnym użytkownikiem
        /// </summary>
        public TestCurrentUserService(string? defaultUser = null, string? defaultUserId = null)
        {
            if (!string.IsNullOrWhiteSpace(defaultUser))
            {
                _currentUserUpn = defaultUser;
            }
            if (!string.IsNullOrWhiteSpace(defaultUserId))
            {
                _currentUserId = defaultUserId;
            }
        }

        public string? GetCurrentUserUpn() => _currentUserUpn;

        public void SetCurrentUserUpn(string? upn) => _currentUserUpn = upn;

        // Implementacja brakującej metody
        public string? GetCurrentUserId() => _currentUserId;

        // Opcjonalnie: metoda do ustawiania testowego ID użytkownika
        public void SetCurrentUserId(string? userId) => _currentUserId = userId;


        /// <summary>
        /// Resetuje do domyślnego użytkownika testowego
        /// </summary>
        public void Reset()
        {
            _currentUserUpn = "test@teamsmanager.local";
            _currentUserId = "test-user-id-123"; // Zresetuj również ID
        }

        /// <summary>
        /// Symuluje brak zalogowanego użytkownika
        /// </summary>
        public void ClearUser()
        {
            _currentUserUpn = null;
            _currentUserId = null; // Wyczyść również ID
        }
    }
}