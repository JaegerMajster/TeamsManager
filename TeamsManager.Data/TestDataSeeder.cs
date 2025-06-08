using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TeamsManager.Data
{
    /// <summary>
    /// Klasa do seedowania przykładowych danych do bazy danych.
    /// Używana do testowania i demonstracji funkcjonalności.
    /// </summary>
    public static class TestDataSeeder
    {
        public static async Task SeedAsync(TeamsManagerDbContext context)
        {
            // Sprawdź czy dane już istnieją
            if (await context.Departments.AnyAsync())
            {
                Console.WriteLine("Dane już istnieją w bazie danych.");
                return;
            }

            Console.WriteLine("Dodawanie przykładowych danych...");

            // Dodaj działy
            var itDepartment = new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "IT",
                Description = "Dział informatyki",
                DepartmentCode = "IT001",
                Email = "it@school.edu.pl",
                Phone = "+48 123 456 789",
                Location = "Budynek A, piętro 2",
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            var mathDepartment = new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Matematyka",
                Description = "Dział matematyki",
                DepartmentCode = "MATH001",
                Email = "math@school.edu.pl",
                Phone = "+48 123 456 790",
                Location = "Budynek B, piętro 1",
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            context.Departments.AddRange(itDepartment, mathDepartment);

            // Dodaj typy szkół
            var primarySchool = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "SP",
                FullName = "Szkoła Podstawowa",
                Description = "Szkoła podstawowa - klasy 1-8",
                ColorCode = "#4CAF50",
                SortOrder = 1,
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            var highSchool = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Liceum ogólnokształcące - klasy 1-4",
                ColorCode = "#2196F3",
                SortOrder = 2,
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            context.SchoolTypes.AddRange(primarySchool, highSchool);

            // Dodaj rok szkolny
            var schoolYear = new SchoolYear
            {
                Id = Guid.NewGuid().ToString(),
                Name = "2024/2025",
                Description = "Rok szkolny 2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                IsCurrent = true,
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            context.SchoolYears.Add(schoolYear);

            // Dodaj użytkowników
            var adminUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@school.edu.pl",
                Role = UserRole.Dyrektor,
                DepartmentId = itDepartment.Id,
                Position = "Administrator systemu",
                Phone = "+48 123 456 791",
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            var teacherUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Anna",
                LastName = "Nowak",
                UPN = "anna.nowak@school.edu.pl",
                Role = UserRole.Nauczyciel,
                DepartmentId = mathDepartment.Id,
                Position = "Nauczyciel matematyki",
                Phone = "+48 123 456 792",
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            context.Users.AddRange(adminUser, teacherUser);

            // Dodaj zespół
            var mathTeam = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Matematyka - Klasa 1A - 2024/2025",
                Description = "Zespół dla uczniów klasy 1A w roku szkolnym 2024/2025",
                Owner = teacherUser.UPN,
                Visibility = TeamVisibility.Public,
                Status = TeamStatus.Active,
                StatusChangeDate = DateTime.Now,
                StatusChangedBy = "System",
                SchoolTypeId = primarySchool.Id,
                SchoolYearId = schoolYear.Id,
                AcademicYear = "2024/2025",
                Semester = "I semestr",
                CourseCode = "MATH_1A",
                Level = "Klasa 1",
                Language = "pl-PL",
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            context.Teams.Add(mathTeam);

            // Dodaj członka zespołu
            var teamMember = new TeamMember
            {
                Id = Guid.NewGuid().ToString(),
                TeamId = mathTeam.Id,
                UserId = teacherUser.Id,
                Role = TeamMemberRole.Owner,
                AddedDate = DateTime.Now,
                AddedBy = "System",
                IsApproved = true,
                ApprovedDate = DateTime.Now,
                ApprovedBy = "System",
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            context.TeamMembers.Add(teamMember);

            // Dodaj kanał
            var generalChannel = new Channel
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Ogólny",
                Description = "Główny kanał zespołu",
                TeamId = mathTeam.Id,
                ChannelType = "Standard",
                IsGeneral = true,
                IsPrivate = false,
                Status = ChannelStatus.Active,
                StatusChangedBy = "System",
                Category = "Główne",
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            context.Channels.Add(generalChannel);

            // Dodaj historię operacji
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.TeamCreated,
                Status = OperationStatus.Completed,
                TargetEntityType = "Team",
                TargetEntityId = mathTeam.Id,
                TargetEntityName = mathTeam.DisplayName,
                OperationDetails = "Utworzono zespół dla klasy 1A",
                StartedAt = DateTime.Now.AddMinutes(-5),
                CompletedAt = DateTime.Now,
                Duration = TimeSpan.FromMinutes(5),
                CreatedDate = DateTime.Now,
                CreatedBy = "System"
            };

            context.OperationHistories.Add(operation);

            // Zapisz zmiany
            await context.SaveChangesAsync();

            Console.WriteLine("Przykładowe dane zostały dodane pomyślnie!");
            Console.WriteLine($"- Działy: {await context.Departments.CountAsync()}");
            Console.WriteLine($"- Użytkownicy: {await context.Users.CountAsync()}");
            Console.WriteLine($"- Typy szkół: {await context.SchoolTypes.CountAsync()}");
            Console.WriteLine($"- Lata szkolne: {await context.SchoolYears.CountAsync()}");
            Console.WriteLine($"- Zespoły: {await context.Teams.CountAsync()}");
            Console.WriteLine($"- Członkowie zespołów: {await context.TeamMembers.CountAsync()}");
            Console.WriteLine($"- Kanały: {await context.Channels.CountAsync()}");
            Console.WriteLine($"- Historia operacji: {await context.OperationHistories.CountAsync()}");
        }
    }
} 