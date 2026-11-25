using System.Collections.Generic;
using TheRavine.EntityControl;

public sealed class EntityCodexPurse : ICodexPurse
{
    private readonly ColonyRegistry _colonies;
    private readonly List<EntityModel> _entities;

    public EntityCodexPurse(ColonyRegistry colonies, List<EntityModel> entities)
    {
        _colonies = colonies;
        _entities = entities;
    }

    public float Balance(int authorId)
    {
        if (CodexAuthor.IsColony(authorId))
            return _colonies.TryGetById(CodexAuthor.ColonyOf(authorId), out var colony) ? colony.Nest.Storage : 0f;

        if (CodexAuthor.IsPlayer(authorId))
            return TryGetPlayerStats(authorId, out var stats) ? stats.En : 0f;

        var model = FindEntity(authorId);
        return model != null ? model.Stats.En : 0f;
    }

    public bool TryDebit(int authorId, float amount)
    {
        if (!(amount > 0f)) return false;

        if (CodexAuthor.IsColony(authorId))
        {
            if (!_colonies.TryGetById(CodexAuthor.ColonyOf(authorId), out var colony) || colony.Nest.Storage < amount) return false;
            colony.Nest.Storage -= amount;
            return true;
        }

        if (CodexAuthor.IsPlayer(authorId))
        {
            if (!ServiceLocator.Players.TryGetPlayerById(CodexAuthor.ClientOf(authorId), out var player)) return false;
            var energy = player.GetEntityComponent<EnergyComponent>();
            return energy != null && TryGetPlayerStats(authorId, out _) && energy.TryConsume(amount);
        }

        var model = FindEntity(authorId);
        if (model == null || model.Stats.En < amount) return false;
        model.Stats.En -= amount;
        return true;
    }

    private bool TryGetPlayerStats(int authorId, out StatsComponent stats)
    {
        stats = null;
        if (!ServiceLocator.Players.TryGetPlayerById(CodexAuthor.ClientOf(authorId), out var player) || player.IsDisposed) return false;

        var mortality = player.GetEntityComponent<MortalityComponent>();
        if (mortality != null && mortality.IsDead) return false;

        stats = player.GetEntityComponent<StatsComponent>();
        return stats != null && !stats.IsDisposed;
    }

    private EntityModel FindEntity(int entityId)
    {
        for (int i = 0; i < _entities.Count; i++)
        {
            var e = _entities[i];
            if (e.EntityId != entityId) continue;
            return e.IsDisposed || e.IsDeathPending || e.Stats == null || e.Stats.IsDisposed ? null : e;
        }
        return null;
    }
}
