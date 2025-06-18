using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Models
{
    /// <summary>
    /// Testy jednostkowe dla GraphUser i wszystkich powiązanych klas
    /// Pokrycie: GraphUser, GraphLicense, GraphServicePlan
    /// </summary>
    public class GraphUserTests
    {
        #region GraphUser Constructor Tests

        [Fact]
        public void GraphUser_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var user = new GraphUser();

            // Assert - podstawowe właściwości
            user.Id.Should().BeNull();
            user.GivenName.Should().BeNull();
            user.Surname.Should().BeNull();
            user.UserPrincipalName.Should().BeNull();
            user.Mail.Should().BeNull();
            user.MailNickname.Should().BeNull();
            user.UserType.Should().BeNull();
            user.AccountEnabled.Should().BeTrue();
            user.CreatedDateTime.Should().BeNull();
            user.LastSignInDateTime.Should().BeNull();

            // Assert - informacje organizacyjne
            user.JobTitle.Should().BeNull();
            user.Department.Should().BeNull();
            user.CompanyName.Should().BeNull();
            user.OfficeLocation.Should().BeNull();
            user.BusinessPhone.Should().BeNull();
            user.MobilePhone.Should().BeNull();
            user.FaxNumber.Should().BeNull();
            user.StreetAddress.Should().BeNull();
            user.City.Should().BeNull();
            user.State.Should().BeNull();
            user.PostalCode.Should().BeNull();
            user.Country.Should().BeNull();

            // Assert - hierarchia organizacyjna
            user.ManagerId.Should().BeNull();
            user.Manager.Should().BeNull();
            user.DirectReports.Should().NotBeNull().And.BeEmpty();

            // Assert - licencje
            user.AssignedLicenses.Should().NotBeNull().And.BeEmpty();
            user.ServicePlans.Should().NotBeNull().And.BeEmpty();
            user.LicenseType.Should().BeNull();

            // Assert - kolekcje
            user.BusinessPhones.Should().NotBeNull().And.BeEmpty();

            // Assert - inne
            user.DisplayName.Should().Be(string.Empty);
        }

        [Fact]
        public void GraphUser_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var user = new GraphUser();
            var createdDate = DateTime.UtcNow.AddDays(-90);
            var lastSignIn = DateTime.UtcNow.AddDays(-7);
            var manager = new GraphUser { Id = "manager-123", GivenName = "Anna", Surname = "Manager" };
            var license = new GraphLicense { SkuId = "lic-123", SkuPartNumber = "ENTERPRISEPACK" };

            // Act
            user.Id = "user-123";
            user.GivenName = "Jan";
            user.Surname = "Kowalski";
            user.UserPrincipalName = "jan.kowalski@school.edu";
            user.Mail = "jan.kowalski@school.edu";
            user.MailNickname = "jankowalski";
            user.UserType = "Member";
            user.AccountEnabled = false;
            user.CreatedDateTime = createdDate;
            user.LastSignInDateTime = lastSignIn;
            user.JobTitle = "Nauczyciel";
            user.Department = "Matematyka";
            user.CompanyName = "Zespół Szkół";
            user.OfficeLocation = "Sala 101";
            user.BusinessPhone = "+48123456789";
            user.MobilePhone = "+48987654321";
            user.FaxNumber = "+48111222333";
            user.StreetAddress = "ul. Szkolna 1";
            user.City = "Warszawa";
            user.State = "Mazowieckie";
            user.PostalCode = "00-001";
            user.Country = "Poland";
            user.ManagerId = "manager-123";
            user.Manager = manager;
            user.LicenseType = "E3";
            user.DisplayName = "Jan Kowalski";
            user.AssignedLicenses.Add(license);
            user.BusinessPhones.Add("+48123456789");

            // Assert
            user.Id.Should().Be("user-123");
            user.GivenName.Should().Be("Jan");
            user.Surname.Should().Be("Kowalski");
            user.UserPrincipalName.Should().Be("jan.kowalski@school.edu");
            user.Mail.Should().Be("jan.kowalski@school.edu");
            user.MailNickname.Should().Be("jankowalski");
            user.UserType.Should().Be("Member");
            user.AccountEnabled.Should().BeFalse();
            user.CreatedDateTime.Should().Be(createdDate);
            user.LastSignInDateTime.Should().Be(lastSignIn);
            user.JobTitle.Should().Be("Nauczyciel");
            user.Department.Should().Be("Matematyka");
            user.CompanyName.Should().Be("Zespół Szkół");
            user.OfficeLocation.Should().Be("Sala 101");
            user.BusinessPhone.Should().Be("+48123456789");
            user.MobilePhone.Should().Be("+48987654321");
            user.FaxNumber.Should().Be("+48111222333");
            user.StreetAddress.Should().Be("ul. Szkolna 1");
            user.City.Should().Be("Warszawa");
            user.State.Should().Be("Mazowieckie");
            user.PostalCode.Should().Be("00-001");
            user.Country.Should().Be("Poland");
            user.ManagerId.Should().Be("manager-123");
            user.Manager.Should().Be(manager);
            user.LicenseType.Should().Be("E3");
            user.DisplayName.Should().Be("Jan Kowalski");
            user.AssignedLicenses.Should().Contain(license);
            user.BusinessPhones.Should().Contain("+48123456789");
        }

        #endregion

        #region GraphUser Computed Properties Tests

        [Fact]
        public void GraphUser_ComputedProperties_ShouldReturnCorrectValues()
        {
            // Arrange
            var user = new GraphUser
            {
                GivenName = "Jan",
                Surname = "Kowalski",
                UserPrincipalName = "jan.kowalski@school.edu",
                AccountEnabled = true
            };

            // Act & Assert - właściwości kompatybilności
            user.FirstName.Should().Be("Jan");
            user.LastName.Should().Be("Kowalski");
            user.UPN.Should().Be("jan.kowalski@school.edu");
            user.IsActive.Should().BeTrue();
            user.FullName.Should().Be("Jan Kowalski");
        }

        [Fact]
        public void GraphUser_FullName_ShouldHandleNullValues()
        {
            // Arrange & Act
            var user1 = new GraphUser { GivenName = "Jan", Surname = null };
            var user2 = new GraphUser { GivenName = null, Surname = "Kowalski" };
            var user3 = new GraphUser { GivenName = null, Surname = null };
            var user4 = new GraphUser { GivenName = "  ", Surname = "  " };

            // Assert
            user1.FullName.Should().Be("Jan");
            user2.FullName.Should().Be("Kowalski");
            user3.FullName.Should().Be("");
            user4.FullName.Should().Be("");
        }

        [Fact]
        public void GraphUser_ActivityStatus_ShouldCalculateCorrectly()
        {
            // Arrange & Act - użytkownik nieaktywny
            var inactiveUser = new GraphUser { AccountEnabled = false };
            inactiveUser.ActivityStatus.Should().Be("Nieaktywny");

            // Act - użytkownik nigdy nie zalogowany
            var neverLoggedInUser = new GraphUser { AccountEnabled = true, LastSignInDateTime = null };
            neverLoggedInUser.ActivityStatus.Should().Be("Nigdy nie zalogowany");

            // Act - bardzo aktywny (7 dni)
            var veryActiveUser = new GraphUser 
            { 
                AccountEnabled = true, 
                LastSignInDateTime = DateTime.UtcNow.AddDays(-3) 
            };
            veryActiveUser.ActivityStatus.Should().Be("Bardzo aktywny");

            // Act - aktywny (30 dni)
            var activeUser = new GraphUser 
            { 
                AccountEnabled = true, 
                LastSignInDateTime = DateTime.UtcNow.AddDays(-20) 
            };
            activeUser.ActivityStatus.Should().Be("Aktywny");

            // Act - umiarkowanie aktywny (90 dni)
            var moderatelyActiveUser = new GraphUser 
            { 
                AccountEnabled = true, 
                LastSignInDateTime = DateTime.UtcNow.AddDays(-60) 
            };
            moderatelyActiveUser.ActivityStatus.Should().Be("Umiarkowanie aktywny");

            // Act - nieaktywny (ponad 90 dni)
            var longInactiveUser = new GraphUser 
            { 
                AccountEnabled = true, 
                LastSignInDateTime = DateTime.UtcNow.AddDays(-120) 
            };
            longInactiveUser.ActivityStatus.Should().Be("Nieaktywny");
        }

        [Fact]
        public void GraphUser_DaysSinceLastSignIn_ShouldCalculateCorrectly()
        {
            // Arrange & Act - nigdy nie zalogowany
            var neverLoggedIn = new GraphUser { LastSignInDateTime = null };
            neverLoggedIn.DaysSinceLastSignIn.Should().BeNull();

            // Act - zalogowany 5 dni temu
            var recentUser = new GraphUser { LastSignInDateTime = DateTime.UtcNow.AddDays(-5) };
            recentUser.DaysSinceLastSignIn.Should().Be(5);

            // Act - zalogowany 100 dni temu
            var oldUser = new GraphUser { LastSignInDateTime = DateTime.UtcNow.AddDays(-100) };
            oldUser.DaysSinceLastSignIn.Should().Be(100);
        }

        [Fact]
        public void GraphUser_IsRecentlyActive_ShouldCalculateCorrectly()
        {
            // Arrange & Act - niedawno aktywny
            var recentUser = new GraphUser { LastSignInDateTime = DateTime.UtcNow.AddDays(-20) };
            recentUser.IsRecentlyActive.Should().BeTrue();

            // Act - nieaktywny przez długi czas
            var oldUser = new GraphUser { LastSignInDateTime = DateTime.UtcNow.AddDays(-50) };
            oldUser.IsRecentlyActive.Should().BeFalse();

            // Act - nigdy nie zalogowany
            var neverLoggedIn = new GraphUser { LastSignInDateTime = null };
            neverLoggedIn.IsRecentlyActive.Should().BeFalse();
        }

        #endregion

        #region GraphUser Methods Tests

        [Fact]
        public void HasLicense_WhenUserHasLicense_ShouldReturnTrue()
        {
            // Arrange
            var user = new GraphUser();
            user.AssignedLicenses.Add(new GraphLicense { SkuId = "lic-123" });
            user.AssignedLicenses.Add(new GraphLicense { SkuId = "lic-456" });

            // Act & Assert
            user.HasLicense("lic-123").Should().BeTrue();
            user.HasLicense("lic-456").Should().BeTrue();
        }

        [Fact]
        public void HasLicense_WhenUserDoesNotHaveLicense_ShouldReturnFalse()
        {
            // Arrange
            var user = new GraphUser();
            user.AssignedLicenses.Add(new GraphLicense { SkuId = "lic-123" });

            // Act & Assert
            user.HasLicense("lic-999").Should().BeFalse();
        }

        [Fact]
        public void HasAdminRole_ShouldReturnFalse()
        {
            // Arrange
            var user = new GraphUser();

            // Act & Assert - na razie zawsze false (zaślepka)
            user.HasAdminRole().Should().BeFalse();
        }

        [Fact]
        public void IsMemberOfGroup_ShouldReturnFalse()
        {
            // Arrange
            var user = new GraphUser();

            // Act & Assert - na razie zawsze false (zaślepka)
            user.IsMemberOfGroup("group-123").Should().BeFalse();
        }

        [Fact]
        public void GetLicense_WhenUserHasLicense_ShouldReturnLicense()
        {
            // Arrange
            var user = new GraphUser();
            var license = new GraphLicense { SkuId = "lic-123", SkuPartNumber = "ENTERPRISEPACK" };
            user.AssignedLicenses.Add(license);

            // Act
            var result = user.GetLicense("lic-123");

            // Assert
            result.Should().Be(license);
            result!.SkuPartNumber.Should().Be("ENTERPRISEPACK");
        }

        [Fact]
        public void GetLicense_WhenUserDoesNotHaveLicense_ShouldReturnNull()
        {
            // Arrange
            var user = new GraphUser();
            user.AssignedLicenses.Add(new GraphLicense { SkuId = "lic-123" });

            // Act
            var result = user.GetLicense("lic-999");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetSummary_ShouldReturnCorrectSummary()
        {
            // Arrange
            var user = new GraphUser
            {
                GivenName = "Jan",
                Surname = "Kowalski",
                Department = "Matematyka",
                AccountEnabled = true
            };
            user.AssignedLicenses.Add(new GraphLicense { SkuId = "lic-1" });
            user.AssignedLicenses.Add(new GraphLicense { SkuId = "lic-2" });

            // Act
            var summary = user.GetSummary();

            // Assert
            summary.Should().Be("Jan Kowalski (Matematyka): Aktywny, 2 licencji");
        }

        [Fact]
        public void GetSummary_WhenNoDepartment_ShouldNotIncludeDepartment()
        {
            // Arrange
            var user = new GraphUser
            {
                GivenName = "Jan",
                Surname = "Kowalski",
                AccountEnabled = false
            };

            // Act
            var summary = user.GetSummary();

            // Assert
            summary.Should().Be("Jan Kowalski: Nieaktywny, 0 licencji");
        }

        [Fact]
        public void GetDetailedInfo_ShouldReturnCompleteInformation()
        {
            // Arrange
            var user = new GraphUser
            {
                GivenName = "Jan",
                Surname = "Kowalski",
                UserPrincipalName = "jan.kowalski@school.edu",
                Mail = "jan.kowalski@school.edu",
                AccountEnabled = true,
                UserType = "Member",
                JobTitle = "Nauczyciel",
                Department = "Matematyka",
                CreatedDateTime = new DateTime(2023, 1, 15),
                LastSignInDateTime = new DateTime(2024, 1, 10, 14, 30, 0)
            };
            user.AssignedLicenses.Add(new GraphLicense { SkuPartNumber = "ENTERPRISEPACK" });
            user.AssignedLicenses.Add(new GraphLicense { SkuPartNumber = "FLOW_FREE" });
            user.DirectReports.Add(new GraphUser { GivenName = "Anna", Surname = "Nowak" });

            // Act
            var info = user.GetDetailedInfo();

            // Assert
            info.Should().Contain("Nazwa: Jan Kowalski");
            info.Should().Contain("UPN: jan.kowalski@school.edu");
            info.Should().Contain("Email: jan.kowalski@school.edu");
            info.Should().Contain("Status: Aktywny");
            info.Should().Contain("Typ: Member");
            info.Should().Contain("Stanowisko: Nauczyciel");
            info.Should().Contain("Dział: Matematyka");
            info.Should().Contain("Utworzony: 2023-01-15");
            info.Should().Contain("Ostatnie logowanie: 2024-01-10 14:30");
            info.Should().Contain("Liczba licencji: 2");
            info.Should().Contain("Liczba podwładnych: 1");
            info.Should().Contain("Licencje:");
            info.Should().Contain("• ENTERPRISEPACK");
            info.Should().Contain("• FLOW_FREE");
        }

        #endregion

        #region GraphUser Conversion Methods Tests

        [Fact]
        public void ToLocalUser_ShouldConvertToLocalUserCorrectly()
        {
            // Arrange
            var graphUser = new GraphUser
            {
                Id = "graph-user-123",
                GivenName = "Jan",
                Surname = "Kowalski",
                UserPrincipalName = "jan.kowalski@school.edu",
                BusinessPhone = "+48123456789",
                JobTitle = "Nauczyciel",
                CreatedDateTime = DateTime.UtcNow.AddDays(-30),
                LastSignInDateTime = DateTime.UtcNow.AddDays(-2)
            };

            // Act
            var localUser = graphUser.ToLocalUser();

            // Assert
            localUser.Should().NotBeNull();
            localUser.FirstName.Should().Be("Jan");
            localUser.LastName.Should().Be("Kowalski");
            localUser.UPN.Should().Be("jan.kowalski@school.edu");
            localUser.ExternalId.Should().Be("graph-user-123");
            localUser.Phone.Should().Be("+48123456789");
            localUser.Position.Should().Be("Nauczyciel");
            localUser.EmploymentDate.Should().Be(graphUser.CreatedDateTime);
            localUser.LastLoginDate.Should().Be(graphUser.LastSignInDateTime);
        }

        [Fact]
        public void ToLocalUser_WhenNullProperties_ShouldUseDefaults()
        {
            // Arrange
            var graphUser = new GraphUser
            {
                GivenName = null,
                Surname = null,
                UserPrincipalName = null,
                BusinessPhone = null,
                MobilePhone = "+48987654321"
            };

            // Act
            var localUser = graphUser.ToLocalUser();

            // Assert
            localUser.FirstName.Should().Be(string.Empty);
            localUser.LastName.Should().Be(string.Empty);
            localUser.UPN.Should().Be(string.Empty);
            localUser.Phone.Should().Be("+48987654321"); // MobilePhone jako fallback
        }

        [Fact]
        public void FromLocalUser_ShouldConvertFromLocalUserCorrectly()
        {
            // Arrange
            var localUser = new User
            {
                Id = "local-user-456",
                ExternalId = "external-123",
                FirstName = "Anna",
                LastName = "Nowak",
                UPN = "anna.nowak@school.edu",
                Phone = "+48111222333",
                Position = "Dyrektor",
                EmploymentDate = DateTime.UtcNow.AddDays(-365),
                LastLoginDate = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            var graphUser = GraphUser.FromLocalUser(localUser);

            // Assert
            graphUser.Should().NotBeNull();
            graphUser.Id.Should().Be("external-123");
            graphUser.GivenName.Should().Be("Anna");
            graphUser.Surname.Should().Be("Nowak");
            graphUser.UserPrincipalName.Should().Be("anna.nowak@school.edu");
            graphUser.AccountEnabled.Should().BeTrue(); // User.IsActive jest true domyślnie
            graphUser.JobTitle.Should().Be("Dyrektor");
            graphUser.BusinessPhone.Should().Be("+48111222333");
            graphUser.CreatedDateTime.Should().Be(localUser.EmploymentDate);
            graphUser.LastSignInDateTime.Should().Be(localUser.LastLoginDate);
        }

        [Fact]
        public void FromLocalUser_WhenInactiveLocalUser_ShouldSetAccountDisabled()
        {
            // Arrange
            var localUser = new User
            {
                FirstName = "Inactive",
                LastName = "User"
            };
            // Ustawiamy BaseEntity.IsActive na false poprzez soft delete
            ((BaseEntity)localUser).MarkAsDeleted("test");

            // Act
            var graphUser = GraphUser.FromLocalUser(localUser);

            // Assert
            graphUser.AccountEnabled.Should().BeFalse();
        }

        #endregion

        #region GraphLicense Tests

        [Fact]
        public void GraphLicense_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var license = new GraphLicense();

            // Assert
            license.SkuId.Should().BeNull();
            license.SkuPartNumber.Should().BeNull();
        }

        [Fact]
        public void GraphLicense_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var license = new GraphLicense();

            // Act
            license.SkuId = "lic-123";
            license.SkuPartNumber = "ENTERPRISEPACK";

            // Assert
            license.SkuId.Should().Be("lic-123");
            license.SkuPartNumber.Should().Be("ENTERPRISEPACK");
        }

        #endregion

        #region GraphServicePlan Tests

        [Fact]
        public void GraphServicePlan_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var servicePlan = new GraphServicePlan();

            // Assert
            servicePlan.ServicePlanId.Should().BeNull();
            servicePlan.ServicePlanName.Should().BeNull();
            servicePlan.ProvisioningStatus.Should().BeNull();
            servicePlan.AppliesTo.Should().BeNull();
        }

        [Fact]
        public void GraphServicePlan_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var servicePlan = new GraphServicePlan();

            // Act
            servicePlan.ServicePlanId = "plan-123";
            servicePlan.ServicePlanName = "EXCHANGE_S_ENTERPRISE";
            servicePlan.ProvisioningStatus = "Success";
            servicePlan.AppliesTo = "User";

            // Assert
            servicePlan.ServicePlanId.Should().Be("plan-123");
            servicePlan.ServicePlanName.Should().Be("EXCHANGE_S_ENTERPRISE");
            servicePlan.ProvisioningStatus.Should().Be("Success");
            servicePlan.AppliesTo.Should().Be("User");
        }

        #endregion

        #region Real World Scenarios Tests

        [Fact]
        public void GraphUser_CompleteUserScenario_ShouldWorkCorrectly()
        {
            // Arrange & Act - tworzymy kompletnego użytkownika
            var user = new GraphUser
            {
                Id = "user-jan-kowalski",
                GivenName = "Jan",
                Surname = "Kowalski",
                UserPrincipalName = "jan.kowalski@school.edu",
                Mail = "jan.kowalski@school.edu",
                MailNickname = "jankowalski",
                UserType = "Member",
                AccountEnabled = true,
                CreatedDateTime = DateTime.UtcNow.AddDays(-180),
                LastSignInDateTime = DateTime.UtcNow.AddDays(-3),
                JobTitle = "Nauczyciel Matematyki",
                Department = "Matematyka",
                CompanyName = "Zespół Szkół Nr 1",
                OfficeLocation = "Sala 101",
                BusinessPhone = "+48123456789",
                MobilePhone = "+48987654321",
                StreetAddress = "ul. Szkolna 1",
                City = "Warszawa",
                State = "Mazowieckie",
                PostalCode = "00-001",
                Country = "Poland",
                DisplayName = "Jan Kowalski"
            };

            // Dodajemy managera
            var manager = new GraphUser
            {
                Id = "manager-anna-nowak",
                GivenName = "Anna",
                Surname = "Nowak",
                UserPrincipalName = "anna.nowak@school.edu",
                JobTitle = "Dyrektor"
            };
            user.Manager = manager;
            user.ManagerId = manager.Id;

            // Dodajemy podwładnych
            user.DirectReports.Add(new GraphUser
            {
                Id = "subordinate-1",
                GivenName = "Piotr",
                Surname = "Wiśniewski",
                JobTitle = "Asystent"
            });

            // Dodajemy licencje
            user.AssignedLicenses.Add(new GraphLicense
            {
                SkuId = "license-office365-e3",
                SkuPartNumber = "ENTERPRISEPACK"
            });

            user.AssignedLicenses.Add(new GraphLicense
            {
                SkuId = "license-teams",
                SkuPartNumber = "TEAMS1"
            });

            // Dodajemy plany usług
            user.ServicePlans.Add(new GraphServicePlan
            {
                ServicePlanId = "plan-exchange",
                ServicePlanName = "EXCHANGE_S_ENTERPRISE",
                ProvisioningStatus = "Success",
                AppliesTo = "User"
            });

            user.BusinessPhones.Add("+48123456789");

            // Assert - sprawdzamy kompletną funkcjonalność
            user.GivenName.Should().Be("Jan");
            user.Surname.Should().Be("Kowalski");
            user.FullName.Should().Be("Jan Kowalski");
            user.FirstName.Should().Be("Jan");
            user.LastName.Should().Be("Kowalski");
            user.UPN.Should().Be("jan.kowalski@school.edu");
            user.IsActive.Should().BeTrue();

            // Test właściwości obliczanych
            user.ActivityStatus.Should().Be("Bardzo aktywny"); // zalogowany 3 dni temu
            user.DaysSinceLastSignIn.Should().Be(3);
            user.IsRecentlyActive.Should().BeTrue(); // 3 dni < 30 dni

            // Test metod licencji
            user.HasLicense("license-office365-e3").Should().BeTrue();
            user.HasLicense("license-teams").Should().BeTrue();
            user.HasLicense("nonexistent-license").Should().BeFalse();

            var e3License = user.GetLicense("license-office365-e3");
            e3License.Should().NotBeNull();
            e3License!.SkuPartNumber.Should().Be("ENTERPRISEPACK");

            // Test hierarchii organizacyjnej
            user.Manager.Should().Be(manager);
            user.ManagerId.Should().Be("manager-anna-nowak");
            user.DirectReports.Should().HaveCount(1);
            user.DirectReports.First().GivenName.Should().Be("Piotr");

            // Test kolekcji
            user.AssignedLicenses.Should().HaveCount(2);
            user.ServicePlans.Should().HaveCount(1);
            user.BusinessPhones.Should().HaveCount(1);

            // Test konwersji do lokalnego użytkownika
            var localUser = user.ToLocalUser();
            localUser.FirstName.Should().Be("Jan");
            localUser.LastName.Should().Be("Kowalski");
            localUser.UPN.Should().Be("jan.kowalski@school.edu");
            localUser.Phone.Should().Be("+48123456789");

            // Test podsumowania
            var summary = user.GetSummary();
            summary.Should().Be("Jan Kowalski (Matematyka): Aktywny, 2 licencji");

            // Test szczegółowych informacji
            var detailedInfo = user.GetDetailedInfo();
            detailedInfo.Should().Contain("Nazwa: Jan Kowalski");
            detailedInfo.Should().Contain("Stanowisko: Nauczyciel Matematyki");
            detailedInfo.Should().Contain("Dział: Matematyka");
            detailedInfo.Should().Contain("Status aktywności: Bardzo aktywny");
            detailedInfo.Should().Contain("Liczba licencji: 2");
            detailedInfo.Should().Contain("• ENTERPRISEPACK");
            detailedInfo.Should().Contain("• TEAMS1");
        }

        [Fact]
        public void GraphUser_InactiveUserScenario_ShouldWorkCorrectly()
        {
            // Arrange & Act - użytkownik nieaktywny
            var user = new GraphUser
            {
                GivenName = "Inactive",
                Surname = "User",
                UserPrincipalName = "inactive.user@school.edu",
                AccountEnabled = false,
                LastSignInDateTime = DateTime.UtcNow.AddDays(-150)
            };

            // Assert
            user.IsActive.Should().BeFalse();
            user.ActivityStatus.Should().Be("Nieaktywny");
            user.IsRecentlyActive.Should().BeFalse();

            var summary = user.GetSummary();
            summary.Should().Be("Inactive User: Nieaktywny, 0 licencji");

            var localUser = user.ToLocalUser();
            // Note: ToLocalUser nie sprawdza AccountEnabled, więc lokalny użytkownik będzie aktywny
            // Można to poprawić w implementacji jeśli potrzebne
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void GraphUser_WithNullGivenNameAndSurname_ShouldHandleGracefully()
        {
            // Arrange
            var user = new GraphUser
            {
                GivenName = null,
                Surname = null,
                UserPrincipalName = "user@school.edu"
            };

            // Act & Assert
            user.FirstName.Should().BeNull();
            user.LastName.Should().BeNull();
            user.FullName.Should().Be("");
            user.UPN.Should().Be("user@school.edu");

            var summary = user.GetSummary();
            summary.Should().Be(": Aktywny, 0 licencji");
        }

        [Fact]
        public void GraphUser_WithEmptyCollections_ShouldHandleGracefully()
        {
            // Arrange
            var user = new GraphUser();

            // Act & Assert
            user.AssignedLicenses.Should().BeEmpty();
            user.DirectReports.Should().BeEmpty();
            user.ServicePlans.Should().BeEmpty();
            user.BusinessPhones.Should().BeEmpty();

            user.HasLicense("any-license").Should().BeFalse();
            user.GetLicense("any-license").Should().BeNull();
        }

        [Fact]
        public void GraphUser_WithNullLastSignInDateTime_ShouldHandleGracefully()
        {
            // Arrange
            var user = new GraphUser
            {
                AccountEnabled = true,
                LastSignInDateTime = null
            };

            // Act & Assert
            user.ActivityStatus.Should().Be("Nigdy nie zalogowany");
            user.DaysSinceLastSignIn.Should().BeNull();
            user.IsRecentlyActive.Should().BeFalse();
        }

        #endregion
    }
} 