namespace TeamsManager.Core.Enums
{
    /// <summary>
    /// Definiuje możliwe statusy kanału w zespole.
    /// </summary>
    public enum ChannelStatus
    {
        /// <summary>
        /// Kanał jest aktywny i w pełni funkcjonalny.
        /// </summary>
        Active = 0,

        /// <summary>
        /// Kanał został zarchiwizowany.
        /// Może być ukryty i mieć ograniczoną funkcjonalność.
        /// </summary>
        Archived = 1
        // W przyszłości można tu dodać inne statusy, np. ReadOnly, Deleted itp.
    }
}