using TheRavine.EntityControl;

using System.Collections.Generic;
using ZLinq;
using System;
using UnityEngine;
using R3;

public class PlayerContainer
{
    private readonly List<AEntity> _players = new();
    private readonly Subject<AEntity> _onPlayerRegistered = new();
    private readonly Subject<Unit> _onPlayersBecameNonEmpty = new();

    private bool _wasEmpty = true;

    public Observable<AEntity> OnPlayerRegistered => _onPlayerRegistered;
    public Observable<Unit> OnPlayersBecameNonEmpty => _onPlayersBecameNonEmpty;

    public bool RegisterPlayer(AEntity player)
    {
        if (player == null || _players.Contains(player))
            return false;

        _players.Add(player);
        _onPlayerRegistered.OnNext(player);

        if (_wasEmpty)
        {
            _wasEmpty = false;
            _onPlayersBecameNonEmpty.OnNext(Unit.Default);
        }

        return true;
    }

    public IReadOnlyList<AEntity> GetAllPlayers() => _players;
    public IReadOnlyList<Transform> GetAllPlayersTransform() => _players.AsValueEnumerable().Select(x => x.GetEntityComponent<TransformComponent>().GetEntityTransform()).ToList();

    public void Clear()
    {
        _players.Clear();
        _wasEmpty = true;
    }

    public Observable<AEntity> WaitForPlayer(Func<AEntity, bool> predicate)
    {
        foreach (var p in _players)
            if (predicate(p))
                return Observable.Return(p);

        return _onPlayerRegistered.Where(predicate);
    }

    public Observable<AEntity> WaitForPlayerById(ulong id)
        => WaitForPlayer(p => p.GetEntityComponent<MainComponent>().GetClientID() == id);
}