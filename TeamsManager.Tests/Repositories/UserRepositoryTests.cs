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
    public class UserRepositoryTests : RepositoryTestBase
    {
        private readonly UserRepository _repository;

        public UserRepositoryTests()
        {
            _repository = new UserRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddUserToDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "IT Department",
                Description = "Information Technology",
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync(); // Zapis z audytem dla Department

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@test.com",
                Role = UserRole.Nauczyciel,
                DepartmentId = department.Id,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(user);
            await SaveChangesAsync();

            // Weryfikacja
            var savedUser = await Context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            savedUser.Should().NotBeNull();
            savedUser!.FirstName.Should().Be("Jan");
            savedUser.LastName.Should().Be("Kowalski");
            savedUser.UPN.Should().Be("jan.kowalski@test.com");
            savedUser.Role.Should().Be(UserRole.Nauczyciel);
            savedUser.CreatedBy.Should().Be("test_user");
            savedUser.CreatedDate.Should().NotBe(default(DateTime));
            savedUser.ModifiedBy.Should().BeNull();
            savedUser.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectUser_WithAllIncludes()
        {
            // Przygotowanie
            await CleanDatabaseAsync();

            var department = new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Department",
                Description = "Test",
                IsActive = true
            };

            var schoolType = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Test",
                IsActive = true
            };

            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Test Team",
                Description = "Test",
                Owner = "owner@test.com",
                Status = TeamStatus.Active,
                IsActive = true
            };

            await Context.Departments.AddAsync(department);
            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync(); // Zapis z audytem

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Test",
                LastName = "User",
                UPN = "test.user@test.com",
                Role = UserRole.Nauczyciel,
                DepartmentId = department.Id,
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync(); // Zapis z audytem

            var teamMembership = new TeamMember
            {
                Id = Guid.NewGuid().ToString(),
                TeamId = team.Id,
                UserId = user.Id,
                Role = TeamMemberRole.Member,
                AddedDate = DateTime.UtcNow,
                IsActive = true
            };

            var schoolTypeAssignment = new UserSchoolType
            {
                Id = Guid.NewGuid().ToString(),
                UserId = user.Id,
                SchoolTypeId = schoolType.Id,
                AssignedDate = DateTime.UtcNow,
                IsCurrentlyActive = true,
                IsActive = true
            };

            await Context.TeamMembers.AddAsync(teamMembership);
            await Context.UserSchoolTypes.AddAsync(schoolTypeAssignment);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetByIdAsync(user.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Department.Should().NotBeNull();
            result.Department!.Name.Should().Be("Test Department");
            result.TeamMemberships.Should().HaveCount(1);
            result.TeamMemberships.First().Team.Should().NotBeNull();
            result.TeamMemberships.First().Team!.DisplayName.Should().Be("Test Team");
            result.SchoolTypeAssignments.Should().HaveCount(1);
            result.SchoolTypeAssignments.First().SchoolType.Should().NotBeNull();
            result.SchoolTypeAssignments.First().SchoolType!.ShortName.Should().Be("LO");
            result.CreatedBy.Should().Be("test_user");
        }

        [Fact]
        public async Task GetUserByUpnAsync_ShouldReturnCorrectUser()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var upn = "unique.user@test.com";
            var department = await GetOrCreateTestDepartmentAsync(); // Metoda pomocnicza do tworzenia/pobierania działu
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Unique",
                LastName = "User",
                UPN = upn,
                Role = UserRole.Uczen,
                DepartmentId = department.Id, // Wymagane pole
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetUserByUpnAsync(upn);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.UPN.Should().Be(upn);
            result.FirstName.Should().Be("Unique");
            result.LastName.Should().Be("User");
            result.CreatedBy.Should().Be("test_user");
        }

        [Theory]
        [InlineData(UserRole.Nauczyciel, 3)]
        [InlineData(UserRole.Uczen, 2)]
        [InlineData(UserRole.Dyrektor, 1)]
        [InlineData(UserRole.Wicedyrektor, 0)]
        public async Task GetUsersByRoleAsync_ShouldReturnMatchingUsers(UserRole role, int expectedCount)
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = await GetOrCreateTestDepartmentAsync();
            var users = new List<User>
            {
                CreateUser("teacher1@test.com", UserRole.Nauczyciel, true, department.Id),
                CreateUser("teacher2@test.com", UserRole.Nauczyciel, true, department.Id),
                CreateUser("teacher3@test.com", UserRole.Nauczyciel, true, department.Id),
                CreateUser("student1@test.com", UserRole.Uczen, true, department.Id),
                CreateUser("student2@test.com", UserRole.Uczen, true, department.Id),
                CreateUser("director@test.com", UserRole.Dyrektor, true, department.Id),
                CreateUser("inactive.teacher@test.com", UserRole.Nauczyciel, false, department.Id),
            };

            await Context.Users.AddRangeAsync(users);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetUsersByRoleAsync(role);

            // Weryfikacja
            result.Should().HaveCount(expectedCount);
            result.Should().OnlyContain(u => u.Role == role && u.IsActive);
        }

        [Theory]
        [InlineData("Jan", 3)]
        [InlineData("KOWALSKI", 1)]
        [InlineData("anna", 1)]
        [InlineData("test.com", 5)]
        [InlineData("nieistniejacy", 0)]
        [InlineData("", 5)]
        [InlineData(null, 5)]
        public async Task SearchUsersAsync_ShouldReturnMatchingUsers(string searchTerm, int expectedCount)
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = await GetOrCreateTestDepartmentAsync();
            var users = new List<User>
            {
                CreateUser("jan.kowalski@test.com", "Jan", "Kowalski", true, department.Id),
                CreateUser("jan.nowak@test.com", "Jan", "Nowak", true, department.Id),
                CreateUser("anna.kowalska@test.com", "Anna", "Kowalska", true, department.Id),
                CreateUser("piotr.wisniewski@test.com", "Piotr", "Wiśniewski", true, department.Id),
                CreateUser("inactive.jan@test.com", "Jan", "Nieaktywny", false, department.Id),
            };

            await Context.Users.AddRangeAsync(users);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.SearchUsersAsync(searchTerm);

            // Weryfikacja
            result.Should().HaveCount(expectedCount);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                result.Should().OnlyContain(u => u.IsActive &&
                    (u.FirstName.ToLower().Contains(searchTerm.ToLower()) ||
                     u.LastName.ToLower().Contains(searchTerm.ToLower()) ||
                     u.UPN.ToLower().Contains(searchTerm.ToLower())));
            }
        }

        [Fact]
        public async Task Update_ShouldModifyUserData()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = await GetOrCreateTestDepartmentAsync();
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Original",
                LastName = "Name",
                UPN = "original@test.com",
                Role = UserRole.Uczen,
                Phone = "123456789",
                DepartmentId = department.Id,
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = user.CreatedBy;
            var initialCreatedDate = user.CreatedDate;
            var currentUser = "user_specific_updater";
            SetTestUser(currentUser);

            // Działanie
            var userToUpdate = await _repository.GetByIdAsync(user.Id);
            userToUpdate!.FirstName = "Updated";
            userToUpdate.LastName = "Surname";
            userToUpdate.Phone = "987654321";
            userToUpdate.Role = UserRole.Nauczyciel;
            // userToUpdate.MarkAsModified(currentUser); // Niepotrzebne

            _repository.Update(userToUpdate);
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy na `currentUser`

            // Weryfikacja
            var updatedUser = await Context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            updatedUser.Should().NotBeNull();
            updatedUser!.FirstName.Should().Be("Updated");
            updatedUser.LastName.Should().Be("Surname");
            updatedUser.Phone.Should().Be("987654321");
            updatedUser.Role.Should().Be(UserRole.Nauczyciel);
            updatedUser.CreatedBy.Should().Be(initialCreatedBy);
            updatedUser.CreatedDate.Should().Be(initialCreatedDate);
            updatedUser.ModifiedBy.Should().Be(currentUser);
            updatedUser.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        [Fact]
        public async Task Delete_ShouldMarkUserAsInactive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = await GetOrCreateTestDepartmentAsync();
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "ToDelete",
                LastName = "User",
                UPN = "delete@test.com",
                Role = UserRole.Uczen,
                DepartmentId = department.Id,
                IsActive = true
            };
            await Context.Users.AddAsync(user);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = user.CreatedBy;
            var initialCreatedDate = user.CreatedDate;
            var currentUser = "user_specific_deleter";
            SetTestUser(currentUser);

            // Działanie
            var userToUpdate = await _repository.GetByIdAsync(user.Id);
            userToUpdate!.MarkAsDeleted(currentUser); // Wartość `deletedBy` zostanie nadpisana
            _repository.Update(userToUpdate);
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy

            // Weryfikacja
            var deletedUser = await Context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id);
            deletedUser.Should().NotBeNull();
            deletedUser!.IsActive.Should().BeFalse();
            deletedUser.CreatedBy.Should().Be(initialCreatedBy);
            deletedUser.CreatedDate.Should().Be(initialCreatedDate);
            deletedUser.ModifiedBy.Should().Be(currentUser);
            deletedUser.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        #region Helper Methods

        private async Task<Department> GetOrCreateTestDepartmentAsync(string departmentName = "Dział Testowy Użytkowników")
        {
            var department = await Context.Departments.FirstOrDefaultAsync(d => d.Name == departmentName);
            if (department == null)
            {
                department = new Department { Id = Guid.NewGuid().ToString(), Name = departmentName, IsActive = true };
                await Context.Departments.AddAsync(department);
                await SaveChangesAsync(); // Zapis z audytem
            }
            return department;
        }

        private User CreateUser(string upn, UserRole role, bool isActive, string departmentId)
        {
            var parts = upn.Split('@')[0].Split('.');
            return new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = parts.Length > 0 ? parts[0] : "Test",
                LastName = parts.Length > 1 ? parts[1] : "User",
                UPN = upn,
                Role = role,
                DepartmentId = departmentId,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
        }

        private User CreateUser(string upn, string firstName, string lastName, bool isActive, string departmentId)
        {
            return new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = UserRole.Uczen, // Domyślna rola dla tej metody pomocniczej
                DepartmentId = departmentId,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
        }

        #endregion
    }
}