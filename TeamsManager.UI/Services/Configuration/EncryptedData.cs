using System;
using System.Text.Json.Serialization;

namespace TeamsManager.UI.Services.Configuration
{
    public class EncryptedData
    {
        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;           // Base64 encrypted payload
        
        [JsonPropertyName("salt")]
        public string Salt { get; set; } = string.Empty;           // Random salt
        
        [JsonPropertyName("iv")]
        public string IV { get; set; } = string.Empty;             // Initialization vector
        
        [JsonPropertyName("keyId")]
        public string KeyId { get; set; } = string.Empty;          // Key rotation ID
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Timestamp
        
        [JsonPropertyName("checksum")]
        public string Checksum { get; set; } = string.Empty;       // Integrity check
        
        [JsonPropertyName("encryptionMethod")]
        public string EncryptionMethod { get; set; } = "AES256-GCM"; // Encryption method
        
        [JsonPropertyName("encrypted")]
        public bool Encrypted { get; set; } = true;                // Encryption flag
        
        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0";               // Data version
    }
} 