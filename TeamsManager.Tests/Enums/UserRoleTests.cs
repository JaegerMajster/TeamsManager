using FluentAssertions;
using TeamsManager.Core.Enums;

namespace TeamsManager.Tests.Enums // Lub TeamsManager.Tests.Core.Enums, jeśli tak organizujesz
{
    public class UserRoleTests
    {
        [Fact]
        public void UserRole_ShouldHaveCorrectValues()
        {
            // Sprawdzenie wartości liczbowych enum
            ((int)UserRole.Uczen).Should().Be(0);
            ((int)UserRole.Sluchacz).Should().Be(1);
            ((int)UserRole.Nauczyciel).Should().Be(2);
            ((int)UserRole.Wicedyrektor).Should().Be(3);
            ((int)UserRole.Dyrektor).Should().Be(4);
        }

        [Fact]
        public void UserRole_ShouldHaveCorrectNames()
        {
            // Sprawdzenie nazw enum
            UserRole.Uczen.ToString().Should().Be("Uczen");
            UserRole.Sluchacz.ToString().Should().Be("Sluchacz");
            UserRole.Nauczyciel.ToString().Should().Be("Nauczyciel");
            UserRole.Wicedyrektor.ToString().Should().Be("Wicedyrektor");
            UserRole.Dyrektor.ToString().Should().Be("Dyrektor");
        }

        [Theory]
        [InlineData(UserRole.Uczen)]
        [InlineData(UserRole.Sluchacz)]
        [InlineData(UserRole.Nauczyciel)]
        [InlineData(UserRole.Wicedyrektor)]
        [InlineData(UserRole.Dyrektor)]
        public void UserRole_AllDefinedValues_ShouldBeValid(UserRole role)
        {
            // Sprawdzenie czy wszystkie wartości enum są zdefiniowane
            Enum.IsDefined(typeof(UserRole), role).Should().BeTrue();
        }

        [Fact]
        public void UserRole_WhenConvertingFromInt_ShouldReturnCorrectEnum()
        {
            // Konwersja z int na enum
            ((UserRole)0).Should().Be(UserRole.Uczen);
            ((UserRole)1).Should().Be(UserRole.Sluchacz);
            ((UserRole)2).Should().Be(UserRole.Nauczyciel);
            ((UserRole)3).Should().Be(UserRole.Wicedyrektor);
            ((UserRole)4).Should().Be(UserRole.Dyrektor);
        }

        [Fact]
        public void UserRole_WhenConvertingFromInvalidInt_ShouldNotBeDefinedOrThrow()
        {
            // Sprawdzenie zachowania dla nieprawidłowej wartości liczbowej
            int invalidRoleValue = 99;
            // Opcja 1: Sprawdzenie, czy nie jest zdefiniowana (bezpieczniejsze)
            Enum.IsDefined(typeof(UserRole), invalidRoleValue).Should().BeFalse();

            // Opcja 2: Sprawdzenie, czy rzutowanie daje wartość, która nie jest jedną ze zdefiniowanych
            // (to może być trudniejsze do asercji w prosty sposób bez iterowania po Enum.GetValues)
            // Generalnie, rzutowanie nieprawidłowej liczby na enum nie rzuci wyjątku,
            // ale wynikowa wartość nie będzie równa żadnej ze zdefiniowanych nazw.
            var undefinedRole = (UserRole)invalidRoleValue;
            Enum.GetValues(typeof(UserRole)).Cast<UserRole>().Should().NotContain(undefinedRole);

            // Uwaga: Bezpośrednie rzutowanie (UserRole)invalidRoleValue nie rzuci wyjątku.
            // Wyjątek mógłby wystąpić, gdybyś próbował użyć tej niezdefiniowanej wartości w switch bez default,
            // lub gdybyś miał metodę walidującą, która sprawdzałaby Enum.IsDefined.
        }
    }
}