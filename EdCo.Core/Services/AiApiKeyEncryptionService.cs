using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EdCo.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EdCo.Core.Services
{
    public class AiApiKeyEncryptionService : IAiApiKeyEncryptionService
    {
        private readonly byte[] _key;

        public AiApiKeyEncryptionService(IConfiguration configuration)
        {
            // Derive a 256-bit key from configuration (Security:MasterKey or Jwt:Key)
            var rawMasterKey = configuration["Security:MasterKey"] 
                               ?? configuration["Jwt:Key"] 
                               ?? "EdCoSuperSecretKeyForJwtAuthenticationWhichShouldBeLongEnough12345!";

            using var sha256 = SHA256.Create();
            _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawMasterKey));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            
            // Write IV first
            ms.Write(aes.IV, 0, aes.IV.Length);

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs, Encoding.UTF8))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            byte[] fullBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            byte[] iv = new byte[aes.BlockSize / 8];
            if (fullBytes.Length < iv.Length)
                throw new ArgumentException("Invalid cipher text length.");

            Array.Copy(fullBytes, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(fullBytes, iv.Length, fullBytes.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);

            return sr.ReadToEnd();
        }
    }
}
