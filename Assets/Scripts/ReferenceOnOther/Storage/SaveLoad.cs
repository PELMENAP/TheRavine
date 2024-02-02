using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace TheRavine.Security
{
    public static class SaveLoad
    {
        private static string encryptionKey = "00240919071967018635036458127536408510761230876409124681293564214912764928358176401854125418924";

        private static string EncryptData<T>(T data)
        {
            string jsonData = JsonUtility.ToJson(data);
            return EncryptString(jsonData);
        }

        private static T DecryptData<T>(string cipherText)
        {
            string jsonData = DecryptString(cipherText);
            return JsonUtility.FromJson<T>(jsonData);
        }

        private static string EncryptString(string text)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            using (Aes aes = Aes.Create())
            {
                Rfc2898DeriveBytes keyDerivation = new Rfc2898DeriveBytes(encryptionKey, aes.IV);
                aes.Key = keyDerivation.GetBytes(32);
                aes.Mode = CipherMode.CBC;
                using (MemoryStream encryptStream = new MemoryStream())
                {
                    encryptStream.Write(aes.IV, 0, aes.IV.Length); // Записываем IV в начало потока
                    using (CryptoStream cryptoStream = new CryptoStream(encryptStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(textBytes, 0, textBytes.Length);
                        cryptoStream.FlushFinalBlock();
                        byte[] encryptedBytes = encryptStream.ToArray();
                        return Convert.ToBase64String(encryptedBytes);
                    }
                }
            }
        }

        private static string DecryptString(string cipherText)
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                aes.Key = new Rfc2898DeriveBytes(encryptionKey, aes.IV).GetBytes(32);
                aes.Mode = CipherMode.CBC;
                byte[] iv = new byte[16];
                Array.Copy(cipherBytes, 0, iv, 0, iv.Length); // Читаем IV из зашифрованных данных
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

        public static void SaveEncryptedData<T>(string key, T data)
        {
            string encryptedData = EncryptData(data);
            PlayerPrefs.SetString(key, encryptedData);
            PlayerPrefs.Save();
        }

        public static T LoadEncryptedData<T>(string key)
        {
            string encryptedData = PlayerPrefs.GetString(key);
            if (!string.IsNullOrEmpty(encryptedData))
            {
                T decryptedData = DecryptData<T>(encryptedData);
                return decryptedData;
            }
            else
            {
                Debug.LogWarning("No data found for key: " + key);
                return default(T);
            }
        }
    }
}