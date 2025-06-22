using Cysharp.Threading.Tasks;
using System.Threading;


namespace TheRavine.Base
{
    public interface IAsyncPersistentStorage
    {
        UniTask SaveAsync<T>(string key, T data, CancellationToken ct = default);
        UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default);
        UniTask<bool> ExistsAsync(string key, CancellationToken ct = default);
        UniTask DeleteAsync(string key, CancellationToken ct = default);
    }
}