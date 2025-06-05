namespace TeamsManager.Core.Models
{
    /// <summary>
    /// [P2-MONITORING] Model zawierający metryki wydajności cache
    /// </summary>
    public class CacheMetrics
    {
        /// <summary>
        /// Liczba trafień cache
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Liczba chybień cache
        /// </summary>
        public long CacheMisses { get; set; }

        /// <summary>
        /// Liczba unieważnień cache
        /// </summary>
        public long CacheInvalidations { get; set; }

        /// <summary>
        /// Współczynnik trafień cache (procent)
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Całkowita liczba operacji cache
        /// </summary>
        public long TotalOperations { get; set; }

        /// <summary>
        /// Średni czas operacji cache w milisekundach
        /// </summary>
        public double AverageOperationTimeMs { get; set; }

        /// <summary>
        /// Całkowity czas operacji cache w milisekundach
        /// </summary>
        public long TotalOperationTimeMs { get; set; }

        /// <summary>
        /// Timestamp pomiaru metryk
        /// </summary>
        public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Sprawdza czy cache działa wydajnie
        /// </summary>
        public bool IsPerformant => HitRate >= 80.0 && AverageOperationTimeMs <= 10.0;

        /// <summary>
        /// Zwraca tekstowy opis stanu cache
        /// </summary>
        public string GetPerformanceStatus()
        {
            return HitRate switch
            {
                >= 90 => "🟢 Doskonały",
                >= 80 => "🟢 Dobry", 
                >= 70 => "🟡 Akceptowalny",
                >= 50 => "🟠 Słaby",
                _ => "🔴 Krytyczny"
            };
        }

        /// <summary>
        /// Zwraca szczegółowy raport wydajności
        /// </summary>
        public override string ToString()
        {
            return $"Cache Performance: {GetPerformanceStatus()} | " +
                   $"Hit Rate: {HitRate:F1}% | " +
                   $"Operations: {TotalOperations:N0} | " +
                   $"Avg Time: {AverageOperationTimeMs:F2}ms | " +
                   $"Invalidations: {CacheInvalidations:N0}";
        }
    }
} 