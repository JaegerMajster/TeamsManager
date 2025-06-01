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
            // Przygotowanie
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(subject);
            await SaveChangesAsync();

            // Weryfikacja
            var savedSubject = await Context.Subjects.FirstOrDefaultAsync(s => s.Id == subject.Id);
            savedSubject.Should().NotBeNull();
            savedSubject!.Code.Should().Be("MAT");
            savedSubject.Name.Should().Be("Matematyka");
            savedSubject.Hours.Should().Be(4);
            savedSubject.Category.Should().Be("Nauki ścisłe");
            savedSubject.CreatedBy.Should().Be("test_user_integration_base_default");
            savedSubject.CreatedDate.Should().NotBe(default(DateTime));
            savedSubject.ModifiedBy.Should().BeNull();
            savedSubject.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectSubject()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("FIZ", "Fizyka"); // Ta metoda pomocnicza zostanie zaktualizowana

            // Działanie
            var result = await _repository.GetByIdAsync(subject.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Code.Should().Be("FIZ");
            result.Name.Should().Be("Fizyka");
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetByCodeAsync_ShouldReturnCorrectSubject_WithDefaultSchoolType()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var targetCode = "INF";

            var subjects = new List<Subject>
            {
                await CreateSubjectAsync(targetCode, "Informatyka", "TECH", true),
                await CreateSubjectAsync("ANG", "Język angielski", "LO"),
                await CreateSubjectAsync(targetCode, "Informatyka stara", "LO", false, false), // nieaktywny z tym samym kodem
            };

            // Działanie
            var result = await _repository.GetByCodeAsync(targetCode);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Code.Should().Be(targetCode);
            result.Name.Should().Be("Informatyka");
            result.IsActive.Should().BeTrue();
            result.DefaultSchoolType.Should().NotBeNull();
            result.DefaultSchoolType!.ShortName.Should().Be("TECH");
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetByCodeAsync_ShouldReturnNull_WhenCodeNotExists()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            await CreateSubjectAsync("MAT", "Matematyka");
            await CreateSubjectAsync("FIZ", "Fizyka");

            // Działanie
            var result = await _repository.GetByCodeAsync("NIEISTNIEJACY");

            // Weryfikacja
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetTeachersAsync_ShouldReturnActiveTeachers()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("MAT", "Matematyka");

            var teachers = new List<User>
            {
                await CreateUserAsync("jan.kowalski@test.com", "Jan", "Kowalski", UserRole.Nauczyciel),
                await CreateUserAsync("anna.nowak@test.com", "Anna", "Nowak", UserRole.Nauczyciel),
                await CreateUserAsync("piotr.wisniewski@test.com", "Piotr", "Wiśniewski", UserRole.Nauczyciel),
                await CreateUserAsync("maria.inactive@test.com", "Maria", "Inactive", UserRole.Nauczyciel, false),
            };

            var assignments = new List<UserSubject>
            {
                CreateUserSubjectAssignment(teachers[0].Id, subject.Id, true),
                CreateUserSubjectAssignment(teachers[1].Id, subject.Id, true),
                CreateUserSubjectAssignment(teachers[2].Id, subject.Id, false),
                CreateUserSubjectAssignment(teachers[3].Id, subject.Id, true),
            };
            await Context.UserSubjects.AddRangeAsync(assignments);
            await Context.SaveChangesAsync(); // Zapis z audytem dla UserSubject

            // Działanie
            var result = await _repository.GetTeachersAsync(subject.Id);

            // Weryfikacja
            result.Should().HaveCount(2);
            result.Select(u => u.UPN).Should().Contain(new[] { "jan.kowalski@test.com", "anna.nowak@test.com" });
            result.Should().OnlyContain(u => u.IsActive);
        }

        [Fact]
        public async Task GetTeachersAsync_ShouldReturnEmpty_WhenNoTeachersAssigned()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("MUZ", "Muzyka");

            // Działanie
            var result = await _repository.GetTeachersAsync(subject.Id);

            // Weryfikacja
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByIdWithDetailsAsync_ShouldReturnSubjectWithDefaultSchoolType()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolType = await CreateSchoolTypeAsync("SP", "Szkoła Podstawowa");
            var subject = await CreateSubjectWithSchoolTypeAsync("PRZYR", "Przyroda", schoolType.Id);

            // Działanie
            var result = await _repository.GetByIdWithDetailsAsync(subject.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Code.Should().Be("PRZYR");
            result.Name.Should().Be("Przyroda");
            result.DefaultSchoolType.Should().NotBeNull();
            result.DefaultSchoolType!.ShortName.Should().Be("SP");
            result.DefaultSchoolType.FullName.Should().Be("Szkoła Podstawowa");
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetByIdWithDetailsAsync_ShouldReturnNull_WhenSubjectInactive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("TEST", "Test Subject", "LO", false, true);

            subject.IsActive = false;
            _repository.Update(subject);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetByIdWithDetailsAsync(subject.Id);

            // Weryfikacja
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAllActiveWithDetailsAsync_ShouldReturnOnlyActiveSubjects()
        {
            // Przygotowanie
            await CleanDatabaseAsync();

            var subjects = new List<Subject>
            {
                await CreateSubjectAsync("MAT", "Matematyka", "LO", true),
                await CreateSubjectAsync("FIZ", "Fizyka", "TECH", true),
                await CreateSubjectAsync("CHEM", "Chemia", "LO", true),
                await CreateSubjectAsync("BIOL", "Biologia", "SP", true),
                await CreateSubjectAsync("HIST", "Historia", "LO", false, false),
            };

            // Działanie
            var result = await _repository.GetAllActiveWithDetailsAsync();

            // Weryfikacja
            result.Should().HaveCount(4);
            result.Should().OnlyContain(s => s.IsActive);
            result.Select(s => s.Code).Should().Contain(new[] { "MAT", "FIZ", "CHEM", "BIOL" });
            result.Should().OnlyContain(s => s.DefaultSchoolType != null);
            result.ToList().ForEach(s => s.CreatedBy.Should().Be("test_user_integration_base_default"));
        }

        [Fact]
        public async Task Update_ShouldModifySubjectData()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("INF", "Informatyka");
            var initialCreatedBy = subject.CreatedBy;
            var initialCreatedDate = subject.CreatedDate;
            var currentUser = "subject_updater";
            SetTestUser(currentUser);

            // Działanie
            var subjectToUpdate = await _repository.GetByIdAsync(subject.Id);
            subjectToUpdate!.Name = "Informatyka rozszerzona";
            subjectToUpdate.Description = "Przedmiot obejmujący programowanie i algorytmikę";
            subjectToUpdate.Hours = 6;
            subjectToUpdate.Category = "Nauki ścisłe - informatyka";
            // subjectToUpdate.MarkAsModified(currentUser); // Niepotrzebne

            _repository.Update(subjectToUpdate);
            await SaveChangesAsync();

            // Weryfikacja
            var updatedSubject = await Context.Subjects.FirstOrDefaultAsync(s => s.Id == subject.Id);
            updatedSubject.Should().NotBeNull();
            updatedSubject!.Name.Should().Be("Informatyka rozszerzona");
            updatedSubject.Description.Should().Be("Przedmiot obejmujący programowanie i algorytmikę");
            updatedSubject.Hours.Should().Be(6);
            updatedSubject.Category.Should().Be("Nauki ścisłe - informatyka");
            updatedSubject.CreatedBy.Should().Be(initialCreatedBy);
            updatedSubject.CreatedDate.Should().Be(initialCreatedDate);
            updatedSubject.ModifiedBy.Should().Be(currentUser);
            updatedSubject.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        [Fact]
        public async Task Delete_ShouldMarkSubjectAsInactive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var subject = await CreateSubjectAsync("LAT", "Łacina");
            var initialCreatedBy = subject.CreatedBy;
            var initialCreatedDate = subject.CreatedDate;
            var currentUser = "subject_deleter";
            SetTestUser(currentUser);

            // Działanie
            var subjectToUpdate = await _repository.GetByIdAsync(subject.Id);
            subjectToUpdate!.MarkAsDeleted(currentUser); // Ta wartość `deletedBy` zostanie nadpisana
            _repository.Update(subjectToUpdate);
            await SaveChangesAsync();

            // Weryfikacja
            var deletedSubject = await Context.Subjects.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subject.Id);
            deletedSubject.Should().NotBeNull();
            deletedSubject!.IsActive.Should().BeFalse();
            deletedSubject.CreatedBy.Should().Be(initialCreatedBy);
            deletedSubject.CreatedDate.Should().Be(initialCreatedDate);
            deletedSubject.ModifiedBy.Should().Be(currentUser);
            deletedSubject.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        [Fact]
        public async Task ComplexScenario_SubjectWithMultipleTeachersAndDetails()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolType = await CreateSchoolTypeAsync("TECH", "Technikum");
            var subject = await CreateSubjectWithSchoolTypeAsync("PROG", "Programowanie", schoolType.Id);

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
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var subjectWithDetails = await _repository.GetByIdWithDetailsAsync(subject.Id);
            var assignedTeachers = await _repository.GetTeachersAsync(subject.Id);

            // Weryfikacja
            subjectWithDetails.Should().NotBeNull();
            subjectWithDetails!.Code.Should().Be("PROG");
            subjectWithDetails.DefaultSchoolType!.ShortName.Should().Be("TECH");
            subjectWithDetails.CreatedBy.Should().Be("test_user_integration_base_default");

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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.SaveChangesAsync(); // Zapis z audytem
            return schoolType;
        }

        private async Task<Subject> CreateSubjectAsync(string code, string name, string schoolTypeShortName = "LO", bool includeSchoolType = true, bool isActive = true)
        {
            string? schoolTypeId = null;
            if (includeSchoolType)
            {
                // Najpierw upewnij się, że SchoolType istnieje lub go stwórz
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
            await Context.Subjects.AddAsync(subject);
            await Context.SaveChangesAsync(); // Zapis z audytem
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.Subjects.AddAsync(subject);
            await Context.SaveChangesAsync(); // Zapis z audytem
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
            // Potrzebujemy DepartmentId, aby dodać użytkownika, więc najpierw tworzymy/pobieramy dział
            var department = await Context.Departments.FirstOrDefaultAsync(d => d.Name == "Dział Testowy dla Nauczycieli");
            if (department == null)
            {
                department = new Department { Id = Guid.NewGuid().ToString(), Name = "Dział Testowy dla Nauczycieli", IsActive = true /* CreatedBy zostanie ustawione */ };
                await Context.Departments.AddAsync(department);
                await Context.SaveChangesAsync();
            }
            user.DepartmentId = department.Id;

            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync(); // Zapis z audytem
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
            // Uwaga: UserSubject jest dodawany do kontekstu i zapisywany oddzielnie w teście GetTeachersAsync
        }

        #endregion
    }
}