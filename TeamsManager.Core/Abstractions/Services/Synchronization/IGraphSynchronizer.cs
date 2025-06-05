using System;
using System.Management.Automation;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services.Synchronization
{
    /// <summary>
    /// Interfejs definiujący kontrakt dla synchronizacji obiektów między Microsoft Graph a lokalną bazą danych.
    /// Implementuje wzorzec wykrywania zmian i inteligentnej synchronizacji.
    /// </summary>
    /// <typeparam name="T">Typ encji dziedziczącej po BaseEntity</typeparam>
    public interface IGraphSynchronizer<T> where T : BaseEntity
    {
        /// <summary>
        /// Synchronizuje obiekt PSObject z Microsoft Graph do lokalnej encji.
        /// Wykonuje inteligentne mapowanie i wykrywanie zmian.
        /// </summary>
        /// <param name="graphObject">Obiekt PSObject zwrócony z Microsoft Graph</param>
        /// <param name="existingEntity">Istniejąca encja z bazy danych (null dla nowych obiektów)</param>
        /// <param name="currentUserUpn">UPN użytkownika wykonującego synchronizację</param>
        /// <returns>Zsynchronizowana encja gotowa do zapisu</returns>
        Task<T> SynchronizeAsync(PSObject graphObject, T? existingEntity = null, string? currentUserUpn = null);

        /// <summary>
        /// Sprawdza czy obiekt z Graph różni się od lokalnej encji i wymaga synchronizacji.
        /// Porównuje kluczowe właściwości i zwraca true jeśli wykryto zmiany.
        /// </summary>
        /// <param name="graphObject">Obiekt PSObject z Microsoft Graph</param>
        /// <param name="existingEntity">Istniejąca encja z bazy danych</param>
        /// <returns>True jeśli wykryto różnice wymagające synchronizacji</returns>
        Task<bool> RequiresSynchronizationAsync(PSObject graphObject, T existingEntity);

        /// <summary>
        /// Mapuje właściwości z PSObject do encji.
        /// Metoda pomocnicza dla czytelności i reużywalności logiki mapowania.
        /// </summary>
        /// <param name="graphObject">Obiekt PSObject z Microsoft Graph</param>
        /// <param name="entity">Encja docelowa</param>
        /// <param name="isUpdate">Czy to aktualizacja istniejącej encji</param>
        void MapProperties(PSObject graphObject, T entity, bool isUpdate = false);

        /// <summary>
        /// Waliduje czy obiekt PSObject zawiera wymagane dane do synchronizacji.
        /// Rzuca wyjątek jeśli brakuje krytycznych właściwości.
        /// </summary>
        /// <param name="graphObject">Obiekt PSObject do walidacji</param>
        /// <exception cref="ArgumentException">Gdy brakuje wymaganych właściwości</exception>
        void ValidateGraphObject(PSObject graphObject);

        /// <summary>
        /// Pobiera unikalne ID obiektu z Microsoft Graph.
        /// Używane do łączenia obiektów Graph z lokalnymi encjami.
        /// </summary>
        /// <param name="graphObject">Obiekt PSObject z Microsoft Graph</param>
        /// <returns>Unikalne ID obiektu w Microsoft Graph</returns>
        string GetGraphId(PSObject graphObject);
    }
} 