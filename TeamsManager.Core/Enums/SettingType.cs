namespace TeamsManager.Core.Enums
{
    /// <summary>
    /// Typ ustawienia aplikacji
    /// Określa jak interpretować wartość w ApplicationSetting
    /// </summary>
    public enum SettingType
    {
        String = 0,   // Wartość tekstowa
        Integer = 1,  // Liczba całkowita
        Boolean = 2,  // Wartość logiczna (true/false)
        Json = 3,     // Obiekt JSON
        DateTime = 4, // Data i czas
        Decimal = 5   // Liczba dziesiętna
    }
}