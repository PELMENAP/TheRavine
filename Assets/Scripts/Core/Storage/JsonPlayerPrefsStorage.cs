using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;


namespace TheRavine.Base
{
    public class JsonPlayerPrefsStorage : IAsyncPersistentStorage
    {
        public async UniTask SaveAsync<T>(string key, T data, CancellationToken ct = default)
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            await UniTask.SwitchToMainThread(ct);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
        {
            await UniTask.SwitchToMainThread(ct);
            string json = PlayerPrefs.GetString(key, null);
            if (string.IsNullOrEmpty(json))
                return default;
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                ServiceLocator.GetService<RavineLogger>()
                    .LogError($"JSON Deserialize error for '{key}': {ex.Message}");
                return default;
            }
        }

        public async UniTask<bool> ExistsAsync(string key, CancellationToken ct = default)
        {
            await UniTask.SwitchToMainThread(ct);
            return PlayerPrefs.HasKey(key);
        }

        public async UniTask DeleteAsync(string key, CancellationToken ct = default)
        {
            await UniTask.SwitchToMainThread(ct);
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }

}