namespace TeamsManager.Core.Enums;

/// <summary>
/// Określa stan zdrowia komponentu systemu
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Komponent działa prawidłowo
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Komponent działa, ale z ograniczeniami
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Komponent nie działa prawidłowo
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// Poziom krytyczności błędu zdrowia
/// </summary>
public enum HealthErrorSeverity
{
    /// <summary>
    /// Informacja
    /// </summary>
    Info = 0,

    /// <summary>
    /// Ostrzeżenie
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Błąd
    /// </summary>
    Error = 2,

    /// <summary>
    /// Krytyczny błąd
    /// </summary>
    Critical = 3
}