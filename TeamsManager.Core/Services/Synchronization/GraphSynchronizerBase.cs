using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.Synchronization;
using TeamsManager.Core.Helpers;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Services.Synchronization
{
    /// <summary>
    /// Bazowa implementacja synchronizatora z wspólną logiką dla wszystkich typów encji.
    /// Wzorzec Template Method dla synchronizacji Graph→DB.
    /// </summary>
    /// <typeparam name="T">Typ encji dziedziczącej po BaseEntity</typeparam>
    /// <typeparam name="TGraphModel">Typ modelu Graph API</typeparam>
    public abstract class GraphSynchronizerBase<T, TGraphModel> : IGraphSynchronizer<T, TGraphModel> where T : BaseEntity, new()
    {
        protected readonly ILogger<GraphSynchronizerBase<T, TGraphModel>> _logger;

        protected GraphSynchronizerBase(ILogger<GraphSynchronizerBase<T, TGraphModel>> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public virtual async Task<T> SynchronizeAsync(TGraphModel graphObject, T? existingEntity = null, string? currentUserUpn = null)
        {
            if (graphObject == null)
                throw new ArgumentNullException(nameof(graphObject));

            // Walidacja obiektu Graph
            ValidateGraphObject(graphObject);

            var graphId = GetGraphId(graphObject);
            var isUpdate = existingEntity != null;
            
            _logger.LogDebug("Synchronizacja {EntityType} z Graph ID: {GraphId}, isUpdate: {IsUpdate}", 
                typeof(T).Name, graphId, isUpdate);

            // Utworzenie lub użycie istniejącej encji
            var entity = existingEntity ?? new T();

            // Mapowanie właściwości
            MapProperties(graphObject, entity, isUpdate);

            // Ustawienie pól audytu
            if (isUpdate)
            {
                entity.MarkAsModified(currentUserUpn ?? "Graph Sync");
                _logger.LogDebug("Oznaczono {EntityType} ID: {EntityId} jako zmodyfikowaną", 
                    typeof(T).Name, entity.Id);
            }
            else
            {
                entity.CreatedDate = DateTime.UtcNow;
                entity.CreatedBy = currentUserUpn ?? "Graph Sync";
                entity.Id = string.IsNullOrEmpty(entity.Id) ? Guid.NewGuid().ToString() : entity.Id;
                _logger.LogDebug("Utworzono nową {EntityType} ID: {EntityId}", 
                    typeof(T).Name, entity.Id);
            }

            // Dodatkowa logika synchronizacji specyficzna dla typu
            await PerformAdditionalSynchronizationAsync(graphObject, entity, isUpdate);

            return entity;
        }

        /// <inheritdoc />
        public virtual async Task<bool> RequiresSynchronizationAsync(TGraphModel graphObject, T existingEntity)
        {
            if (graphObject == null || existingEntity == null)
                return true;

            try
            {
                ValidateGraphObject(graphObject);

                // Utworzenie tymczasowej encji z danymi z Graph
                var tempEntity = new T();
                MapProperties(graphObject, tempEntity, false);

                // Porównanie kluczowych właściwości
                var hasChanges = await DetectChangesAsync(tempEntity, existingEntity);
                
                if (hasChanges)
                {
                    _logger.LogDebug("Wykryto zmiany w {EntityType} ID: {EntityId}", 
                        typeof(T).Name, existingEntity.Id);
                }

                return hasChanges;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykrywania zmian dla {EntityType}", typeof(T).Name);
                // W przypadku błędu, bezpieczniej jest wykonać synchronizację
                return true;
            }
        }

        /// <inheritdoc />
        public abstract void MapProperties(TGraphModel graphObject, T entity, bool isUpdate = false);

        /// <inheritdoc />
        public abstract void ValidateGraphObject(TGraphModel graphObject);

        /// <inheritdoc />
        public abstract string GetGraphId(TGraphModel graphObject);

        /// <summary>
        /// Metoda do nadpisania w klasach pochodnych dla dodatkowej logiki synchronizacji.
        /// Na przykład: synchronizacja relacji, członków zespołu, kanałów itp.
        /// </summary>
        protected virtual Task PerformAdditionalSynchronizationAsync(TGraphModel graphObject, T entity, bool isUpdate)
        {
            // Domyślnie brak dodatkowej logiki
            return Task.CompletedTask;
        }

        /// <summary>
        /// Wykrywa zmiany między encjami. Do nadpisania dla specyficznej logiki porównania.
        /// </summary>
        protected abstract Task<bool> DetectChangesAsync(T tempEntity, T existingEntity);

        /// <summary>
        /// Pomocnicza metoda do bezpiecznego pobierania wartości z TGraphModel z logowaniem.
        /// </summary>
        protected TValue? GetPropertyValue<TValue>(TGraphModel graphObject, string propertyName, TValue? defaultValue = default)
        {
            try
            {
                if (typeof(TValue) == typeof(string))
                {
                    return (TValue)(object)GraphModelMapper.GetString(graphObject, propertyName, defaultValue?.ToString());
                }
                else if (typeof(TValue) == typeof(bool))
                {
                    return (TValue)(object)GraphModelMapper.GetBoolean(graphObject, propertyName, 
                        defaultValue != null ? Convert.ToBoolean(defaultValue) : false);
                }
                else if (typeof(TValue) == typeof(DateTime?))
                {
                    return (TValue)(object)GraphModelMapper.GetDateTime(graphObject, propertyName);
                }
                else if (typeof(TValue) == typeof(object[]))
                {
                    // Dla tablic obiektów, próbujemy pobrać bezpośrednio
                    var property = graphObject.GetType().GetProperty(propertyName);
                    var value = property?.GetValue(graphObject);
                    if (value is object[] array)
                    {
                        return (TValue)(object)array;
                    }
                    return defaultValue;
                }
                else if (typeof(TValue) == typeof(TGraphModel))
                {
                    // Dla zagnieżdżonych TGraphModel
                    var property = graphObject.GetType().GetProperty(propertyName);
                    var value = property?.GetValue(graphObject);
                    if (value is TGraphModel psObj)
                    {
                        return (TValue)(object)psObj;
                    }
                    return defaultValue;
                }
                else if (typeof(TValue) == typeof(int?))
                {
                    return (TValue)(object)GraphModelMapper.GetNullableInt32(graphObject, propertyName);
                }
                else
                {
                    // Dla innych typów używamy ogólnej metody
                    var property = graphObject.GetType().GetProperty(propertyName);
                    var value = property?.GetValue(graphObject);
                    if (value != null && value is TValue typedValue)
                    {
                        return typedValue;
                    }
                    return defaultValue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nie udało się pobrać właściwości {PropertyName} typu {Type} z TGraphModel", 
                    propertyName, typeof(TValue).Name);
                return defaultValue;
            }
        }

        /// <summary>
        /// Porównuje dwie wartości string z uwzględnieniem null i pustych stringów.
        /// </summary>
        protected bool HasStringChanged(string? newValue, string? oldValue)
        {
            // Traktuj null i pusty string jako równoważne
            var normalizedNew = string.IsNullOrWhiteSpace(newValue) ? null : newValue.Trim();
            var normalizedOld = string.IsNullOrWhiteSpace(oldValue) ? null : oldValue.Trim();
            
            return !string.Equals(normalizedNew, normalizedOld, StringComparison.Ordinal);
        }
    }
} 
