using Microsoft.EntityFrameworkCore;
using TeamsManager.Data;
using TeamsManager.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsManager.Tests.Infrastructure
{
    /// <summary>
    /// Rozszerzony DbContext dla testów integracyjnych
    /// Dziedziczy z produkcyjnego TeamsManagerDbContext
    /// </summary>
    public class TestDbContext : TeamsManagerDbContext
    {
        private string? _currentTestUser = "test_user";
        private bool _bypassAuditFields = false;

        public TestDbContext(DbContextOptions<TeamsManagerDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Ustawia użytkownika dla bieżącego testu
        /// </summary>
        public void SetTestUser(string userName)
        {
            _currentTestUser = userName;
        }

        /// <summary>
        /// Resetuje użytkownika do domyślnego
        /// </summary>
        public void ResetTestUser()
        {
            _currentTestUser = "test_user";
        }

        /// <summary>
        /// Wyłącza automatyczne wypełnianie pól audytowych
        /// Przydatne gdy test chce kontrolować te wartości
        /// </summary>
        public void DisableAutoAudit()
        {
            _bypassAuditFields = true;
        }

        /// <summary>
        /// Włącza automatyczne wypełnianie pól audytowych
        /// </summary>
        public void EnableAutoAudit()
        {
            _bypassAuditFields = false;
        }

        public override int SaveChanges()
        {
            if (!_bypassAuditFields)
            {
                SetTestAuditFields();
            }
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_bypassAuditFields)
            {
                SetTestAuditFields();
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Ustawia pola audytowe dla testów
        /// Zachowuje wartości już ustawione przez kod testowy
        /// </summary>
        private void SetTestAuditFields()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            var currentTime = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        // Dla nowych encji - ustaw tylko jeśli puste
                        if (entry.Entity.CreatedDate == default)
                        {
                            entry.Entity.CreatedDate = currentTime;
                        }
                        if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy))
                        {
                            entry.Entity.CreatedBy = _currentTestUser ?? "test_system";
                        }
                        if (!entry.Property(e => e.IsActive).IsModified)
                        {
                            entry.Entity.IsActive = true;
                        }
                        break;

                    case EntityState.Modified:
                        // Dla modyfikowanych - NIE nadpisuj jeśli już ustawione
                        var modifiedByProp = entry.Property(e => e.ModifiedBy);
                        var modifiedDateProp = entry.Property(e => e.ModifiedDate);

                        // Sprawdź czy wartość została zmieniona w tym cyklu
                        if (!modifiedByProp.IsModified && string.IsNullOrWhiteSpace(entry.Entity.ModifiedBy))
                        {
                            entry.Entity.ModifiedBy = _currentTestUser ?? "test_system";
                        }

                        if (!modifiedDateProp.IsModified && !entry.Entity.ModifiedDate.HasValue)
                        {
                            entry.Entity.ModifiedDate = currentTime;
                        }
                        break;
                }
            }
        }
    }
}