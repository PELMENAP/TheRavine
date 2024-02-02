using System;
using System.Text;
using System.Security.Cryptography;

public static class EncryptionUtility
{
    public static string Encrypt(string plainText, string key)
    {
        byte[] encryptedBytes;
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = Encoding.UTF8.GetBytes(key);
            aesAlg.IV = new byte[aesAlg.BlockSize / 8];
            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            byte[] bytesToEncrypt = Encoding.UTF8.GetBytes(plainText);
            encryptedBytes = encryptor.TransformFinalBlock(bytesToEncrypt, 0, bytesToEncrypt.Length);
        }
        return Convert.ToBase64String(encryptedBytes);
    }

    public static string Decrypt(string cipherText, string key)
    {
        byte[] decryptedBytes;
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = Encoding.UTF8.GetBytes(key);
            aesAlg.IV = new byte[aesAlg.BlockSize / 8];
            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        }
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
