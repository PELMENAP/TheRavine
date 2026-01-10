using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    public class InputStreamManager
    {
        private readonly List<UniTaskCompletionSource<int>> _waitingReaders = new();
        private int _lastInputValue;

        public void PushInput(int value)
        {
            _lastInputValue = value;

            var readers = new List<UniTaskCompletionSource<int>>(_waitingReaders);
            _waitingReaders.Clear();

            foreach (var reader in readers)
            {
                reader.TrySetResult(value);
            }
        }

        public async UniTask<int> GetAsync()
        {
            var completionSource = new UniTaskCompletionSource<int>();
            _waitingReaders.Add(completionSource);
            
            return await completionSource.Task;
        }

        public void Clear()
        {
            foreach (var reader in _waitingReaders)
            {
                reader.TrySetCanceled();
            }
            _waitingReaders.Clear();
        }

        public int GetWaitingReadersCount() => _waitingReaders.Count;
    }
}