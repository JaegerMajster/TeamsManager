using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;
using TeamsManager.Tests.Repositories;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System.Collections.Generic;

namespace TeamsManager.Tests.Performance
{
    [Collection("Sequential")]
    public class RepositoryPerformanceTests : RepositoryTestBase
    {
        private readonly ITestOutputHelper _output;
        private readonly ITeamRepository _teamRepository;
        private readonly IUserRepository _userRepository;

        public RepositoryPerformanceTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _teamRepository = GetRepository<ITeamRepository>();
            _userRepository = GetRepository<IUserRepository>();
        }

        [Fact]
        public async Task ComparePerformance_TeamRepositoryMethods()
        {
            // Arrange - utwórz dane testowe
            await SeedLargeDataset();

            // Test 1: GetTeamByNameAsync (bez Include)
            var sw1 = Stopwatch.StartNew();
            var team1 = await _teamRepository.GetTeamByNameAsync("Test Team 50");
            sw1.Stop();
            
            // Wymuszenie lazy loading dla porównania
            if (team1 != null)
            {
                _ = team1.Members?.Count;
                _ = team1.Channels?.Count;
            }
            var timeWithLazyLoading = sw1.ElapsedMilliseconds;

            // Test 2: GetActiveTeamByNameAsync (z Include)
            var sw2 = Stopwatch.StartNew();
            var team2 = await _teamRepository.GetActiveTeamByNameAsync("Test Team 50");
            sw2.Stop();
            var timeWithEagerLoading = sw2.ElapsedMilliseconds;

            // Analiza
            _output.WriteLine($"=== Analiza wydajności TeamRepository ===");
            _output.WriteLine($"GetTeamByNameAsync (lazy): {timeWithLazyLoading}ms");
            _output.WriteLine($"GetActiveTeamByNameAsync (eager): {timeWithEagerLoading}ms");
            _output.WriteLine($"Różnica: {timeWithEagerLoading - timeWithLazyLoading}ms");

            // Test pamięci
            var querySql1 = Context.Teams.Where(t => t.DisplayName == "Test Team 50").ToQueryString();
            var querySql2 = Context.Teams
                .Include(t => t.SchoolType)
                .Include(t => t.SchoolYear)
                .Include(t => t.Template)
                .Include(t => t.Members).ThenInclude(m => m.User)
                .Include(t => t.Channels)
                .Where(t => t.DisplayName == "Test Team 50" && t.Status == TeamStatus.Active)
                .ToQueryString();

            _output.WriteLine("\n=== Generowane zapytania SQL ===");
            _output.WriteLine("Bez Include:");
            _output.WriteLine(querySql1);
            _output.WriteLine("\nZ Include:");
            _output.WriteLine(querySql2);
        }

        [Fact]
        public async Task MeasureImpact_UserRepositoryIncludes()
        {
            // Podobny test dla UserRepository
            await SeedLargeDataset();

            var iterations = 10;
            long totalTimeWithoutInclude = 0;
            long totalTimeWithInclude = 0;

            for (int i = 0; i < iterations; i++)
            {
                // Bez Include
                var sw1 = Stopwatch.StartNew();
                await _userRepository.GetUserByUpnAsync($"user{i}@example.com");
                sw1.Stop();
                totalTimeWithoutInclude += sw1.ElapsedMilliseconds;

                // Z Include
                var sw2 = Stopwatch.StartNew();
                await _userRepository.GetActiveUserByUpnAsync($"user{i}@example.com");
                sw2.Stop();
                totalTimeWithInclude += sw2.ElapsedMilliseconds;
            }

            var avgWithoutInclude = totalTimeWithoutInclude / iterations;
            var avgWithInclude = totalTimeWithInclude / iterations;

            _output.WriteLine($"=== Średnie czasy dla {iterations} iteracji ===");
            _output.WriteLine($"GetUserByUpnAsync: {avgWithoutInclude}ms");
            _output.WriteLine($"GetActiveUserByUpnAsync: {avgWithInclude}ms");
            _output.WriteLine($"Overhead Include: {avgWithInclude - avgWithoutInclude}ms ({((avgWithInclude - avgWithoutInclude) * 100.0 / avgWithoutInclude):F1}%)");
        }

        private async Task SeedLargeDataset()
        {
            await CleanDatabaseAsync();

            // Utwórz dane podstawowe
            var department = new Department 
            { 
                Id = "dept-1", 
                Name = "Test Department",
                IsActive = true 
            };
            Context.Departments.Add(department);

            var schoolType = new SchoolType 
            { 
                Id = "st-1", 
                ShortName = "TST",
                FullName = "Test School Type",
                IsActive = true 
            };
            Context.SchoolTypes.Add(schoolType);

            var schoolYear = new SchoolYear 
            { 
                Id = "sy-1", 
                Name = "Test School Year",
                IsActive = true 
            };
            Context.SchoolYears.Add(schoolYear);

            var template = new TeamTemplate 
            { 
                Id = "tt-1", 
                Name = "Test Template",
                IsActive = true 
            };
            Context.TeamTemplates.Add(template);

            await SaveChangesAsync();

            // Najpierw dodaj wszystkich użytkowników
            var users = new List<User>();
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    var user = new User
                    {
                        Id = $"user-{i}-{j}",
                        UPN = $"user{i}-{j}@example.com",
                        FirstName = $"User{i}",
                        LastName = $"Test{j}",
                        IsActive = j % 5 != 0,
                        Role = UserRole.Nauczyciel,
                        DepartmentId = department.Id
                    };
                    users.Add(user);
                    Context.Users.Add(user);
                }
            }

            // Dodaj dodatkowych użytkowników (do 10 iteracji w teście)
            for (int i = 0; i < 10; i++)
            {
                var user = new User
                {
                    Id = $"perf-user-{i}",
                    UPN = $"user{i}@example.com",
                    FirstName = $"Performance",
                    LastName = $"User{i}",
                    IsActive = true,
                    Role = UserRole.Nauczyciel,
                    DepartmentId = department.Id
                };
                users.Add(user);
                Context.Users.Add(user);
            }

            await SaveChangesAsync();

            // Teraz dodaj zespoły
            var teams = new List<Team>();
            for (int i = 0; i < 100; i++)
            {
                var team = new Team
                {
                    Id = $"team-{i}",
                    DisplayName = $"Test Team {i}",
                    Description = $"Description for team {i}",
                    Status = i % 10 == 0 ? TeamStatus.Archived : TeamStatus.Active,
                    Visibility = TeamVisibility.Private,
                    Owner = $"owner{i}@example.com",
                    SchoolTypeId = schoolType.Id,
                    SchoolYearId = schoolYear.Id,
                    TemplateId = template.Id
                };
                teams.Add(team);
                Context.Teams.Add(team);
            }

            await SaveChangesAsync();

            // Dodaj członków zespołów
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    var teamMember = new TeamMember
                    {
                        Id = $"tm-{i}-{j}",
                        TeamId = $"team-{i}",
                        UserId = $"user-{i}-{j}",
                        Role = TeamMemberRole.Member
                    };
                    Context.TeamMembers.Add(teamMember);
                }
            }

            await SaveChangesAsync();

            // Dodaj kanały
            for (int i = 0; i < 100; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    var channel = new Channel
                    {
                        Id = $"channel-{i}-{k}",
                        DisplayName = $"Channel {k}",
                        TeamId = $"team-{i}"
                    };
                    Context.Channels.Add(channel);
                }
            }

            await SaveChangesAsync();
        }
    }
} 