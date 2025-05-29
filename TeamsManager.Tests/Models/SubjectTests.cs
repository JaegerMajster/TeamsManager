using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class SubjectTests
    {
        // Metoda pomocnicza do tworzenia obiektu SchoolType (jeśli potrzebna do testów DefaultSchoolType)
        private SchoolType CreateTestSchoolType(string id = "st-1", string shortName = "LO")
        {
            return new SchoolType { Id = id, ShortName = shortName, FullName = $"Liceum {shortName}", IsActive = true };
        }

        [Fact]
        public void Subject_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var subject = new Subject();

            // Sprawdzenie pól bezpośrednich
            subject.Id.Should().Be(string.Empty);
            subject.Name.Should().Be(string.Empty);
            subject.Code.Should().BeNull();
            subject.Description.Should().BeNull();
            subject.Hours.Should().BeNull();
            subject.DefaultSchoolTypeId.Should().BeNull();
            subject.Category.Should().BeNull();

            // Pola z BaseEntity
            subject.IsActive.Should().BeTrue();
            // subject.CreatedDate - zależne od logiki BaseEntity/DbContext

            // Właściwości nawigacyjne
            subject.DefaultSchoolType.Should().BeNull(); // Domyślnie
            subject.TeacherAssignments.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void Subject_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var subject = new Subject();
            var schoolType = CreateTestSchoolType("st-tech", "TECH");
            var id = "subj-prog-dotnet";
            var name = "Programowanie w .NET";
            var code = "PRG_NET_ADV";
            var description = "Zaawansowany kurs programowania aplikacji w ekosystemie .NET.";
            var hours = 180;
            var category = "Technologie Informatyczne";

            // Wykonanie
            subject.Id = id;
            subject.Name = name;
            subject.Code = code;
            subject.Description = description;
            subject.Hours = hours;
            subject.DefaultSchoolTypeId = schoolType.Id;
            subject.DefaultSchoolType = schoolType; // Przypisanie obiektu nawigacyjnego
            subject.Category = category;
            subject.IsActive = false; // Testujemy zmianę z BaseEntity

            // Sprawdzenie
            subject.Id.Should().Be(id);
            subject.Name.Should().Be(name);
            subject.Code.Should().Be(code);
            subject.Description.Should().Be(description);
            subject.Hours.Should().Be(hours);
            subject.DefaultSchoolTypeId.Should().Be(schoolType.Id);
            subject.DefaultSchoolType.Should().Be(schoolType);
            subject.Category.Should().Be(category);
            subject.IsActive.Should().BeFalse();
        }

        // Jeśli w przyszłości Subject zyskałby właściwości obliczane lub bardziej złożoną logikę,
        // poniżej można by dodać dla nich testy.
        // Przykład (zakładając hipotetyczną właściwość):
        /*
        [Fact]
        public void Subject_ComputedProperty_Example_ShouldCalculateCorrectly()
        {
            // Przygotowanie
            var subject = new Subject { Name = "Matematyka", Hours = 60 };

            // Wykonanie
            // var result = subject.SomeComputedProperty; // np. subject.IsLongCourse

            // Sprawdzenie
            // result.Should().Be(true); // lub cokolwiek innego
        }
        */

        [Fact]
        public void Subject_TeacherAssignments_ShouldBeInitializedAsEmptyList()
        {
            // Przygotowanie i Wykonanie
            var subject = new Subject();

            // Sprawdzenie
            subject.TeacherAssignments.Should().NotBeNull().And.BeEmpty();
        }
    }
}