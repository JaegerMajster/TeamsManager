using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
    public class SchoolTypeServiceTests
    {
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolTypeService>> _mockLogger;

        private readonly SchoolTypeService _schoolTypeService;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        public SchoolTypeServiceTests()
        {
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolTypeService>>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);

            _schoolTypeService = new SchoolTypeService(
                _mockSchoolTypeRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task GetSchoolTypeByIdAsync_ExistingSchoolType_ShouldReturnSchoolType()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var expectedSchoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Szkoła średnia ogólnokształcąca",
                IsActive = true
            };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(expectedSchoolType);

            // Act
            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(expectedSchoolType);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
        }

        [Fact]
        public async Task GetSchoolTypeByIdAsync_NonExistingSchoolType_ShouldReturnNull()
        {
            // Arrange
            var schoolTypeId = "non-existing-st";
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);

            // Assert
            result.Should().BeNull();
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_ShouldReturnActiveSchoolTypes()
        {
            // Arrange
            var activeSchoolTypes = new List<SchoolType>
            {
                new SchoolType { Id = "st-1", ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true },
                new SchoolType { Id = "st-2", ShortName = "TZ", FullName = "Technikum Zawodowe", IsActive = true }
            };

            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(activeSchoolTypes);

            // Act
            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(activeSchoolTypes);
            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_NoActiveSchoolTypes_ShouldReturnEmptyList()
        {
            // Arrange
            var emptyList = new List<SchoolType>();
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(emptyList);

            // Act
            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task CreateSchoolTypeAsync_ValidData_ShouldCreateAndReturnSchoolTypeAndLog()
        {
            // Arrange
            var shortName = "KKZ";
            var fullName = "Kwalifikacyjne Kursy Zawodowe";
            var description = "Kursy zawodowe dla dorosłych";

            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType>()); // Brak istniejących

            SchoolType? addedSchoolType = null;
            _mockSchoolTypeRepository.Setup(r => r.AddAsync(It.IsAny<SchoolType>()))
                                    .Callback<SchoolType>(st => addedSchoolType = st)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _schoolTypeService.CreateSchoolTypeAsync(shortName, fullName, description);

            // Assert
            result.Should().NotBeNull();
            addedSchoolType.Should().NotBeNull();
            result.Should().BeSameAs(addedSchoolType);

            result!.ShortName.Should().Be(shortName);
            result.FullName.Should().Be(fullName);
            result.Description.Should().Be(description);
            result.IsActive.Should().BeTrue();
            result.CreatedBy.Should().Be(_currentLoggedInUserUpn);

            _mockSchoolTypeRepository.Verify(r => r.AddAsync(It.IsAny<SchoolType>()), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Theory]
        [InlineData("", "Pełna nazwa", "Opis")]
        [InlineData("   ", "Pełna nazwa", "Opis")]
        [InlineData(null, "Pełna nazwa", "Opis")]
        [InlineData("Skrót", "", "Opis")]
        [InlineData("Skrót", "   ", "Opis")]
        [InlineData("Skrót", null, "Opis")]
        public async Task CreateSchoolTypeAsync_EmptyShortNameOrFullName_ShouldReturnNullAndLogFailed(string shortName, string fullName, string description)
        {
            // Act
            var result = await _schoolTypeService.CreateSchoolTypeAsync(shortName, fullName, description);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Skrócona nazwa i pełna nazwa typu szkoły są wymagane.");

            _mockSchoolTypeRepository.Verify(r => r.AddAsync(It.IsAny<SchoolType>()), Times.Never);
        }

        [Fact]
        public async Task CreateSchoolTypeAsync_ShortNameAlreadyExists_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            var shortName = "LO";
            var fullName = "Liceum Ogólnokształcące";
            var description = "Opis";

            var existingSchoolType = new SchoolType
            {
                Id = "existing-st",
                ShortName = shortName,
                IsActive = true
            };

            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType> { existingSchoolType });

            // Act
            var result = await _schoolTypeService.CreateSchoolTypeAsync(shortName, fullName, description);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Typ szkoły o skróconej nazwie '{shortName}' już istnieje");

            _mockSchoolTypeRepository.Verify(r => r.AddAsync(It.IsAny<SchoolType>()), Times.Never);
        }

        [Fact]
        public async Task UpdateSchoolTypeAsync_ExistingSchoolTypeWithValidData_ShouldUpdateAndReturnTrueAndLog()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var existingSchoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Stary opis",
                IsActive = true
            };

            var schoolTypeToUpdate = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące - Nowa nazwa",
                Description = "Nowy opis",
                ColorCode = "#FF5722",
                SortOrder = 10
            };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(existingSchoolType);

            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType>()); // Brak konfliktów nazw

            // Act
            var result = await _schoolTypeService.UpdateSchoolTypeAsync(schoolTypeToUpdate);

            // Assert
            result.Should().BeTrue();

            existingSchoolType.FullName.Should().Be("Liceum Ogólnokształcące - Nowa nazwa");
            existingSchoolType.Description.Should().Be("Nowy opis");
            existingSchoolType.ColorCode.Should().Be("#FF5722");
            existingSchoolType.SortOrder.Should().Be(10);

            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockSchoolTypeRepository.Verify(r => r.Update(existingSchoolType), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task UpdateSchoolTypeAsync_SchoolTypeNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var schoolTypeId = "non-existing-st";
            var schoolTypeToUpdate = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "TZ",
                FullName = "Technikum Zawodowe"
            };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _schoolTypeService.UpdateSchoolTypeAsync(schoolTypeToUpdate);

            // Assert
            result.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Typ szkoły o ID '{schoolTypeId}' nie istnieje");

            _mockSchoolTypeRepository.Verify(r => r.Update(It.IsAny<SchoolType>()), Times.Never);
        }

        [Fact]
        public async Task UpdateSchoolTypeAsync_ShortNameConflict_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var existingSchoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                IsActive = true
            };

            var schoolTypeToUpdate = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "TZ", // Próba zmiany na istniejącą nazwę
                FullName = "Liceum Ogólnokształcące"
            };

            var conflictingSchoolType = new SchoolType
            {
                Id = "other-st",
                ShortName = "TZ",
                IsActive = true
            };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(existingSchoolType);

            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType> { conflictingSchoolType });

            // Act
            var result = await _schoolTypeService.UpdateSchoolTypeAsync(schoolTypeToUpdate);

            // Assert
            result.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Typ szkoły o skróconej nazwie 'TZ' już istnieje");

            _mockSchoolTypeRepository.Verify(r => r.Update(It.IsAny<SchoolType>()), Times.Never);
        }

        [Fact]
        public void UpdateSchoolTypeAsync_NullSchoolType_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(
                () => _schoolTypeService.UpdateSchoolTypeAsync(null!));

            exception.Should().NotBeNull();
        }

        [Fact]
        public void UpdateSchoolTypeAsync_EmptySchoolTypeId_ShouldThrowArgumentNullException()
        {
            // Arrange
            var schoolTypeToUpdate = new SchoolType
            {
                Id = "",
                ShortName = "LO",
                FullName = "Liceum"
            };

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(
                () => _schoolTypeService.UpdateSchoolTypeAsync(schoolTypeToUpdate));

            exception.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteSchoolTypeAsync_ExistingUnusedSchoolType_ShouldSoftDeleteAndReturnTrueAndLog()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                IsActive = true
            };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.DeleteSchoolTypeAsync(schoolTypeId);

            // Assert
            result.Should().BeTrue();
            schoolType.IsActive.Should().BeFalse(); // Soft delete

            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockSchoolTypeRepository.Verify(r => r.Update(schoolType), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task DeleteSchoolTypeAsync_SchoolTypeNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var schoolTypeId = "non-existing-st";

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _schoolTypeService.DeleteSchoolTypeAsync(schoolTypeId);

            // Assert
            result.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Typ szkoły o ID '{schoolTypeId}' nie istnieje");

            _mockSchoolTypeRepository.Verify(r => r.Update(It.IsAny<SchoolType>()), Times.Never);
        }

        [Fact]
        public async Task DeleteSchoolTypeAsync_AlreadyInactiveSchoolType_ShouldReturnTrueAndLogCompleted()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                IsActive = false // Już nieaktywny
            };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.DeleteSchoolTypeAsync(schoolTypeId);

            // Assert
            result.Should().BeTrue();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.OperationDetails.Should().Contain("był już nieaktywny");

            _mockSchoolTypeRepository.Verify(r => r.Update(It.IsAny<SchoolType>()), Times.Never);
        }

        [Fact]
        public async Task AssignViceDirectorToSchoolTypeAsync_ValidUserAndSchoolType_ShouldAssignAndReturnTrueAndLog()
        {
            // Arrange
            var viceDirectorUserId = "user-123";
            var schoolTypeId = "st-123";

            var viceDirector = new User
            {
                Id = viceDirectorUserId,
                UPN = "vicedirector@example.com",
                Role = UserRole.Wicedyrektor,
                IsActive = true,
                SupervisedSchoolTypes = new List<SchoolType>()
            };

            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(viceDirectorUserId))
                              .ReturnsAsync(viceDirector);

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.AssignViceDirectorToSchoolTypeAsync(viceDirectorUserId, schoolTypeId);

            // Assert
            result.Should().BeTrue();
            viceDirector.SupervisedSchoolTypes.Should().Contain(schoolType);

            _mockUserRepository.Verify(r => r.Update(viceDirector), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserAssignedToSchoolType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task AssignViceDirectorToSchoolTypeAsync_UserNotViceDirectorOrDirector_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var userId = "user-123";
            var schoolTypeId = "st-123";

            var user = new User
            {
                Id = userId,
                UPN = "teacher@example.com",
                Role = UserRole.Nauczyciel, // Nie wicedyrektor
                IsActive = true
            };

            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                              .ReturnsAsync(user);

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.AssignViceDirectorToSchoolTypeAsync(userId, schoolTypeId);

            // Assert
            result.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserAssignedToSchoolType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie ma uprawnień wicedyrektora lub dyrektora");

            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
        }

        [Theory]
        [InlineData(true, false)] // User not found
        [InlineData(false, true)] // SchoolType not found
        [InlineData(false, false)] // Both not found
        public async Task AssignViceDirectorToSchoolTypeAsync_UserOrSchoolTypeNotFound_ShouldReturnFalseAndLogFailed(bool userExists, bool schoolTypeExists)
        {
            // Arrange
            var userId = "user-123";
            var schoolTypeId = "st-123";

            var user = userExists ? new User { Id = userId, Role = UserRole.Wicedyrektor, IsActive = true } : null;
            var schoolType = schoolTypeExists ? new SchoolType { Id = schoolTypeId, IsActive = true } : null;

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                              .ReturnsAsync(user);

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.AssignViceDirectorToSchoolTypeAsync(userId, schoolTypeId);

            // Assert
            result.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserAssignedToSchoolType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);

            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task AssignViceDirectorToSchoolTypeAsync_AlreadyAssigned_ShouldReturnTrueAndLogNoActionOrWarning()
        {
            // Arrange
            var viceDirectorUserId = "user-123";
            var schoolTypeId = "st-123";

            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                IsActive = true
            };

            var viceDirector = new User
            {
                Id = viceDirectorUserId,
                UPN = "vicedirector@example.com",
                Role = UserRole.Wicedyrektor,
                IsActive = true,
                SupervisedSchoolTypes = new List<SchoolType> { schoolType } // Już przypisany
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(viceDirectorUserId))
                              .ReturnsAsync(viceDirector);

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.AssignViceDirectorToSchoolTypeAsync(viceDirectorUserId, schoolTypeId);

            // Assert
            result.Should().BeTrue();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserAssignedToSchoolType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.OperationDetails.Should().Contain("był już przypisany");

            // Nie powinno być żadnych zmian w kolekcji
            viceDirector.SupervisedSchoolTypes.Should().HaveCount(1);
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task RemoveViceDirectorFromSchoolTypeAsync_ExistingAssignment_ShouldRemoveAndReturnTrueAndLog()
        {
            // Arrange
            var viceDirectorUserId = "user-123";
            var schoolTypeId = "st-123";

            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                IsActive = true
            };

            var viceDirector = new User
            {
                Id = viceDirectorUserId,
                UPN = "vicedirector@example.com",
                Role = UserRole.Wicedyrektor,
                IsActive = true,
                SupervisedSchoolTypes = new List<SchoolType> { schoolType }
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(viceDirectorUserId))
                              .ReturnsAsync(viceDirector);

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.RemoveViceDirectorFromSchoolTypeAsync(viceDirectorUserId, schoolTypeId);

            // Assert
            result.Should().BeTrue();
            viceDirector.SupervisedSchoolTypes.Should().NotContain(schoolType);

            _mockUserRepository.Verify(r => r.Update(viceDirector), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserRemovedFromSchoolType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task RemoveViceDirectorFromSchoolTypeAsync_AssignmentNotFound_ShouldReturnTrueAndLogNoAction()
        {
            // Arrange
            var viceDirectorUserId = "user-123";
            var schoolTypeId = "st-123";

            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                IsActive = true
            };

            var viceDirector = new User
            {
                Id = viceDirectorUserId,
                UPN = "vicedirector@example.com",
                Role = UserRole.Wicedyrektor,
                IsActive = true,
                SupervisedSchoolTypes = new List<SchoolType>() // Brak przypisania
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(viceDirectorUserId))
                              .ReturnsAsync(viceDirector);

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.RemoveViceDirectorFromSchoolTypeAsync(viceDirectorUserId, schoolTypeId);

            // Assert
            result.Should().BeTrue(); // Operacja "udana" bo nie było czego usuwać

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserRemovedFromSchoolType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.OperationDetails.Should().Contain("nie nadzorował");

            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task RemoveViceDirectorFromSchoolTypeAsync_UserOrSchoolTypeNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var userId = "user-123";
            var schoolTypeId = "st-123";

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                              .ReturnsAsync((User?)null);

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                    .ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _schoolTypeService.RemoveViceDirectorFromSchoolTypeAsync(userId, schoolTypeId);

            // Assert
            result.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserRemovedFromSchoolType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie istnieje");

            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
        }
    }
} 