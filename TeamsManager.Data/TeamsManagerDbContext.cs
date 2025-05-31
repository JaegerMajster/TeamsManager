using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Models;
using TeamsManager.Core.Abstractions;

namespace TeamsManager.Data
{
    public class TeamsManagerDbContext : DbContext
    {
        private readonly ICurrentUserService? _currentUserService;

        // Konstruktor dla produkcji - z ICurrentUserService
        public TeamsManagerDbContext(
            DbContextOptions<TeamsManagerDbContext> options,
            ICurrentUserService currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }
        public TeamsManagerDbContext(DbContextOptions<TeamsManagerDbContext> options) : base(options)
        {
            _currentUserService = null;
        }

        // ===== DEFINICJA TABEL =====

        // Podstawowe encje
        public DbSet<User> Users { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<SchoolType> SchoolTypes { get; set; }
        public DbSet<SchoolYear> SchoolYears { get; set; }
        public DbSet<Subject> Subjects { get; set; }

        // Zespoły i ich elementy
        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamMember> TeamMembers { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<TeamTemplate> TeamTemplates { get; set; }

        // Tabele pośrednie i pomocnicze
        public DbSet<UserSchoolType> UserSchoolTypes { get; set; }
        public DbSet<OperationHistory> OperationHistories { get; set; }
        public DbSet<ApplicationSetting> ApplicationSettings { get; set; }
        public DbSet<UserSubject> UserSubjects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== KONFIGURACJA WSPÓLNA DLA WSZYSTKICH ENCJI BASEENTITY =====
            ConfigureBaseEntity<User>(modelBuilder);
            ConfigureBaseEntity<Department>(modelBuilder);
            ConfigureBaseEntity<SchoolType>(modelBuilder);
            ConfigureBaseEntity<SchoolYear>(modelBuilder);
            ConfigureBaseEntity<Team>(modelBuilder);
            ConfigureBaseEntity<TeamMember>(modelBuilder);
            ConfigureBaseEntity<Channel>(modelBuilder);
            ConfigureBaseEntity<TeamTemplate>(modelBuilder);
            ConfigureBaseEntity<UserSchoolType>(modelBuilder);
            ConfigureBaseEntity<OperationHistory>(modelBuilder);
            ConfigureBaseEntity<ApplicationSetting>(modelBuilder);
            ConfigureBaseEntity<Subject>(modelBuilder);
            ConfigureBaseEntity<UserSubject>(modelBuilder);

            // ===== KONFIGURACJA UŻYTKOWNIKÓW =====
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(u => u.LastName).IsRequired().HasMaxLength(50);
                entity.Property(u => u.UPN).IsRequired().HasMaxLength(100);
                entity.Property(u => u.DepartmentId).IsRequired();
                entity.Property(u => u.Phone).HasMaxLength(20);
                entity.Property(u => u.AlternateEmail).HasMaxLength(100);
                entity.Property(u => u.ExternalId).HasMaxLength(50);
                entity.Property(u => u.Position).HasMaxLength(100);
                entity.Property(u => u.Notes).HasMaxLength(1000);

                // Konwersja enum UserRole na liczbę całkowitą
                entity.Property(u => u.Role).HasConversion<int>();

                // Indeks unikalny na UPN dla szybkiego wyszukiwania i logowania
                entity.HasIndex(u => u.UPN).IsUnique();

                // Indeks na ExternalId dla integracji z systemami zewnętrznymi
                entity.HasIndex(u => u.ExternalId);

                // Relacja z działem
                entity.HasOne(u => u.Department)
                      .WithMany(d => d.Users)
                      .HasForeignKey(u => u.DepartmentId)
                      .OnDelete(DeleteBehavior.Restrict); // Nie można usunąć działu jeśli ma przypisanych użytkowników

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(u => u.FullName);
                entity.Ignore(u => u.DisplayName);
                entity.Ignore(u => u.Email);
                entity.Ignore(u => u.Initials);
                entity.Ignore(u => u.Age);
                entity.Ignore(u => u.YearsOfService);
                entity.Ignore(u => u.RoleDisplayName);
                entity.Ignore(u => u.ActiveMembershipsCount);
                entity.Ignore(u => u.OwnedTeamsCount);
                entity.Ignore(u => u.AssignedSchoolTypes);
                entity.Ignore(u => u.CanManageTeams);
                entity.Ignore(u => u.CanManageUsers);
                entity.Ignore(u => u.HasAdminRights);
                entity.Ignore(u => u.DefaultTeamRole);
            });

