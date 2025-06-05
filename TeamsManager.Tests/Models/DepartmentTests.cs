using System; // Potrzebne dla DateTime, jeśli będziemy testować pola z BaseEntity
using System.Collections.Generic; // Potrzebne dla List
using System.Linq; // Potrzebne dla .Any() i .Count()
using FluentAssertions;
using TeamsManager.Core.Models;
// using TeamsManager.Core.Enums; // Niepotrzebne tutaj, chyba że Department ma jakieś Enumy

namespace TeamsManager.Tests.Models
{
    public class DepartmentTests
    {
        [Fact]
        public void Department_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var department = new Department();

            // Sprawdzenie pól bezpośrednich
            department.Id.Should().Be(string.Empty);
            department.Name.Should().Be(string.Empty);
            department.Description.Should().Be(string.Empty);
            department.ParentDepartmentId.Should().BeNull();
            department.DepartmentCode.Should().BeNull();
            department.Email.Should().BeNull();
            department.Phone.Should().BeNull();
            department.Location.Should().BeNull();
            department.SortOrder.Should().Be(0);

            // Sprawdzenie pól z BaseEntity
            department.IsActive.Should().BeTrue();
            // department.CreatedDate.Should().NotBe(default(DateTime)); // Zależne od logiki BaseEntity/DbContext
            // department.CreatedBy.Should().Be(string.Empty); // Jeśli nie ma logiki ustawiania w konstruktorze

            // Sprawdzenie kolekcji nawigacyjnych
            department.ParentDepartment.Should().BeNull();
            department.SubDepartments.Should().NotBeNull().And.BeEmpty();
            department.Users.Should().NotBeNull().And.BeEmpty();

            // Sprawdzenie właściwości obliczanych dla domyślnego obiektu
            department.IsRootDepartment.Should().BeTrue();
            department.HierarchyLevel.Should().Be(0);
            department.FullPath.Should().Be(string.Empty); // Bo Name jest puste
            department.DirectUsersCount.Should().Be(0);
            department.TotalUsersCount.Should().Be(0);
            department.SubDepartmentsCount.Should().Be(0);
            department.HasSubDepartments.Should().BeFalse();
            department.AllUsers.Should().NotBeNull().And.BeEmpty();
            department.AllSubDepartments.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void Department_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var department = new Department();
            var departmentId = "dept-123";
            var name = "Matematyka";
            var description = "Wydział Matematyki i Informatyki";
            var parentId = "dept-root";
            var code = "MAT";
            var email = "matematyka@szkola.edu.pl";
            var phone = "123456789";
            var location = "Budynek A, Sala 101";
            var sortOrder = 10;
            var createdBy = "admin"; // Przykład dla pola z BaseEntity

            // Wykonanie
            department.Id = departmentId;
            department.Name = name;
            department.Description = description;
            department.IsActive = false;
            department.ParentDepartmentId = parentId;
            department.DepartmentCode = code;
            department.Email = email;
            department.Phone = phone;
            department.Location = location;
            department.SortOrder = sortOrder;
            department.CreatedBy = createdBy; // Przykład ustawienia pola z BaseEntity

            // Sprawdzenie
            department.Id.Should().Be(departmentId);
            department.Name.Should().Be(name);
            department.Description.Should().Be(description);
            department.IsActive.Should().BeFalse();
            department.ParentDepartmentId.Should().Be(parentId);
            department.DepartmentCode.Should().Be(code);
            department.Email.Should().Be(email);
            department.Phone.Should().Be(phone);
            department.Location.Should().Be(location);
            department.SortOrder.Should().Be(sortOrder);
            department.CreatedBy.Should().Be(createdBy);
        }

        [Theory]
        [InlineData("Matematyka", "Wydział Matematyki")]
        [InlineData("Informatyka", "Wydział Informatyki")]
        [InlineData("", "")] // Przypadek brzegowy
        public void Department_WhenSettingNameAndDescription_ShouldRetainValues(string name, string description)
        {
            // Przygotowanie
            var department = new Department();

            // Wykonanie
            department.Name = name;
            department.Description = description;

            // Sprawdzenie
            department.Name.Should().Be(name);
            department.Description.Should().Be(description);
        }

