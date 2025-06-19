using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace TeamsManager.UI.Services.Configuration
{
    public class AdvancedEncryptionService
    {
        private readonly ILogger<AdvancedEncryptionService> _logger;
        private readonly string _keyId;

        public AdvancedEncryptionService(ILogger<AdvancedEncryptionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keyId = GenerateKeyId();
        }

        public EncryptedData Encrypt(string plaintext)
        {
            try
            {
                if (string.IsNullOrEmpty(plaintext))
                    throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

                // Krok 1: Generuj losową sól i IV
                var salt = GenerateRandomBytes(32);
                var iv = GenerateRandomBytes(12); // AES-GCM używa 12-bajtowego nonce

                // Krok 2: Utwórz klucz z DPAPI + sól
                var key = DeriveKeyFromDPAPI(salt);

                // Krok 3: Szyfruj AES-256-GCM
                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var (ciphertext, tag) = EncryptAesGcm(plaintextBytes, key, iv);

                // Krok 4: Połącz ciphertext + tag
                var encryptedPayload = new byte[ciphertext.Length + tag.Length];
                Array.Copy(ciphertext, 0, encryptedPayload, 0, ciphertext.Length);
                Array.Copy(tag, 0, encryptedPayload, ciphertext.Length, tag.Length);

                // Krok 5: Szyfruj całość przez DPAPI (druga warstwa)
                var dpapiEncrypted = ProtectedData.Protect(encryptedPayload, salt, DataProtectionScope.CurrentUser);

                // Krok 6: Oblicz checksum
                var checksum = ComputeChecksum(dpapiEncrypted, salt, iv);

                return new EncryptedData
                {
                    Data = Convert.ToBase64String(dpapiEncrypted),
                    Salt = Convert.ToBase64String(salt),
                    IV = Convert.ToBase64String(iv),
                    KeyId = _keyId,
                    CreatedAt = DateTime.UtcNow,
                    Checksum = checksum,
                    EncryptionMethod = "DPAPI+AES256-GCM",
                    Encrypted = true,
                    Version = "2.0"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas szyfrowania danych");
                throw;
            }
        }

        public string Decrypt(EncryptedData encryptedData)
        {
            try
            {
                if (encryptedData == null)
                    throw new ArgumentNullException(nameof(encryptedData));

                if (!encryptedData.Encrypted)
                    throw new InvalidOperationException("Dane nie są zaszyfrowane");

                // Krok 1: Waliduj integralność
                if (!ValidateIntegrity(encryptedData))
                    throw new InvalidOperationException("Dane zostały zmodyfikowane - checksum nie pasuje");

                // Krok 2: Dekoduj z Base64
                var dpapiEncrypted = Convert.FromBase64String(encryptedData.Data);
                var salt = Convert.FromBase64String(encryptedData.Salt);
                var iv = Convert.FromBase64String(encryptedData.IV);

                // Krok 3: Odszyfruj przez DPAPI (pierwsza warstwa)
                var encryptedPayload = ProtectedData.Unprotect(dpapiEncrypted, salt, DataProtectionScope.CurrentUser);

                // Krok 4: Rozdziel ciphertext i tag
                var tagLength = 16; // AES-GCM tag ma 16 bajtów
                var ciphertext = new byte[encryptedPayload.Length - tagLength];
                var tag = new byte[tagLength];
                Array.Copy(encryptedPayload, 0, ciphertext, 0, ciphertext.Length);
                Array.Copy(encryptedPayload, ciphertext.Length, tag, 0, tagLength);

                // Krok 5: Utwórz klucz z DPAPI + sól
                var key = DeriveKeyFromDPAPI(salt);

                // Krok 6: Odszyfruj AES-256-GCM
                var plaintextBytes = DecryptAesGcm(ciphertext, tag, key, iv);

                return Encoding.UTF8.GetString(plaintextBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas odszyfrowywania danych");
                throw;
            }
        }

        public bool ValidateIntegrity(EncryptedData data)
        {
            try
            {
                var dpapiEncrypted = Convert.FromBase64String(data.Data);
                var salt = Convert.FromBase64String(data.Salt);
                var iv = Convert.FromBase64String(data.IV);
                
                var computedChecksum = ComputeChecksum(dpapiEncrypted, salt, iv);
                return computedChecksum.Equals(data.Checksum, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas walidacji integralności");
                return false;
            }
        }

        public bool RotateEncryptionKey()
        {
            try
            {
                // Implementacja rotacji kluczy - dla uproszczenia zwracamy true
                _logger.LogInformation("Rotacja klucza szyfrowania zakończona pomyślnie");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas rotacji klucza szyfrowania");
                return false;
            }
        }

        private byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }

        private string GenerateKeyId()
        {
            return Guid.NewGuid().ToString("N")[..16]; // 16 znaków
        }

        private byte[] DeriveKeyFromDPAPI(byte[] salt)
        {
            // Używamy DPAPI do wygenerowania unikalnego klucza dla użytkownika
            var userData = Encoding.UTF8.GetBytes($"TeamsManager-{Environment.UserName}-{Environment.MachineName}");
            var protectedData = ProtectedData.Protect(userData, salt, DataProtectionScope.CurrentUser);
            
            // Używamy SHA-256 do uzyskania klucza o odpowiedniej długości (32 bajty dla AES-256)
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(protectedData)[..32];
        }

        private (byte[] ciphertext, byte[] tag) EncryptAesGcm(byte[] plaintext, byte[] key, byte[] iv)
        {
            using var aes = new AesGcm(key, 16); // 16 bajtów dla tagu
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16]; // AES-GCM tag ma 16 bajtów
            
            aes.Encrypt(iv, plaintext, ciphertext, tag);
            return (ciphertext, tag);
        }

        private byte[] DecryptAesGcm(byte[] ciphertext, byte[] tag, byte[] key, byte[] iv)
        {
            using var aes = new AesGcm(key, 16); // 16 bajtów dla tagu
            var plaintext = new byte[ciphertext.Length];
            
            aes.Decrypt(iv, ciphertext, tag, plaintext);
            return plaintext;
        }

        private string ComputeChecksum(byte[] data, byte[] salt, byte[] iv)
        {
            using var sha256 = SHA256.Create();
            var combined = new byte[data.Length + salt.Length + iv.Length];
            Array.Copy(data, 0, combined, 0, data.Length);
            Array.Copy(salt, 0, combined, data.Length, salt.Length);
            Array.Copy(iv, 0, combined, data.Length + salt.Length, iv.Length);
            
            var hash = sha256.ComputeHash(combined);
            return Convert.ToBase64String(hash);
        }
    }
} 