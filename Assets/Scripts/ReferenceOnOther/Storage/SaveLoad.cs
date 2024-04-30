using UnityEngine;

namespace TheRavine.Security
{
    public static class SaveLoad
    {
        private static string EncryptData<T>(T data)
        {
            string jsonData = JsonUtility.ToJson(data);
            return EncryptionUtility.EncryptString(jsonData);
        }

        private static T DecryptData<T>(string cipherText)
        {
            string jsonData = EncryptionUtility.DecryptString(cipherText);
            return JsonUtility.FromJson<T>(jsonData);
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
                try
                {
                    T decryptedData = DecryptData<T>(encryptedData);
                    return decryptedData;
                }
                catch
                {
                    // data is corrupted
                    return default(T);
                }
            }
            else
            {
                Debug.LogWarning("No data found for key: " + key);
                return default(T);
            }
        }
    }
}