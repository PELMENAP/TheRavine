using System;
using System.Collections.Generic;
using System.IO;
using TheRavine.Base;
using TheRavine.Generator;
using UnityEngine;

public partial class EntityManager
{
    private WorldRegistry _checkpointRegistry;

    private PopulationSnapshot CapturePopulation()
    {
        if (!_simulationStarted || !WorldAuthority.IsAuthority) return null;
        for (int i = 0; i < _colonies.Count; i++) _colonies[i].Brain.CompletePending();
        foreach (var entity in _entities)
            if (!entity.SettleFuel()) throw new InvalidOperationException($"Cannot settle fuel for agent {entity.EntityId}");
        int live = 0;
        foreach (var entity in _entities)
            if (!entity.IsDeathPending && !entity.IsDisposed) live++;
        if (live > PopulationSnapshot.MaxAgents || _colonies.Count > PopulationSnapshot.MaxColonies)
            throw new InvalidDataException("Population exceeds checkpoint limits");
        double now = SimulationClock.TimeD;
        var snapshot = new PopulationSnapshot
        {
            Captured = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastEntityId = _population.LastId, LastColonyId = _colonies.LastId,
            Colonies = new PopulationSnapshot.Colony[_colonies.Count], Agents = new PopulationSnapshot.Agent[live]
        };
        for (int i = 0; i < _colonies.Count; i++)
        {
            var c = _colonies[i];
            snapshot.Colonies[i] = new PopulationSnapshot.Colony
            {
                Id = c.ColonyId, Wild = c.IsWild, Arm = (byte)c.Arm,
                Leader = c.Leader?.EntityId ?? 0, X = c.Nest.Position.x, Z = c.Nest.Position.y,
                Hunger = c.Nest.Hunger, Storage = c.Nest.Storage, LearningRate = c.Genome.BaseLearningRate,
                GradientNorm = c.Genome.MaxGradientNorm, Entropy = c.Genome.EntropyRegularization,
                ElectionRemaining = Math.Max(0, c.NextElection - now)
            };
        }
        int slot = 0;
        foreach (var e in _entities)
        {
            if (e.IsDeathPending || e.IsDisposed) continue;
            snapshot.Agents[slot++] = new PopulationSnapshot.Agent
            {
                Id = e.EntityId, Parent = e.Parent.EntityId, ColonyId = e.ColonyId,
                Position = _motion.PositionOf(e.ManagerIndex), Age = e.TimeAlive, Caste = (byte)e.Caste, Fixed = e.CasteFixed,
                Health = e.Stats.Hp, Energy = e.Stats.En, Stomach = e.Digestion.Stomach, Carrying = e.Carrying,
                AttackRemaining = Math.Max(0, e.AttackReadyTime - now),
                ReviewRemaining = Math.Max(0, e.NextCasteReview - now),
                Genes = GeneticParameters.AsReadOnlySpan(in e.Brain.Context.CoordMLP.Params).ToArray()
            };
        }
        return snapshot;
    }

    private void RestoreColonies(PopulationSnapshot snapshot)
    {
        ValidateRuntimePopulation(snapshot, maxPopulation, SimulationRules.Active.PredatorCount);
        foreach (var c in snapshot.Colonies)
        {
            _map.GetMapData(MapGenerator.WorldToChunk(ChunkFoodIndex.CellOf(c.X, c.Z)));
            var colony = AddColony(new Vector3(c.X, 0, c.Z), c.Wild, c);
            if (c.Wild) _wild = colony;
        }
        _colonies.RestoreLastId(snapshot.LastColonyId);
    }

    public static void ValidateRuntimePopulation(PopulationSnapshot snapshot, int maxCivil, int maxWild)
    {
        int wildId = 0, wildCount = 0;
        foreach (var c in snapshot.Colonies)
            if (c.Wild) wildId = c.Id;
        foreach (var agent in snapshot.Agents)
            if (agent.ColonyId == wildId) wildCount++;
        if (wildCount > maxWild || snapshot.Agents.Length - wildCount > maxCivil)
            throw new InvalidDataException("Saved population exceeds the scene capacity");
        ValidatePopulationGenes(snapshot);
    }

    public static void ValidatePopulationGenes(PopulationSnapshot snapshot)
    {
        foreach (var c in snapshot.Colonies)
        {
            CheckGene(GeneticParameters.IdxBaseLearningRate, c.LearningRate);
            CheckGene(GeneticParameters.IdxMaxGradientNorm, c.GradientNorm);
            CheckGene(GeneticParameters.IdxEntropyRegularization, c.Entropy);
        }
        foreach (var agent in snapshot.Agents)
        {
            for (int i = 0; i < agent.Genes.Length; i++) CheckGene(i, agent.Genes[i]);
        }
    }

    private static void CheckGene(int index, float value)
    {
        var range = GeneticParameters.ParameterRanges[index];
        if (!(value >= range.min && value <= range.max)) throw new InvalidDataException($"Gene {index} outside its range");
    }

    private void RestoreAgents(PopulationSnapshot snapshot)
    {
        _population.RestoreLastId(snapshot.LastEntityId);
        var live = new Dictionary<int, EntityModel>(snapshot.Agents.Length);
        foreach (var a in snapshot.Agents)
        {
            _map.GetMapData(MapGenerator.WorldToChunk(ChunkFoodIndex.CellOf(a.Position.x, a.Position.z)));
            _colonies.TryGetById(a.ColonyId, out var colony);
            GeneticParameters genes = default;
            a.Genes.AsSpan().CopyTo(GeneticParameters.AsSpan(ref genes));
            var model = _population.SpawnEntity(a.Position, colony, colony.Brain.CreateContext(genes), saved: a);
            if (model == null) throw new InvalidOperationException($"Cannot register agent {a.Id}");
            live.Add(a.Id, model);
        }
        foreach (var a in snapshot.Agents)
        {
            live.TryGetValue(a.Parent, out var parent);
            live[a.Id].RestoreParent(parent, a.Parent);
        }
        foreach (var c in snapshot.Colonies)
        {
            _colonies.TryGetById(c.Id, out var colony);
            live.TryGetValue(c.Leader, out var leader);
            colony.SetLeader(leader);
            colony.NextElection = SimulationClock.TimeD + c.ElectionRemaining;
        }
        if (_entities.Count > 0) RebuildTickSnapshot();
    }
}
