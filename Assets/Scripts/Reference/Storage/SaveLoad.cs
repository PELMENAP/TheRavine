using UnityEngine;

using TheRavine.Security;
namespace TheRavine.Base
{
    public static class SaveLoad
    {
        private const string worldKeyPrefix = "world_", settingsKeyPrefix = "globalSettings_";
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
        public static void SaveEncryptedData<T>(string key, T data, bool gameSettings = false)
        {
            string encryptedData = EncryptData(data);
            PlayerPrefs.SetString(gameSettings ? settingsKeyPrefix : worldKeyPrefix + key, encryptedData);
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

        public static bool FileExists(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public static void DeleteFile(string key)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogWarning($"Key '{key}' not found for deletion");
            }
        }

        public static string[] GetAllWorldNames()
        {
            var worldNames = new System.Collections.Generic.List<string>();
            
            var prefsKeys = GetAllPlayerPrefsKeys();
            foreach (string key in prefsKeys)
            {
                if (key.StartsWith(worldKeyPrefix))
                {
                    string worldName = key.Substring(worldKeyPrefix.Length);
                    worldNames.Add(worldName);
                }
            }
            
            return worldNames.ToArray();
        }

        private static string[] GetAllPlayerPrefsKeys()
        {
            var keys = new System.Collections.Generic.List<string>();
            
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Unity\\UnityEditor\\{Application.companyName}\\{Application.productName}"))
            {
                if (key != null)
                {
                    keys.AddRange(key.GetValueNames().Where(name => !name.EndsWith("_h3320113202")));
                }
            }
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            string plistPath = $"~/Library/Preferences/unity.{Application.companyName}.{Application.productName}.plist";
            if (System.IO.File.Exists(plistPath))
            {
                string[] lines = System.IO.File.ReadAllLines(plistPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("<key>") && !lines[i].Contains("_h"))
                    {
                        string keyLine = lines[i];
                        int startIndex = keyLine.IndexOf("<key>") + 5;
                        int endIndex = keyLine.IndexOf("</key>");
                        if (startIndex > 4 && endIndex > startIndex)
                        {
                            keys.Add(keyLine.Substring(startIndex, endIndex - startIndex));
                        }
                    }
                }
            }
#else
            var playerPrefsType = typeof(PlayerPrefs);
            var method = playerPrefsType.GetMethod("GetAllKeys", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            
            if (method != null)
            {
                var result = method.Invoke(null, null) as string[];
                if (result != null)
                    keys.AddRange(result);
            }
#endif
            
            return keys.ToArray();
        }
    }
}