using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{

    public class ScriptFileManager : IFileManager<string, string>
    {
        private readonly IAsyncPersistentStorage _storage;
        private const string FilesListKey = "script_files_list";
        private const string FileKeyPrefix = "script_file_";

        public ScriptFileManager(IAsyncPersistentStorage storage)
        {
            _storage = storage;
        }

        public async UniTask SaveAsync(string fileName, string content)
        {
            string contentKey = FileKeyPrefix + fileName;
            await _storage.SaveAsync(contentKey, content);

            var list = await ListIdsAsync() as List<string>;
            if (!list.Contains(fileName))
            {
                list.Add(fileName);
                await _storage.SaveAsync(FilesListKey, list);
            }
        }

        public async UniTask<string> LoadAsync(string fileName)
        {
            string contentKey = FileKeyPrefix + fileName;
            return await _storage.LoadAsync<string>(contentKey);
        }

        public async UniTask<bool> ExistsAsync(string fileName)
        {
            string contentKey = FileKeyPrefix + fileName;
            return await _storage.ExistsAsync(contentKey);
        }

        public async UniTask DeleteAsync(string fileName)
        {
            string contentKey = FileKeyPrefix + fileName;
            await _storage.DeleteAsync(contentKey);

            var list = await ListIdsAsync() as List<string>;
            if (list.Remove(fileName))
                await _storage.SaveAsync(FilesListKey, list);
        }

        public async UniTask<IReadOnlyList<string>> ListIdsAsync()
        {
            var list = await _storage.LoadAsync<List<string>>(FilesListKey);
            return list ?? new List<string>();
        }
    }
}