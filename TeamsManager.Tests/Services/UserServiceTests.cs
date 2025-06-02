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
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<ISubjectService> _mockSubjectService; // Dodany mock dla ISubjectService
        private readonly Mock<IPowerShellService> _mockPowerShellService; // Dodany mock dla IPowerShellService

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
            _mockSubjectService = new Mock<ISubjectService>(); // Inicjalizacja mocka ISubjectService
            _mockPowerShellService = new Mock<IPowerShellService>(); // Inicjalizacja mocka IPowerShellService

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);
            // Usunięto Setup dla Update, jeśli SaveOperationHistoryAsync zawsze robi AddAsync

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
                _mockMemoryCache.Object,
                _mockSubjectService.Object, // Przekazanie mocka ISubjectService
                _mockPowerShellService.Object // Przekazanie mocka IPowerShellService
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

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newUser.UPN)).ReturnsAsync((User?)null);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync("dept1")).ReturnsAsync(new Department { Id = "dept1", IsActive = true });
            _mockUserRepository.Setup(r => r.AddAsync(It.IsAny<User>()))
                                 .Callback<User>(u => u.Id = createdUserWithId.Id)
                                 .Returns(Task.CompletedTask);
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var result = await _userService.CreateUserAsync(newUser.FirstName, newUser.LastName, newUser.UPN, newUser.Role, "dept1", "password123", "mock-access-token");
            result.Should().NotBeNull();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityName == $"{newUser.FirstName} {newUser.LastName} ({newUser.UPN})" && op.Type == OperationType.UserCreated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);


            _mockMemoryCache.Verify(m => m.Remove(AllActiveUsersCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + result!.Id), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + result.UPN), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UsersByRoleCacheKeyPrefix + result.Role.ToString()), Times.AtLeastOnce);

            AssertCacheInvalidationByReFetchingAllActiveUsers(new List<User> { result });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
        }

        [Fact]
        public async Task UpdateUserAsync_ShouldInvalidateCache_AndHandleUpnAndRoleChange()
        {
            ResetCapturedOperationHistory();
            var userId = "user-update-cache";
            var oldUpn = "old.update@example.com";
            var newUpn = "new.update@example.com";
            var oldRole = UserRole.Uczen;
            var newRole = UserRole.Nauczyciel;
            var departmentId = "dept1";

            var existingUserInDb = new User
            {
                Id = userId,
                UPN = oldUpn,
                Role = oldRole,
                FirstName = "Old",
                LastName = "Data",
                DepartmentId = departmentId,
                IsActive = true,
                CreatedBy = "initial",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };
            var userToUpdateData = new User
            {
                Id = userId,
                UPN = newUpn,
                Role = newRole,
                FirstName = "New",
                LastName = "Data",
                DepartmentId = departmentId,
                IsActive = true
            };
            var department = new Department { Id = departmentId, Name = "Test Department", IsActive = true };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(existingUserInDb);
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newUpn)).ReturnsAsync((User?)null);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(department);
            _mockUserRepository.Setup(r => r.Update(It.IsAny<User>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var updateResult = await _userService.UpdateUserAsync(userToUpdateData, "mock-access-token");
            updateResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == userId && op.Type == OperationType.UserUpdated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + userId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + oldUpn), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + newUpn), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UsersByRoleCacheKeyPrefix + oldRole.ToString()), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UsersByRoleCacheKeyPrefix + newRole.ToString()), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(AllActiveUsersCacheKey), Times.AtLeastOnce);

            var expectedAfterUpdate = new User
            {
                Id = userId,
                UPN = newUpn,
                Role = newRole,
                FirstName = "New",
                LastName = "Data",
                DepartmentId = departmentId,
                Department = department,
                IsActive = true,
                CreatedBy = existingUserInDb.CreatedBy,
                CreatedDate = existingUserInDb.CreatedDate
            };
            AssertCacheInvalidationByReFetchingAllActiveUsers(new List<User> { expectedAfterUpdate });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.UserUpdated);
        }

        [Fact]
        public async Task DeactivateUserAsync_ShouldInvalidateCacheForUserAndLists()
        {
            ResetCapturedOperationHistory();
            var userId = "user-deactivate";
            var user = new User { Id = userId, UPN = "deactivate@example.com", Role = UserRole.Nauczyciel, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockUserRepository.Setup(r => r.Update(It.IsAny<User>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var deactivateResult = await _userService.DeactivateUserAsync(userId, "mock-access-token");
            deactivateResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == userId && op.Type == OperationType.UserDeactivated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(UserByIdCacheKeyPrefix + userId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UserByUpnCacheKeyPrefix + user.UPN), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UsersByRoleCacheKeyPrefix + user.Role.ToString()), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(AllActiveUsersCacheKey), Times.AtLeastOnce);

            AssertCacheInvalidationByReFetchingAllActiveUsers(new List<User>());

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.UserDeactivated);
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
    }
}