using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LLMUnity;
using UnityEngine;

public sealed class LLMItemDescriptionService : IDisposable
{
    private readonly LLMAgent _agent;
    private readonly ItemTagProvider _tagProvider;
    private readonly ItemDescriptionRegistry _registry;
    private readonly SemaphoreSlim _generationLock = new(1, 1);

    private CancellationTokenSource _debounceCts;
    private CancellationTokenSource _generationCts;

    private const float DebounceSeconds = 0.4f;

    public event Action<string> OnDescriptionToken;
    public event Action OnDescriptionStarted;

    public LLMItemDescriptionService(
        LLMAgent agent,
        ItemTagProvider tagProvider,
        ItemDescriptionRegistry registry)
    {
        _agent = agent;
        _tagProvider = tagProvider;
        _registry = registry;
    }

    public void RequestDescription(IInventoryItem item, PlayerContext player)
    {
        CancelDebounce();
        _debounceCts = new CancellationTokenSource();
        DebounceAsync(item, player, _debounceCts.Token).Forget();
    }

    private async UniTaskVoid DebounceAsync(
        IInventoryItem item,
        PlayerContext player,
        CancellationToken ct)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(DebounceSeconds), cancellationToken: ct);

        if (ct.IsCancellationRequested) return;

        await GenerateAsync(item, player, ct);
    }

    private async UniTask GenerateAsync(
        IInventoryItem item,
        PlayerContext player,
        CancellationToken debounceCt)
    {
        CancelGeneration();

        bool acquired = await _generationLock
            .WaitAsync(TimeSpan.FromSeconds(2))
            .AsUniTask()
            .AttachExternalCancellation(debounceCt);

        if (!acquired || debounceCt.IsCancellationRequested) return;

        _generationCts = new CancellationTokenSource();
        var genCt = CancellationTokenSource
            .CreateLinkedTokenSource(_generationCts.Token, debounceCt)
            .Token;

        try
        {
            OnDescriptionStarted?.Invoke();

            var itemCtx = _tagProvider.GetContext(item);
            var prompt = ItemPromptBuilder.Build(itemCtx, player, player.Expertise, player.Doubt);

            void OnToken(string token)
            {
                if (!genCt.IsCancellationRequested)
                    OnDescriptionToken?.Invoke(token);
            }

            string result;
            try
            {
                result = await _agent
                    .Chat(query: prompt, callback: OnToken, addToHistory: false)
                    .AsUniTask()
                    .AttachExternalCancellation(genCt);
            }
            catch (OperationCanceledException)
            {
                _agent.CancelRequests();
                throw;
            }

            if (string.IsNullOrWhiteSpace(result)) return;

            // Пишем в registry — теперь он источник истины
            _registry.SetDynamic(item, result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogError($"[LLMItemDescriptionService] {ex}");
        }
        finally
        {
            _generationLock.Release();
        }
    }

    private void CancelDebounce()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    private void CancelGeneration()
    {
        _generationCts?.Cancel();
        _generationCts?.Dispose();
        _generationCts = null;
    }

    public void Dispose()
    {
        CancelDebounce();
        CancelGeneration();
        _agent.CancelRequests();
        _generationLock.Dispose();
    }
}