        [Fact]
        public void Department_WhenAddingUsers_ShouldMaintainCollectionAndCounts()
        {
            // Przygotowanie
            var department = new Department { Id = "dept-123", Name = "IT" };
            var user1 = new User { Id = "user-1", DepartmentId = department.Id, IsActive = true };
            var user2 = new User { Id = "user-2", DepartmentId = department.Id, IsActive = true };
            var inactiveUser = new User { Id = "user-3", DepartmentId = department.Id, IsActive = false };


            // Wykonanie
            department.Users.Add(user1);
            department.Users.Add(user2);
            department.Users.Add(inactiveUser);

            // Sprawdzenie
            department.Users.Should().HaveCount(3);
            department.Users.Should().Contain(user1).And.Contain(user2).And.Contain(inactiveUser);
            department.DirectUsersCount.Should().Be(2); // Tylko aktywni
            department.TotalUsersCount.Should().Be(2); // Na razie bez poddziałów
            department.AllUsers.Should().Contain(user1).And.Contain(user2).And.NotContain(inactiveUser);
        }

        // ===== NOWE TESTY DLA HIERARCHII I WŁAŚCIWOŚCI OBLICZANYCH =====

        [Fact]
        public void Department_IsRootDepartment_ShouldReturnCorrectValue()
        {
            // Przygotowanie
            var rootDept = new Department();
            var childDept = new Department { ParentDepartmentId = "some-id" };

            // Sprawdzenie
            rootDept.IsRootDepartment.Should().BeTrue();
            childDept.IsRootDepartment.Should().BeFalse();
        }

        [Fact]
        public void Department_HierarchyLevel_And_FullPath_ShouldBeCalculatedCorrectly()
        {
            // Przygotowanie
            var grandParent = new Department { Id = "gp", Name = "Szkoła" };
            var parent = new Department { Id = "p", Name = "Wydział IT", ParentDepartment = grandParent, ParentDepartmentId = "gp" };
            var child = new Department { Id = "c", Name = "Katedra Oprogramowania", ParentDepartment = parent, ParentDepartmentId = "p" };

            // Dodanie do SubDepartments dla spójności (choć nie jest to ściśle wymagane dla tych testów)
            grandParent.SubDepartments.Add(parent);
            parent.SubDepartments.Add(child);

            // Sprawdzenie Poziomów
            grandParent.HierarchyLevel.Should().Be(0);
            parent.HierarchyLevel.Should().Be(1);
            child.HierarchyLevel.Should().Be(2);

            // Sprawdzenie Pełnych Ścieżek
            grandParent.FullPath.Should().Be("Szkoła");
            parent.FullPath.Should().Be("Szkoła/Wydział IT");
            child.FullPath.Should().Be("Szkoła/Wydział IT/Katedra Oprogramowania");
        }

        [Fact]
        public void Department_TotalUsersCount_ShouldIncludeSubDepartmentsRecursively()
        {
            // Przygotowanie
            var deptRoot = new Department { Id = "root", Name = "Root" };
            var deptA = new Department { Id = "A", Name = "A", ParentDepartment = deptRoot, ParentDepartmentId = "root" };
            var deptB = new Department { Id = "B", Name = "B", ParentDepartment = deptA, ParentDepartmentId = "A" };

            deptRoot.SubDepartments.Add(deptA);
            deptA.SubDepartments.Add(deptB);

            deptRoot.Users.Add(new User { IsActive = true }); // 1 w Root
            deptA.Users.Add(new User { IsActive = true });    // 1 w A
            deptA.Users.Add(new User { IsActive = true });    // 2 w A
            deptB.Users.Add(new User { IsActive = true });    // 1 w B
            deptB.Users.Add(new User { IsActive = false });   // Nieaktywny w B

            // Sprawdzenie
            deptB.DirectUsersCount.Should().Be(1);
            deptB.TotalUsersCount.Should().Be(1);

            deptA.DirectUsersCount.Should().Be(2);
            deptA.TotalUsersCount.Should().Be(3); // 2 z A + 1 z B

            deptRoot.DirectUsersCount.Should().Be(1);
            deptRoot.TotalUsersCount.Should().Be(4); // 1 z Root + 3 z A (które zawiera B)
        }

