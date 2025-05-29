namespace TeamsManager.Core.Enums
{
    /// <summary>
    /// Status operacji wykonywanej w systemie
    /// </summary>
    public enum OperationStatus
    {
        Pending = 0,     // Oczekująca - operacja została zaplanowana
        InProgress = 1,  // W trakcie - operacja jest wykonywana
        Completed = 2,   // Zakończona - operacja zakończona sukcesem
        Failed = 3,      // Nieudana - operacja zakończona błędem
        Cancelled = 4,   // Anulowana - operacja została przerwana przez użytkownika
        PartialSuccess = 5 // Częściowy sukces - część operacji się udała
    }
}