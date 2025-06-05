using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TeamsManager.Core.Helpers;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Szablon nazwy zespołu
    /// Umożliwia automatyczne generowanie nazw zespołów według określonych wzorców
    /// </summary>
    public class TeamTemplate : BaseEntity
    {
        /// <summary>
        /// Nazwa szablonu (np. "Szablon Edukacyjny", "Szablon Kursów")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Wzorzec szablonu z placeholderami
        /// Np. "{TypSzkoly} {Oddzial} - {Przedmiot} - {Nauczyciel}"
        /// </summary>
        public string Template { get; set; } = string.Empty;

        /// <summary>
        /// Opis szablonu i jego zastosowania
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Czy to jest szablon domyślny dla danego typu szkoły
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// Czy szablon jest uniwersalny (dla wszystkich typów szkół)
        /// Jeśli true, SchoolTypeId powinno być null
        /// </summary>
        public bool IsUniversal { get; set; } = false;

        /// <summary>
        /// Identyfikator typu szkoły dla którego przeznaczony jest szablon
        /// Null oznacza szablon uniwersalny
        /// </summary>
        public string? SchoolTypeId { get; set; }

        /// <summary>
        /// Przykład wygenerowanej nazwy na podstawie szablonu
        /// </summary>
        public string? ExampleOutput { get; set; }

        /// <summary>
        /// Kategoria szablonu (np. "Edukacyjny", "Biznesowy", "Kursowy")
        /// </summary>
        public string Category { get; set; } = "Ogólny";

        /// <summary>
        /// Język szablonu (jeśli ma znaczenie dla formatowania)
        /// </summary>
        public string Language { get; set; } = "Polski";

        /// <summary>
        /// Maksymalna długość generowanej nazwy
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Czy usuwać polskie znaki z generowanej nazwy
        /// </summary>
        public bool RemovePolishChars { get; set; } = false;

        /// <summary>
        /// Prefiks dodawany do wszystkich nazw generowanych z tego szablonu
        /// </summary>
        public string? Prefix { get; set; }

        /// <summary>
        /// Sufiks dodawany do wszystkich nazw generowanych z tego szablonu
        /// </summary>
        public string? Suffix { get; set; }

        /// <summary>
        /// Separator używany między elementami szablonu
        /// </summary>
        public string Separator { get; set; } = " - ";

        /// <summary>
        /// Kolejność sortowania przy wyświetlaniu
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Liczba użyć szablonu (statystyka)
        /// </summary>
        public int UsageCount { get; set; } = 0;

        /// <summary>
        /// Data ostatniego użycia szablonu
        /// </summary>
        public DateTime? LastUsedDate { get; set; }

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Typ szkoły dla którego przeznaczony jest szablon
        /// </summary>
        public SchoolType? SchoolType { get; set; }

        /// <summary>
        /// Zespoły utworzone na podstawie tego szablonu
        /// </summary>
        public List<Team> Teams { get; set; } = new List<Team>();

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Lista placeholderów w szablonie
        /// Np. dla "{Szkola} {Oddzial}" zwróci ["Szkola", "Oddzial"]
        /// </summary>
        public List<string> Placeholders
        {
            get
            {
                var regex = new Regex(@"\{([^}]+)\}");
                var matches = regex.Matches(Template);
                return matches.Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Distinct()
                    .ToList();
            }
        }

        /// <summary>
        /// Czy szablon ma placeholdery
        /// </summary>
        public bool HasPlaceholders => Placeholders.Any();

        /// <summary>
        /// Liczba placeholderów w szablonie
        /// </summary>
        public int PlaceholderCount => Placeholders.Count;

        /// <summary>
        /// Nazwa wyświetlana szablonu z informacją o typie szkoły
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (IsUniversal)
                    return $"{Name} (Uniwersalny)";

                if (SchoolType != null)
                    return $"{Name} ({SchoolType.ShortName})";

                return Name;
            }
        }

        /// <summary>
        /// Liczba zespołów utworzonych na podstawie tego szablonu
        /// </summary>
        public int TeamsCreatedCount => Teams?.Count(t => t.IsActive) ?? 0;

        /// <summary>
        /// Popularność szablonu na podstawie liczby użyć
        /// </summary>
        public string PopularityLevel
        {
            get
            {
                return UsageCount switch
                {
                    0 => "Nieużywany",
                    < 5 => "Rzadko używany",
                    < 20 => "Średnio używany",
                    < 50 => "Często używany",
                    _ => "Bardzo popularny"
                };
            }
        }

        // ===== METODY POMOCNICZE =====

        /// <summary>
        /// Generuje nazwę zespołu na podstawie szablonu i podanych wartości
        /// </summary>
        /// <param name="values">Słownik wartości dla placeholderów</param>
        /// <param name="modifiedBy">Osoba wykonująca generowanie (UPN).</param>
        /// <returns>Wygenerowana nazwa zespołu</returns>
        public string GenerateTeamName(Dictionary<string, string> values, string? modifiedBy = null)
        {
            if (string.IsNullOrWhiteSpace(Template))
                return string.Empty;

            var result = Template;

            // Zastąp placeholdery wartościami
            foreach (var placeholder in Placeholders)
            {
                var placeholderPattern = $"{{{placeholder}}}";
                var value = values.ContainsKey(placeholder) ? values[placeholder] : $"[{placeholder}]";
                result = result.Replace(placeholderPattern, value);
            }

            // Dodaj prefiks i sufiks jeśli są zdefiniowane
            if (!string.IsNullOrWhiteSpace(Prefix))
                result = $"{Prefix}{Separator}{result}";

            if (!string.IsNullOrWhiteSpace(Suffix))
                result = $"{result}{Separator}{Suffix}";

            // Usuń polskie znaki jeśli wymagane
            if (RemovePolishChars)
                result = RemovePolishCharacters(result);

            // Skróć do maksymalnej długości jeśli określona
            if (MaxLength.HasValue && result.Length > MaxLength.Value)
                result = result.Substring(0, MaxLength.Value).TrimEnd();

            // Zapisz statystyki użycia
            IncrementUsage(modifiedBy);

            return result.Trim();
        }

        /// <summary>
        /// Waliduje szablon - sprawdza poprawność składni
        /// </summary>
        /// <returns>Lista błędów walidacji lub pusta lista jeśli szablon jest poprawny</returns>
        public List<string> ValidateTemplate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Template))
            {
                errors.Add("Szablon nie może być pusty");
                return errors; // Zwróć od razu, jeśli szablon jest pusty
            }

            // Regex, który dopasowuje zawartość między {}, dopuszczając pustą zawartość
            var placeholderContentRegex = new Regex(@"\{([^}]*)\}"); // Używamy * zamiast +
            var matches = placeholderContentRegex.Matches(Template);

            foreach (Match match in matches)
            {
                var placeholderName = match.Groups[1].Value;

                // Sprawdź czy nazwa placeholdera jest pusta
                if (string.IsNullOrWhiteSpace(placeholderName))
                {
                    // Sprawdzamy, czy to faktycznie było "{}" a nie np. "{   }"
                    if (match.Value == "{}")
                    {
                        errors.Add("Znaleziono pusty placeholder {}. Nazwa placeholdera nie może być pusta.");
                    }
                    else if (string.IsNullOrWhiteSpace(placeholderName) && match.Value.Length > 2)
                    {
                        errors.Add($"Placeholder '{match.Value}' zawiera tylko białe znaki jako nazwę.");
                    }
                }
                // Sprawdź czy placeholder zawiera tylko dozwolone znaki (jeśli nie jest pusty)
                else if (!Regex.IsMatch(placeholderName, @"^[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ0-9_]+$"))
                {
                    errors.Add($"Placeholder '{placeholderName}' wewnątrz '{match.Value}' zawiera niedozwolone znaki.");
                }
            }

            // Sprawdź czy nie ma nieparzystych nawiasów
            // Ta logika może być bardziej skomplikowana, jeśli chcemy dokładnie analizować zagnieżdżenia.
            // Proste sprawdzenie liczby nawiasów:
            var openBraces = Template.Count(c => c == '{');
            var closeBraces = Template.Count(c => c == '}');

            if (openBraces != closeBraces)
            {
                errors.Add("Niezrównoważone nawiasy klamrowe w szablonie.");
            }
            // Można dodać bardziej zaawansowaną logikę sprawdzania poprawności nawiasów, np. stos.

            return errors.Distinct().ToList(); // Zwracaj unikalne błędy
        }

        /// <summary>
        /// Zwiększa licznik użyć szablonu
        /// </summary>
        /// <param name="modifiedBy">Osoba wykonująca aktualizację (UPN).</param>
        public void IncrementUsage(string? modifiedBy = null)
        {
            UsageCount++;
            LastUsedDate = DateTime.UtcNow;
            MarkAsModified(modifiedBy ?? AuditHelper.SystemActivityUpdate);
        }

        /// <summary>
        /// Tworzy przykład wygenerowanej nazwy z przykładowymi danymi
        /// </summary>
        /// <returns>Przykładowa nazwa</returns>
        public string GenerateExample()
        {
            var exampleValues = new Dictionary<string, string>
            {
                { "TypSzkoly", "LO" },
                { "Szkola", "LO" },
                { "Oddzial", "1A" },
                { "Klasa", "1A" },
                { "Przedmiot", "Matematyka" },
                { "Nauczyciel", "Jan Kowalski" },
                { "Rok", "2024/2025" },
                { "Semestr", "I" },
                { "Kurs", "Podstawy programowania" },
                { "Grupa", "Grupa 1" }
            };

            return GenerateTeamName(exampleValues);
        }

        /// <summary>
        /// Usuwa polskie znaki z tekstu
        /// </summary>
        /// <param name="text">Tekst do przetworzenia</param>
        /// <returns>Tekst bez polskich znaków</returns>
        private string RemovePolishCharacters(string text)
        {
            var polishChars = "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ";
            var englishChars = "acelnoszzACELNOSZZ";

            var result = text;
            for (int i = 0; i < polishChars.Length; i++)
            {
                result = result.Replace(polishChars[i], englishChars[i]);
            }

            return result;
        }

        /// <summary>
        /// Klonuje szablon z nową nazwą
        /// </summary>
        /// <param name="newName">Nazwa dla sklonowanego szablonu</param>
        /// <returns>Sklonowany szablon</returns>
        public TeamTemplate Clone(string newName)
        {
            return new TeamTemplate
            {
                Name = newName,
                Template = Template,
                Description = $"Kopia: {Description}",
                IsDefault = false, // Kopia nie może być domyślna
                IsUniversal = IsUniversal,
                SchoolTypeId = SchoolTypeId,
                Category = Category,
                Language = Language,
                MaxLength = MaxLength,
                RemovePolishChars = RemovePolishChars,
                Prefix = Prefix,
                Suffix = Suffix,
                Separator = Separator,
                SortOrder = SortOrder + 1
            };
        }
    }
}