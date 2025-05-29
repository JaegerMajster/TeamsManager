using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Models;

namespace TeamsManager.Tests.Models
{
    public class TeamTemplateTests
    {


        [Theory]
        [InlineData("{TypSzkoly} {Oddzial} - {Przedmiot}", new[] { "TypSzkoly", "Oddzial", "Przedmiot" })]
        [InlineData("Zespół dla {Nauczyciel}", new[] { "Nauczyciel" })]
        [InlineData("Stały tekst bez placeholderów", new string[0])]
        [InlineData("{Duplikat}_{Duplikat}", new[] { "Duplikat" })] // Sprawdzenie distinct
        [InlineData("", new string[0])]
        [InlineData("{}", new string[0])]
        [InlineData("{Valid_Name1}-{INVALID CHARS!}", new[] { "Valid_Name1", "INVALID CHARS!" })] // Do walidacji, ale Placeholders i tak wyciągnie
        public void Placeholders_ShouldExtractCorrectPlaceholdersFromTemplate(string templateText, string[] expectedPlaceholders)
        {
            // Przygotowanie
            var template = new TeamTemplate { Template = templateText };

            // Wykonanie
            var actualPlaceholders = template.Placeholders;

            // Sprawdzenie
            actualPlaceholders.Should().BeEquivalentTo(expectedPlaceholders);
            template.HasPlaceholders.Should().Be(expectedPlaceholders.Any());
            template.PlaceholderCount.Should().Be(expectedPlaceholders.Length);
        }

        [Fact]
        public void DisplayName_ShouldFormatCorrectly_ForUniversalAndSpecificTemplates()
        {
            // Przygotowanie
            var universalTemplate = new TeamTemplate { Name = "Ogólny", IsUniversal = true };
            var specificTemplate = new TeamTemplate { Name = "Liceum", IsUniversal = false, SchoolType = new SchoolType { ShortName = "LO" } };
            var specificNoSchoolTypeTemplate = new TeamTemplate { Name = "Specyficzny Bez Typu", IsUniversal = false, SchoolType = null };


            // Sprawdzenie
            universalTemplate.DisplayName.Should().Be("Ogólny (Uniwersalny)");
            specificTemplate.DisplayName.Should().Be("Liceum (LO)");
            specificNoSchoolTypeTemplate.DisplayName.Should().Be("Specyficzny Bez Typu"); // Bez dopisku, bo SchoolType jest null
        }

        [Fact]
        public void GenerateTeamName_ShouldCorrectlySubstitutePlaceholders()
        {
            // Przygotowanie
            var template = new TeamTemplate
            {
                Template = "{TypSzkoly} {Oddzial} - {Przedmiot} - {Nauczyciel}"
            };
            var values = new Dictionary<string, string>
            {
                { "TypSzkoly", "LO" },
                { "Oddzial", "1A" },
                { "Przedmiot", "Matematyka" },
                { "Nauczyciel", "Jan Kowalski" }
            };

            // Wykonanie
            var teamName = template.GenerateTeamName(values);

            // Sprawdzenie
            teamName.Should().Be("LO 1A - Matematyka - Jan Kowalski");
        }

        [Fact]
        public void GenerateTeamName_ShouldHandleMissingPlaceholders()
        {
            // Przygotowanie
            var template = new TeamTemplate
            {
                Template = "Kurs: {Kurs} - Prowadzący: {Prowadzacy}"
            };
            var values = new Dictionary<string, string>
            {
                { "Kurs", "Programowanie C#" }
                // Brak wartości dla {Prowadzacy}
            };

            // Wykonanie
            var teamName = template.GenerateTeamName(values);

            // Sprawdzenie
            teamName.Should().Be("Kurs: Programowanie C# - Prowadzący: [Prowadzacy]");
        }

        [Fact]
        public void GenerateTeamName_ShouldApplyPrefixSuffixAndSeparator()
        {
            // Przygotowanie
            var template = new TeamTemplate
            {
                Template = "{Element1}{Element2}", // Celowo bez separatora w szablonie
                Prefix = "Projekt:",
                Suffix = "(Zakończony)",
                Separator = " :: " // Niestandardowy separator
            };
            var values = new Dictionary<string, string>
            {
                { "Element1", "Alpha" },
                { "Element2", "Beta" }
            };

            // Wykonanie
            var teamName = template.GenerateTeamName(values);

            // Sprawdzenie
            // Efekt: "Projekt: :: AlphaBeta :: (Zakończony)" - separator jest między prefix/suffix a resztą
            // Aby uzyskać "Projekt: :: Alpha :: Beta :: (Zakończony)" szablon musiałby być "{Element1}{Separator}{Element2}"
            // Obecna logika GenerateTeamName dodaje separator między Prefix/Suffix a wygenerowanym 'result'
            // Jeśli chcemy separator między Element1 a Element2, musi być w Template.
            // Sprawdźmy obecne działanie:
            teamName.Should().Be("Projekt: :: AlphaBeta :: (Zakończony)");
        }

        [Fact]
        public void GenerateTeamName_ShouldRemovePolishChars_WhenFlagSet()
        {
            // Przygotowanie
            var template = new TeamTemplate
            {
                Template = "Żółta gęś - {Imie}",
                RemovePolishChars = true
            };
            var values = new Dictionary<string, string> { { "Imie", "Zażółć" } };

            // Wykonanie
            var teamName = template.GenerateTeamName(values);

            // Sprawdzenie
            teamName.Should().Be("Zolta ges - Zazolc");
        }

