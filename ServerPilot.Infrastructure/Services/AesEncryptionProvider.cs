using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ServerPilot.Infrastructure.Services
{
    public static class AesEncryptionProvider
    {
        private static byte[]? _key;

        public static void Initialize(IConfiguration configuration)
        {
            var keyString = configuration["EncryptionKey"] ?? configuration["Jwt:Key"] ?? "SUPER_SECRET_KEY_FOR_SERVER_PILOT_STAGE_1";
            
            // Generar key a 256 bits (32 bytes)
            using var sha256 = SHA256.Create();
            _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
        }

        public static string? Encrypt(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            if (_key == null) throw new InvalidOperationException("Encryption provider no inicializado.");

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();
            var iv = aes.IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            using var ms = new MemoryStream();
            ms.Write(iv, 0, iv.Length); // prepend IV
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string? Decrypt(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            if (_key == null) throw new InvalidOperationException("Encryption provider no inicializado.");

            try
            {
                var fullCipher = Convert.FromBase64String(cipherText);
                using var aes = Aes.Create();
                aes.Key = _key;

                var iv = new byte[aes.BlockSize / 8];
                var cipher = new byte[fullCipher.Length - iv.Length];

                Array.Copy(fullCipher, iv, iv.Length);
                Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(cipher);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                return sr.ReadToEnd();
            }
            catch
            {
                // Si falla (por ej. si las contraseñas antiguas estaban en texto plano), devolverlo tal cual.
                return cipherText;
            }
        }
    }
}