            // ===== KONFIGURACJA DZIAŁÓW =====
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
                entity.Property(d => d.Description).HasMaxLength(500);
                entity.Property(d => d.DepartmentCode).HasMaxLength(20);
                entity.Property(d => d.Email).HasMaxLength(100);
                entity.Property(d => d.Phone).HasMaxLength(20);
                entity.Property(d => d.Location).HasMaxLength(200);

                // Indeks na nazwie działu dla szybkiego wyszukiwania
                entity.HasIndex(d => d.Name);
                entity.HasIndex(d => d.DepartmentCode);

                // Relacja hierarchiczna - dział nadrzędny
                entity.HasOne(d => d.ParentDepartment)
                      .WithMany(d => d.SubDepartments)
                      .HasForeignKey(d => d.ParentDepartmentId)
                      .OnDelete(DeleteBehavior.Restrict); // Nie można usunąć działu nadrzędnego jeśli ma poddziały

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(d => d.IsRootDepartment);
                entity.Ignore(d => d.HierarchyLevel);
                entity.Ignore(d => d.FullPath);
                entity.Ignore(d => d.DirectUsersCount);
                entity.Ignore(d => d.TotalUsersCount);
                entity.Ignore(d => d.SubDepartmentsCount);
                entity.Ignore(d => d.HasSubDepartments);
                entity.Ignore(d => d.AllUsers);
                entity.Ignore(d => d.AllSubDepartments);
            });

            // ===== KONFIGURACJA TYPÓW SZKÓŁ =====
            modelBuilder.Entity<SchoolType>(entity =>
            {
                entity.HasKey(st => st.Id);
                entity.Property(st => st.ShortName).IsRequired().HasMaxLength(10);
                entity.Property(st => st.FullName).IsRequired().HasMaxLength(200);
                entity.Property(st => st.Description).HasMaxLength(500);
                entity.Property(st => st.ColorCode).HasMaxLength(7); // Format #RRGGBB

                // Indeks unikalny na ShortName
                entity.HasIndex(st => st.ShortName).IsUnique();
                entity.HasIndex(st => st.FullName);

                // Relacja wiele-do-wielu z wicedyrektorami (nadzór nad typami szkół)
                entity.HasMany(st => st.SupervisingViceDirectors)
                      .WithMany(u => u.SupervisedSchoolTypes)
                      .UsingEntity(j => j.ToTable("UserSchoolTypeSupervision"));

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(st => st.DisplayName);
                entity.Ignore(st => st.ActiveTeamsCount);
                entity.Ignore(st => st.AssignedTeachersCount);
                entity.Ignore(st => st.AssignedTeachers);
            });

            // ===== KONFIGURACJA LAT SZKOLNYCH =====
            modelBuilder.Entity<SchoolYear>(entity =>
            {
                entity.HasKey(sy => sy.Id);
                entity.Property(sy => sy.Name).IsRequired().HasMaxLength(20); // np. "2024/2025"
                entity.Property(sy => sy.Description).HasMaxLength(500);

                // Indeks na nazwie roku szkolnego
                entity.HasIndex(sy => sy.Name).IsUnique();
                entity.HasIndex(sy => sy.IsCurrent);

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(sy => sy.HasStarted);
                entity.Ignore(sy => sy.HasEnded);
                entity.Ignore(sy => sy.IsCurrentlyActive);
                entity.Ignore(sy => sy.DaysRemaining);
                entity.Ignore(sy => sy.CompletionPercentage);
                entity.Ignore(sy => sy.CurrentSemester);
                entity.Ignore(sy => sy.ActiveTeamsCount);
            });

            // ===== KONFIGURACJA PRZEDMIOTÓW =====
            modelBuilder.Entity<Subject>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
                entity.Property(s => s.Code).HasMaxLength(20);
                entity.Property(s => s.Description).HasMaxLength(1000);
                entity.Property(s => s.Category).HasMaxLength(100);

                entity.HasIndex(s => s.Name);
                entity.HasIndex(s => s.Code).IsUnique(false); // Kod może nie być unikalny globalnie, ale warto go indeksować

                // Opcjonalna relacja jeden-do-wielu z SchoolType (jeden typ szkoły może mieć wiele przedmiotów "domyślnych")
                entity.HasOne(s => s.DefaultSchoolType)
                      .WithMany() // Zakładamy, że SchoolType nie ma bezpośredniej kolekcji "DefaultSubjects"
                      .HasForeignKey(s => s.DefaultSchoolTypeId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ===== KONFIGURACJA PRZYPISAŃ NAUCZYCIEL-PRZEDMIOT =====
            modelBuilder.Entity<UserSubject>(entity =>
            {
                entity.HasKey(us => us.Id);
                entity.Property(us => us.UserId).IsRequired();
                entity.Property(us => us.SubjectId).IsRequired();
                entity.Property(us => us.Notes).HasMaxLength(500);

                // Indeks unikalny, aby jeden nauczyciel nie mógł być wielokrotnie przypisany do tego samego przedmiotu
                // (chyba że rozróżniamy przypisania np. datą lub innym atrybutem - wtedy ten indeks może być inny)
                entity.HasIndex(us => new { us.UserId, us.SubjectId }).IsUnique();

                // Relacja z User
                entity.HasOne(us => us.User)
                      .WithMany(u => u.TaughtSubjects) // Odwołanie do nowej kolekcji w User.cs
                      .HasForeignKey(us => us.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // Usunięcie użytkownika usuwa jego przypisania do przedmiotów

                // Relacja z Subject
                entity.HasOne(us => us.Subject)
                      .WithMany(s => s.TeacherAssignments) // Odwołanie do kolekcji w Subject.cs
                      .HasForeignKey(us => us.SubjectId)
                      .OnDelete(DeleteBehavior.Cascade); // Usunięcie przedmiotu usuwa jego przypisania do nauczycieli
            });

            // ===== KONFIGURACJA ZESPOŁÓW =====
            modelBuilder.Entity<Team>(entity =>
            {
                entity.Property(t => t.DisplayName).IsRequired().HasMaxLength(200); // Pozostaje lub dostosuj MaxLength
                entity.Property(t => t.Description).HasMaxLength(1000); // Pozostaje lub dostosuj MaxLength
                entity.Property(t => t.Owner).IsRequired().HasMaxLength(100); // Pozostaje
                entity.Property(t => t.Visibility).HasConversion<int>();

                // Status i pola związane ze zmianą statusu
                entity.Property(t => t.Status).HasConversion<int>().IsRequired(); // Upewnij się, że jest IsRequired
                entity.Property(t => t.StatusChangeDate); // DateTime? jest domyślnie nullowalne
                entity.Property(t => t.StatusChangedBy).HasMaxLength(100); // UPN osoby zmieniającej status
                entity.Property(t => t.StatusChangeReason).HasMaxLength(500); // Powód zmiany statusu

                // Klucze obce i inne właściwości
                entity.Property(t => t.TemplateId); // Nullable string, MaxLength może być zdefiniowane przez typ klucza w TeamTemplate
                entity.Property(t => t.SchoolTypeId); // Nullable string
                entity.Property(t => t.SchoolYearId); // Nullable string

                entity.Property(t => t.AcademicYear).HasMaxLength(20);
                entity.Property(t => t.Semester).HasMaxLength(50); // Zwiększyłem trochę, np. "Semestr Letni Dodatkowy"
                entity.Property(t => t.ExternalId).HasMaxLength(100); // Zwiększyłem trochę dla elastyczności
                entity.Property(t => t.CourseCode).HasMaxLength(50); // Zwiększyłem
                entity.Property(t => t.Level).HasMaxLength(100);
                entity.Property(t => t.Language).HasMaxLength(50);
                entity.Property(t => t.Tags).HasMaxLength(500);
                entity.Property(t => t.Notes).HasColumnType("TEXT"); // Dla dłuższych notatek
                                                                     // IsVisible i RequiresApproval są bool, domyślnie OK
                entity.Property(t => t.LastActivityDate);

                // Indeksy
                entity.HasIndex(t => t.DisplayName);
                entity.HasIndex(t => t.Status);
                entity.HasIndex(t => t.Owner);
                entity.HasIndex(t => t.ExternalId);
                entity.HasIndex(t => t.SchoolTypeId);
                entity.HasIndex(t => t.SchoolYearId);
                entity.HasIndex(t => t.TemplateId);
                entity.HasIndex(t => t.IsActive); // Indeks na IsActive z BaseEntity

                // Relacje (pozostają takie same jak wcześniej, ale weryfikujemy)
                entity.HasOne(t => t.Template)
                      .WithMany(tt => tt.Teams)
                      .HasForeignKey(t => t.TemplateId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.SchoolType)
                      .WithMany(st => st.Teams)
                      .HasForeignKey(t => t.SchoolTypeId)
                      .OnDelete(DeleteBehavior.SetNull); // Lub Restrict, jeśli typ szkoły nie może być usunięty, gdy ma zespoły

                entity.HasOne(t => t.SchoolYear)
                      .WithMany(sy => sy.Teams)
                      .HasForeignKey(t => t.SchoolYearId)
                      .OnDelete(DeleteBehavior.SetNull); // Lub Restrict

                entity.HasMany(t => t.Members)
                      .WithOne(m => m.Team)
                      .HasForeignKey(m => m.TeamId)
                      .OnDelete(DeleteBehavior.Cascade); // Usunięcie Team usunie jego TeamMember

                entity.HasMany(t => t.Channels)
                      .WithOne(c => c.Team)
                      .HasForeignKey(c => c.TeamId)
                      .OnDelete(DeleteBehavior.Cascade); // Usunięcie Team usunie jego Channel

                // Ignorowanie właściwości obliczanych (lista powinna być kompletna)
                entity.Ignore(t => t.IsEffectivelyActive);
                entity.Ignore(t => t.IsFullyOperational); // Nowa nazwa
                entity.Ignore(t => t.MemberCount);
                entity.Ignore(t => t.OwnerCount);
                entity.Ignore(t => t.RegularMemberCount);
                entity.Ignore(t => t.AllActiveUsers);
                entity.Ignore(t => t.Owners);
                entity.Ignore(t => t.RegularMembers);
                entity.Ignore(t => t.IsAtCapacity);
                entity.Ignore(t => t.CapacityPercentage);
                entity.Ignore(t => t.ChannelCount);
                entity.Ignore(t => t.DaysUntilEnd);
                entity.Ignore(t => t.DaysSinceStart);
                entity.Ignore(t => t.CompletionPercentage);
                entity.Ignore(t => t.DisplayNameWithStatus);
                entity.Ignore(t => t.ShortDescription);
            });

            // ===== KONFIGURACJA CZŁONKÓW ZESPOŁU =====
            modelBuilder.Entity<TeamMember>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.TeamId).IsRequired();
                entity.Property(m => m.UserId).IsRequired(); // Wymagane - każdy członek musi być powiązany z użytkownikiem
                entity.Property(m => m.AddedBy).HasMaxLength(100);
                entity.Property(m => m.RemovedBy).HasMaxLength(100);
                entity.Property(m => m.RoleChangedBy).HasMaxLength(100);
                entity.Property(m => m.ApprovedBy).HasMaxLength(100);
                entity.Property(m => m.RemovalReason).HasMaxLength(500);
                entity.Property(m => m.CustomPermissions).HasMaxLength(1000);
                entity.Property(m => m.Notes).HasMaxLength(1000);
                entity.Property(m => m.Source).HasMaxLength(50);

                // Konwersja enum TeamMemberRole na liczbę całkowitą
                entity.Property(m => m.Role).HasConversion<int>();
                entity.Property(m => m.PreviousRole).HasConversion<int>();

                // Indeks unikalny - jeden użytkownik może być w zespole tylko raz
                entity.HasIndex(m => new { m.UserId, m.TeamId }).IsUnique();
                entity.HasIndex(m => m.Role);
                entity.HasIndex(m => m.IsApproved);

                // Relacja z użytkownikiem (wymagana)
                entity.HasOne(m => m.User)
                      .WithMany(u => u.TeamMemberships)
                      .HasForeignKey(m => m.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // Usunięcie Użytkownika usunie jego Członkostwa w zespołach

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(m => m.Email);
                entity.Ignore(m => m.DisplayName);
                entity.Ignore(m => m.FullName);
                entity.Ignore(m => m.IsMembershipActive);
                entity.Ignore(m => m.IsOwner);
                entity.Ignore(m => m.IsMember);
                entity.Ignore(m => m.IsPendingApproval);
                entity.Ignore(m => m.DaysInTeam);
                entity.Ignore(m => m.IsRecentlyAdded);
                entity.Ignore(m => m.IsRecentlyActive);
                entity.Ignore(m => m.RoleDescription);
                entity.Ignore(m => m.MembershipStatus);
                entity.Ignore(m => m.MembershipSummary);
            });

            // ===== KONFIGURACJA KANAŁÓW =====
            modelBuilder.Entity<Channel>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.DisplayName).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Description).HasMaxLength(500);
                entity.Property(c => c.TeamId).IsRequired();
                entity.Property(c => c.ChannelType).HasMaxLength(20);

                // Konfiguracja dla pól związanych ze statusem
                entity.Property(c => c.Status).HasConversion<int>();
                entity.Property(c => c.StatusChangedBy).HasMaxLength(100);
                entity.Property(c => c.StatusChangeReason).HasMaxLength(500); 
                
                // Istniejące konfiguracje dla innych pól...
                entity.Property(c => c.NotificationSettings).HasMaxLength(1000);
                entity.Property(c => c.Category).HasMaxLength(50);
                entity.Property(c => c.Tags).HasMaxLength(500);
                entity.Property(c => c.ExternalUrl).HasMaxLength(500);

                // Indeks na nazwie kanału i zespole
                entity.HasIndex(c => c.DisplayName);
                entity.HasIndex(c => new { c.TeamId, c.DisplayName });
                entity.HasIndex(c => c.IsGeneral);
                entity.HasIndex(c => c.IsPrivate);

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(c => c.IsCurrentlyActive);
                entity.Ignore(c => c.IsRecentlyActive);
                entity.Ignore(c => c.DaysSinceLastActivity);
                entity.Ignore(c => c.DaysSinceLastMessage);
                entity.Ignore(c => c.FilesSizeFormatted);
                entity.Ignore(c => c.StatusDescription);
                entity.Ignore(c => c.ActivityLevel);
                entity.Ignore(c => c.ShortSummary);
            });

            // ===== KONFIGURACJA SZABLONÓW ZESPOŁÓW =====
            modelBuilder.Entity<TeamTemplate>(entity =>
            {
                entity.HasKey(tt => tt.Id);
                entity.Property(tt => tt.Name).IsRequired().HasMaxLength(100);
                entity.Property(tt => tt.Template).IsRequired().HasMaxLength(500);
                entity.Property(tt => tt.Description).HasMaxLength(1000);
                entity.Property(tt => tt.ExampleOutput).HasMaxLength(300);
                entity.Property(tt => tt.Category).HasMaxLength(50);
                entity.Property(tt => tt.Language).HasMaxLength(20);
                entity.Property(tt => tt.Prefix).HasMaxLength(50);
                entity.Property(tt => tt.Suffix).HasMaxLength(50);
                entity.Property(tt => tt.Separator).HasMaxLength(10);

                // Indeks na nazwie szablonu
                entity.HasIndex(tt => tt.Name);
                entity.HasIndex(tt => tt.IsDefault);
                entity.HasIndex(tt => tt.IsUniversal);
                entity.HasIndex(tt => tt.Category);

                // Relacja z typem szkoły (opcjonalna dla szablonów uniwersalnych)
                entity.HasOne(tt => tt.SchoolType)
                      .WithMany(st => st.Templates)
                      .HasForeignKey(tt => tt.SchoolTypeId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(tt => tt.Placeholders);
                entity.Ignore(tt => tt.HasPlaceholders);
                entity.Ignore(tt => tt.PlaceholderCount);
                entity.Ignore(tt => tt.DisplayName);
                entity.Ignore(tt => tt.TeamsCreatedCount);
                entity.Ignore(tt => tt.PopularityLevel);
            });

            // ===== KONFIGURACJA PRZYPISAŃ UŻYTKOWNIK-TYP SZKOŁY =====
            modelBuilder.Entity<UserSchoolType>(entity =>
            {
                entity.HasKey(ust => ust.Id);
                entity.Property(ust => ust.UserId).IsRequired();
                entity.Property(ust => ust.SchoolTypeId).IsRequired();
                entity.Property(ust => ust.Notes).HasMaxLength(500);
                entity.Property(ust => ust.WorkloadPercentage).HasColumnType("decimal(5,2)"); // np. 87.50%

                // Indeks unikalny - jeden użytkownik może być przypisany do tego samego typu szkoły tylko raz
                entity.HasIndex(ust => new { ust.UserId, ust.SchoolTypeId }).IsUnique();
                entity.HasIndex(ust => ust.IsCurrentlyActive);

                // Relacja z User
                entity.HasOne(ust => ust.User)
                      .WithMany(u => u.SchoolTypeAssignments)
                      .HasForeignKey(ust => ust.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacja ze SchoolType
                entity.HasOne(ust => ust.SchoolType)
                      .WithMany(st => st.TeacherAssignments)
                      .HasForeignKey(ust => ust.SchoolTypeId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(ust => ust.IsActiveToday);
                entity.Ignore(ust => ust.DaysAssigned);
                entity.Ignore(ust => ust.AssignmentDescription);
            });

            // ===== KONFIGURACJA HISTORII OPERACJI =====
            modelBuilder.Entity<OperationHistory>(entity =>
            {
                entity.HasKey(oh => oh.Id);
                entity.Property(oh => oh.TargetEntityType).IsRequired().HasMaxLength(50);
                entity.Property(oh => oh.TargetEntityId).IsRequired().HasMaxLength(50);
                entity.Property(oh => oh.TargetEntityName).HasMaxLength(200);
                entity.Property(oh => oh.OperationDetails).HasColumnType("TEXT"); // Dla dużych JSON-ów
                entity.Property(oh => oh.ErrorMessage).HasMaxLength(1000);
                entity.Property(oh => oh.ErrorStackTrace).HasColumnType("TEXT");
                entity.Property(oh => oh.UserIpAddress).HasMaxLength(45); // IPv6
                entity.Property(oh => oh.UserAgent).HasMaxLength(500);
                entity.Property(oh => oh.SessionId).HasMaxLength(100);
                entity.Property(oh => oh.ParentOperationId).HasMaxLength(50);
                entity.Property(oh => oh.Tags).HasMaxLength(200);

                // Konwersja enums na liczby całkowite
                entity.Property(oh => oh.Type).HasConversion<int>();
                entity.Property(oh => oh.Status).HasConversion<int>();

                // Indeksy dla wydajnych zapytań
                entity.HasIndex(oh => oh.Type);
                entity.HasIndex(oh => oh.Status);
                entity.HasIndex(oh => oh.TargetEntityType);
                entity.HasIndex(oh => oh.TargetEntityId);
                entity.HasIndex(oh => oh.StartedAt);
                entity.HasIndex(oh => oh.CreatedBy);
                entity.HasIndex(oh => oh.ParentOperationId);

                // Właściwości obliczane - nie przechowywane w bazie danych
                entity.Ignore(oh => oh.IsInProgress);
                entity.Ignore(oh => oh.IsCompleted);
                entity.Ignore(oh => oh.IsSuccessful);
                entity.Ignore(oh => oh.ProgressPercentage);
                entity.Ignore(oh => oh.DurationInSeconds);
                entity.Ignore(oh => oh.StatusDescription);
                entity.Ignore(oh => oh.ShortDescription);
            });

            // ===== KONFIGURACJA USTAWIEŃ APLIKACJI =====
            modelBuilder.Entity<ApplicationSetting>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Key).IsRequired().HasMaxLength(100);
                entity.Property(a => a.Value).IsRequired().HasColumnType("TEXT");
                entity.Property(a => a.Description).HasMaxLength(500);
                entity.Property(a => a.Category).HasMaxLength(50);
                entity.Property(a => a.DefaultValue).HasColumnType("TEXT");
                entity.Property(a => a.ValidationPattern).HasMaxLength(200);
                entity.Property(a => a.ValidationMessage).HasMaxLength(200);

                // Konwersja enum SettingType na liczbę całkowitą
                entity.Property(a => a.Type).HasConversion<int>();

                // Indeks unikalny na kluczu ustawienia
                entity.HasIndex(a => a.Key).IsUnique();
                entity.HasIndex(a => a.Category);
                entity.HasIndex(a => a.Type);
                entity.HasIndex(a => a.IsRequired);
                entity.HasIndex(a => a.IsVisible);
            });
        }

        /// <summary>
        /// Konfiguruje wspólne właściwości dla wszystkich encji dziedziczących z BaseEntity
        /// </summary>
        /// <typeparam name="T">Typ encji dziedziczącej z BaseEntity</typeparam>
        /// <param name="modelBuilder">ModelBuilder</param>
        private void ConfigureBaseEntity<T>(ModelBuilder modelBuilder) where T : BaseEntity
        {
            modelBuilder.Entity<T>(entity =>
            {
                // Konfiguracja pól audytu
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ModifiedBy).HasMaxLength(100);

                // Indeksy na polach audytu dla wydajności
                entity.HasIndex(e => e.CreatedDate);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.CreatedBy);
            });
        }

        /// <summary>
        /// Automatycznie ustawia wartości audytu przed zapisem do bazy danych
        /// </summary>
        /// <returns></returns>
        public override int SaveChanges()
        {
            SetAuditFields();
            return base.SaveChanges();
        }

        /// <summary>
        /// Automatycznie ustawia wartości audytu przed zapisem do bazy danych (async)
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetAuditFields();
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Ustawia pola audytu dla nowych i modyfikowanych encji
        /// </summary>
        private void SetAuditFields()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            var currentUser = GetCurrentUser(); // TODO: Implementuj pobieranie aktualnego użytkownika
            var currentTime = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedDate = currentTime;
                        entry.Entity.CreatedBy = currentUser;
                        // Tylko ustaw IsActive = true jeśli nie zostało explicite ustawione na false
                        if (entry.Entity.IsActive != false)
                        {
                            entry.Entity.IsActive = true;
                        }
                        break;

                    case EntityState.Modified:
                        entry.Entity.ModifiedDate = currentTime;
                        entry.Entity.ModifiedBy = currentUser;
                        break;
                }
            }
        }

        /// <summary>
        /// Pobiera identyfikator aktualnego użytkownika
        /// TODO: Implementuj pobieranie z kontekstu HTTP lub innego źródła
        /// </summary>
        /// <returns>UPN aktualnego użytkownika</returns>
        protected virtual string GetCurrentUser()
        {
            return _currentUserService?.GetCurrentUserUpn() ?? "system@teamsmanager.local";
        }
    }
}