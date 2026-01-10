using TheRavine.EntityControl;

using System.Collections.Generic;
using ZLinq;
using System;
using UnityEngine;
using R3;

public class PlayerContainer
{
    private readonly List<AEntity> _players = new();
    private readonly Dictionary<ulong, AEntity> _playersByClientId = new();
    private readonly Dictionary<string, AEntity> _playersByDisplayName = new();
    private readonly Dictionary<string, int> _nameCounters = new();
    
    private readonly Subject<AEntity> _onPlayerRegistered = new();
    private readonly Subject<Unit> _onPlayersBecameNonEmpty = new();

    private bool _wasEmpty = true;

    public Observable<AEntity> OnPlayerRegistered => _onPlayerRegistered;
    public Observable<Unit> OnPlayersBecameNonEmpty => _onPlayersBecameNonEmpty;

    public bool RegisterPlayer(AEntity player)
    {
        if (player == null || _players.Contains(player))
            return false;

        var mainComponent = player.GetEntityComponent<MainComponent>();
        var clientId = mainComponent.GetClientID();
        var baseName = mainComponent.GetEntityName();

        if (_playersByClientId.ContainsKey(clientId))
            return false;

        var displayName = GenerateUniqueDisplayName(baseName);
        
        _players.Add(player);
        _playersByClientId.Add(clientId, player);
        _playersByDisplayName.Add(displayName, player);
        
        _onPlayerRegistered.OnNext(player);

        if (_wasEmpty)
        {
            _wasEmpty = false;
            _onPlayersBecameNonEmpty.OnNext(Unit.Default);
        }

        return true;
    }

    public bool UnregisterPlayer(AEntity player)
    {
        if (player == null || !_players.Contains(player))
            return false;

        var mainComponent = player.GetEntityComponent<MainComponent>();
        var clientId = mainComponent.GetClientID();
        var displayName = GetPlayerDisplayName(player);

        _players.Remove(player);
        _playersByClientId.Remove(clientId);
        
        if (!string.IsNullOrEmpty(displayName))
            _playersByDisplayName.Remove(displayName);

        if (_players.Count == 0)
            _wasEmpty = true;

        return true;
    }

    private string GenerateUniqueDisplayName(string baseName)
    {
        if (!_nameCounters.ContainsKey(baseName))
        {
            _nameCounters[baseName] = 1;
            return baseName;
        }

        _nameCounters[baseName]++;
        return $"{baseName}#{_nameCounters[baseName]}";
    }

    public AEntity GetPlayerById(ulong clientId)
    {
        return _playersByClientId.TryGetValue(clientId, out var player) ? player : null;
    }

    public bool TryGetPlayerById(ulong clientId, out AEntity player)
    {
        return _playersByClientId.TryGetValue(clientId, out player);
    }

    public AEntity GetPlayerByName(string displayName)
    {
        return _playersByDisplayName.TryGetValue(displayName, out var player) ? player : null;
    }

    public bool TryGetPlayerByName(string displayName, out AEntity player)
    {
        return _playersByDisplayName.TryGetValue(displayName, out player);
    }

    public string GetPlayerDisplayName(AEntity player)
    {
        foreach (var kvp in _playersByDisplayName)
        {
            if (kvp.Value == player)
                return kvp.Key;
        }
        return null;
    }

    public IReadOnlyList<string> GetAllDisplayNames()
    {
        return new List<string>(_playersByDisplayName.Keys);
    }

    public IReadOnlyList<AEntity> FindPlayersByBaseName(string baseName)
    {
        var result = new List<AEntity>();
        foreach (var player in _players)
        {
            var name = player.GetEntityComponent<MainComponent>().GetEntityName();
            if (name.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                result.Add(player);
        }
        return result;
    }

    public IReadOnlyList<AEntity> GetAllPlayers() => _players;
    
    public IReadOnlyList<Transform> GetAllPlayersTransform() => 
        _players.AsValueEnumerable()
            .Select(x => x.GetEntityComponent<TransformComponent>().GetEntityTransform())
            .ToList();

    public void Clear()
    {
        _players.Clear();
        _playersByClientId.Clear();
        _playersByDisplayName.Clear();
        _nameCounters.Clear();
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
    {
        if (_playersByClientId.TryGetValue(id, out var player))
            return Observable.Return(player);

        return _onPlayerRegistered.Where(p => 
            p.GetEntityComponent<MainComponent>().GetClientID() == id);
    }

    public Observable<AEntity> WaitForPlayerByName(string displayName)
    {
        if (_playersByDisplayName.TryGetValue(displayName, out var player))
            return Observable.Return(player);

        return _onPlayerRegistered.Where(p => 
            GetPlayerDisplayName(p)?.Equals(displayName, StringComparison.OrdinalIgnoreCase) == true);
    }
}