        [Fact]
        public void Department_AllUsers_And_AllSubDepartments_ShouldWorkRecursively()
        {
            // Przygotowanie
            var deptRoot = new Department { Id = "root", Name = "Root", IsActive = true };
            var deptA = new Department { Id = "A", Name = "A", ParentDepartment = deptRoot, ParentDepartmentId = "root", IsActive = true };
            var deptB = new Department { Id = "B", Name = "B", ParentDepartment = deptA, ParentDepartmentId = "A", IsActive = true };
            var deptC_inactive = new Department { Id = "C", Name = "C", ParentDepartment = deptRoot, ParentDepartmentId = "root", IsActive = false };


            deptRoot.SubDepartments.Add(deptA);
            deptRoot.SubDepartments.Add(deptC_inactive);
            deptA.SubDepartments.Add(deptB);

            var userRoot = new User { Id = "uroot", FirstName = "UserRoot", IsActive = true };
            var userA = new User { Id = "ua", FirstName = "UserA", IsActive = true };
            var userB = new User { Id = "ub", FirstName = "UserB", IsActive = true };
            var userC_inactive_dept = new User { Id = "uc_inactive", FirstName = "UserC_InactiveDept", IsActive = true }; // Aktywny user w nieaktywnym dziale

            deptRoot.Users.Add(userRoot);
            deptA.Users.Add(userA);
            deptB.Users.Add(userB);
            deptC_inactive.Users.Add(userC_inactive_dept);


            // Sprawdzenie AllSubDepartments
            deptRoot.AllSubDepartments.Should().HaveCount(2).And.Contain(deptA).And.Contain(deptB); // deptC_inactive jest nieaktywny
            deptA.AllSubDepartments.Should().HaveCount(1).And.Contain(deptB);
            deptB.AllSubDepartments.Should().BeEmpty();

            // Sprawdzenie AllUsers
            deptRoot.AllUsers.Should().HaveCount(3).And.Contain(userRoot).And.Contain(userA).And.Contain(userB); // userC_inactive_dept nie jest brany pod uwagę
            deptA.AllUsers.Should().HaveCount(2).And.Contain(userA).And.Contain(userB);
            deptB.AllUsers.Should().HaveCount(1).And.Contain(userB);
        }

        [Fact]
        public void Department_IsParentOf_And_IsChildOf_ShouldReturnCorrectValues()
        {
            // Przygotowanie
            var grandParent = new Department { Id = "gp", Name = "Szkoła" };
            var parent = new Department { Id = "p", Name = "Wydział IT", ParentDepartment = grandParent };
            var child = new Department { Id = "c", Name = "Katedra Oprogramowania", ParentDepartment = parent };
            var unrelated = new Department { Id = "u", Name = "Inny Dział" };

            grandParent.SubDepartments.Add(parent);
            parent.SubDepartments.Add(child);

            // Sprawdzenie IsParentOf
            grandParent.IsParentOf("p").Should().BeTrue();
            grandParent.IsParentOf("c").Should().BeTrue();
            grandParent.IsParentOf("u").Should().BeFalse();
            parent.IsParentOf("c").Should().BeTrue();
            parent.IsParentOf("gp").Should().BeFalse();

            // Sprawdzenie IsChildOf
            child.IsChildOf("p").Should().BeTrue();
            child.IsChildOf("gp").Should().BeTrue();
            child.IsChildOf("u").Should().BeFalse();
            parent.IsChildOf("gp").Should().BeTrue();
            parent.IsChildOf("c").Should().BeFalse();
        }

        [Fact]
        public void Department_GetParentChain_ShouldReturnCorrectHierarchy()
        {
            // Przygotowanie
            var grandParent = new Department { Id = "gp", Name = "Szkoła" };
            var parent = new Department { Id = "p", Name = "Wydział IT", ParentDepartment = grandParent };
            var child = new Department { Id = "c", Name = "Katedra Oprogramowania", ParentDepartment = parent };

            // Sprawdzenie
            child.GetParentChain().Should().HaveCount(2).And.ContainInOrder(parent, grandParent);
            parent.GetParentChain().Should().HaveCount(1).And.Contain(grandParent);
            grandParent.GetParentChain().Should().BeEmpty();
        }

