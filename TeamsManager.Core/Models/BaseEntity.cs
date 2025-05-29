using System;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Klasa bazowa dla wszystkich encji w systemie
    /// Zawiera wspólne pola do audytu i śledzenia zmian
    /// </summary>
    public abstract class BaseEntity
    {
        public string Id { get; set; } = string.Empty;

        // Pola audytu - kto i kiedy utworzył
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty; // UPN użytkownika który utworzył

        // Pola audytu - kto i kiedy zmodyfikował
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; } // UPN użytkownika który zmodyfikował

        // Standardowe pole do soft delete
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Metoda do oznaczania encji jako zmodyfikowanej
        /// </summary>
        /// <param name="modifiedBy">UPN użytkownika wykonującego modyfikację</param>
        public void MarkAsModified(string modifiedBy)
        {
            ModifiedDate = DateTime.UtcNow;
            ModifiedBy = modifiedBy;
        }

        /// <summary>
        /// Metoda do soft delete encji
        /// </summary>
        /// <param name="deletedBy">UPN użytkownika wykonującego usunięcie</param>
        public void MarkAsDeleted(string deletedBy)
        {
            IsActive = false;
            MarkAsModified(deletedBy);
        }
    }
}