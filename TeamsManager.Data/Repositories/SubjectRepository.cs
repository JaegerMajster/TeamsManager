﻿// Plik: TeamsManager.Data/Repositories/SubjectRepository.cs
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeamsManager.Data.Repositories
{
    /// <summary>
    /// Repozytorium dla operacji na encji Subject.
    /// </summary>
    public class SubjectRepository : GenericRepository<Subject>, ISubjectRepository
    {
        /// <summary>
        /// Konstruktor repozytorium przedmiotów.
        /// </summary>
        /// <param name="context">Kontekst bazy danych.</param>
        public SubjectRepository(TeamsManagerDbContext context) : base(context)
        {
            // _context jest już dostępne z klasy bazowej GenericRepository
        }

        // UWAGA: Decyzja architektoniczna - filtrowanie po IsActive
        // GetByIdAsync nadpisuje metodę bazową i ZAWSZE filtruje po IsActive dla spójności.
        // Jeśli potrzebujesz nieaktywnych przedmiotów, użyj GetByIdIncludingInactiveAsync.
        // Ta zmiana zapewnia, że domyślnie wszystkie metody zwracają tylko aktywne przedmioty.

        /// <summary>
        /// Asynchronicznie pobiera aktywny przedmiot po jego ID.
        /// Nadpisuje metodę bazową, aby zapewnić spójne filtrowanie po IsActive.
        /// Dla nieaktywnych przedmiotów użyj GetByIdIncludingInactiveAsync.
        /// </summary>
        /// <param name="id">ID przedmiotu.</param>
        /// <returns>Znaleziony aktywny przedmiot lub null.</returns>
        public override async Task<Subject?> GetByIdAsync(object id)
        {
            if (id is string stringId)
            {
                return await _dbSet.FirstOrDefaultAsync(s => s.Id == stringId && s.IsActive);
            }
            return null;
        }

        /// <summary>
        /// Asynchronicznie pobiera przedmiot po jego ID, włączając nieaktywne przedmioty.
        /// Używać tylko gdy logika biznesowa wymaga dostępu do nieaktywnych przedmiotów.
        /// </summary>
        /// <param name="subjectId">ID przedmiotu.</param>
        /// <returns>Znaleziony przedmiot (aktywny lub nieaktywny) lub null.</returns>
        public async Task<Subject?> GetByIdIncludingInactiveAsync(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
                return null;
                
            return await _dbSet.FirstOrDefaultAsync(s => s.Id == subjectId);
        }

        /// <summary>
        /// Asynchronicznie pobiera przedmiot na podstawie jego unikalnego kodu,
        /// dołączając domyślnie szczegóły takie jak DefaultSchoolType.
        /// Zwraca tylko aktywne przedmioty.
        /// </summary>
        /// <param name="code">Kod przedmiotu.</param>
        /// <returns>Znaleziony, aktywny przedmiot lub null, jeśli nie istnieje.</returns>
        public async Task<Subject?> GetByCodeAsync(string code)
        {
            // Używamy _dbSet zamiast _context.Subjects dla spójności z GenericRepository
            return await _dbSet
                         .Include(s => s.DefaultSchoolType)
                         .FirstOrDefaultAsync(s => s.Code == code && s.IsActive);
        }

        /// <summary>
        /// Asynchronicznie pobiera listę aktywnych nauczycieli przypisanych do danego przedmiotu.
        /// </summary>
        /// <param name="subjectId">ID przedmiotu.</param>
        /// <returns>Kolekcja aktywnych nauczycieli przypisanych do przedmiotu.</returns>
        public async Task<IEnumerable<User>> GetTeachersAsync(string subjectId)
        {
            // Sprawdzenie, czy sam przedmiot jest aktywny (opcjonalne, może być logiką serwisu)
            // var subject = await _dbSet.FirstOrDefaultAsync(s => s.Id == subjectId && s.IsActive);
            // if (subject == null) return Enumerable.Empty<User>();

            // Pobierz przypisania UserSubject dla danego przedmiotu,
            // które są aktywne i mają aktywnego, załadowanego użytkownika (nauczyciela).
            // Dołącz także encję User, aby uniknąć problemu N+1.
            var assignments = await _context.UserSubjects // Używamy _context do dostępu do innych DbSet
                                            .Include(us => us.User)
                                            .Where(us => us.SubjectId == subjectId &&
                                                         us.IsActive &&
                                                         us.User != null &&
                                                         us.User.IsActive)
                                            .ToListAsync();

            return assignments.Select(us => us.User!) // User! jest bezpieczne dzięki warunkowi User != null w Where
                              .Distinct()
                              .ToList();
        }

        /// <summary>
        /// Asynchronicznie pobiera aktywny przedmiot po jego ID, dołączając szczegóły
        /// takie jak DefaultSchoolType.
        /// </summary>
        /// <param name="subjectId">ID przedmiotu.</param>
        /// <returns>Znaleziony, aktywny przedmiot lub null.</returns>
        public async Task<Subject?> GetByIdWithDetailsAsync(string subjectId)
        {
            return await _dbSet
                         .Include(s => s.DefaultSchoolType)
                         .FirstOrDefaultAsync(s => s.Id == subjectId && s.IsActive);
        }

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne przedmioty, dołączając szczegóły
        /// takie jak DefaultSchoolType.
        /// </summary>
        /// <returns>Kolekcja aktywnych przedmiotów ze szczegółami.</returns>
        public async Task<IEnumerable<Subject>> GetAllActiveWithDetailsAsync()
        {
            return await _dbSet
                         .Include(s => s.DefaultSchoolType)
                         .Where(s => s.IsActive)
                         .ToListAsync();
        }
    }
}