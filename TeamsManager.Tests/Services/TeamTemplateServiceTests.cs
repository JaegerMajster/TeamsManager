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
    public class TeamTemplateServiceTests
    {
        // Mocki dla zależności serwisu
        private readonly Mock<ITeamTemplateRepository> _mockTeamTemplateRepository;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<TeamTemplateService>> _mockLogger;

        // Testowany serwis
        private readonly ITeamTemplateService _teamTemplateService;

        // Przykładowy UPN użytkownika wykonującego operacje
        private readonly string _currentLoggedInUserUpn = "test.admin@example.com";
        // Przechwycony obiekt OperationHistory do weryfikacji logowania
        private OperationHistory? _capturedOperationHistory;

        public TeamTemplateServiceTests()
        {
            // Inicjalizacja mocków
            _mockTeamTemplateRepository = new Mock<ITeamTemplateRepository>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<TeamTemplateService>>();

            // Konfiguracja ICurrentUserService
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Konfiguracja OperationHistoryRepository do przechwytywania logów
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);

            // Inicjalizacja serwisu
            _teamTemplateService = new TeamTemplateService(
                _mockTeamTemplateRepository.Object,
                _mockSchoolTypeRepository.Object,
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

        // Metody pomocnicze do tworzenia obiektów testowych
        private SchoolType CreateTestSchoolType(string id = "st-1", string shortName = "LO", bool isActive = true)
        {
            return new SchoolType { Id = id, ShortName = shortName, FullName = "Liceum Ogólnokształcące", IsActive = isActive };
        }

        private TeamTemplate CreateTestTemplate(
            string id = "tpl-1",
            string name = "Test Template",
            bool isActive = true,
            bool isUniversal = true,
            string? schoolTypeId = null,
            SchoolType? schoolType = null,
            bool isDefault = false,
            string templateContent = "{Placeholder}")
        {
            return new TeamTemplate
            {
                Id = id,
                Name = name,
                Template = templateContent,
                Description = "Test Description",
                IsActive = isActive,
                IsUniversal = isUniversal,
                SchoolTypeId = schoolTypeId,
                SchoolType = schoolType,
                IsDefault = isDefault,
                CreatedBy = "test_setup"
            };
        }

        // Testy dla GetTemplateByIdAsync
        [Fact]
        public async Task GetTemplateByIdAsync_ExistingActiveTemplate_ShouldReturnTemplate()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-exists";
            var expectedTemplate = CreateTestTemplate(templateId);
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(expectedTemplate);

            // Act
            var result = await _teamTemplateService.GetTemplateByIdAsync(templateId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedTemplate);
            _mockTeamTemplateRepository.Verify(r => r.GetByIdAsync(templateId), Times.Once);
        }

        [Fact]
        public async Task GetTemplateByIdAsync_ExistingActiveTemplateWithSchoolTypeNotLoaded_ShouldLoadSchoolType()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-sch-type";
            var schoolTypeId = "st-for-tpl";
            var schoolType = CreateTestSchoolType(schoolTypeId);
            var templateWithoutSchoolTypeLoaded = CreateTestTemplate(templateId, isUniversal: false, schoolTypeId: schoolTypeId, schoolType: null);

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(templateWithoutSchoolTypeLoaded);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);

            // Act
            var result = await _teamTemplateService.GetTemplateByIdAsync(templateId);

            // Assert
            result.Should().NotBeNull();
            result!.SchoolType.Should().NotBeNull();
            result.SchoolType.Should().BeEquivalentTo(schoolType);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
        }


        [Fact]
        public async Task GetTemplateByIdAsync_NonExistingTemplate_ShouldReturnNull()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-non-existent";
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _teamTemplateService.GetTemplateByIdAsync(templateId);

            // Assert
            result.Should().BeNull();
        }

        // Testy dla GetAllActiveTemplatesAsync
        [Fact]
        public async Task GetAllActiveTemplatesAsync_WhenActiveTemplatesExist_ShouldReturnThem()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var activeTemplates = new List<TeamTemplate>
            {
                CreateTestTemplate("tpl-a1", "Active Template 1"),
                CreateTestTemplate("tpl-a2", "Active Template 2")
            };
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                    .ReturnsAsync(activeTemplates);

            // Act
            var result = await _teamTemplateService.GetAllActiveTemplatesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(activeTemplates);
        }

        [Fact]
        public async Task GetAllActiveTemplatesAsync_WhenNoActiveTemplatesExist_ShouldReturnEmptyList()
        {
            // Arrange
            ResetCapturedOperationHistory();
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                    .ReturnsAsync(new List<TeamTemplate>());

            // Act
            var result = await _teamTemplateService.GetAllActiveTemplatesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        // Testy dla GetUniversalTemplatesAsync
        [Fact]
        public async Task GetUniversalTemplatesAsync_WhenUniversalTemplatesExist_ShouldReturnThem()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var universalTemplates = new List<TeamTemplate> { CreateTestTemplate(isUniversal: true) };
            _mockTeamTemplateRepository.Setup(r => r.GetUniversalTemplatesAsync()).ReturnsAsync(universalTemplates);

            // Act
            var result = await _teamTemplateService.GetUniversalTemplatesAsync();

            // Assert
            result.Should().NotBeNull().And.ContainSingle().Which.IsUniversal.Should().BeTrue();
        }

        // Testy dla GetTemplatesBySchoolTypeAsync
        [Fact]
        public async Task GetTemplatesBySchoolTypeAsync_WhenTemplatesForSchoolTypeExist_ShouldReturnThem()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolTypeId = "st-specific";
            var specificTemplates = new List<TeamTemplate> { CreateTestTemplate(schoolTypeId: schoolTypeId, isUniversal: false) };
            _mockTeamTemplateRepository.Setup(r => r.GetTemplatesBySchoolTypeAsync(schoolTypeId)).ReturnsAsync(specificTemplates);

            // Act
            var result = await _teamTemplateService.GetTemplatesBySchoolTypeAsync(schoolTypeId);

            // Assert
            result.Should().NotBeNull().And.ContainSingle().Which.SchoolTypeId.Should().Be(schoolTypeId);
        }

        // Testy dla GetDefaultTemplateForSchoolTypeAsync
        [Fact]
        public async Task GetDefaultTemplateForSchoolTypeAsync_WhenDefaultExists_ShouldReturnIt()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolTypeId = "st-with-default";
            var defaultTemplate = CreateTestTemplate(schoolTypeId: schoolTypeId, isUniversal: false, isDefault: true);
            _mockTeamTemplateRepository.Setup(r => r.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId)).ReturnsAsync(defaultTemplate);

            // Act
            var result = await _teamTemplateService.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId);

            // Assert
            result.Should().NotBeNull().And.Match<TeamTemplate>(t => t.IsDefault && t.SchoolTypeId == schoolTypeId);
        }


        // Testy dla CreateTemplateAsync
        [Fact]
        public async Task CreateTemplateAsync_ValidUniversalTemplate_ShouldCreateAndReturnTemplateAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "Uniwersalny Szablon";
            var content = "{Projekt}";
            TeamTemplate? addedTemplate = null;
            _mockTeamTemplateRepository.Setup(r => r.AddAsync(It.IsAny<TeamTemplate>()))
                                    .Callback<TeamTemplate>(t => addedTemplate = t)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _teamTemplateService.CreateTemplateAsync(name, content, "Opis", isUniversal: true);

            // Assert
            result.Should().NotBeNull();
            addedTemplate.Should().NotBeNull();
            result!.Name.Should().Be(name);
            result.Template.Should().Be(content);
            result.IsUniversal.Should().BeTrue();
            result.SchoolTypeId.Should().BeNull();
            result.IsDefault.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityName.Should().Be(name);
        }

        [Fact]
        public async Task CreateTemplateAsync_ValidSchoolTypeSpecificTemplate_ShouldCreateAndReturnTemplateAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "Szablon dla LO";
            var content = "{Klasa} - {Nauczyciel}";
            var schoolTypeId = "st-lo";
            var schoolType = CreateTestSchoolType(schoolTypeId);
            TeamTemplate? addedTemplate = null;

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);
            _mockTeamTemplateRepository.Setup(r => r.AddAsync(It.IsAny<TeamTemplate>()))
                                    .Callback<TeamTemplate>(t => addedTemplate = t)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _teamTemplateService.CreateTemplateAsync(name, content, "Opis", isUniversal: false, schoolTypeId: schoolTypeId);

            // Assert
            result.Should().NotBeNull();
            addedTemplate.Should().NotBeNull();
            result!.Name.Should().Be(name);
            result.IsUniversal.Should().BeFalse();
            result.SchoolTypeId.Should().Be(schoolTypeId);
            result.SchoolType.Should().Be(schoolType);
            result.IsDefault.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
        }

        [Theory]
        [InlineData("", "{Content}", "Nazwa szablonu i jego zawartość (wzorzec) są wymagane.")]
        [InlineData("Nazwa", " ", "Nazwa szablonu i jego zawartość (wzorzec) są wymagane.")]
        public async Task CreateTemplateAsync_EmptyNameOrContent_ShouldReturnNullAndLogFailed(string name, string content, string expectedError)
        {
            // Arrange
            ResetCapturedOperationHistory();

            // Act
            var result = await _teamTemplateService.CreateTemplateAsync(name, content, "Opis", true);

            // Assert
            result.Should().BeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be(expectedError);
        }

        [Fact]
        public async Task CreateTemplateAsync_NonUniversalWithoutSchoolType_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();

            // Act
            var result = await _teamTemplateService.CreateTemplateAsync("Nazwa", "{Content}", "Opis", isUniversal: false, schoolTypeId: null);

            // Assert
            result.Should().BeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Dla szablonu nieuniwersalnego wymagany jest typ szkoły (SchoolTypeId).");
        }

        [Fact]
        public async Task CreateTemplateAsync_InvalidSchoolType_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolTypeId = "st-invalid";
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _teamTemplateService.CreateTemplateAsync("Nazwa", "{Content}", "Opis", isUniversal: false, schoolTypeId: schoolTypeId);

            // Assert
            result.Should().BeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Typ szkoły o ID '{schoolTypeId}' podany dla szablonu nie istnieje lub jest nieaktywny.");
        }

        [Fact]
        public async Task CreateTemplateAsync_InvalidTemplateSyntax_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var invalidContent = "Szablon z {NiezamknietymPlaceholder";

            // Act
            var result = await _teamTemplateService.CreateTemplateAsync("Błędny Szablon", invalidContent, "Opis", true);

            // Assert
            result.Should().BeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("Błędy walidacji szablonu:");
            _capturedOperationHistory.ErrorMessage.Should().Contain("Niezrównoważone nawiasy klamrowe w szablonie.");
        }

        // Testy dla UpdateTemplateAsync
        [Fact]
        public async Task UpdateTemplateAsync_ExistingTemplateWithValidData_ShouldUpdateAndReturnTrueAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-update";
            var existingTemplate = CreateTestTemplate(templateId, "Stara Nazwa");
            var updatedTemplateData = CreateTestTemplate(templateId, "Nowa Nazwa", schoolTypeId: "st-new", isUniversal: false); // isDefault jest false
            var schoolTypeForUpdate = CreateTestSchoolType("st-new");

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(existingTemplate);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync("st-new")).ReturnsAsync(schoolTypeForUpdate);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                    .ReturnsAsync(new List<TeamTemplate>());


            // Act
            var result = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);

            // Assert
            result.Should().BeTrue();
            _mockTeamTemplateRepository.Verify(r => r.Update(It.Is<TeamTemplate>(t =>
                t.Id == templateId &&
                t.Name == "Nowa Nazwa" &&
                t.SchoolTypeId == "st-new" &&
                !t.IsUniversal &&
                !t.IsDefault &&
                t.ModifiedBy == _currentLoggedInUserUpn
            )), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task UpdateTemplateAsync_TemplateNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateToUpdate = CreateTestTemplate("tpl-non-existent");
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateToUpdate.Id)).ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _teamTemplateService.UpdateTemplateAsync(templateToUpdate);

            // Assert
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Szablon nie istnieje.");
        }

        [Fact]
        public async Task UpdateTemplateAsync_SetAsDefault_WhenAnotherDefaultExists_ShouldUnsetOldDefaultAndSetNewDefault()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolTypeId = "st-multi-default";
            var schoolType = CreateTestSchoolType(schoolTypeId);

            var oldDefaultTemplate = CreateTestTemplate("tpl-old-default", "Stary Domyślny", schoolTypeId: schoolTypeId, schoolType: schoolType, isUniversal: false, isDefault: true);
            var templateToSetAsDefault = CreateTestTemplate("tpl-new-default", "Nowy Domyślny", schoolTypeId: schoolTypeId, schoolType: schoolType, isUniversal: false, isDefault: false);

            var updatedTemplateData = CreateTestTemplate(templateToSetAsDefault.Id, templateToSetAsDefault.Name, schoolTypeId: schoolTypeId, schoolType: schoolType, isUniversal: false, isDefault: true);

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateToSetAsDefault.Id)).ReturnsAsync(templateToSetAsDefault);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);

            Expression<Func<TeamTemplate, bool>>? capturedPredicate = null;
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                .Callback<Expression<Func<TeamTemplate, bool>>>(p => capturedPredicate = p)
                .ReturnsAsync((Expression<Func<TeamTemplate, bool>> predicate) =>
                    new List<TeamTemplate> { oldDefaultTemplate }.Where(predicate.Compile()).ToList());


            // Act
            var result = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);

            // Assert
            result.Should().BeTrue();
            oldDefaultTemplate.IsDefault.Should().BeFalse();
            templateToSetAsDefault.IsDefault.Should().BeTrue();

            _mockTeamTemplateRepository.Verify(r => r.Update(oldDefaultTemplate), Times.Once);
            _mockTeamTemplateRepository.Verify(r => r.Update(templateToSetAsDefault), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);

            // Bardziej odporna weryfikacja predykatu
            _mockTeamTemplateRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()), Times.Once);
            capturedPredicate.Should().NotBeNull();
            var compiledPredicate = capturedPredicate!.Compile();
            // Testowanie logiki predykatu
            compiledPredicate(new TeamTemplate { SchoolTypeId = schoolTypeId, IsDefault = true, Id = "other-id", IsActive = true }).Should().BeTrue(); // Powinien pasować
            compiledPredicate(new TeamTemplate { SchoolTypeId = schoolTypeId, IsDefault = true, Id = templateToSetAsDefault.Id, IsActive = true }).Should().BeFalse(); // Nie powinien pasować (Id != templateToUpdate.Id)
            compiledPredicate(new TeamTemplate { SchoolTypeId = "another-st", IsDefault = true, Id = "other-id", IsActive = true }).Should().BeFalse(); // Inny SchoolTypeId
            compiledPredicate(new TeamTemplate { SchoolTypeId = schoolTypeId, IsDefault = false, Id = "other-id", IsActive = true }).Should().BeFalse(); // IsDefault = false
            compiledPredicate(new TeamTemplate { SchoolTypeId = schoolTypeId, IsDefault = true, Id = "other-id", IsActive = false }).Should().BeFalse(); // IsActive = false
        }

        [Fact]
        public async Task UpdateTemplateAsync_SetAsDefault_WhenNoOtherDefaultExists_ShouldSetNewDefault()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolTypeId = "st-single-default";
            var schoolType = CreateTestSchoolType(schoolTypeId);
            var templateToSetAsDefault = CreateTestTemplate("tpl-sole-default", "Jedyny Domyślny", schoolTypeId: schoolTypeId, schoolType: schoolType, isUniversal: false, isDefault: false);

            var updatedTemplateData = CreateTestTemplate(templateToSetAsDefault.Id, templateToSetAsDefault.Name, schoolTypeId: schoolTypeId, schoolType: schoolType, isUniversal: false, isDefault: true);

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateToSetAsDefault.Id)).ReturnsAsync(templateToSetAsDefault);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);

            Expression<Func<TeamTemplate, bool>>? capturedPredicate = null;
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                    .Callback<Expression<Func<TeamTemplate, bool>>>(p => capturedPredicate = p)
                                    .ReturnsAsync(new List<TeamTemplate>()); // Symulacja, że nie ma innych domyślnych

            // Act
            var result = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);

            // Assert
            result.Should().BeTrue();
            templateToSetAsDefault.IsDefault.Should().BeTrue();
            _mockTeamTemplateRepository.Verify(r => r.Update(templateToSetAsDefault), Times.Once);

            _mockTeamTemplateRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()), Times.Once);
            capturedPredicate.Should().NotBeNull();
            var compiledPredicate = capturedPredicate!.Compile();
            // Testowanie logiki predykatu - powinien zwracać false dla każdego szablonu, bo lista jest pusta
            compiledPredicate(new TeamTemplate { SchoolTypeId = schoolTypeId, IsDefault = true, Id = "any-other-id", IsActive = true }).Should().BeTrue(); // Predykat szuka pasujących, ale lista wynikowa będzie pusta

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
        }


        // Testy dla DeleteTemplateAsync
        [Fact]
        public async Task DeleteTemplateAsync_ExistingTemplate_ShouldSoftDeleteAndReturnTrueAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-to-delete";
            var templateToDelete = CreateTestTemplate(templateId);
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(templateToDelete);

            // Act
            var result = await _teamTemplateService.DeleteTemplateAsync(templateId);

            // Assert
            result.Should().BeTrue();
            templateToDelete.IsActive.Should().BeFalse();
            _mockTeamTemplateRepository.Verify(r => r.Update(It.Is<TeamTemplate>(t => t.Id == templateId && !t.IsActive)), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task DeleteTemplateAsync_TemplateNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-non-existent-delete";
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _teamTemplateService.DeleteTemplateAsync(templateId);

            // Assert
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be($"Szablon o ID '{templateId}' nie istnieje.");
        }

        [Fact]
        public async Task DeleteTemplateAsync_TemplateAlreadyInactive_ShouldDoNothingAndReturnTrueAndLogNoAction()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-already-inactive";
            var inactiveTemplate = CreateTestTemplate(templateId, isActive: false);
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(inactiveTemplate);

            // Act
            var result = await _teamTemplateService.DeleteTemplateAsync(templateId);

            // Assert
            result.Should().BeTrue();
            inactiveTemplate.IsActive.Should().BeFalse();
            _mockTeamTemplateRepository.Verify(r => r.Update(It.IsAny<TeamTemplate>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.OperationDetails.Should().Contain("był już nieaktywny");
        }


        // Testy dla GenerateTeamNameFromTemplateAsync
        [Fact]
        public async Task GenerateTeamNameFromTemplateAsync_ValidTemplateAndValues_ShouldReturnGeneratedNameAndIncrementUsage()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-gen";
            var template = CreateTestTemplate(templateId, templateContent: "Kurs: {Kurs} - {Rok}");
            template.UsageCount = 5;
            var values = new Dictionary<string, string> { { "Kurs", "Programowanie" }, { "Rok", "2025" } };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(template);

            // Act
            var resultName = await _teamTemplateService.GenerateTeamNameFromTemplateAsync(templateId, values);

            // Assert
            resultName.Should().Be("Kurs: Programowanie - 2025");
            template.UsageCount.Should().Be(6);
            template.LastUsedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GenerateTeamNameFromTemplateAsync_TemplateNotFoundOrInactive_ShouldReturnNull()
        {
            // Arrange
            ResetCapturedOperationHistory();
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync("tpl-non-exist")).ReturnsAsync((TeamTemplate?)null);
            var inactiveTemplate = CreateTestTemplate("tpl-inactive", isActive: false);
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync("tpl-inactive")).ReturnsAsync(inactiveTemplate);

            // Act
            var result1 = await _teamTemplateService.GenerateTeamNameFromTemplateAsync("tpl-non-exist", new Dictionary<string, string>());
            var result2 = await _teamTemplateService.GenerateTeamNameFromTemplateAsync("tpl-inactive", new Dictionary<string, string>());

            // Assert
            result1.Should().BeNull();
            result2.Should().BeNull();
        }

        [Fact]
        public async Task GenerateTeamNameFromTemplateAsync_MissingRequiredPlaceholders_ShouldUseDefaultsInName()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var templateId = "tpl-missing-placeholders";
            var template = CreateTestTemplate(templateId, templateContent: "Kurs: {Kurs} - Nauczyciel: {Nauczyciel}");
            var values = new Dictionary<string, string> { { "Kurs", "Zaawansowane Algorytmy" } }; // Brakuje "Nauczyciel"

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(template);

            // Act
            var resultName = await _teamTemplateService.GenerateTeamNameFromTemplateAsync(templateId, values);

            // Assert
            resultName.Should().Be("Kurs: Zaawansowane Algorytmy - Nauczyciel: [Nauczyciel]");
        }


        // Testy dla CloneTemplateAsync
        [Fact]
        public async Task CloneTemplateAsync_ExistingTemplate_ShouldCreateCloneAndReturnItAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var originalTemplateId = "tpl-original";
            var newName = "Sklonowany Szablon";
            var originalTemplate = CreateTestTemplate(originalTemplateId, "Oryginał", schoolTypeId: "st-1", isUniversal: false, isDefault: true);
            TeamTemplate? clonedTemplateCapture = null;

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(originalTemplateId)).ReturnsAsync(originalTemplate);
            _mockTeamTemplateRepository.Setup(r => r.AddAsync(It.IsAny<TeamTemplate>()))
                                    .Callback<TeamTemplate>(t => clonedTemplateCapture = t)
                                    .Returns(Task.CompletedTask);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<TeamTemplate, bool>>>(
                expr => expr.Compile().Invoke(new TeamTemplate { Name = newName, IsActive = true })
            ))).ReturnsAsync(new List<TeamTemplate>());


            // Act
            var result = await _teamTemplateService.CloneTemplateAsync(originalTemplateId, newName);

            // Assert
            result.Should().NotBeNull();
            clonedTemplateCapture.Should().NotBeNull();
            result.Should().BeEquivalentTo(clonedTemplateCapture, options => options.Excluding(t => t.Id).Excluding(t => t.CreatedDate).Excluding(t => t.CreatedBy));

            result!.Name.Should().Be(newName);
            result.IsDefault.Should().BeFalse();
            result.SchoolTypeId.Should().Be(originalTemplate.SchoolTypeId);
            result.Id.Should().NotBeNullOrEmpty().And.NotBe(originalTemplateId);
            result.CreatedBy.Should().Be(_currentLoggedInUserUpn);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateCloned);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(originalTemplateId);
            _capturedOperationHistory.OperationDetails.Should().Contain($"sklonowany do nowego szablonu ID: {result.Id}");
        }

        [Fact]
        public async Task CloneTemplateAsync_NewNameAlreadyExistsAndIsActive_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var originalTemplateId = "tpl-original-for-conflict";
            var newName = "Istniejąca Nazwa Aktywna";
            var originalTemplate = CreateTestTemplate(originalTemplateId, "Oryginał Do Konfliktu");
            var existingTemplateWithName = CreateTestTemplate("tpl-conflict", newName, isActive: true);

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(originalTemplateId)).ReturnsAsync(originalTemplate);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<TeamTemplate, bool>>>(
                expr => expr.Compile().Invoke(new TeamTemplate { Name = newName, IsActive = true })
            ))).ReturnsAsync(new List<TeamTemplate> { existingTemplateWithName });

            // Act
            var result = await _teamTemplateService.CloneTemplateAsync(originalTemplateId, newName);

            // Assert
            result.Should().BeNull();
            _mockTeamTemplateRepository.Verify(r => r.AddAsync(It.IsAny<TeamTemplate>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateCloned);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.TargetEntityId.Should().Be(originalTemplateId);
            _capturedOperationHistory.ErrorMessage.Should().Be($"Szablon o nazwie '{newName}' już istnieje i jest aktywny. Klonowanie przerwane.");
        }


        [Fact]
        public async Task CloneTemplateAsync_OriginalTemplateNotFound_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync("tpl-non-exist")).ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _teamTemplateService.CloneTemplateAsync("tpl-non-exist", "Nowa Nazwa");

            // Assert
            result.Should().BeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Oryginalny szablon nie istnieje.");
        }
    }
}
