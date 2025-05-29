using System.Collections.Generic;
using System.Linq;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Dział/wydział/jednostka organizacyjna
    /// Może tworzyć hierarchię działów (dział nadrzędny -> poddziały)
    /// </summary>
    public class Department : BaseEntity
    {
        /// <summary>
        /// Nazwa działu (np. "Matematyka", "Informatyka", "Administracja")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Szczegółowy opis działu, jego zadań i zakresu odpowiedzialności
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Identyfikator działu nadrzędnego (dla hierarchii)
        /// Jeśli null - to dział główny (root)
        /// </summary>
        public string? ParentDepartmentId { get; set; }

        /// <summary>
        /// Kod działu używany w systemach zewnętrznych
        /// Np. kod z dziennika elektronicznego, systemu kadrowego
        /// </summary>
        public string? DepartmentCode { get; set; }

        /// <summary>
        /// Adres email działu (opcjonalny)
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Numer telefonu działu (opcjonalny)
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Lokalizacja działu (sala, budynek, piętro)
        /// </summary>
        public string? Location { get; set; }

        /// <summary>
        /// Kolejność sortowania przy wyświetlaniu
        /// </summary>
        public int SortOrder { get; set; } = 0;

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Dział nadrzędny w hierarchii
        /// </summary>
        public Department? ParentDepartment { get; set; }

        /// <summary>
        /// Poddziały (działy podrzędne)
        /// </summary>
        public List<Department> SubDepartments { get; set; } = new List<Department>();

        /// <summary>
        /// Użytkownicy przypisani bezpośrednio do tego działu
        /// </summary>
        public List<User> Users { get; set; } = new List<User>();

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Czy to jest dział główny (brak działu nadrzędnego)
        /// </summary>
        public bool IsRootDepartment => string.IsNullOrEmpty(ParentDepartmentId);

        /// <summary>
        /// Poziom w hierarchii (0 = root, 1 = pierwszy poziom, itd.)
        /// </summary>
        public int HierarchyLevel
        {
            get
            {
                var level = 0;
                var parent = ParentDepartment;
                while (parent != null)
                {
                    level++;
                    parent = parent.ParentDepartment;
                }
                return level;
            }
        }

        /// <summary>
        /// Pełna ścieżka działu w hierarchii
        /// Np. "Szkoła/Wydział Matematyki/Katedra Algebry"
        /// </summary>
        public string FullPath
        {
            get
            {
                var path = new List<string>();
                var current = this;

                while (current != null)
                {
                    path.Insert(0, current.Name);
                    current = current.ParentDepartment;
                }

                return string.Join("/", path);
            }
        }

        /// <summary>
        /// Liczba bezpośrednich użytkowników w dziale
        /// </summary>
        public int DirectUsersCount => Users?.Count(u => u.IsActive) ?? 0;

        /// <summary>
        /// Liczba wszystkich użytkowników w dziale i poddziałach (rekurencyjnie)
        /// </summary>
        public int TotalUsersCount
        {
            get
            {
                var count = DirectUsersCount;
                foreach (var subDept in SubDepartments.Where(sd => sd.IsActive))
                {
                    count += subDept.TotalUsersCount;
                }
                return count;
            }
        }

        /// <summary>
        /// Liczba poddziałów
        /// </summary>
        public int SubDepartmentsCount => SubDepartments?.Count(sd => sd.IsActive) ?? 0;

        /// <summary>
        /// Czy dział ma poddziały
        /// </summary>
        public bool HasSubDepartments => SubDepartmentsCount > 0;

        /// <summary>
        /// Lista wszystkich użytkowników w dziale i poddziałach (rekurencyjnie)
        /// </summary>
        public List<User> AllUsers
        {
            get
            {
                var allUsers = Users.Where(u => u.IsActive).ToList();

                foreach (var subDept in SubDepartments.Where(sd => sd.IsActive))
                {
                    allUsers.AddRange(subDept.AllUsers);
                }

                return allUsers;
            }
        }

        /// <summary>
        /// Lista wszystkich poddziałów (rekurencyjnie)
        /// </summary>
        public List<Department> AllSubDepartments
        {
            get
            {
                var allSubs = new List<Department>();

                foreach (var subDept in SubDepartments.Where(sd => sd.IsActive))
                {
                    allSubs.Add(subDept);
                    allSubs.AddRange(subDept.AllSubDepartments);
                }

                return allSubs;
            }
        }

        // ===== METODY POMOCNICZE =====

        /// <summary>
        /// Sprawdza czy dany dział jest poddziałem tego działu (rekurencyjnie)
        /// </summary>
        /// <param name="departmentId">ID działu do sprawdzenia</param>
        /// <returns>True jeśli jest poddziałem</returns>
        public bool IsParentOf(string departmentId)
        {
            return AllSubDepartments.Any(sd => sd.Id == departmentId);
        }

        /// <summary>
        /// Sprawdza czy dany dział jest działem nadrzędnym tego działu (rekurencyjnie)
        /// </summary>
        /// <param name="departmentId">ID działu do sprawdzenia</param>
        /// <returns>True jeśli jest działem nadrzędnym</returns>
        public bool IsChildOf(string departmentId)
        {
            var parent = ParentDepartment;
            while (parent != null)
            {
                if (parent.Id == departmentId)
                    return true;
                parent = parent.ParentDepartment;
            }
            return false;
        }

        /// <summary>
        /// Pobiera wszystkich przełożonych w hierarchii (dział nadrzędny i wyżej)
        /// </summary>
        /// <returns>Lista działów nadrzędnych</returns>
        public List<Department> GetParentChain()
        {
            var parents = new List<Department>();
            var parent = ParentDepartment;

            while (parent != null)
            {
                parents.Add(parent);
                parent = parent.ParentDepartment;
            }

            return parents;
        }
    }
}