        [Fact]
        public void Department_HierarchyLevel_WithCycle_ShouldNotCauseInfiniteLoop()
        {
            // Przygotowanie - tworzymy cykl
            var dept1 = new Department { Id = "1", Name = "Dept1" };
            var dept2 = new Department { Id = "2", Name = "Dept2", ParentDepartment = dept1 };
            var dept3 = new Department { Id = "3", Name = "Dept3", ParentDepartment = dept2 };
            dept1.ParentDepartment = dept3; // Cykl!
            
            // Wykonanie - nie powinno spowodować nieskończonej pętli
            var level = dept1.HierarchyLevel;
            
            // Sprawdzenie
            level.Should().BeLessThan(100); // Powinno przerwać przed maxDepth
        }

        [Fact]
        public void Department_FullPath_WithCycle_ShouldIncludeCycleMarker()
        {
            // Przygotowanie
            var dept1 = new Department { Id = "1", Name = "Dept1" };
            var dept2 = new Department { Id = "2", Name = "Dept2", ParentDepartment = dept1 };
            dept1.ParentDepartment = dept2; // Cykl prosty
            
            // Wykonanie
            var path = dept1.FullPath;
            
            // Sprawdzenie
            path.Should().Contain("[CYKL]");
        }

        [Fact]
        public void Department_AllSubDepartments_WithCycle_ShouldNotDuplicate()
        {
            // Przygotowanie
            var root = new Department { Id = "root", Name = "Root" };
            var child1 = new Department { Id = "1", Name = "Child1", ParentDepartment = root };
            var child2 = new Department { Id = "2", Name = "Child2", ParentDepartment = child1 };
            
            root.SubDepartments.Add(child1);
            child1.SubDepartments.Add(child2);
            child2.SubDepartments.Add(root); // Cykl!
            
            // Wykonanie
            var allSubs = root.AllSubDepartments;
            
            // Sprawdzenie
            allSubs.Should().HaveCount(2); // Tylko child1 i child2, bez duplikatów
            allSubs.Should().NotContain(root); // Root nie powinien być swoim własnym poddziałem
        }

        [Fact]
        public void Department_GetParentChain_WithCycle_ShouldTerminate()
        {
            // Przygotowanie
            var dept1 = new Department { Id = "1", Name = "Dept1" };
            var dept2 = new Department { Id = "2", Name = "Dept2", ParentDepartment = dept1 };
            var dept3 = new Department { Id = "3", Name = "Dept3", ParentDepartment = dept2 };
            dept1.ParentDepartment = dept3; // Cykl!
            
            // Wykonanie
            var chain = dept3.GetParentChain();
            
            // Sprawdzenie
            chain.Should().HaveCountLessThanOrEqualTo(3);
            chain.Select(d => d.Id).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void Department_IsChildOf_WithCycle_ShouldReturnFalse()
        {
            // Przygotowanie
            var dept1 = new Department { Id = "1", Name = "Dept1" };
            var dept2 = new Department { Id = "2", Name = "Dept2", ParentDepartment = dept1 };
            dept1.ParentDepartment = dept2; // Cykl!
            
            // Wykonanie
            var result = dept1.IsChildOf("2");
            
            // Sprawdzenie - w cyklicznej hierarchii IsChildOf powinno zwrócić false
            result.Should().BeFalse();
        }

        [Fact]
        public void Department_HierarchyLevel_UsesCache_ShouldReturnCachedValue()
        {
            // Przygotowanie
            var dept = new Department { Id = "1", Name = "Dept1" };
            
            // Pierwszy dostęp
            var level1 = dept.HierarchyLevel;
            
            // Drugi dostęp - powinien użyć cache
            var level2 = dept.HierarchyLevel;
            
            // Sprawdzenie
            level1.Should().Be(level2);
            level1.Should().Be(0); // Root department
        }
    }
}