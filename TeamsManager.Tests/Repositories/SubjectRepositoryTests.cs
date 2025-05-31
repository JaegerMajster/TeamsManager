using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Data.Repositories;
using Xunit;

namespace TeamsManager.Tests.Repositories
{
    [Collection("Sequential")]

        public class SubjectRepositoryTests : RepositoryTestBase
    {
        private readonly SubjectRepository _repository;

        public SubjectRepositoryTests()
        {
            _repository = new SubjectRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddSubjectToDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var schoolType = await CreateSchoolTypeAsync("LO", "Liceum Ogólnokształcące");
            
            var subject = new Subject
            {
                Id = Guid.NewGuid().ToString(),
                Code = "MAT",
                Name = "Matematyka",
                Description = "Przedmiot ścisły obejmujący algebrę, geometrię i analizę",
                DefaultSchoolTypeId = schoolType.Id,
                Hours = 4,
                Category = "Nauki ścisłe",
                CreatedBy = "test_user",
                IsActive = true
            };

            // Act
            await _repository.AddAsync(subject);
            await SaveChangesAsync();

            // Assert
            var savedSubject = await Context.Subjects.FirstOrDefaultAsync(s => s.Id == subject.Id);
            savedSubject.Should().NotBeNull();
            savedSubject!.Code.Should().Be("MAT");
            savedSubject.Name.Should().Be("Matematyka");
            savedSubject.Hours.Should().Be(4);
            savedSubject.Category.Should().Be("Nauki ścisłe");
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectSubject()
        {
            // Arrange
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("FIZ", "Fizyka");

            // Act
            var result = await _repository.GetByIdAsync(subject.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("FIZ");
            result.Name.Should().Be("Fizyka");
        }

        [Fact]
        public async Task GetByCodeAsync_ShouldReturnCorrectSubject_WithDefaultSchoolType()
        {
            // Arrange
            await CleanDatabaseAsync();
            var targetCode = "INF";
            
            // Tworzymy przedmioty
            var subjects = new List<Subject>
            {
                await CreateSubjectAsync(targetCode, "Informatyka", "TECH", true),
                await CreateSubjectAsync("ANG", "Język angielski", "LO"),
                await CreateSubjectAsync(targetCode, "Informatyka stara", "LO", false, false), // nieaktywny z tym samym kodem
            };

            // Act
            var result = await _repository.GetByCodeAsync(targetCode);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be(targetCode);
            result.Name.Should().Be("Informatyka");
            result.IsActive.Should().BeTrue();
            result.DefaultSchoolType.Should().NotBeNull();
            result.DefaultSchoolType!.ShortName.Should().Be("TECH");
        }

        [Fact]
        public async Task GetByCodeAsync_ShouldReturnNull_WhenCodeNotExists()
        {
            // Arrange
            await CleanDatabaseAsync();
            await CreateSubjectAsync("MAT", "Matematyka");
            await CreateSubjectAsync("FIZ", "Fizyka");

            // Act
            var result = await _repository.GetByCodeAsync("NIEISTNIEJACY");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetTeachersAsync_ShouldReturnActiveTeachers()
        {
            // Arrange
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("MAT", "Matematyka");
            
            // Tworzenie nauczycieli
            var teachers = new List<User>
            {
                await CreateUserAsync("jan.kowalski@test.com", "Jan", "Kowalski", UserRole.Nauczyciel),
                await CreateUserAsync("anna.nowak@test.com", "Anna", "Nowak", UserRole.Nauczyciel),
                await CreateUserAsync("piotr.wisniewski@test.com", "Piotr", "Wiśniewski", UserRole.Nauczyciel),
                await CreateUserAsync("maria.inactive@test.com", "Maria", "Inactive", UserRole.Nauczyciel, false), // nieaktywny
            };

            // Przypisania nauczycieli do przedmiotu
            var assignments = new List<UserSubject>
            {
                CreateUserSubjectAssignment(teachers[0].Id, subject.Id, true),
                CreateUserSubjectAssignment(teachers[1].Id, subject.Id, true),
                CreateUserSubjectAssignment(teachers[2].Id, subject.Id, false), // nieaktywne przypisanie
                CreateUserSubjectAssignment(teachers[3].Id, subject.Id, true), // aktywne przypisanie ale nieaktywny użytkownik
            };
            await Context.UserSubjects.AddRangeAsync(assignments);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetTeachersAsync(subject.Id);

            // Assert
            result.Should().HaveCount(2);
            result.Select(u => u.UPN).Should().Contain(new[] { "jan.kowalski@test.com", "anna.nowak@test.com" });
            result.Should().OnlyContain(u => u.IsActive);
        }

        [Fact]
        public async Task GetTeachersAsync_ShouldReturnEmpty_WhenNoTeachersAssigned()
        {
            // Arrange
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("MUZ", "Muzyka");

            // Act
            var result = await _repository.GetTeachersAsync(subject.Id);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByIdWithDetailsAsync_ShouldReturnSubjectWithDefaultSchoolType()
        {
            // Arrange
            await CleanDatabaseAsync();
            var schoolType = await CreateSchoolTypeAsync("SP", "Szkoła Podstawowa");
            var subject = await CreateSubjectWithSchoolTypeAsync("PRZYR", "Przyroda", schoolType.Id);

            // Act
            var result = await _repository.GetByIdWithDetailsAsync(subject.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("PRZYR");
            result.Name.Should().Be("Przyroda");
            result.DefaultSchoolType.Should().NotBeNull();
            result.DefaultSchoolType!.ShortName.Should().Be("SP");
            result.DefaultSchoolType.FullName.Should().Be("Szkoła Podstawowa");
        }

        [Fact]
        public async Task GetByIdWithDetailsAsync_ShouldReturnNull_WhenSubjectInactive()
        {
            // Arrange
            await CleanDatabaseAsync();
            // Najpierw tworzymy aktywny przedmiot
            var subject = await CreateSubjectAsync("TEST", "Test Subject", "LO", false, true);
            
            // Teraz go dezaktywujemy
            subject.IsActive = false;
            _repository.Update(subject);
            await SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdWithDetailsAsync(subject.Id);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAllActiveWithDetailsAsync_ShouldReturnOnlyActiveSubjects()
        {
            // Arrange
            await CleanDatabaseAsync();
            
            var subjects = new List<Subject>
            {
                await CreateSubjectAsync("MAT", "Matematyka", "LO", true),
                await CreateSubjectAsync("FIZ", "Fizyka", "TECH", true),
                await CreateSubjectAsync("CHEM", "Chemia", "LO", true),
                await CreateSubjectAsync("BIOL", "Biologia", "SP", true),
                await CreateSubjectAsync("HIST", "Historia", "LO", false, false), // nieaktywny
            };

            // Act
            var result = await _repository.GetAllActiveWithDetailsAsync();

            // Assert
            result.Should().HaveCount(4);
            result.Should().OnlyContain(s => s.IsActive);
            result.Select(s => s.Code).Should().Contain(new[] { "MAT", "FIZ", "CHEM", "BIOL" });
            result.Should().OnlyContain(s => s.DefaultSchoolType != null); // Wszystkie powinny mieć załadowany DefaultSchoolType
        }

        [Fact]
        public async Task Update_ShouldModifySubjectData()
        {
            // Arrange
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("INF", "Informatyka");

            // Act
            subject.Name = "Informatyka rozszerzona";
            subject.Description = "Przedmiot obejmujący programowanie i algorytmikę";
            subject.Hours = 6;
            subject.Category = "Nauki ścisłe - informatyka";
            subject.MarkAsModified("updater");

            _repository.Update(subject);
            await SaveChangesAsync();

            // Assert
            var updatedSubject = await Context.Subjects.FirstOrDefaultAsync(s => s.Id == subject.Id);
            updatedSubject.Should().NotBeNull();
            updatedSubject!.Name.Should().Be("Informatyka rozszerzona");
            updatedSubject.Description.Should().Be("Przedmiot obejmujący programowanie i algorytmikę");
            updatedSubject.Hours.Should().Be(6);
            updatedSubject.Category.Should().Be("Nauki ścisłe - informatyka");
            updatedSubject.ModifiedBy.Should().Be("system@teamsmanager.local");
            updatedSubject.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_ShouldMarkSubjectAsInactive()
        {
            // Arrange
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("LAT", "Łacina");

            // Act
            subject.MarkAsDeleted("deleter");
            _repository.Update(subject);
            await SaveChangesAsync();

            // Assert
            var deletedSubject = await Context.Subjects.FirstOrDefaultAsync(s => s.Id == subject.Id);
            deletedSubject.Should().NotBeNull();
            deletedSubject!.IsActive.Should().BeFalse();
            deletedSubject.ModifiedBy.Should().Be("system@teamsmanager.local");
            deletedSubject.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task ComplexScenario_SubjectWithMultipleTeachersAndDetails()
        {
            // Arrange
            await CleanDatabaseAsync();
            var schoolType = await CreateSchoolTypeAsync("TECH", "Technikum");
            var subject = await CreateSubjectWithSchoolTypeAsync("PROG", "Programowanie", schoolType.Id);
            
            // Dodaj kilku nauczycieli
            var teachers = new List<User>
            {
                await CreateUserAsync("teacher1@test.com", "Teacher", "One", UserRole.Nauczyciel),
                await CreateUserAsync("teacher2@test.com", "Teacher", "Two", UserRole.Nauczyciel),
            };

            foreach (var teacher in teachers)
            {
                var assignment = CreateUserSubjectAssignment(teacher.Id, subject.Id, true);
                await Context.UserSubjects.AddAsync(assignment);
            }
            await Context.SaveChangesAsync();

            // Act - pobierz przedmiot z detalami
            var subjectWithDetails = await _repository.GetByIdWithDetailsAsync(subject.Id);
            var assignedTeachers = await _repository.GetTeachersAsync(subject.Id);

            // Assert
            subjectWithDetails.Should().NotBeNull();
            subjectWithDetails!.Code.Should().Be("PROG");
            subjectWithDetails.DefaultSchoolType!.ShortName.Should().Be("TECH");
            
            assignedTeachers.Should().HaveCount(2);
            assignedTeachers.Select(t => t.UPN).Should().Contain(new[] { "teacher1@test.com", "teacher2@test.com" });
        }

        #region Helper Methods

        private async Task<SchoolType> CreateSchoolTypeAsync(string shortName, string fullName)
        {
            var schoolType = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = shortName,
                FullName = fullName,
                Description = $"Typ szkoły: {fullName}",
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.SaveChangesAsync();
            return schoolType;
        }

        private async Task<Subject> CreateSubjectAsync(string code, string name, string schoolTypeShortName = "LO", bool includeSchoolType = true, bool isActive = true)
        {
            string? schoolTypeId = null;
            if (includeSchoolType)
            {
                var schoolType = await Context.SchoolTypes.FirstOrDefaultAsync(st => st.ShortName == schoolTypeShortName);
                if (schoolType == null)
                {
                    schoolType = await CreateSchoolTypeAsync(schoolTypeShortName, $"{schoolTypeShortName} - Pełna nazwa");
                }
                schoolTypeId = schoolType.Id;
            }

            var subject = new Subject
            {
                Id = Guid.NewGuid().ToString(),
                Code = code,
                Name = name,
                Description = $"Opis przedmiotu {name}",
                DefaultSchoolTypeId = schoolTypeId,
                Hours = 3,
                Category = "Ogólne",
                CreatedBy = "test_user",
                IsActive = isActive
            };
            await Context.Subjects.AddAsync(subject);
            await Context.SaveChangesAsync();
            return subject;
        }

        private async Task<Subject> CreateSubjectWithSchoolTypeAsync(string code, string name, string schoolTypeId)
        {
            var subject = new Subject
            {
                Id = Guid.NewGuid().ToString(),
                Code = code,
                Name = name,
                Description = $"Opis przedmiotu {name}",
                DefaultSchoolTypeId = schoolTypeId,
                Hours = 4,
                Category = "Specjalne",
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Subjects.AddAsync(subject);
            await Context.SaveChangesAsync();
            return subject;
        }

        private async Task<User> CreateUserAsync(string upn, string firstName, string lastName, UserRole role, bool isActive = true)
        {
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = role,
                CreatedBy = "test_user",
                IsActive = isActive
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync();
            return user;
        }

        private UserSubject CreateUserSubjectAssignment(string userId, string subjectId, bool isActive)
        {
            return new UserSubject
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                SubjectId = subjectId,
                AssignedDate = DateTime.UtcNow,
                Notes = isActive ? "Aktywne przypisanie" : "Nieaktywne przypisanie",
                CreatedBy = "test_user",
                IsActive = isActive
            };
        }

        #endregion
    }
 
}
