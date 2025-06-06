using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using TeamsManager.Data;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.Data.Repositories;

namespace TeamsManager.Tests.Performance
{
    [Collection("Sequential")]
    public class RepositoryPerformanceTests : IAsyncDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TeamsManagerDbContext _context;
        private readonly TeamRepository _teamRepository;
        private readonly UserRepository _userRepository;

        public RepositoryPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Stwórz prosty InMemory context bez audytu dla testów wydajności
            var options = new DbContextOptionsBuilder<TeamsManagerDbContext>()
                .UseInMemoryDatabase($"PerformanceTest_{Guid.NewGuid()}")
                .EnableSensitiveDataLogging()
                .Options;
            
            _context = new TeamsManagerDbContext(options);
            _teamRepository = new TeamRepository(_context);
            _userRepository = new UserRepository(_context);
        }

        [Fact]
        public async Task ComparePerformance_TeamRepositoryMethods()
        {
            // Arrange - utwórz dane testowe
            await SeedLargeDataset();

            _output.WriteLine("=== ANALIZA WYDAJNOŚCI TeamRepository ===");

            // Test 1: GetTeamByNameAsync (bez Include) - używamy zespołu 51 (aktywny)
            var sw1 = Stopwatch.StartNew();
            var team1 = await _teamRepository.GetTeamByNameAsync("Test Team 51");
            sw1.Stop();
            
            // Wymuszenie lazy loading dla porównania
            long lazyLoadingTime = 0;
            if (team1 != null)
            {
                var sw1b = Stopwatch.StartNew();
                _ = team1.Members?.Count;
                _ = team1.Channels?.Count;
                _ = team1.SchoolType?.FullName;
                sw1b.Stop();
                lazyLoadingTime = sw1b.ElapsedMilliseconds;
            }
            var timeWithLazyLoading = sw1.ElapsedMilliseconds + lazyLoadingTime;

            // Test 2: GetActiveTeamByNameAsync (z Include) - używamy zespołu 51 (aktywny)
            var sw2 = Stopwatch.StartNew();
            var team2 = await _teamRepository.GetActiveTeamByNameAsync("Test Team 51");
            sw2.Stop();
            var timeWithEagerLoading = sw2.ElapsedMilliseconds;

            // Analiza
            _output.WriteLine($"GetTeamByNameAsync (bez Include): {sw1.ElapsedMilliseconds}ms");
            _output.WriteLine($"Lazy loading dostępu do relacji: {lazyLoadingTime}ms");
            _output.WriteLine($"ŁĄCZNIE (lazy): {timeWithLazyLoading}ms");
            _output.WriteLine($"GetActiveTeamByNameAsync (z Include): {timeWithEagerLoading}ms");
            _output.WriteLine($"Różnica: {timeWithEagerLoading - timeWithLazyLoading}ms");
            
            if (timeWithLazyLoading > 0)
            {
                var percentageOverhead = ((timeWithEagerLoading - timeWithLazyLoading) * 100.0 / timeWithLazyLoading);
                _output.WriteLine($"Overhead Include: {percentageOverhead:F1}%");
            }

            // Weryfikacja SQL
            var querySql1 = _context.Teams.Where(t => t.DisplayName == "Test Team 51").ToQueryString();
            var querySql2 = _context.Teams
                .Include(t => t.SchoolType)
                .Include(t => t.SchoolYear)
                .Include(t => t.Template)
                .Include(t => t.Members).ThenInclude(m => m.User)
                .Include(t => t.Channels)
                .Where(t => t.DisplayName == "Test Team 51" && t.Status == TeamStatus.Active)
                .ToQueryString();

            _output.WriteLine("\n=== GENEROWANE ZAPYTANIA SQL ===");
            _output.WriteLine("Bez Include:");
            _output.WriteLine(querySql1);
            _output.WriteLine("\nZ Include:");
            _output.WriteLine(querySql2);

            // Assertions
            team1.Should().NotBeNull();
            team2.Should().NotBeNull();
            team1!.DisplayName.Should().Be(team2!.DisplayName);
        }

        [Fact]
        public async Task MeasureImpact_UserRepositoryIncludes()
        {
            // Arrange
            await SeedLargeDataset();
            var iterations = 10;
            long totalTimeWithoutInclude = 0;
            long totalTimeWithInclude = 0;

            _output.WriteLine("=== ANALIZA WYDAJNOŚCI UserRepository ===");

            for (int i = 0; i < iterations; i++)
            {
                // Bez Include
                var sw1 = Stopwatch.StartNew();
                var user1 = await _userRepository.GetUserByUpnAsync($"user{i}@example.com");
                sw1.Stop();
                totalTimeWithoutInclude += sw1.ElapsedMilliseconds;

                // Z Include
                var sw2 = Stopwatch.StartNew();
                var user2 = await _userRepository.GetActiveUserByUpnAsync($"user{i}@example.com");
                sw2.Stop();
                totalTimeWithInclude += sw2.ElapsedMilliseconds;

                _output.WriteLine($"Iteracja {i + 1}: GetUserByUpnAsync={sw1.ElapsedMilliseconds}ms, GetActiveUserByUpnAsync={sw2.ElapsedMilliseconds}ms");
            }

            var avgWithoutInclude = totalTimeWithoutInclude / iterations;
            var avgWithInclude = totalTimeWithInclude / iterations;

            _output.WriteLine($"\n=== ŚREDNIE CZASY dla {iterations} iteracji ===");
            _output.WriteLine($"GetUserByUpnAsync: {avgWithoutInclude}ms");
            _output.WriteLine($"GetActiveUserByUpnAsync: {avgWithInclude}ms");
            
            if (avgWithoutInclude > 0)
            {
                var overheadMs = avgWithInclude - avgWithoutInclude;
                var overheadPercent = (overheadMs * 100.0 / avgWithoutInclude);
                _output.WriteLine($"Overhead Include: {overheadMs}ms ({overheadPercent:F1}%)");
            }

            // Assertions
            avgWithInclude.Should().BeGreaterThanOrEqualTo(avgWithoutInclude, "Include should take at least as long as regular query");
        }

        [Fact]
        public async Task AnalyzeMemoryUsage_IncludePatterns()
        {
            // Arrange
            await SeedLargeDataset();

            _output.WriteLine("=== ANALIZA UŻYCIA PAMIĘCI ===");

            // Przed testami
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);

            // Test 1: Wiele zapytań bez Include (symuluje N+1)
            var sw1 = Stopwatch.StartNew();
            var teams = await _context.Teams.Take(10).ToListAsync();
            foreach (var team in teams)
            {
                _ = team.Members?.Count; // Wymusza lazy loading
                _ = team.Channels?.Count;
            }
            sw1.Stop();
            var memoryAfterLazy = GC.GetTotalMemory(false);

            // Test 2: Jedno zapytanie z Include
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryBeforeEager = GC.GetTotalMemory(false);
            
            var sw2 = Stopwatch.StartNew();
            var teamsWithInclude = await _context.Teams
                .Include(t => t.Members).ThenInclude(m => m.User)
                .Include(t => t.Channels)
                .Take(10)
                .ToListAsync();
            sw2.Stop();
            var memoryAfterEager = GC.GetTotalMemory(false);

            _output.WriteLine($"Czas lazy loading (N+1): {sw1.ElapsedMilliseconds}ms");
            _output.WriteLine($"Czas eager loading (Include): {sw2.ElapsedMilliseconds}ms");
            _output.WriteLine($"Pamięć początkowa: {initialMemory:N0} bytes");
            _output.WriteLine($"Pamięć po lazy loading: {memoryAfterLazy:N0} bytes");
            _output.WriteLine($"Pamięć po eager loading: {memoryAfterEager:N0} bytes");
            _output.WriteLine($"Wzrost pamięci lazy: {memoryAfterLazy - initialMemory:N0} bytes");
            _output.WriteLine($"Wzrost pamięci eager: {memoryAfterEager - memoryBeforeEager:N0} bytes");

            // Assertions
            teams.Should().HaveCount(10);
            teamsWithInclude.Should().HaveCount(10);
        }

        private async Task SeedLargeDataset()
        {
            // Sprawdź czy już zasiano
            if (await _context.Teams.AnyAsync())
                return;

            _output.WriteLine("Seeding large dataset for performance tests...");

            // Utwórz podstawowe encje
            var schoolType = new SchoolType 
            { 
                Id = "st-1", 
                ShortName = "TST",
                FullName = "Test School Type",
                CreatedBy = "test"
            };
            _context.SchoolTypes.Add(schoolType);

            var schoolYear = new SchoolYear 
            { 
                Id = "sy-1", 
                Name = "2024/2025",
                CreatedBy = "test"
            };
            _context.SchoolYears.Add(schoolYear);

            var template = new TeamTemplate
            {
                Id = "tt-1",
                Name = "Test Template",
                Description = "Template for testing",
                CreatedBy = "test"
            };
            _context.TeamTemplates.Add(template);

            // Utwórz 100 zespołów z członkami
            var teams = new List<Team>();
            var users = new List<User>();
            
            for (int i = 0; i < 100; i++)
            {
                var team = new Team
                {
                    Id = $"team-{i}",
                    DisplayName = $"Test Team {i}",
                    Description = $"Test team description {i}",
                    Status = i % 10 == 0 ? TeamStatus.Archived : TeamStatus.Active,
                    SchoolTypeId = schoolType.Id,
                    SchoolYearId = schoolYear.Id,
                    TemplateId = template.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-i),
                    CreatedBy = "test"
                };

                teams.Add(team);

                // Dodaj kanały do zespołu
                for (int c = 0; c < 3; c++)
                {
                    var channel = new Channel
                    {
                        Id = $"channel-{i}-{c}",
                        DisplayName = $"Channel {c} of Team {i}",
                        TeamId = team.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-i),
                        CreatedBy = "test"
                    };
                    _context.Channels.Add(channel);
                }

                // Utwórz członków zespołu
                for (int j = 0; j < 20; j++)
                {
                    var user = new User
                    {
                        Id = $"user-{i}-{j}",
                        UPN = $"user{i}-{j}@example.com",
                        FirstName = $"First{i}-{j}",
                        LastName = $"Last{i}-{j}",
                        IsActive = j % 5 != 0, // 80% aktywnych
                        CreatedDate = DateTime.UtcNow.AddDays(-i),
                        CreatedBy = "test"
                    };
                    users.Add(user);

                    var teamMember = new TeamMember
                    {
                        Id = $"tm-{i}-{j}",
                        TeamId = team.Id,
                        UserId = user.Id,
                        Role = j == 0 ? TeamMemberRole.Owner : TeamMemberRole.Member,
                        CreatedBy = "test"
                    };
                    _context.TeamMembers.Add(teamMember);
                }
            }

            _context.Teams.AddRange(teams);
            _context.Users.AddRange(users);

            await _context.SaveChangesAsync();
            _output.WriteLine($"Seeded {teams.Count} teams with {users.Count} users");
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
        }
    }
} 
