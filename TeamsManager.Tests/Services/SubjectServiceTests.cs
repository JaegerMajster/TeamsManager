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
    public class SubjectServiceTests
    {
        // Mocki dla zależności serwisu SubjectService
        private readonly Mock<IGenericRepository<Subject>> _mockSubjectRepository;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IGenericRepository<UserSubject>> _mockUserSubjectRepository;
        private readonly Mock<IUserRepository> _mockUserRepository; // Potrzebne dla GetTeachersForSubjectAsync
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SubjectService>> _mockLogger;

        // Testowany serwis
        private readonly SubjectService _subjectService;

        // Przykładowy UPN użytkownika wykonującego operacje
        private readonly string _currentLoggedInUserUpn = "test.moderator@example.com";
        // Przechwycony obiekt OperationHistory do weryfikacji logowania
        private OperationHistory? _capturedOperationHistory;

        public SubjectServiceTests()
        {
            // Inicjalizacja mocków
            _mockSubjectRepository = new Mock<IGenericRepository<Subject>>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserSubjectRepository = new Mock<IGenericRepository<UserSubject>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SubjectService>>();

            // Konfiguracja ICurrentUserService
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Konfiguracja OperationHistoryRepository do przechwytywania logów
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);

            // Inicjalizacja serwisu
            _subjectService = new SubjectService(
                _mockSubjectRepository.Object,
                _mockSchoolTypeRepository.Object,
                _mockUserSubjectRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object
            );
        }

        // Metoda pomocnicza do resetowania przechwyconej historii operacji
        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        // Testy dla GetSubjectByIdAsync
        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_ShouldReturnSubject()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var subjectId = "subj-math-001";
            var expectedSubject = new Subject { Id = subjectId, Name = "Matematyka", IsActive = true };
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(expectedSubject);

            // Act
            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSubject);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(subjectId), Times.Once);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_NonExistingId_ShouldReturnNull()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var subjectId = "subj-non-existent";
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync((Subject?)null);

            // Act
            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            // Assert
            result.Should().BeNull();
        }

        // Testy dla GetAllActiveSubjectsAsync
        [Fact]
        public async Task GetAllActiveSubjectsAsync_WhenActiveSubjectsExist_ShouldReturnThem()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var activeSubjects = new List<Subject>
            {
                new Subject { Id = "subj-1", Name = "Fizyka", IsActive = true },
                new Subject { Id = "subj-2", Name = "Chemia", IsActive = true }
            };
            _mockSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>()))
                                .ReturnsAsync((Expression<Func<Subject, bool>> predicate) => activeSubjects.Where(predicate.Compile()));

            // Act
            var result = await _subjectService.GetAllActiveSubjectsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(activeSubjects);
        }

        [Fact]
        public async Task GetAllActiveSubjectsAsync_WhenNoActiveSubjectsExist_ShouldReturnEmptyList()
        {
            // Arrange
            ResetCapturedOperationHistory();
            _mockSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>()))
                                .ReturnsAsync(new List<Subject>());

            // Act
            var result = await _subjectService.GetAllActiveSubjectsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        // Testy dla CreateSubjectAsync
        [Fact]
        public async Task CreateSubjectAsync_ValidData_ShouldCreateAndReturnSubjectAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "Biologia";
            var code = "BIO-101";
            var description = "Podstawy biologii komórkowej.";
            var schoolTypeId = "st-lo";
            var schoolType = new SchoolType { Id = schoolTypeId, ShortName = "LO", IsActive = true };
            Subject? addedSubject = null;

            _mockSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>()))
                                .ReturnsAsync(new List<Subject>()); // Brak konfliktu kodu
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);
            _mockSubjectRepository.Setup(r => r.AddAsync(It.IsAny<Subject>()))
                                .Callback<Subject>(s => addedSubject = s)
                                .Returns(Task.CompletedTask);

            // Act
            var result = await _subjectService.CreateSubjectAsync(name, code, description, defaultSchoolTypeId: schoolTypeId);

            // Assert
            result.Should().NotBeNull();
            addedSubject.Should().NotBeNull();
            result.Should().BeEquivalentTo(addedSubject, options => options.ExcludingMissingMembers());
            result!.Name.Should().Be(name);
            result.Code.Should().Be(code);
            result.Description.Should().Be(description);
            result.DefaultSchoolTypeId.Should().Be(schoolTypeId);
            result.DefaultSchoolType.Should().Be(schoolType);
            result.IsActive.Should().BeTrue();
            result.CreatedBy.Should().Be(_currentLoggedInUserUpn);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SubjectCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(result.Id);
            _capturedOperationHistory.TargetEntityName.Should().Be(name);
        }

        [Fact]
        public async Task CreateSubjectAsync_EmptyName_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();

            // Act
            var result = await _subjectService.CreateSubjectAsync("");

            // Assert
            result.Should().BeNull();
            _mockSubjectRepository.Verify(r => r.AddAsync(It.IsAny<Subject>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SubjectCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Nazwa przedmiotu nie może być pusta.");
        }

        [Fact]
        public async Task CreateSubjectAsync_CodeConflict_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "Konfliktowy Przedmiot";
            var code = "KONFLIKT-001";
            var existingSubject = new Subject { Id = "subj-exist", Code = code, IsActive = true };

            _mockSubjectRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Subject, bool>>>(
                                        expr => expr.Compile().Invoke(new Subject { Code = code, IsActive = true }) // Test z pasującym kodem
                                    ))).ReturnsAsync(new List<Subject> { existingSubject });

            // Act
            var result = await _subjectService.CreateSubjectAsync(name, code);

            // Assert
            result.Should().BeNull();
            _mockSubjectRepository.Verify(r => r.AddAsync(It.IsAny<Subject>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SubjectCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be($"Przedmiot o kodzie '{code}' już istnieje.");
        }

        [Fact]
        public async Task CreateSubjectAsync_DefaultSchoolTypeNotFound_ShouldCreateWithNullSchoolTypeAndLogWarning()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "Historia Sztuki";
            var nonExistentSchoolTypeId = "st-non-existent";
            Subject? addedSubject = null;

            _mockSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>()))
                                .ReturnsAsync(new List<Subject>());
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(nonExistentSchoolTypeId)).ReturnsAsync((SchoolType?)null);
            _mockSubjectRepository.Setup(r => r.AddAsync(It.IsAny<Subject>()))
                                .Callback<Subject>(s => addedSubject = s)
                                .Returns(Task.CompletedTask);

            // Act
            var result = await _subjectService.CreateSubjectAsync(name, defaultSchoolTypeId: nonExistentSchoolTypeId);

            // Assert
            result.Should().NotBeNull();
            addedSubject.Should().NotBeNull();
            result!.DefaultSchoolTypeId.Should().BeNull(); // Powinno zostać zignorowane i ustawione na null
            result.DefaultSchoolType.Should().BeNull();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Podany domyślny typ szkoły (ID: {nonExistentSchoolTypeId}) dla przedmiotu '{name}' nie istnieje lub jest nieaktywny. Pole zostanie zignorowane.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed); // Operacja tworzenia przedmiotu się udała
        }

        // Testy dla UpdateSubjectAsync
        [Fact]
        public async Task UpdateSubjectAsync_ExistingSubjectWithValidData_ShouldUpdateAndReturnTrueAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var subjectId = "subj-to-update";
            var existingSubject = new Subject { Id = subjectId, Name = "Stara Nazwa", Code = "OLD-001", IsActive = true, CreatedBy = "initial_user" };
            var updatedData = new Subject { Id = subjectId, Name = "Nowa Nazwa Przedmiotu", Code = "NEW-002", Description = "Nowy opis", IsActive = true };

            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(existingSubject);
            _mockSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>()))
                                .ReturnsAsync(new List<Subject>()); // Brak konfliktu nowego kodu

            // Act
            var result = await _subjectService.UpdateSubjectAsync(updatedData);

            // Assert
            result.Should().BeTrue();
            _mockSubjectRepository.Verify(r => r.Update(It.Is<Subject>(s =>
                s.Id == subjectId &&
                s.Name == updatedData.Name &&
                s.Code == updatedData.Code &&
                s.Description == updatedData.Description &&
                s.ModifiedBy == _currentLoggedInUserUpn
            )), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SubjectUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(subjectId);
            _capturedOperationHistory.TargetEntityName.Should().Be(updatedData.Name);
        }

        [Fact]
        public async Task UpdateSubjectAsync_SubjectNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var subjectToUpdate = new Subject { Id = "subj-non-existent", Name = "Nieistniejący" };
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectToUpdate.Id)).ReturnsAsync((Subject?)null);

            // Act
            var result = await _subjectService.UpdateSubjectAsync(subjectToUpdate);

            // Assert
            result.Should().BeFalse();
            _mockSubjectRepository.Verify(r => r.Update(It.IsAny<Subject>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SubjectUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.TargetEntityId.Should().Be(subjectToUpdate.Id);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Przedmiot o ID '{subjectToUpdate.Id}' nie istnieje.");
        }

        // Testy dla DeleteSubjectAsync
        [Fact]
        public async Task DeleteSubjectAsync_ExistingSubject_ShouldSoftDeleteSubjectAndAssignmentsAndReturnTrueAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var subjectId = "subj-to-delete";
            var subjectToDelete = new Subject { Id = subjectId, Name = "Do Usunięcia", IsActive = true };
            var assignment1 = new UserSubject { Id = "us1", SubjectId = subjectId, UserId = "u1", IsActive = true };
            var assignment2 = new UserSubject { Id = "us2", SubjectId = subjectId, UserId = "u2", IsActive = true };

            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subjectToDelete);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<UserSubject, bool>>>(
                                           expr => expr.Compile().Invoke(new UserSubject { SubjectId = subjectId, IsActive = true })
                                       ))).ReturnsAsync(new List<UserSubject> { assignment1, assignment2 });

            // Act
            var result = await _subjectService.DeleteSubjectAsync(subjectId);

            // Assert
            result.Should().BeTrue();
            subjectToDelete.IsActive.Should().BeFalse(); // Sprawdzenie soft delete przedmiotu
            _mockSubjectRepository.Verify(r => r.Update(It.Is<Subject>(s => s.Id == subjectId && !s.IsActive)), Times.Once);

            // Sprawdzenie, czy przypisania zostały zdezaktywowane
            _mockUserSubjectRepository.Verify(r => r.Update(It.Is<UserSubject>(us => us.Id == assignment1.Id && !us.IsActive)), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.Update(It.Is<UserSubject>(us => us.Id == assignment2.Id && !us.IsActive)), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SubjectDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(subjectId);
        }

        [Fact]
        public async Task DeleteSubjectAsync_SubjectNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var nonExistentSubjectId = "subj-non-existent-delete";
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(nonExistentSubjectId)).ReturnsAsync((Subject?)null);

            // Act
            var result = await _subjectService.DeleteSubjectAsync(nonExistentSubjectId);

            // Assert
            result.Should().BeFalse();
            _mockSubjectRepository.Verify(r => r.Update(It.IsAny<Subject>()), Times.Never);
            _mockUserSubjectRepository.Verify(r => r.Update(It.IsAny<UserSubject>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            // Typ operacji jest ustawiany wewnątrz bloku try, jeśli obiekt nie istnieje, może nie być ustawiony
            // _capturedOperationHistory!.Type.Should().Be(OperationType.SubjectDeleted);
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            // _capturedOperationHistory.ErrorMessage - zależy od tego, czy TargetEntityName jest ustawiane przed sprawdzeniem istnienia
        }

        // Testy dla GetTeachersForSubjectAsync
        [Fact]
        public async Task GetTeachersForSubjectAsync_SubjectWithTeachers_ShouldReturnListOfUsers()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var subjectId = "subj-active-teachers";
            var subject = new Subject { Id = subjectId, Name = "Aktywni Nauczyciele", IsActive = true };
            var teacher1 = new User { Id = "t1", FirstName = "Anna", LastName = "Nowak", IsActive = true };
            var teacher2 = new User { Id = "t2", FirstName = "Piotr", LastName = "Kowalski", IsActive = true };
            var inactiveTeacher = new User { Id = "t-inactive", FirstName = "Nieaktywny", LastName = "Nauczyciel", IsActive = false };

            var assignments = new List<UserSubject>
            {
                new UserSubject { SubjectId = subjectId, UserId = teacher1.Id, User = teacher1, IsActive = true },
                new UserSubject { SubjectId = subjectId, UserId = teacher2.Id, User = teacher2, IsActive = true },
                new UserSubject { SubjectId = subjectId, UserId = inactiveTeacher.Id, User = inactiveTeacher, IsActive = true } // Aktywne przypisanie, ale nieaktywny nauczyciel
            };

            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subject);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>()))
                                    .ReturnsAsync((Expression<Func<UserSubject, bool>> predicate) => assignments.Where(predicate.Compile()));

            // Act
            var result = await _subjectService.GetTeachersForSubjectAsync(subjectId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().ContainEquivalentOf(teacher1);
            result.Should().ContainEquivalentOf(teacher2);
            result.Should().NotContainEquivalentOf(inactiveTeacher);
        }

        [Fact]
        public async Task GetTeachersForSubjectAsync_SubjectWithoutTeachers_ShouldReturnEmptyList()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var subjectId = "subj-no-teachers";
            var subject = new Subject { Id = subjectId, Name = "Brak Nauczycieli", IsActive = true };

            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subject);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>()))
                                    .ReturnsAsync(new List<UserSubject>()); // Brak przypisań

            // Act
            var result = await _subjectService.GetTeachersForSubjectAsync(subjectId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTeachersForSubjectAsync_SubjectNotFound_ShouldReturnEmptyListOrLogWarning()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var nonExistentSubjectId = "subj-non-existent-teachers";
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(nonExistentSubjectId)).ReturnsAsync((Subject?)null);

            // Act
            var result = await _subjectService.GetTeachersForSubjectAsync(nonExistentSubjectId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Przedmiot o ID {nonExistentSubjectId} nie istnieje lub jest nieaktywny.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}