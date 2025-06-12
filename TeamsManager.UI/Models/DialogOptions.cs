using System;

namespace TeamsManager.UI.Models
{
    /// <summary>
    /// Typ okna dialogowego
    /// </summary>
    public enum DialogType
    {
        /// <summary>
        /// Informacja - niebieska ikona, przycisk "OK"
        /// </summary>
        Information,
        
        /// <summary>
        /// Ostrzeżenie - żółta ikona, przycisk "OK"
        /// </summary>
        Warning,
        
        /// <summary>
        /// Błąd - czerwona ikona, przycisk "OK"
        /// </summary>
        Error,
        
        /// <summary>
        /// Sukces - zielona ikona, przycisk "OK"
        /// </summary>
        Success,
        
        /// <summary>
        /// Potwierdzenie - pytajnik, przyciski "Tak"/"Nie" lub niestandardowe
        /// </summary>
        Confirmation,
        
        /// <summary>
        /// Pytanie - pytajnik, niestandardowe przyciski
        /// </summary>
        Question
    }

    /// <summary>
    /// Wynik okna dialogowego
    /// </summary>
    public enum DialogResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No,
        Primary,
        Secondary
    }

    /// <summary>
    /// Opcje konfiguracji okna dialogowego
    /// </summary>
    public class DialogOptions
    {
        /// <summary>
        /// Typ dialogu (określa ikonę i domyślne przyciski)
        /// </summary>
        public DialogType Type { get; set; } = DialogType.Information;

        /// <summary>
        /// Tytuł okna
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Główna treść komunikatu
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Dodatkowe szczegóły (opcjonalne, mniejszą czcionką)
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Tekst głównego przycisku (null = domyślny dla typu)
        /// </summary>
        public string? PrimaryButtonText { get; set; }

        /// <summary>
        /// Tekst drugiego przycisku (null = domyślny dla typu)
        /// </summary>
        public string? SecondaryButtonText { get; set; }

        /// <summary>
        /// Czy pokazać drugi przycisk
        /// </summary>
        public bool ShowSecondaryButton { get; set; }

        /// <summary>
        /// Czy główny przycisk ma być domyślny (Enter)
        /// </summary>
        public bool IsPrimaryDefault { get; set; } = true;

        /// <summary>
        /// Czy drugi przycisk ma być przyciskiem anulowania (Escape)
        /// </summary>
        public bool IsSecondaryCancel { get; set; } = true;

        /// <summary>
        /// Maksymalna szerokość okna (null = automatyczna)
        /// </summary>
        public double? MaxWidth { get; set; }

        /// <summary>
        /// Czy komunikat może zawierać formatowanie (pogrubienie, kursywa)
        /// </summary>
        public bool AllowFormatting { get; set; } = false;
    }

    /// <summary>
    /// Wynik okna dialogowego z dodatkowymi informacjami
    /// </summary>
    public class DialogResponse
    {
        /// <summary>
        /// Wynik dialogu
        /// </summary>
        public DialogResult Result { get; set; } = DialogResult.None;

        /// <summary>
        /// Czy użytkownik kliknął główny przycisk
        /// </summary>
        public bool IsPrimary => Result == DialogResult.OK || Result == DialogResult.Yes || Result == DialogResult.Primary;

        /// <summary>
        /// Czy użytkownik kliknął drugi przycisk lub anulował
        /// </summary>
        public bool IsSecondary => Result == DialogResult.Cancel || Result == DialogResult.No || Result == DialogResult.Secondary;

        /// <summary>
        /// Czy dialog został anulowany (Escape, X)
        /// </summary>
        public bool IsCancelled => Result == DialogResult.Cancel || Result == DialogResult.None;

        /// <summary>
        /// Czas wyświetlania dialogu
        /// </summary>
        public TimeSpan DisplayTime { get; set; }

        /// <summary>
        /// Dodatkowe dane zwrotne (opcjonalne)
        /// </summary>
        public object? Tag { get; set; }
    }
} 