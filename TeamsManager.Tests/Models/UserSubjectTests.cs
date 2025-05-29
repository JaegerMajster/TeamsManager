using System;
using FluentAssertions;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class UserSubjectTests
    {
        // Metody pomocnicze do tworzenia obiektów
        private User CreateTestUser(string id = "user-1", string upn = "teacher@example.com")
        {
            return new User { Id = id, UPN = upn, FirstName = "Nauczyciel", LastName = "Testowy", IsActive = true };
        }

        private Subject CreateTestSubject(string id = "subj-1", string name = "Matematyka")
        {
            return new Subject { Id = id, Name = name, IsActive = true };
        }

        [Fact]
        public void UserSubject_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var userSubject = new UserSubject();

            // Sprawdzenie pól bezpośrednich
            userSubject.Id.Should().Be(string.Empty);
            userSubject.UserId.Should().Be(string.Empty);
            userSubject.SubjectId.Should().Be(string.Empty);
            userSubject.AssignedDate.Should().Be(default(DateTime));
            userSubject.Notes.Should().BeNull();

            // Pola z BaseEntity
            userSubject.IsActive.Should().BeTrue();
            // userSubject.CreatedDate - zależne od logiki BaseEntity/DbContext

            // Właściwości nawigacyjne
            userSubject.User.Should().BeNull();
            userSubject.Subject.Should().BeNull();
        }

        [Fact]
        public void UserSubject_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var userSubject = new UserSubject();
            var user = CreateTestUser();
            var subject = CreateTestSubject();
            var assignedDate = DateTime.UtcNow.AddDays(-7);
            var notes = "Prowadzący dla grupy zaawansowanej";

            // Wykonanie
            userSubject.Id = "usubj-1";
            userSubject.User = user;
            userSubject.UserId = user.Id;
            userSubject.Subject = subject;
            userSubject.SubjectId = subject.Id;
            userSubject.AssignedDate = assignedDate;
            userSubject.Notes = notes;
            userSubject.IsActive = false; // Testujemy zmianę z BaseEntity

            // Sprawdzenie
            userSubject.Id.Should().Be("usubj-1");
            userSubject.UserId.Should().Be(user.Id);
            userSubject.User.Should().Be(user);
            userSubject.SubjectId.Should().Be(subject.Id);
            userSubject.Subject.Should().Be(subject);
            userSubject.AssignedDate.Should().Be(assignedDate);
            userSubject.Notes.Should().Be(notes);
            userSubject.IsActive.Should().BeFalse();
        }

        // Jeśli w przyszłości UserSubject zyskałby właściwości obliczane lub bardziej złożoną logikę,
        // poniżej można by dodać dla nich testy.
        // Przykład:
        /*
        [Fact]
        public void UserSubject_ComputedProperty_Example_ShouldCalculateCorrectly()
        {
            // Przygotowanie
            var user = CreateTestUser();
            var subject = CreateTestSubject();
            var userSubject = new UserSubject { User = user, Subject = subject, AssignedDate = DateTime.UtcNow.AddYears(-1) };

            // Wykonanie
            // var result = userSubject.YearsOfTeachingSubject; // Hipotetyczna właściwość

            // Sprawdzenie
            // result.Should().BeGreaterOrEqualTo(1);
        }
        */
    }
}