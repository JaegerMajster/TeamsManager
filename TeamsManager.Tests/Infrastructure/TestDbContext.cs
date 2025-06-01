// Plik: TeamsManager.Tests/Infrastructure/TestDbContext.cs
using Microsoft.EntityFrameworkCore;
using TeamsManager.Data;
using TeamsManager.Core.Abstractions; // Potrzebne dla ICurrentUserService
using System.Threading.Tasks; // Dla Task
using System.Threading; // Dla CancellationToken
using System; // Dla IDisposable, Guid

namespace TeamsManager.Tests.Infrastructure
{
    public class TestDbContext : TeamsManagerDbContext
    {
        private bool _bypassAuditFields = false;
        private readonly ICurrentUserService _testSpecificCurrentUserService; // Przechowujemy dla nadpisania GetCurrentUser

        // Ten konstruktor powinien być używany przez DI w IntegrationTestBase
        public TestDbContext(DbContextOptions<TeamsManagerDbContext> options, ICurrentUserService currentUserService)
            : base(options, currentUserService) // Przekazujemy currentUserService do bazowego TeamsManagerDbContext
        {
            _testSpecificCurrentUserService = currentUserService;
        }

        // Jeśli istnieje drugi konstruktor public TestDbContext(DbContextOptions<TeamsManagerDbContext> options) : base(options),
        // to DI może wybrać go, jeśli nie będzie w stanie rozwiązać ICurrentUserService podczas tworzenia TestDbContext.
        // Usuń go lub uczyń go mniej preferowanym (np. internal, jeśli to możliwe i potrzebne dla innych celów).
        // Dla naszych celów, chcemy, aby DI ZAWSZE używało konstruktora z ICurrentUserService.

        public void DisableAutoAudit()
        {
            _bypassAuditFields = true;
        }

        public void EnableAutoAudit()
        {
            _bypassAuditFields = false;
        }

        protected override string GetCurrentUser()
        {
            // Ta metoda nadpisuje GetCurrentUser z TeamsManagerDbContext.
            // Zapewnia, że logika audytu w TeamsManagerDbContext (SetAuditFields)
            // użyje użytkownika z TestCurrentUserService, który jest kontrolowany w testach.
            return _testSpecificCurrentUserService?.GetCurrentUserUpn() ?? "test_user_fallback_TestDbContext";
        }

        public override int SaveChanges()
        {
            if (_bypassAuditFields)
            {
                // Jeśli omijamy audyt, musimy znaleźć sposób, aby base.SetAuditFields() nie zostało wywołane,
                // lub aby było ono nieszkodliwe. Nadpisanie GetCurrentUser() powinno wystarczyć,
                // jeśli SetAuditFields w bazie tylko ustawia pola i nie robi niczego innego.
                // Jeśli _bypassAuditFields ma całkowicie pominąć logikę SetAuditFields z TeamsManagerDbContext,
                // to SetAuditFields w TeamsManagerDbContext musiałoby być virtual i tutaj nadpisane,
                // aby nic nie robić, gdy _bypassAuditFields == true.

                // Na razie zakładamy, że produkcyjne SetAuditFields (z użytkownikiem testowym) jest akceptowalne,
                // a _bypassAuditFields jest na wypadek, gdybyśmy chcieli ręcznie ustawić pola audytu w teście.
                return base.SaveChanges();
            }
            return base.SaveChanges(); // To wywoła SetAuditFields z TeamsManagerDbContext, które użyje nadpisanego GetCurrentUser.
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_bypassAuditFields)
            {
                return await base.SaveChangesAsync(true, cancellationToken); // Przekazujemy true, aby EF Core wiedziało, że zmiany są akceptowane
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}