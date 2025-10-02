using UnityEngine;
using System.Collections.Generic;

public class PlayerContainer
{
    private readonly List<Transform> _players = new();

    public void RegisterPlayer(Transform player)
    {
        if (player != null && !_players.Contains(player))
            _players.Add(player);
    }

    public Transform GetFirstPlayer() => _players.Count > 0 ? _players[0] : null;

    public IReadOnlyList<Transform> GetAllPlayers() => _players;

    public void Clear() => _players.Clear();
}
