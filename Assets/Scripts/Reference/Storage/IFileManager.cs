using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IFileManager<TEntityIdentifier, TEntity>
{
    UniTask SaveAsync(TEntityIdentifier id, TEntity entity);
    UniTask<TEntity> LoadAsync(TEntityIdentifier id);
    UniTask<bool> ExistsAsync(TEntityIdentifier id);
    UniTask DeleteAsync(TEntityIdentifier id);
    UniTask<IReadOnlyList<TEntityIdentifier>> ListIdsAsync();
}