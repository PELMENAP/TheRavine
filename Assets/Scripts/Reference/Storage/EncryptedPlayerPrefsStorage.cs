using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

using TheRavine.Security;


namespace TheRavine.Base
{
    public class EncryptedPlayerPrefsStorage : IAsyncPersistentStorage
    {
        public async UniTask SaveAsync<T>(string key, T data, CancellationToken ct = default)
        {
            var cipher = await UniTask.RunOnThreadPool(() =>
            {
                string json = JsonUtility.ToJson(data);
                return EncryptionUtility.EncryptString(json);
            }, cancellationToken: ct);

            await UniTask.SwitchToMainThread(ct);
            PlayerPrefs.SetString(key, cipher);
            PlayerPrefs.Save();
        }
        public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
        {
            await UniTask.SwitchToMainThread(ct);
            string cipher = PlayerPrefs.GetString(key);

            if (string.IsNullOrEmpty(cipher))
                return default;

            try
            {
                return await UniTask.RunOnThreadPool(() =>
                {
                    string json = EncryptionUtility.DecryptString(cipher);
                    return JsonUtility.FromJson<T>(json);
                }, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                ServiceLocator.GetService<ILogger>().LogError($"Decrypt/Parse error for '{key}': {ex.Message}");
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