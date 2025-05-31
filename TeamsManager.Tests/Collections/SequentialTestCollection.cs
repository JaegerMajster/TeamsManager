using Xunit;

namespace TeamsManager.Tests.Collections
{
    /// <summary>
    /// Kolekcja testów które muszą być uruchamiane sekwencyjnie
    /// aby uniknąć konfliktów w bazie danych
    /// </summary>
    [CollectionDefinition("Sequential")]
    public class SequentialTestCollection
    {
        // Ta klasa nie ma kodu. Służy tylko jako miejsce dla atrybutu [CollectionDefinition]
    }
}