        [Fact]
        public void GenerateTeamName_ShouldTruncateToMaxLength_WhenSet()
        {
            // Przygotowanie
            var template = new TeamTemplate
            {
                Template = "Bardzo długa nazwa szablonu zespołu przekraczająca limit",
                MaxLength = 20
            };
            var values = new Dictionary<string, string>();

            // Wykonanie
            var teamName = template.GenerateTeamName(values);

            // Sprawdzenie
            teamName.Should().HaveLength(20).And.Be("Bardzo długa nazwa s");
        }

        [Fact]
        public void IncrementUsage_ShouldUpdateUsageCountAndLastUsedDate()
        {
            // Przygotowanie
            var template = new TeamTemplate { UsageCount = 5, LastUsedDate = DateTime.UtcNow.AddDays(-1) };
            var initialUsageCount = template.UsageCount;
            var initialLastUsedDate = template.LastUsedDate;

            // Wykonanie
            template.IncrementUsage();

            // Sprawdzenie
            template.UsageCount.Should().Be(initialUsageCount + 1);
            template.LastUsedDate.Should().NotBeNull();
            template.LastUsedDate.Should().BeAfter(initialLastUsedDate.Value);
            template.LastUsedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void GenerateTeamName_ShouldCallIncrementUsage()
        {
            // Przygotowanie
            var template = new TeamTemplate { Template = "Test" };
            var initialUsageCount = template.UsageCount;

            // Wykonanie
            template.GenerateTeamName(new Dictionary<string, string>());

            // Sprawdzenie
            template.UsageCount.Should().Be(initialUsageCount + 1);
        }

        [Theory]
        [InlineData("{Poprawny}", true, 0)] // Poprawny
        [InlineData("Tekst {} Placeholder", false, 1)] // Pusty placeholder
        [InlineData("Tekst {Błędny Znak!}", false, 1)] // Błędne znaki
        [InlineData("Niezamknięty {Placeholder", false, 1)] // Niezamknięty (testuje zrównoważenie)
        [InlineData("Za dużo zamknięć }", false, 1)] // Za dużo zamknięć (testuje zrównoważenie)
        [InlineData("", false, 1)] // Pusty szablon
        public void ValidateTemplate_ShouldReturnCorrectValidationStatus(string templateText, bool expectedIsValid, int expectedErrorCount)
        {
            // Przygotowanie
            var template = new TeamTemplate { Template = templateText };

            // Wykonanie
            var errors = template.ValidateTemplate();

            // Sprawdzenie
            (errors.Count == 0).Should().Be(expectedIsValid);
            errors.Should().HaveCount(expectedErrorCount);
        }

        [Fact]
        public void GenerateExample_ShouldReturnNonEmptyString_WhenTemplateIsValid()
        {
            // Przygotowanie
            var template = new TeamTemplate { Template = "{TypSzkoly} - {Przedmiot}" };

            // Wykonanie
            var example = template.GenerateExample();

            // Sprawdzenie
            example.Should().NotBeNullOrWhiteSpace();
            // Można dodać bardziej szczegółowe sprawdzenia, np. czy zawiera "LO - Matematyka" (z domyślnych wartości w metodzie)
            example.Should().Contain("LO").And.Contain("Matematyka");
        }

        [Fact]
        public void Clone_ShouldCreateACopyWithNewNameAndCorrectProperties()
        {
            // Przygotowanie
            var original = new TeamTemplate
            {
                Id = "orig-123",
                Name = "Oryginalny Szablon",
                Template = "{A}-{B}",
                Description = "Opis oryginału",
                IsDefault = true,
                IsUniversal = false,
                SchoolTypeId = "school-type-1",
                Category = "Edukacja",
                Language = "pl-PL",
                MaxLength = 50,
                RemovePolishChars = true,
                Prefix = "PRE-",
                Suffix = "-SUF",
                Separator = "::",
                SortOrder = 5,
                CreatedBy = "user1",
                CreatedDate = DateTime.UtcNow.AddDays(-10)
            };
            var newName = "Sklonowany Szablon";

            // Wykonanie
            var clone = original.Clone(newName);

            // Sprawdzenie
            clone.Should().NotBeNull();
            clone.Id.Should().Be(string.Empty); // Klon powinien mieć nowe ID (lub być pusty do wygenerowania przez bazę)
            clone.Name.Should().Be(newName);
            clone.Template.Should().Be(original.Template);
            clone.Description.Should().Be($"Kopia: {original.Description}");
            clone.IsDefault.Should().BeFalse(); // Kopia nie jest domyślna
            clone.IsUniversal.Should().Be(original.IsUniversal);
            clone.SchoolTypeId.Should().Be(original.SchoolTypeId);
            clone.Category.Should().Be(original.Category);
            clone.Language.Should().Be(original.Language);
            clone.MaxLength.Should().Be(original.MaxLength);
            clone.RemovePolishChars.Should().Be(original.RemovePolishChars);
            clone.Prefix.Should().Be(original.Prefix);
            clone.Suffix.Should().Be(original.Suffix);
            clone.Separator.Should().Be(original.Separator);
            clone.SortOrder.Should().Be(original.SortOrder + 1);

            // Pola z BaseEntity powinny być nowe dla klona
            clone.CreatedDate.Should().NotBe(original.CreatedDate); // Powinno być nowe lub default
            clone.CreatedBy.Should().Be(string.Empty); // Powinno być puste lub ustawione przez system dla nowego obiektu
            clone.IsActive.Should().BeTrue(); // Domyślnie
        }
    }
}