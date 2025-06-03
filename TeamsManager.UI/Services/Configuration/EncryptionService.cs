using System;
using System.Security.Cryptography;
using System.Text;

namespace TeamsManager.UI.Services.Configuration
{
    /// <summary>
    /// Serwis do szyfrowania i deszyfrowania wrażliwych danych
    /// Używa Windows Data Protection API (DPAPI)
    /// </summary>
    public class EncryptionService
    {
        /// <summary>
        /// Szyfruje tekst używając DPAPI
        /// Zaszyfrowane dane mogą być odszyfrowane tylko przez tego samego użytkownika Windows
        /// </summary>
        /// <param name="plainText">Tekst do zaszyfrowania</param>
        /// <returns>Zaszyfrowany tekst w formacie Base64</returns>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                // Konwertuj tekst na bajty
                var plainBytes = Encoding.UTF8.GetBytes(plainText);

                // Zaszyfruj używając DPAPI
                // null = brak dodatkowej entropii
                // DataProtectionScope.CurrentUser = tylko bieżący użytkownik może odszyfrować
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                // Konwertuj na Base64 dla łatwego przechowywania
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                // W przypadku błędu szyfrowania, loguj błąd (w przyszłości)
                System.Diagnostics.Debug.WriteLine($"Błąd szyfrowania: {ex.Message}");
                throw new InvalidOperationException("Nie udało się zaszyfrować danych", ex);
            }
        }

        /// <summary>
        /// Odszyfrowuje tekst zaszyfrowany przez metodę Encrypt
        /// </summary>
        /// <param name="encryptedText">Zaszyfrowany tekst w formacie Base64</param>
        /// <returns>Odszyfrowany tekst</returns>
        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                // Konwertuj z Base64
                var encryptedBytes = Convert.FromBase64String(encryptedText);

                // Odszyfruj używając DPAPI
                var plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser
                );

                // Konwertuj z powrotem na tekst
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (FormatException)
            {
                // Nieprawidłowy format Base64
                System.Diagnostics.Debug.WriteLine("Nieprawidłowy format zaszyfrowanych danych");
                return string.Empty;
            }
            catch (CryptographicException)
            {
                // Nie można odszyfrować - być może dane były zaszyfrowane przez innego użytkownika
                System.Diagnostics.Debug.WriteLine("Nie można odszyfrować danych - być może były zaszyfrowane przez innego użytkownika");
                return string.Empty;
            }
            catch (Exception ex)
            {
                // Inny błąd
                System.Diagnostics.Debug.WriteLine($"Błąd odszyfrowywania: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Testuje czy szyfrowanie działa poprawnie
        /// </summary>
        /// <returns>True jeśli szyfrowanie/deszyfrowanie działa</returns>
        public bool TestEncryption()
        {
            try
            {
                const string testText = "Test123!@#";
                var encrypted = Encrypt(testText);
                var decrypted = Decrypt(encrypted);
                return decrypted == testText;
            }
            catch
            {
                return false;
            }
        }
    }
}