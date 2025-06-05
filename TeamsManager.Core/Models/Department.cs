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
        // ===== POLA PRYWATNE =====
        private int? _hierarchyLevel;
        private bool _hierarchyLevelCalculated = false;

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
                if (_hierarchyLevelCalculated && _hierarchyLevel.HasValue)
                    return _hierarchyLevel.Value;

                var visited = new HashSet<string>();
                var level = 0;
                var current = this;
                
                // Dodaj bieżący element aby wykryć cykl zawierający sam siebie
                if (!string.IsNullOrEmpty(current.Id))
                    visited.Add(current.Id);
                
                var parent = ParentDepartment;
                const int maxDepth = 100; // Zabezpieczenie przed zbyt głęboką hierarchią
                
                while (parent != null && level < maxDepth)
                {
                    if (!string.IsNullOrEmpty(parent.Id))
                    {
                        if (!visited.Add(parent.Id))
                        {
                            // Wykryto cykl! Logowanie jest opcjonalne - może wymagać ILogger
                            // Zwracamy aktualny poziom zamiast rzucać wyjątek
                            break;
                        }
                    }
                    
                    level++;
                    parent = parent.ParentDepartment;
                }
                
                _hierarchyLevel = level;
                _hierarchyLevelCalculated = true;
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
                var visited = new HashSet<string>();
                var current = this;
                const int maxDepth = 100;
                var depth = 0;

                while (current != null && depth < maxDepth)
                {
                    if (!string.IsNullOrEmpty(current.Id))
                    {
                        if (!visited.Add(current.Id))
                        {
                            // Wykryto cykl - dodaj marker i przerwij
                            path.Insert(0, "[CYKL]");
                            break;
                        }
                    }
                    
                    path.Insert(0, current.Name);
                    current = current.ParentDepartment;
                    depth++;
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
                var visited = new HashSet<string>();
                
                // Dodaj bieżący departament do visited
                if (!string.IsNullOrEmpty(Id))
                    visited.Add(Id);
                
                CollectSubDepartmentsRecursive(this, allSubs, visited);
                return allSubs;
            }
        }

        // ===== METODY POMOCNICZE =====

        /// <summary>
        /// Nowa metoda pomocnicza do bezpiecznego zbierania poddziałów
        /// </summary>
        private void CollectSubDepartmentsRecursive(Department parent, List<Department> result, HashSet<string> visited)
        {
            foreach (var subDept in parent.SubDepartments.Where(sd => sd.IsActive))
            {
                if (!string.IsNullOrEmpty(subDept.Id) && !visited.Add(subDept.Id))
                {
                    // Cykl wykryty - pomijamy ten poddział
                    continue;
                }
                
                result.Add(subDept);
                CollectSubDepartmentsRecursive(subDept, result, visited);
            }
        }

        // ===== METODY WEWNĘTRZNE =====
        /// <summary>
        /// Invaliduje cache hierarchii - używane przy zmianach struktury
        /// </summary>
        internal void InvalidateHierarchyCache()
        {
            _hierarchyLevel = null;
            _hierarchyLevelCalculated = false;
        }

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
            if (string.IsNullOrEmpty(departmentId))
                return false;
                
            // Element nie może być dzieckiem samego siebie
            if (Id == departmentId)
                return false;
                
            var visited = new HashSet<string>();
            var parent = ParentDepartment;
            const int maxDepth = 100;
            var depth = 0;
            
            // Dodaj bieżący element
            if (!string.IsNullOrEmpty(Id))
                visited.Add(Id);
            
            while (parent != null && depth < maxDepth)
            {
                if (parent.Id == departmentId)
                    return true;
                    
                if (!string.IsNullOrEmpty(parent.Id))
                {
                    if (!visited.Add(parent.Id))
                    {
                        // Cykl - element nie może być dzieckiem w cyklicznej hierarchii
                        return false;
                    }
                }
                
                parent = parent.ParentDepartment;
                depth++;
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
            var visited = new HashSet<string>();
            var parent = ParentDepartment;
            const int maxDepth = 100;
            var depth = 0;
            
            // Dodaj bieżący element do visited
            if (!string.IsNullOrEmpty(Id))
                visited.Add(Id);

            while (parent != null && depth < maxDepth)
            {
                if (!string.IsNullOrEmpty(parent.Id))
                {
                    if (!visited.Add(parent.Id))
                    {
                        // Cykl wykryty - przerywamy
                        break;
                    }
                }
                
                parents.Add(parent);
                parent = parent.ParentDepartment;
                depth++;
            }

            return parents;
        }

    }
}