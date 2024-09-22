using System;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace TheRavine.Security
{
    public static class EncryptionUtility
    {
        private static string encryptionKey = "00240ny87c2yn2t42ux7v12390uv5c02mx88924";
        private static readonly byte[] salt = Encoding.UTF8.GetBytes("01jr8h232h9u3he27g3");
        public static string EncryptString(string text)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            using (Aes aes = Aes.Create())
            {
                aes.Key = new Rfc2898DeriveBytes(encryptionKey, salt).GetBytes(aes.KeySize / 8);
                aes.Mode = CipherMode.CBC;
                aes.GenerateIV();
                using (MemoryStream encryptStream = new MemoryStream())
                {
                    encryptStream.Write(aes.IV, 0, aes.IV.Length);
                    using (CryptoStream cryptoStream = new CryptoStream(encryptStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(textBytes, 0, textBytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }
                    byte[] encryptedBytes = encryptStream.ToArray();
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
        }

        public static string DecryptString(string cipherText)
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                aes.Key = new Rfc2898DeriveBytes(encryptionKey, salt).GetBytes(aes.KeySize / 8);
                aes.Mode = CipherMode.CBC;
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                aes.IV = iv;
                using (MemoryStream decryptStream = new MemoryStream(cipherBytes, iv.Length, cipherBytes.Length - iv.Length))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(decryptStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}