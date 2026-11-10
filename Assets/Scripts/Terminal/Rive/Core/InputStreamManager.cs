using System;
using System.Collections.Generic;
using System.Threading;
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

        public async UniTask<int> GetAsync(CancellationToken token, int timeoutMs)
        {
            token.ThrowIfCancellationRequested();

            var completionSource = new UniTaskCompletionSource<int>();
            _waitingReaders.Add(completionSource);

            using var expiry = CancellationTokenSource.CreateLinkedTokenSource(token);
            ExpireAsync(completionSource, timeoutMs, expiry.Token).Forget();

            try
            {
                return await completionSource.Task.AttachExternalCancellation(token);
            }
            finally
            {
                expiry.Cancel();
                _waitingReaders.Remove(completionSource);
            }
        }

        private static async UniTaskVoid ExpireAsync(UniTaskCompletionSource<int> completionSource, int timeoutMs, CancellationToken token)
        {
            var canceled = await UniTask.Delay(timeoutMs, cancellationToken: token, cancelImmediately: true).SuppressCancellationThrow();
            if (!canceled)
                completionSource.TrySetException(new TimeoutException("Input timeout"));
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