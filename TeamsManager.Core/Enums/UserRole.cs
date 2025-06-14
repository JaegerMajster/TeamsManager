namespace TeamsManager.Core.Enums
{
    /// <summary>
    /// Role użytkowników w systemie edukacyjnym
    /// Określają podstawowe uprawnienia i typ dostępu
    /// </summary>
    public enum UserRole
    {
        Uczen = 0,                    // Uczeń - członek zespołów, brak uprawnień zarządzania
        Sluchacz = 1,                 // Słuchacz kursów - członek zespołów
        Nauczyciel = 2,               // Nauczyciel - właściciel zespołów, może zarządzać zespołami
        PracownikAdministracyjny = 3, // Pracownik administracyjny - obsługa administracyjna szkoły
        Wicedyrektor = 4,             // Wicedyrektor - nadzoruje typy szkół, pełne uprawnienia do zespołów
        Dyrektor = 5,                 // Dyrektor - pełne uprawnienia w całym systemie
        Administrator = 6             // Administrator - pełne uprawnienia techniczne i administracyjne
    }
}