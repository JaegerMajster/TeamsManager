using System;

namespace TeamsManager.UI.Services.Configuration
{
    public class EncryptedData
    {
        public string Data { get; set; } = string.Empty;           // Base64 encrypted payload
        public string Salt { get; set; } = string.Empty;           // Random salt
        public string IV { get; set; } = string.Empty;             // Initialization vector
        public string KeyId { get; set; } = string.Empty;          // Key rotation ID
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Timestamp
        public string Checksum { get; set; } = string.Empty;       // Integrity check
        public string EncryptionMethod { get; set; } = "AES256-GCM"; // Encryption method
        public bool Encrypted { get; set; } = true;                // Encryption flag
        public string Version { get; set; } = "2.0";               // Data version
    }
} 