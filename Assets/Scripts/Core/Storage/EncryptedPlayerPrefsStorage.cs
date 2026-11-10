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
        private const int FlushDelayMs = 1500;

        private static bool _dirty;
        private static bool _flushScheduled;

        static EncryptedPlayerPrefsStorage()
        {
            Application.quitting += FlushNow;
            Application.focusChanged += OnFocusChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _dirty          = false;
            _flushScheduled = false;
        }

        private static void OnFocusChanged(bool focus)
        {
            if (!focus) FlushNow();
        }

        private static void FlushNow()
        {
            if (!_dirty) return;
            _dirty = false;
            PlayerPrefs.Save();
        }

        private static void MarkDirty()
        {
            _dirty = true;
            if (_flushScheduled) return;
            _flushScheduled = true;
            FlushLaterAsync().Forget();
        }

        private static async UniTaskVoid FlushLaterAsync()
        {
            await UniTask.Delay(FlushDelayMs, DelayType.UnscaledDeltaTime);
            _flushScheduled = false;
            FlushNow();
        }

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
            MarkDirty();
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
                ServiceLocator.GetService<RavineLogger>()
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
                MarkDirty();
            }
        }
    }
}