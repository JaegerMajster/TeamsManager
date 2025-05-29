using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Kanał w zespole Microsoft Teams
    /// Reprezentuje miejsce komunikacji w ramach zespołu
    /// </summary>
    public class Channel : BaseEntity
    {
        /// <summary>
        /// Nazwa wyświetlana kanału
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Opis kanału i jego przeznaczenia
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Typ kanału (Standard, Private, Shared)
        /// </summary>
        public string ChannelType { get; set; } = "Standard";

        /// <summary>
        /// Czy kanał jest kanałem ogólnym (domyślnym)
        /// Kanał ogólny nie może być usunięty
        /// </summary>
        public bool IsGeneral { get; set; } = false;

        /// <summary>
        /// Czy kanał jest prywatny
        /// Prywatne kanały są dostępne tylko dla wybranych członków
        /// </summary>
        public bool IsPrivate { get; set; } = false;

        /// <summary>
        /// Czy kanał jest tylko do odczytu
        /// </summary>
        public bool IsReadOnly { get; set; } = false;

        /// <summary>
        /// Status kanału (np. Aktywny, Zarchiwizowany).
        /// </summary>
        public ChannelStatus Status { get; set; } = ChannelStatus.Active;

        /// <summary>
        /// Data zmiany statusu na zarchiwizowany.
        /// </summary>
        public DateTime? StatusChangeDate { get; set; } // Ogólna data zmiany statusu

        /// <summary>
        /// Osoba, która ostatnio zmieniła status kanału (UPN).
        /// </summary>
        public string? StatusChangedBy { get; set; }

        /// <summary>
        /// Powód ostatniej zmiany statusu (np. powód archiwizacji).
        /// </summary>
        public string? StatusChangeReason { get; set; }

        /// <summary>
        /// Data ostatniej aktywności w kanale
        /// </summary>
        public DateTime? LastActivityDate { get; set; }

        /// <summary>
        /// Data ostatniej wiadomości w kanale
        /// </summary>
        public DateTime? LastMessageDate { get; set; }

        /// <summary>
        /// Liczba wiadomości w kanale
        /// </summary>
        public int MessageCount { get; set; } = 0;

        /// <summary>
        /// Liczba plików udostępnionych w kanale
        /// </summary>
        public int FilesCount { get; set; } = 0;

        /// <summary>
        /// Rozmiar plików w kanale (w bajtach)
        /// </summary>
        public long FilesSize { get; set; } = 0;

        /// <summary>
        /// Ustawienia powiadomień dla kanału
        /// Format JSON z ustawieniami
        /// </summary>
        public string? NotificationSettings { get; set; }

        /// <summary>
        /// Czy kanał ma moderację włączoną
        /// </summary>
        public bool IsModerationEnabled { get; set; } = false;

        /// <summary>
        /// Kategoria kanału (np. "Ogólne", "Projekty", "Socjalne")
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Znaczniki/tagi dla kanału
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// Zewnętrzny URL powiązany z kanałem (opcjonalny)
        /// </summary>
        public string? ExternalUrl { get; set; }

        /// <summary>
        /// Kolejność sortowania kanałów w zespole
        /// </summary>
        public int SortOrder { get; set; } = 0;

        // ===== KLUCZE OBCE =====

        /// <summary>
        /// Identyfikator zespołu do którego należy kanał
        /// </summary>
        public string TeamId { get; set; } = string.Empty;

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Zespół do którego należy kanał
        /// </summary>
        public Team? Team { get; set; }

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Czy kanał jest obecnie aktywny (nie zarchiwizowany)
        /// </summary>
        public bool IsCurrentlyActive => IsActive && Status == ChannelStatus.Active;

        /// <summary>
        /// Czy kanał był niedawno aktywny (ostatnie 30 dni)
        /// </summary>
        public bool IsRecentlyActive => LastActivityDate.HasValue &&
                                        (DateTime.UtcNow - LastActivityDate.Value).Days < 30;

        /// <summary>
        /// Ile dni minęło od ostatniej aktywności
        /// </summary>
        public int? DaysSinceLastActivity
        {
            get
            {
                if (!LastActivityDate.HasValue) return null;
                return Math.Max(0, (DateTime.UtcNow - LastActivityDate.Value).Days);
            }
        }

        /// <summary>
        /// Ile dni minęło od ostatniej wiadomości
        /// </summary>
        public int? DaysSinceLastMessage
        {
            get
            {
                if (!LastMessageDate.HasValue) return null;
                return Math.Max(0, (DateTime.UtcNow - LastMessageDate.Value).Days);
            }
        }

        /// <summary>
        /// Rozmiar plików w formacie czytelnym dla człowieka
        /// </summary>
        public string FilesSizeFormatted
        {
            get
            {
                if (FilesSize == 0) return "0 B";

                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = FilesSize;
                int order = 0;

                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }

                return $"{len:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// Status kanału w czytelnej formie
        /// </summary>
        public string StatusDescription
        {
            get
            {
                if (!IsActive) return "Nieaktywny (ogólnie)"; // Encja BaseEntity.IsActive = false
                return Status switch
                {
                    ChannelStatus.Active => IsPrivate ? "Prywatny" : (IsReadOnly ? "Tylko do odczytu" : "Aktywny"),
                    ChannelStatus.Archived => "Zarchiwizowany",
                    _ => "Nieznany status"
                };
            }
        }

        /// <summary>
        /// Poziom aktywności kanału na podstawie liczby wiadomości
        /// </summary>
        public string ActivityLevel
        {
            get
            {
                return MessageCount switch
                {
                    0 => "Brak aktywności",
                    < 10 => "Niska aktywność",
                    < 50 => "Średnia aktywność",
                    < 200 => "Wysoka aktywność",
                    _ => "Bardzo wysoka aktywność"
                };
            }
        }

        /// <summary>
        /// Krótki opis kanału dla list i podglądów
        /// </summary>
        public string ShortSummary
        {
            get
            {
                var parts = new List<string>();

                if (IsGeneral) parts.Add("Kanał główny");
                if (IsPrivate) parts.Add("Prywatny");
                if (IsReadOnly) parts.Add("Tylko odczyt");
                if (MessageCount > 0) parts.Add($"{MessageCount} wiadomości");
                if (FilesCount > 0) parts.Add($"{FilesCount} plików");

                return parts.Any() ? string.Join(" • ", parts) : "Kanał zespołu";
            }
        }

        // ===== METODY POMOCNICZE =====

        /// <summary>
        /// Archiwizuje kanał z podaniem powodu
        /// </summary>
        /// <param name="reason">Powód archiwizacji</param>
        /// <param name="archivedBy">Osoba archiwizująca (UPN)</param>
        public void Archive(string reason, string archivedBy)
        {
            if (IsGeneral)
                throw new InvalidOperationException("Nie można zarchiwizować kanału ogólnego.");

            if (Status == ChannelStatus.Archived) return; // Już zarchiwizowany

            Status = ChannelStatus.Archived;
            StatusChangeDate = DateTime.UtcNow;
            StatusChangedBy = archivedBy;
            StatusChangeReason = reason;
            MarkAsModified(archivedBy);
            // Możesz też zaktualizować LastActivityDate, jeśli archiwizacja jest traktowana jako aktywność
            // UpdateLastActivity(); 
        }

        /// <summary>
        /// Przywraca kanał z archiwum
        /// </summary>
        /// <param name="restoredBy">Osoba przywracająca (UPN)</param>
        public void Restore(string restoredBy)
        {
            if (Status == ChannelStatus.Active) return; // Już aktywny

            Status = ChannelStatus.Active;
            StatusChangeDate = DateTime.UtcNow;
            StatusChangedBy = restoredBy;
            StatusChangeReason = "Przywrócono z archiwum"; // Lub inny domyślny powód
            MarkAsModified(restoredBy);
        }

        /// <summary>
        /// Ustawia kanał jako tylko do odczytu
        /// </summary>
        /// <param name="setBy">Osoba ustawiająca (UPN)</param>
        public void SetReadOnly(string setBy)
        {
            IsReadOnly = true;
            MarkAsModified(setBy);
        }

        /// <summary>
        /// Usuwa ograniczenie tylko do odczytu
        /// </summary>
        /// <param name="setBy">Osoba usuwająca ograniczenie (UPN)</param>
        public void RemoveReadOnly(string setBy)
        {
            IsReadOnly = false;
            MarkAsModified(setBy);
        }

        /// <summary>
        /// Aktualizuje statistyki aktywności kanału
        /// </summary>
        /// <param name="messageCount">Nowa liczba wiadomości</param>
        /// <param name="filesCount">Nowa liczba plików</param>
        /// <param name="filesSize">Nowy rozmiar plików</param>
        public void UpdateActivityStats(int? messageCount = null, int? filesCount = null, long? filesSize = null)
        {
            if (messageCount.HasValue)
            {
                MessageCount = messageCount.Value;
                LastMessageDate = DateTime.UtcNow;
            }

            if (filesCount.HasValue)
                FilesCount = filesCount.Value;

            if (filesSize.HasValue)
                FilesSize = filesSize.Value;

            LastActivityDate = DateTime.UtcNow;
        }

        /// <summary>
        /// Sprawdza czy kanał może być usunięty
        /// </summary>
        /// <returns>True jeśli można usunąć</returns>
        public bool CanBeDeleted()
        {
            // Kanał ogólny nie może być usunięty
            if (IsGeneral) return false;

            // Kanały z dużą aktywnością wymagają potwierdzenia
            if (MessageCount > 100) return false;

            return true;
        }

        /// <summary>
        /// Pobiera powód dlaczego kanał nie może być usunięty
        /// </summary>
        /// <returns>Powód lub null jeśli można usunąć</returns>
        public string? GetDeletionBlockReason()
        {
            if (IsGeneral)
                return "Kanał ogólny nie może być usunięty";

            if (MessageCount > 100)
                return $"Kanał zawiera {MessageCount} wiadomości - wymagane ręczne potwierdzenie";

            return null;
        }
    }
}