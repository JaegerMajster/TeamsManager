namespace TeamsManager.Core.Enums
{
    /// <summary>
    /// Role użytkowników w systemie edukacyjnym
    /// Określają podstawowe uprawnienia i typ dostępu
    /// </summary>
    public enum UserRole
    {
        Uczen = 0,        // Uczeń - członek zespołów, brak uprawnień zarządzania
        Sluchacz = 1,     // Słuchacz kursów - członek zespołów
        Nauczyciel = 2,   // Nauczyciel - właściciel zespołów, może zarządzać zespołami
        Wicedyrektor = 3, // Wicedyrektor - nadzoruje typy szkół, pełne uprawnienia do zespołów
        Dyrektor = 4      // Dyrektor - pełne uprawnienia w całym systemie
    }
}