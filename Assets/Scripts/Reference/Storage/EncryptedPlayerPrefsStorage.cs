using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using MemoryPack;

using TheRavine.Security;


namespace TheRavine.Base
{
    public class EncryptedPlayerPrefsStorage : IAsyncPersistentStorage
    {
        public async UniTask SaveAsync<T>(string key, T data, CancellationToken ct = default)
        {
            var encrypted = await UniTask.RunOnThreadPool(() =>
            {
                byte[] raw = MemoryPackSerializer.Serialize(data);
                byte[] cipher = EncryptionUtility.EncryptBytes(raw);

                return Convert.ToBase64String(cipher);
            }, cancellationToken: ct);

            await UniTask.SwitchToMainThread(ct);

            PlayerPrefs.SetString(key, encrypted);
            PlayerPrefs.Save();
        }

        public async UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default)
        {
            await UniTask.SwitchToMainThread(ct);
            string base64 = PlayerPrefs.GetString(key, null);

            if (string.IsNullOrEmpty(base64))
                return default;

            try
            {
                return await UniTask.RunOnThreadPool(() =>
                {
                    byte[] cipher = Convert.FromBase64String(base64);
                    byte[] raw = EncryptionUtility.DecryptBytes(cipher);

                    return MemoryPackSerializer.Deserialize<T>(raw);
                }, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                ServiceLocator.GetService<IRavineLogger>()
                    .LogError($"Decrypt/Deserialize error for '{key}': {ex.Message}");
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