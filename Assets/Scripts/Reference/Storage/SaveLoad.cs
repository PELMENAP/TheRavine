using UnityEngine;
using System.Collections.Generic;

using TheRavine.Security;
namespace TheRavine.Base
{
    public static class SaveLoad
    {
        private const string worldKeyPrefix = "world_";
        private const string settingsKeyPrefix = "settings_";
        private const string worldIndexKey = "worldIndex";
        
        [System.Serializable]
        private class WorldIndex
        {
            public List<string> worldNames = new List<string>();
        }

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

        private static string GetFullKey(string key, bool gameSettings = false)
        {
            return gameSettings ? settingsKeyPrefix + key : worldKeyPrefix + key;
        }

        public static void SaveEncryptedData<T>(string key, T data, bool gameSettings = false)
        {
            string fullKey = GetFullKey(key, gameSettings);
            string encryptedData = EncryptData(data);
            
            PlayerPrefs.SetString(fullKey, encryptedData);
            
            if (!gameSettings)
            {
                UpdateWorldIndex(key, add: true);
            }
            
            PlayerPrefs.Save();
        }

        public static T LoadEncryptedData<T>(string key, bool gameSettings = false)
        {
            string fullKey = GetFullKey(key, gameSettings);
            string encryptedData = PlayerPrefs.GetString(fullKey);
            
            if (!string.IsNullOrEmpty(encryptedData))
            {
                try
                {
                    return DecryptData<T>(encryptedData);
                }
                catch (System.Exception ex)
                {
                    ServiceLocator.GetLogger().LogError($"Failed to decrypt data for key '{key}': {ex.Message}");
                    return default(T);
                }
            }
            else
            {
                ServiceLocator.GetLogger().LogWarning($"No data found for key: {key}");
                return default(T);
            }
        }

        public static bool FileExists(string key, bool gameSettings = false)
        {
            string fullKey = GetFullKey(key, gameSettings);
            return PlayerPrefs.HasKey(fullKey);
        }

        public static void DeleteFile(string key, bool gameSettings = false)
        {
            string fullKey = GetFullKey(key, gameSettings);
            
            if (PlayerPrefs.HasKey(fullKey))
            {
                PlayerPrefs.DeleteKey(fullKey);
                if (!gameSettings)
                {
                    UpdateWorldIndex(key, add: false);
                }
                
                PlayerPrefs.Save();
            }
            else
            {
                ServiceLocator.GetLogger().LogWarning($"Key '{key}' not found for deletion");
            }
        }

        public static string[] GetAllWorldNames()
        {
            var worldIndex = LoadWorldIndex();
            
            var validWorldNames = new List<string>();
            foreach (string worldName in worldIndex.worldNames)
            {
                if (FileExists(worldName, gameSettings: false))
                {
                    validWorldNames.Add(worldName);
                }
            }
            
            if (validWorldNames.Count != worldIndex.worldNames.Count)
            {
                worldIndex.worldNames = validWorldNames;
                SaveWorldIndex(worldIndex);
            }
            
            return validWorldNames.ToArray();
        }

        private static void UpdateWorldIndex(string worldName, bool add)
        {
            var worldIndex = LoadWorldIndex();
            
            if (add)
            {
                if (!worldIndex.worldNames.Contains(worldName))
                {
                    worldIndex.worldNames.Add(worldName);
                    SaveWorldIndex(worldIndex);
                }
            }
            else
            {
                if (worldIndex.worldNames.Remove(worldName))
                {
                    SaveWorldIndex(worldIndex);
                }
            }
        }

        private static WorldIndex LoadWorldIndex()
        {
            string indexData = PlayerPrefs.GetString(worldIndexKey, "");
            
            if (!string.IsNullOrEmpty(indexData))
            {
                try
                {
                    return JsonUtility.FromJson<WorldIndex>(indexData);
                }
                catch
                {
                    ServiceLocator.GetLogger().LogWarning("World index corrupted, creating new one");
                }
            }
            
            return new WorldIndex();
        }

        private static void SaveWorldIndex(WorldIndex worldIndex)
        {
            string indexData = JsonUtility.ToJson(worldIndex);
            PlayerPrefs.SetString(worldIndexKey, indexData);
        }

        [System.Obsolete("Use only for migration from old system")]
        public static void MigrateFromOldSystem()
        {
            #if UNITY_EDITOR
            ServiceLocator.GetLogger().LogInfo("Starting migration from old PlayerPrefs system...");
            
            var worldIndex = new WorldIndex();
            var allKeys = GetAllPlayerPrefsKeysLegacy();
            
            foreach (string key in allKeys)
            {
                if (key.StartsWith(worldKeyPrefix))
                {
                    string worldName = key.Substring(worldKeyPrefix.Length);
                    worldIndex.worldNames.Add(worldName);
                }
            }
            
            SaveWorldIndex(worldIndex);
            ServiceLocator.GetLogger().LogInfo($"Migration completed. Found {worldIndex.worldNames.Count} worlds.");
            #endif
        }

        #if UNITY_EDITOR
        private static string[] GetAllPlayerPrefsKeysLegacy()
        {
            var keys = new List<string>();
            
            #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Unity\\UnityEditor\\{Application.companyName}\\{Application.productName}"))
            {
                if (key != null)
                {
                    keys.AddRange(key.GetValueNames().Where(name => !name.EndsWith("_h3320113202")));
                }
            }
            #endif
            
            return keys.ToArray();
        }
        #endif
    }
}