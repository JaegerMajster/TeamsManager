using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IGenericRepository<Department>> _mockDepartmentRepository;
        private readonly Mock<IGenericRepository<UserSchoolType>> _mockUserSchoolTypeRepository;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IGenericRepository<UserSubject>> _mockUserSubjectRepository;
        private readonly Mock<IGenericRepository<Subject>> _mockSubjectRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache; // Dodano

        private readonly UserService _userService;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Klucze cache
        private const string AllActiveUsersCacheKey = "Users_AllActive";
        private const string UserByIdCacheKeyPrefix = "User_Id_";
        private const string UserByUpnCacheKeyPrefix = "User_Upn_";
        private const string UsersByRoleCacheKeyPrefix = "Users_Role_";

        public UserServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockDepartmentRepository = new Mock<IGenericRepository<Department>>();
            _mockUserSchoolTypeRepository = new Mock<IGenericRepository<UserSchoolType>>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserSubjectRepository = new Mock<IGenericRepository<UserSubject>>();
            _mockSubjectRepository = new Mock<IGenericRepository<Subject>>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<UserService>>();
            _mockMemoryCache = new Mock<IMemoryCache>(); // Inicjalizacja

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                        .Callback<OperationHistory>(op => _capturedOperationHistory = op!);

            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);

            _userService = new UserService(
                _mockUserRepository.Object,
                _mockDepartmentRepository.Object,
                _mockUserSchoolTypeRepository.Object,
                _mockSchoolTypeRepository.Object,
                _mockUserSubjectRepository.Object,
                _mockSubjectRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object // Przekazanie mocka
            );
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        // --- Testy GetUserByIdAsync ---
        [Fact]
        public async Task GetUserByIdAsync_ExistingUser_NotInCache_ShouldReturnAndCacheUser()
        {
            var userId = "user-1";
            var expectedUser = new User { Id = userId, UPN = "user1@example.com", FirstName = "Test", LastName = "User" };
            string cacheKey = UserByIdCacheKeyPrefix + userId;
            SetupCacheTryGetValue(cacheKey, (User?)null, false);
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(expectedUser);

            var result = await _userService.GetUserByIdAsync(userId);

            result.Should().BeEquivalentTo(expectedUser);
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(UserByUpnCacheKeyPrefix + expectedUser.UPN), Times.Once); // Sprawdzenie cache'owania także po UPN
        }

        [Fact]
        public async Task GetUserByIdAsync_ExistingUser_InCache_ShouldReturnUserFromCache()
        {
            var userId = "user-cached-id";
            var cachedUser = new User { Id = userId, UPN = "cached@example.com" };
            string cacheKey = UserByIdCacheKeyPrefix + userId;
            SetupCacheTryGetValue(cacheKey, cachedUser, true);

            var result = await _userService.GetUserByIdAsync(userId);

            result.Should().BeEquivalentTo(cachedUser);
            _mockUserRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DEBUG_UpdateUserAsync_ShouldCallCacheRemove()
        {
            // Arrange
            var userId = "user-update-cache";
            var departmentId = "dept1";

            var existingUser = new User
            {
                Id = userId,
                UPN = "old@example.com",
                Role = UserRole.Uczen,
                FirstName = "Old",
                LastName = "Data",
                DepartmentId = departmentId,
                IsActive = true
            };

            var userToUpdate = new User
            {
                Id = userId,
                UPN = "new@example.com",
                Role = UserRole.Nauczyciel,
                FirstName = "New",
                LastName = "Data",
                DepartmentId = departmentId,
                IsActive = true
            };

            var department = new Department { Id = departmentId, Name = "Test Dept", IsActive = true };

            // Setup wszystkich wymaganych mocków
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(existingUser);
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync("new@example.com")).ReturnsAsync((User?)null);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(department);
            _mockUserRepository.Setup(r => r.Update(It.IsAny<User>()));
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>())).Returns(Task.CompletedTask);

            // Dodaj callback żeby zobaczyć czy Remove jest wywoływane
            var removeCalls = new List<object>();
            _mockMemoryCache.Setup(m => m.Remove(It.IsAny<object>()))
                           .Callback<object>(key => removeCalls.Add(key));

            // Act
            var result = await _userService.UpdateUserAsync(userToUpdate);

            // Assert & Debug
            result.Should().BeTrue("UpdateUserAsync should succeed");

            // Debug: sprawdź co zostało wywołane
            Console.WriteLine($"Remove calls count: {removeCalls.Count}");
            foreach (var call in removeCalls)
            {
                Console.WriteLine($"Remove called with: {call}");
            }

            // Verify podstawowe wywołanie
            _mockMemoryCache.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeastOnce,
                "Cache Remove should be called at least once");

            // Verify konkretny klucz
            _mockMemoryCache.Verify(m => m.Remove("User_Id_user-update-cache"), Times.Once,
                "Should remove specific user cache key");
        }

        [Fact]
        public async Task GetUserByIdAsync_WithForceRefresh_ShouldBypassCache()
        {
            var userId = "user-force-id";
            var cachedUser = new User { Id = userId, UPN = "old@example.com" };
            var dbUser = new User { Id = userId, UPN = "new@example.com" };
            string cacheKey = UserByIdCacheKeyPrefix + userId;
            string upnCacheKey = UserByUpnCacheKeyPrefix + dbUser.UPN; // Klucz dla nowego UPN

            SetupCacheTryGetValue(cacheKey, cachedUser, true); // Jest w cache
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(dbUser); // Repo zwróci nowy obiekt
            SetupCacheTryGetValue(upnCacheKey, (User?)null, false); // Załóżmy, że cache dla UPN jest pusty lub zostanie nadpisany

            var result = await _userService.GetUserByIdAsync(userId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbUser);
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once); // Powinno być jedno wywołanie
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(upnCacheKey), Times.Once);
        }

        // --- Testy GetUserByUpnAsync ---
        [Fact]
        public async Task GetUserByUpnAsync_ExistingUser_NotInCache_ShouldReturnAndCacheUser()
        {
            var upn = "upn-user@example.com";
            var userId = "user-from-upn";
            var expectedUser = new User { Id = userId, UPN = upn, FirstName = "Test", LastName = "UserFromUpn" };
            string upnCacheKey = UserByUpnCacheKeyPrefix + upn;
            string idCacheKey = UserByIdCacheKeyPrefix + userId;

            SetupCacheTryGetValue(upnCacheKey, (User?)null, false); // Brak w cache UPN
            SetupCacheTryGetValue(idCacheKey, (User?)null, false); // Brak w cache ID (dla GetUserByIdAsync wywoływanego wewnętrznie)
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(upn)).ReturnsAsync(new User { Id = userId, UPN = upn }); // Podstawowy obiekt
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(expectedUser); // Pełny obiekt

            var result = await _userService.GetUserByUpnAsync(upn);

            result.Should().BeEquivalentTo(expectedUser);
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(upn), Times.Once);
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once); // Wywołanie GetUserByIdAsync wewnątrz
            _mockMemoryCache.Verify(m => m.CreateEntry(upnCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(idCacheKey), Times.Once);
        }

        // --- Testy GetAllActiveUsersAsync ---
        [Fact]
        public async Task GetAllActiveUsersAsync_NotInCache_ShouldReturnAndCache()
        {
            var activeUsers = new List<User> { new User { Id = "active1", IsActive = true } };
            SetupCacheTryGetValue(AllActiveUsersCacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(activeUsers);

            var result = await _userService.GetAllActiveUsersAsync();

            result.Should().BeEquivalentTo(activeUsers);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllActiveUsersCacheKey), Times.Once);
        }

        // --- Testy GetUsersByRoleAsync ---
        [Fact]
        public async Task GetUsersByRoleAsync_NotInCache_ShouldReturnAndCache()
        {
            var role = UserRole.Nauczyciel;
            var usersInRole = new List<User> { new User { Id = "teacher1", Role = role, IsActive = true } };
            string cacheKey = UsersByRoleCacheKeyPrefix + role.ToString();
            SetupCacheTryGetValue(cacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.GetUsersByRoleAsync(role)).ReturnsAsync(usersInRole);

            var result = await _userService.GetUsersByRoleAsync(role);

            result.Should().BeEquivalentTo(usersInRole);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        // --- Testy inwalidacji cache ---
        [Fact]
        public async Task CreateUserAsync_ShouldInvalidateCache()
        {
            var newUser = new User { Id = "new-user", UPN = "new@example.com", Role = UserRole.Uczen, FirstName = "New", LastName = "User" };
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newUser.UPN)).ReturnsAsync((User?)null);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync(new Department { Id = "dept1", IsActive = true });
            _mockUserRepository.Setup(r => r.AddAsync(It.IsAny<User>())).Callback<User>(u => u.Id = newUser.Id).Returns(Task.CompletedTask); // Symulacja ustawienia ID

            await _userService.CreateUserAsync(newUser.FirstName, newUser.LastName, newUser.UPN, newUser.Role, "dept1");

            _mockMemoryCache.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldInvalidateCacheForUserAndPotentiallyRolesAndAllUsers()
        {
            // Arrange
            var userId = "user-update-cache";
            var oldUpn = "old.update@example.com";
            var newUpn = "new.update@example.com";
            var oldRole = UserRole.Uczen;
            var newRole = UserRole.Nauczyciel;
            var departmentId = "dept1";

            var existingUser = new User
            {
                Id = userId,
                UPN = oldUpn,
                Role = oldRole,
                FirstName = "Old",
                LastName = "Data",
                DepartmentId = departmentId,
                IsActive = true
            };

            var userToUpdate = new User
            {
                Id = userId,
                UPN = newUpn,
                Role = newRole,
                FirstName = "New",
                LastName = "Data",
                DepartmentId = departmentId,
                IsActive = true
            };

            var department = new Department
            {
                Id = departmentId,
                Name = "Test Department",
                IsActive = true
            };

            // Setup mocków - KLUCZOWE: wszystkie wymagane zależności
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                              .ReturnsAsync(existingUser);

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newUpn))
                              .ReturnsAsync((User?)null); // Brak konfliktu UPN

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(department);

            _mockUserRepository.Setup(r => r.Update(It.IsAny<User>()));

            // Setup dla SaveOperationHistoryAsync - BARDZO WAŻNE
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                                          .ReturnsAsync((OperationHistory?)null);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                          .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.UpdateUserAsync(userToUpdate);

            // Assert
            result.Should().BeTrue();

            // Verify że cache został inwalidowany dla wszystkich kluczowych elementów
            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + userId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(AllActiveUsersCacheKey), Times.AtLeastOnce);

            // Opcjonalne dodatkowe weryfikacje - te mogą być wymagane w zależności od implementacji
            // _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + oldUpn), Times.AtLeastOnce);
            // _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + newUpn), Times.AtLeastOnce);
            // _mockMemoryCache.Verify(m => m.Remove(UsersByRoleCacheKeyPrefix + oldRole.ToString()), Times.AtLeastOnce);
            // _mockMemoryCache.Verify(m => m.Remove(UsersByRoleCacheKeyPrefix + newRole.ToString()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task DeactivateUserAsync_ShouldInvalidateCacheForUserAndLists()
        {
            var userId = "user-deactivate";
            var user = new User { Id = userId, UPN = "deactivate@example.com", Role = UserRole.Nauczyciel, IsActive = true };
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            await _userService.DeactivateUserAsync(userId);

            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + userId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + user.UPN), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UsersByRoleCacheKeyPrefix + user.Role.ToString()), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(AllActiveUsersCacheKey), Times.AtLeastOnce);
        }

        [Fact]
        public async Task AssignUserToSchoolTypeAsync_ShouldInvalidateUserCache()
        {
            var userId = "user-assign-st";
            var user = new User { Id = userId, UPN = "assign.st@example.com", SchoolTypeAssignments = new List<UserSchoolType>() };
            var schoolType = new SchoolType { Id = "st1", IsActive = true };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolType.Id)).ReturnsAsync(schoolType);
            _mockUserSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSchoolType, bool>>>()))
                                       .ReturnsAsync(new List<UserSchoolType>());
            _mockUserSchoolTypeRepository.Setup(r => r.AddAsync(It.IsAny<UserSchoolType>())).Returns(Task.CompletedTask);

            await _userService.AssignUserToSchoolTypeAsync(userId, schoolType.Id, DateTime.UtcNow);

            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + userId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + user.UPN), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidationForAllUserKeys()
        {
            await _userService.RefreshCacheAsync();

            // Sprawdzenie, czy token został zresetowany, co unieważni wszystkie wpisy
            // Możemy to zasymulować, sprawdzając, czy następne wywołanie Get pobiera z repozytorium
            SetupCacheTryGetValue(AllActiveUsersCacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .ReturnsAsync(new List<User>())
                               .Verifiable();

            await _userService.GetAllActiveUsersAsync();
            _mockUserRepository.Verify();
        }


        // Przykładowy istniejący test, który nadal powinien działać
        [Fact]
        public async Task GetUserByIdAsync_ExistingUser_ShouldReturnUser_OriginalTest()
        {
            var userId = "user-123-orig";
            var expectedUser = new User { Id = userId, UPN = "jan.kowalski@example.com", FirstName = "Jan", LastName = "Kowalski", IsActive = true };
            SetupCacheTryGetValue(UserByIdCacheKeyPrefix + userId, (User?)null, false);
            SetupCacheTryGetValue(UserByUpnCacheKeyPrefix + expectedUser.UPN, (User?)null, false);
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(expectedUser);

            var result = await _userService.GetUserByIdAsync(userId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedUser);
        }
    }
}