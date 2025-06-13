using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Jednostka organizacyjna - hierarchiczna struktura organizacji (LO, Semestr I, itp.)
    /// </summary>
    public class OrganizationalUnit : BaseEntity
    {
        /// <summary>
        /// Nazwa jednostki organizacyjnej
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Kod jednostki organizacyjnej (generowany automatycznie)
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// Opis jednostki organizacyjnej
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Identyfikator nadrzędnej jednostki organizacyjnej
        /// </summary>
        [StringLength(36)]
        public string? ParentUnitId { get; set; }

        /// <summary>
        /// Nadrzędna jednostka organizacyjna
        /// </summary>
        [ForeignKey(nameof(ParentUnitId))]
        public virtual OrganizationalUnit? ParentUnit { get; set; }

        /// <summary>
        /// Podrzędne jednostki organizacyjne
        /// </summary>
        public virtual ICollection<OrganizationalUnit> SubUnits { get; set; } = new List<OrganizationalUnit>();

        /// <summary>
        /// Działy przypisane do tej jednostki organizacyjnej
        /// </summary>
        public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

        /// <summary>
        /// Kolejność sortowania
        /// </summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// Czy jest jednostką główną (bez rodzica)
        /// </summary>
        [NotMapped]
        public bool IsRootUnit => string.IsNullOrEmpty(ParentUnitId);

        /// <summary>
        /// Poziom w hierarchii (0 = root)
        /// </summary>
        [NotMapped]
        public int Level
        {
            get
            {
                int level = 0;
                var current = ParentUnit;
                while (current != null)
                {
                    level++;
                    current = current.ParentUnit;
                }
                return level;
            }
        }

        /// <summary>
        /// Pełna ścieżka w hierarchii
        /// </summary>
        [NotMapped]
        public string FullPath
        {
            get
            {
                var path = new List<string>();
                var current = this;
                while (current != null)
                {
                    path.Insert(0, current.Name);
                    current = current.ParentUnit;
                }
                return string.Join(" → ", path);
            }
        }
    }
} 