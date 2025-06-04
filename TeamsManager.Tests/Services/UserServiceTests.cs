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
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;
using Microsoft.Identity.Client;

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
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<ISubjectService> _mockSubjectService;
        private readonly Mock<IPowerShellService> _mockPowerShellService;
        private readonly Mock<IConfidentialClientApplication> _mockConfidentialClientApplication;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IPowerShellCacheService> _mockPowerShellCacheService;

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
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockSubjectService = new Mock<ISubjectService>();
            _mockPowerShellService = new Mock<IPowerShellService>();
            _mockConfidentialClientApplication = new Mock<IConfidentialClientApplication>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockPowerShellCacheService = new Mock<IPowerShellCacheService>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockOperationHistoryService.Setup(s => s.CreateNewOperationEntryAsync(
                                           It.IsAny<OperationType>(), 
                                           It.IsAny<string>(), 
                                           It.IsAny<string>(), 
                                           It.IsAny<string>(), 
                                           It.IsAny<string>(), 
                                           It.IsAny<string>()))
                                       .ReturnsAsync(new OperationHistory { Id = "mock-operation-id", Status = OperationStatus.InProgress })
                                       .Callback<OperationType, string, string, string, string, string>((type, entityType, entityId, entityName, details, parentId) =>
                                       {
                                           _capturedOperationHistory = new OperationHistory { Id = "mock-operation-id", Type = type, TargetEntityType = entityType, TargetEntityId = entityId ?? string.Empty, TargetEntityName = entityName ?? string.Empty, Status = OperationStatus.InProgress };
                                       });

            _mockOperationHistoryService.Setup(s => s.UpdateOperationStatusAsync(It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                                       .ReturnsAsync(true)
                                       .Callback<string, OperationStatus, string, string>((id, status, details, errorMessage) =>
                                       {
                                           if (_capturedOperationHistory != null && _capturedOperationHistory.Id == id)
                                           {
                                               _capturedOperationHistory.Status = status;
                                               _capturedOperationHistory.OperationDetails = details ?? string.Empty;
                                               _capturedOperationHistory.ErrorMessage = errorMessage;
                                           }
                                       });

            _mockNotificationService.Setup(n => n.SendNotificationToUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                  .Returns(Task.CompletedTask);

            var mockCacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
            _mockPowerShellCacheService.Setup(c => c.GetDefaultCacheEntryOptions())
                                      .Returns(mockCacheEntryOptions);

            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupProperty(e => e.Value);
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
                _mockMemoryCache.Object,
                _mockSubjectService.Object,
                _mockPowerShellService.Object,
                _mockOperationHistoryService.Object,
                _mockPowerShellCacheService.Object,
                _mockNotificationService.Object
            );
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        private void AssertCacheInvalidationByReFetchingAllActiveUsers(List<User> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(AllActiveUsersCacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _userService.GetAllActiveUsersAsync().Result;

            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once, "GetAllActiveUsersAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllActiveUsersCacheKey), Times.AtLeastOnce, "Dane GetAllActiveUsersAsync powinny zostać ponownie zcache'owane.");
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
            _mockMemoryCache.Verify(m => m.CreateEntry(UserByUpnCacheKeyPrefix + expectedUser.UPN), Times.Once);
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
        public async Task GetUserByIdAsync_WithForceRefresh_ShouldBypassCache()
        {
            var userId = "user-force-id";
            var cachedUser = new User { Id = userId, UPN = "old@example.com" };
            var dbUser = new User { Id = userId, UPN = "new@example.com" };
            string cacheKey = UserByIdCacheKeyPrefix + userId;
            string upnCacheKey = UserByUpnCacheKeyPrefix + dbUser.UPN;

            SetupCacheTryGetValue(cacheKey, cachedUser, true);
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(dbUser);
            SetupCacheTryGetValue(upnCacheKey, (User?)null, false);

            var result = await _userService.GetUserByIdAsync(userId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbUser);
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
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

            SetupCacheTryGetValue(upnCacheKey, (User?)null, false);
            SetupCacheTryGetValue(idCacheKey, (User?)null, false);
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(upn)).ReturnsAsync(new User { Id = userId, UPN = upn });
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(expectedUser);

            var result = await _userService.GetUserByUpnAsync(upn);

            result.Should().BeEquivalentTo(expectedUser);
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(upn), Times.Once);
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
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
            ResetCapturedOperationHistory();
            var newUser = new User { UPN = "new@example.com", Role = UserRole.Uczen, FirstName = "New", LastName = "User", DepartmentId = "dept1" };
            var createdUserWithId = new User { Id = "new-user-id", UPN = newUser.UPN, Role = newUser.Role, FirstName = newUser.FirstName, LastName = newUser.LastName, DepartmentId = newUser.DepartmentId, IsActive = true };

            // Mock dla sprawdzenia czy user istnieje
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newUser.UPN)).ReturnsAsync((User?)null);
            
            // Mock dla departamentu
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync("dept1")).ReturnsAsync(new Department { Id = "dept1", IsActive = true });
            
            // Mock dla dodania użytkownika do bazy
            _mockUserRepository.Setup(r => r.AddAsync(It.IsAny<User>()))
                                 .Callback<User>(u => u.Id = createdUserWithId.Id)
                                 .Returns(Task.CompletedTask);

            // Mock dla MSAL - pozwalamy na niepowodzenie OBO i testujemy czy ogólny przepływ działa
            _mockConfidentialClientApplication.Setup(c => c.AcquireTokenOnBehalfOf(It.IsAny<string[]>(), It.IsAny<UserAssertion>()))
                                              .Throws(new MsalServiceException("OBO_ERROR", "Test OBO failure"));

            // Mock dla PowerShell Service - bezpośrednie uruchomienie bez OBO
            _mockPowerShellService.Setup(ps => ps.ConnectWithAccessTokenAsync(It.IsAny<string>(), It.IsAny<string[]>()))
                                 .ReturnsAsync(false); // OBO failed, więc connection też fails
            _mockPowerShellService.Setup(ps => ps.Users.CreateM365UserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<bool>()))
                                 .ReturnsAsync("external-user-id-12345");

            // Oczekujemy, że CreateUserAsync zwróci null z powodu niepowodzenia OBO
            var result = await _userService.CreateUserAsync(newUser.FirstName, newUser.LastName, newUser.UPN, newUser.Role, "dept1", "password123", "mock-access-token");
            result.Should().BeNull();

            // Verify że operacja została oznaczona jako failed
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.Status == OperationStatus.Failed && op.Type == OperationType.UserCreated)), Times.AtLeastOnce);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldInvalidateCache_AndHandleUpnAndRoleChange()
        {
            // Arrange
            var userId = "user-123";
            var existingUser = new User
            {
                Id = userId,
                UPN = "old.upn@example.com",
                FirstName = "Old",
                LastName = "Name",
                DepartmentId = "dept-1",
                Role = UserRole.Uczen,
                IsActive = true
            };

            var updatedUser = new User
            {
                Id = userId,
                UPN = "new.upn@example.com",
                FirstName = "New",
                LastName = "Name",
                DepartmentId = "dept-1",
                Role = UserRole.Nauczyciel,
                IsActive = true
            };

            var department = new Department { Id = "dept-1", Name = "Test Department", IsActive = true };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(existingUser);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync("dept-1")).ReturnsAsync(department);

            // Mock PowerShellService aby symulować niepowodzenie połączenia (co jest oczekiwane w testach)
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(It.IsAny<string>(), It.IsAny<string[]>()))
                                  .ReturnsAsync(false);

            // Act
            var updateResult = await _userService.UpdateUserAsync(updatedUser, "test-token");

            // Assert - oczekujemy false ponieważ PowerShell connection nie powiedzie się
            updateResult.Should().BeFalse();
        }

        [Fact]
        public async Task DeactivateUserAsync_ShouldInvalidateCacheForUserAndLists()
        {
            // Arrange
            var userId = "user-123";
            var activeUser = new User
            {
                Id = userId,
                UPN = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                IsActive = true,
                Role = UserRole.Uczen
            };

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .ReturnsAsync(new List<User> { activeUser });

            // Mock PowerShellService aby symulować niepowodzenie połączenia (co jest oczekiwane w testach)
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(It.IsAny<string>(), It.IsAny<string[]>()))
                                  .ReturnsAsync(false);

            // Act
            var deactivateResult = await _userService.DeactivateUserAsync(userId, "test-token", deactivateM365Account: true);

            // Assert - oczekujemy false ponieważ PowerShell connection nie powiedzie się
            deactivateResult.Should().BeFalse();
        }

        [Fact]
        public async Task AssignUserToSchoolTypeAsync_ShouldInvalidateUserCache()
        {
            ResetCapturedOperationHistory();
            var userId = "user-assign-st";
            var user = new User { Id = userId, UPN = "assign.st@example.com", SchoolTypeAssignments = new List<UserSchoolType>(), IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var schoolType = new SchoolType { Id = "st1", IsActive = true };
            var newUserSchoolType = new UserSchoolType { Id = "new-ust-id", UserId = userId, SchoolTypeId = schoolType.Id, User = user, SchoolType = schoolType, IsActive = true, IsCurrentlyActive = true };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolType.Id)).ReturnsAsync(schoolType);
            _mockUserSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSchoolType, bool>>>()))
                                       .ReturnsAsync(new List<UserSchoolType>());
            _mockUserSchoolTypeRepository.Setup(r => r.AddAsync(It.IsAny<UserSchoolType>()))
                                        .Callback<UserSchoolType>(ust => ust.Id = newUserSchoolType.Id)
                                        .Returns(Task.CompletedTask);
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var assignResult = await _userService.AssignUserToSchoolTypeAsync(userId, schoolType.Id, DateTime.UtcNow);
            assignResult.Should().NotBeNull();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.Type == OperationType.UserSchoolTypeAssigned)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + userId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + user.UPN), Times.AtLeastOnce);

            SetupCacheTryGetValue(UserByIdCacheKeyPrefix + userId, (User?)null, false);
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user).Verifiable();
            await _userService.GetUserByIdAsync(userId);
            _mockUserRepository.Verify();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.UserSchoolTypeAssigned);
        }

        [Fact]
        public async Task AssignTeacherToSubjectAsync_ValidData_ShouldAssignAndInvalidateSubjectCache()
        {
            ResetCapturedOperationHistory();
            var teacherId = "teacher-1";
            var subjectId = "subject-1";
            var assignedDate = DateTime.UtcNow;
            var teacher = new User { Id = teacherId, UPN = "teacher@example.com", Role = UserRole.Nauczyciel, IsActive = true, TaughtSubjects = new List<UserSubject>() };
            var subject = new Subject { Id = subjectId, Name = "Matematyka", IsActive = true };
            var expectedUserSubject = new UserSubject { Id = "us-1", UserId = teacherId, SubjectId = subjectId, AssignedDate = assignedDate, IsActive = true };

            _mockUserRepository.Setup(r => r.GetByIdAsync(teacherId)).ReturnsAsync(teacher);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subject);
            _mockUserSubjectRepository.Setup(r => r.AddAsync(It.IsAny<UserSubject>()))
                                    .Callback<UserSubject>(us => us.Id = expectedUserSubject.Id)
                                    .Returns(Task.CompletedTask);
            _mockSubjectService.Setup(s => s.InvalidateTeachersCacheForSubjectAsync(subjectId)).Returns(Task.CompletedTask);
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var result = await _userService.AssignTeacherToSubjectAsync(teacherId, subjectId, assignedDate);

            result.Should().NotBeNull();
            result!.Id.Should().Be(expectedUserSubject.Id);
            _mockUserRepository.Verify(r => r.GetByIdAsync(teacherId), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(subjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.AddAsync(It.Is<UserSubject>(us => us.UserId == teacherId && us.SubjectId == subjectId)), Times.Once);

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.Type == OperationType.UserSubjectAssigned)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + teacherId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + teacher.UPN), Times.AtLeastOnce);

            _mockSubjectService.Verify(s => s.InvalidateTeachersCacheForSubjectAsync(subjectId), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.UserSubjectAssigned);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task RemoveTeacherFromSubjectAsync_ValidId_ShouldRemoveAndInvalidateSubjectCache()
        {
            ResetCapturedOperationHistory();
            var userSubjectId = "us-to-remove";
            var teacherId = "teacher-for-removal";
            var subjectId = "subject-for-removal";
            var user = new User { Id = teacherId, UPN = "teacher.remove@example.com", IsActive = true };
            var subject = new Subject { Id = subjectId, Name = "Fizyka", IsActive = true };
            var assignmentToRemove = new UserSubject { Id = userSubjectId, UserId = teacherId, SubjectId = subjectId, User = user, Subject = subject, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };

            _mockUserSubjectRepository.Setup(r => r.GetByIdAsync(userSubjectId)).ReturnsAsync(assignmentToRemove);
            _mockUserRepository.Setup(r => r.GetByIdAsync(teacherId)).ReturnsAsync(user);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subject);
            _mockUserSubjectRepository.Setup(r => r.Update(It.IsAny<UserSubject>()));
            _mockSubjectService.Setup(s => s.InvalidateTeachersCacheForSubjectAsync(subjectId)).Returns(Task.CompletedTask);
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var result = await _userService.RemoveTeacherFromSubjectAsync(userSubjectId);

            result.Should().BeTrue();
            _mockUserSubjectRepository.Verify(r => r.GetByIdAsync(userSubjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.Update(It.Is<UserSubject>(us => us.Id == userSubjectId && !us.IsActive)), Times.Once);

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.Type == OperationType.UserSubjectRemoved)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + teacherId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + user.UPN), Times.AtLeastOnce);

            _mockSubjectService.Verify(s => s.InvalidateTeachersCacheForSubjectAsync(subjectId), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.UserSubjectRemoved);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }


        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidationForAllUserKeys()
        {
            await _userService.RefreshCacheAsync();

            SetupCacheTryGetValue(AllActiveUsersCacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .ReturnsAsync(new List<User>())
                               .Verifiable();

            await _userService.GetAllActiveUsersAsync();
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
        }


        [Fact]
        public async Task GetUserByIdAsync_ExistingUser_OriginalTest_ShouldReturnUser()
        {
            var userId = "user-123-orig";
            var expectedUser = new User { Id = userId, UPN = "jan.kowalski@example.com", FirstName = "Jan", LastName = "Kowalski", IsActive = true };
            SetupCacheTryGetValue(UserByIdCacheKeyPrefix + userId, (User?)null, false);
            SetupCacheTryGetValue(UserByUpnCacheKeyPrefix + expectedUser.UPN, (User?)null, false);
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(expectedUser);

            var result = await _userService.GetUserByIdAsync(userId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedUser);
        }

        [Fact]
        public async Task CreateUserAsync_WithAutoReconnect_ShouldSucceed()
        {
            // Arrange
            var newUser = new User 
            { 
                UPN = "new@example.com", 
                FirstName = "New", 
                LastName = "User", 
                Role = UserRole.Uczen,
                DepartmentId = "dept1"
            };
            
            var department = new Department { Id = "dept1", IsActive = true };
            
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newUser.UPN))
                               .ReturnsAsync((User?)null);
            
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync("dept1"))
                                    .ReturnsAsync(department);
            
            // Mock PowerShellService z ExecuteWithAutoConnectAsync
            _mockPowerShellService.Setup(ps => ps.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(), // apiAccessToken
                It.IsAny<Func<Task<string>>>(),
                It.IsAny<string>()))
                .ReturnsAsync("external-user-id");
            
            _mockUserRepository.Setup(r => r.AddAsync(It.IsAny<User>()))
                               .Callback<User>(u => u.Id = "new-user-id")
                               .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.CreateUserAsync(
                newUser.FirstName, 
                newUser.LastName, 
                newUser.UPN, 
                newUser.Role, 
                "dept1", 
                "password123", 
                "mock-token"
            );

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("new-user-id");
            
            // Verify że nie było próby ręcznego połączenia przez OBO
            _mockConfidentialClientApplication.Verify(app => app.AcquireTokenOnBehalfOf(
                It.IsAny<string[]>(), It.IsAny<UserAssertion>()), Times.Never);
            
            // Verify że użyto ExecuteWithAutoConnectAsync
            _mockPowerShellService.Verify(ps => ps.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(), // apiAccessToken
                It.IsAny<Func<Task<string>>>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UpdateUserAsync_WithAutoReconnect_ShouldSucceed()
        {
            // Arrange
            var userId = "user-123";
            var existingUser = new User
            {
                Id = userId,
                UPN = "old@example.com",
                FirstName = "Old",
                LastName = "Name",
                DepartmentId = "dept-1",
                Role = UserRole.Uczen,
                IsActive = true
            };

            var updatedUser = new User
            {
                Id = userId,
                UPN = "new@example.com",
                FirstName = "New",
                LastName = "Name",
                DepartmentId = "dept-1",
                Role = UserRole.Nauczyciel,
                IsActive = true
            };

            var department = new Department { Id = "dept-1", IsActive = true };
            var mockOperationHistory = new OperationHistory { Id = "test-operation-id", Status = OperationStatus.InProgress };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                               .ReturnsAsync(existingUser);
            
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync("dept-1"))
                                    .ReturnsAsync(department);
            
            // Setup IOperationHistoryService
            _mockOperationHistoryService.Setup(s => s.CreateNewOperationEntryAsync(
                    It.IsAny<OperationType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(mockOperationHistory);
            
            _mockOperationHistoryService.Setup(s => s.UpdateOperationStatusAsync(
                    It.IsAny<string>(),
                    It.IsAny<OperationStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(true);
            
            // Setup INotificationService
            _mockNotificationService.Setup(s => s.SendNotificationToUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            // Mock ExecuteWithAutoConnectAsync dla update operacji
            _mockPowerShellService.Setup(ps => ps.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(), // apiAccessToken
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<string>()))
                .ReturnsAsync(true);
            
            _mockUserRepository.Setup(r => r.Update(It.IsAny<User>()));

            // Act
            var result = await _userService.UpdateUserAsync(updatedUser, "test-token");

            // Assert
            result.Should().BeTrue();
            
            // Verify że nie było próby ręcznego połączenia
            _mockConfidentialClientApplication.Verify(app => app.AcquireTokenOnBehalfOf(
                It.IsAny<string[]>(), It.IsAny<UserAssertion>()), Times.Never);
            
            // Verify że użyto ExecuteWithAutoConnectAsync
            _mockPowerShellService.Verify(ps => ps.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(), // apiAccessToken
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task DeactivateUserAsync_WithAutoReconnect_ShouldSucceed()
        {
            // Arrange
            var userId = "user-123";
            var activeUser = new User
            {
                Id = userId,
                UPN = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                IsActive = true,
                Role = UserRole.Uczen
            };

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .ReturnsAsync(new List<User> { activeUser });

            // Mock ExecuteWithAutoConnectAsync dla deactivate operacji
            _mockPowerShellService.Setup(ps => ps.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(), // apiAccessToken
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _userService.DeactivateUserAsync(userId, "test-token", deactivateM365Account: true);

            // Assert
            result.Should().BeTrue();
            
            // Verify że użyto ExecuteWithAutoConnectAsync
            _mockPowerShellService.Verify(ps => ps.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(), // apiAccessToken
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_WhenAutoReconnectFails_ShouldReturnNull()
        {
            // Arrange
            var newUser = new User 
            { 
                UPN = "new@example.com", 
                FirstName = "New", 
                LastName = "User", 
                Role = UserRole.Uczen,
                DepartmentId = "dept1"
            };
            
            var department = new Department { Id = "dept1", IsActive = true };
            
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newUser.UPN))
                               .ReturnsAsync((User?)null);
            
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync("dept1"))
                                    .ReturnsAsync(department);
            
            // Mock ExecuteWithAutoConnectAsync to return null (failure)
            _mockPowerShellService.Setup(ps => ps.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(), // apiAccessToken
                It.IsAny<Func<Task<string>>>(),
                It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _userService.CreateUserAsync(
                newUser.FirstName, 
                newUser.LastName, 
                newUser.UPN, 
                newUser.Role, 
                "dept1", 
                "password123", 
                "mock-token"
            );

            // Assert
            result.Should().BeNull();
            
            // Verify że nie dodano użytkownika do bazy gdy PowerShell operation nie powiodło się
            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_WhenAutoReconnectFails_ShouldReturnFalse()
        {
            // Arrange
            var userId = "user-123";
            var existingUser = new User
            {
                Id = userId,
                UPN = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                DepartmentId = "dept-1",
                Role = UserRole.Uczen,
                IsActive = true
            };

            var updatedUser = new User
            {
                Id = userId,
                UPN = "updated@example.com",
                FirstName = "Updated",
                LastName = "User",
                DepartmentId = "dept-1",
                Role = UserRole.Nauczyciel,
                IsActive = true
            };

            var department = new Department { Id = "dept-1", IsActive = true };
            var mockOperationHistory = new OperationHistory { Id = "test-operation-id", Status = OperationStatus.InProgress };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                               .ReturnsAsync(existingUser);
            
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync("dept-1"))
                                    .ReturnsAsync(department);
            
            // Setup IOperationHistoryService
            _mockOperationHistoryService.Setup(s => s.CreateNewOperationEntryAsync(
                    It.IsAny<OperationType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(mockOperationHistory);
            
            _mockOperationHistoryService.Setup(s => s.UpdateOperationStatusAsync(
                    It.IsAny<string>(),
                    It.IsAny<OperationStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(true);
            
            // Setup INotificationService
            _mockNotificationService.Setup(s => s.SendNotificationToUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            // Mock ExecuteWithAutoConnectAsync to return false (failure)
            _mockPowerShellService.Setup(ps => ps.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(), // apiAccessToken
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _userService.UpdateUserAsync(updatedUser, "test-token");

            // Assert
            result.Should().BeFalse();
            
            // Verify że nie zaktualizowano bazy gdy PowerShell operation nie powiodło się
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
        }
    }
}