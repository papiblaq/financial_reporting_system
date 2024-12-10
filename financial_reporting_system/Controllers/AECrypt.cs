using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PHilae.Cipher
{
    public class AECrypt
    {
        private readonly byte[] salt = { 169, 155, 200, 50, 86, 53, 227, 3 }; // Java negative bytes are represented as unsigned here
        private readonly byte[] iv = new byte[16]; // 16-byte IV, initialized to zeros
        private string passPhrase = "Rubikon Universal Core Banking System";
        private Aes aes;

        public AECrypt()
        {
            Initialize();
        }

        public AECrypt(string pass)
        {
            passPhrase = string.IsNullOrEmpty(pass) ? passPhrase : pass;
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                var key = GenerateKey(passPhrase, salt);
                aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing AES: {ex.Message}");
            }
        }

        private byte[] GenerateKey(string passphrase, byte[] salt)
        {
            using (var rfc2898DeriveBytes = new Rfc2898DeriveBytes(passphrase, salt, 1024, HashAlgorithmName.SHA512))
            {
                return rfc2898DeriveBytes.GetBytes(16); // 128-bit key
            }
        }

        public string Encrypt(string plainStr)
        {
            try
            {
                if (!string.IsNullOrEmpty(plainStr))
                {
                    using (var encryptor = aes.CreateEncryptor())
                    {
                        var plainBytes = Encoding.UTF8.GetBytes(plainStr);
                        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                        return Convert.ToBase64String(encryptedBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Encryption error: {ex.Message}");
                Initialize();
            }
            return plainStr;
        }

        public string Decrypt(string encryptedStr)
        {
            try
            {
                if (!string.IsNullOrEmpty(encryptedStr))
                {
                    using (var decryptor = aes.CreateDecryptor())
                    {
                        var encryptedBytes = Convert.FromBase64String(encryptedStr);
                        var plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                        return Encoding.UTF8.GetString(plainBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Decryption error: {ex.Message}");
                Initialize();
            }
            return encryptedStr;
        }

        public bool IsEncrypted(string text)
        {
            try
            {
                if (!string.IsNullOrEmpty(text) && text.Length >= 3)
                {
                    string decrypted = Decrypt(text);
                    return text != decrypted;
                }
            }
            catch (Exception)
            {
                Initialize();
            }
            return false;
        }
    }
}