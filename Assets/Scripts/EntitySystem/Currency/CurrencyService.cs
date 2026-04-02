using R3;
using System;
using System.Collections.Generic;
public sealed class CurrencyService : IDisposable
{
    private readonly PlayerContainer _players;
    private readonly RavineLogger _logger;
    private readonly HashSet<string> _registeredModifiers = new();

    public Observable<CurrencyTransaction> OnTransactionCompleted => _onTransaction;
    private readonly Subject<CurrencyTransaction> _onTransaction = new();

    public CurrencyService(PlayerContainer players, RavineLogger logger)
    {
        _players = players;
        _logger = logger;
    }

    public void RegisterModifier(ICurrencyModifier modifier)
        => _registeredModifiers.Add(modifier.ModifierId);

    public bool TryModify(ICurrencyModifier source, ulong clientId, int delta)
    {
        if (!_registeredModifiers.Contains(source.ModifierId))
        {
            _logger.LogWarning($"[Currency] Неавторизованный источник: {source.ModifierId}");
            return false;
        }

        if (!_players.TryGetPlayerById(clientId, out var player))
        {
            _logger.LogWarning($"[Currency] Игрок {clientId} не найден");
            return false;
        }

        var component = player.GetEntityComponent<CurrencyComponent>();
        if (component == null) return false;

        int current = component.GetRaw();
        int next = current + delta;

        if (next < 0)
        {
            _logger.LogWarning($"[Currency] Недостаточно средств: {current} + {delta}");
            return false;
        }

        RequestCurrencyUpdateServerRpc(clientId, next);
        return true;
    }

    private void RequestCurrencyUpdateServerRpc(ulong clientId, int newValue)
    {
        if (_players.TryGetPlayerById(clientId, out var player))
        {
            var netBehaviour = player.GetEntityComponent<CurrencyNetworkComponent>();
            netBehaviour?.RequestUpdateRpc(newValue);
        }
    }

    public void Dispose() => _onTransaction.Dispose();
}

public readonly struct CurrencyTransaction
{
    public readonly ulong ClientId;
    public readonly int Delta;
    public readonly string SourceId;
    public readonly int ResultAmount;

    public CurrencyTransaction(ulong clientId, int delta, string sourceId, int result)
    {
        ClientId = clientId;
        Delta = delta;
        SourceId = sourceId;
        ResultAmount = result;
    }
}