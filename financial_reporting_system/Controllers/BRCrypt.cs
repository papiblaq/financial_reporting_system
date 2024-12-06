using System;
using System.IO;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

public class BRCrypt
{
    private static readonly byte[] salt = { 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90 };
    private static readonly int iterationCount = 19;
    private static readonly string passphrase = "This is supernova project !";

    private static PbeParametersGenerator GetPbeParametersGenerator()
    {
        Pkcs5S2ParametersGenerator generator = new Pkcs5S2ParametersGenerator();
        generator.Init(
            PbeParametersGenerator.Pkcs5PasswordToBytes(passphrase.ToCharArray()),
            salt,
            iterationCount
        );
        return generator;
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        PbeParametersGenerator generator = GetPbeParametersGenerator();
        ParametersWithIV parameters = (ParametersWithIV)generator.GenerateDerivedParameters("DES", 64, 64);

        IBufferedCipher cipher = CipherUtilities.GetCipher("DES/CBC/PKCS5Padding");
        cipher.Init(true, parameters);

        byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = cipher.DoFinal(inputBytes);

        return Convert.ToBase64String(encryptedBytes);
    }

    public static string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            return encryptedText;
        }

        try
        {
            PbeParametersGenerator generator = GetPbeParametersGenerator();
            ParametersWithIV parameters = (ParametersWithIV)generator.GenerateDerivedParameters("DES", 64, 64);

            IBufferedCipher cipher = CipherUtilities.GetCipher("DES/CBC/PKCS5Padding");
            cipher.Init(false, parameters);

            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

            // Debugging: Print the length of the encrypted bytes
            Console.WriteLine($"Encrypted Bytes Length: {encryptedBytes.Length}");

            byte[] decryptedBytes = cipher.DoFinal(encryptedBytes);

            // Debugging: Print the length of the decrypted bytes
            Console.WriteLine($"Decrypted Bytes Length: {decryptedBytes.Length}");

            string decryptedString = Encoding.UTF8.GetString(decryptedBytes);

            // Debugging: Print the decrypted text
            Console.WriteLine($"Decrypted Text: {decryptedString}");

            return decryptedString;
        }
        catch (Exception ex)
        {
            // Print the exception details for debugging
            Console.WriteLine($"Decryption Error: {ex.Message}");
            throw;
        }
    }

    public static bool IsEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 3)
        {
            return false;
        }

        try
        {
            string decryptedText = Decrypt(text);
            return !text.Equals(decryptedText);
        }
        catch
        {
            return false;
        }
    }
}