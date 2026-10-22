using System;
using System.Collections.Generic;
using Unity.Mathematics;

using TheRavine.EntityControl.Virology;

public sealed class EvolutionService
{
    private readonly List<EntityModel> _entities;
    private readonly ColonyRegistry    _colonies;

    private float[] _fitnessScratch = new float[64];
    private float   _fitnessMedian;
    private float   _fitnessSpread = 1f;
    private bool    _fitnessStatsReady;

    private int[]   _evolveIndices = new int[64];
    private float[] _evolveKeys    = new float[64];

    private float[] _colonyFitness = Array.Empty<float>();
    private int[]   _colonyAlive   = Array.Empty<int>();

    private XorShift32 _tournamentRng;
    private readonly EntityModel[] _mateCandidates = new EntityModel[InfectionService.MaxNeighbors];

    public float AvgEntropy { get; private set; }
    public float AvgFitness { get; private set; }

    public EvolutionService(List<EntityModel> entities, ColonyRegistry colonies, uint seed)
    {
        _entities      = entities;
        _colonies      = colonies;
        _tournamentRng = new XorShift32(seed);
        _colonyFitness = new float[colonies.Count];
        _colonyAlive   = new int[colonies.Count];
    }

    public void UpdateFitnessStats()
    {
        int n = _entities.Count;
        if (n > 0)
        {
            if (n > _fitnessScratch.Length)
            {
                int cap = _fitnessScratch.Length;
                while (cap < n) cap <<= 1;
                _fitnessScratch = new float[cap];
            }

            float entropySum = 0f;
            float fitnessSum = 0f;
            Array.Clear(_colonyFitness, 0, _colonyFitness.Length);
            Array.Clear(_colonyAlive, 0, _colonyAlive.Length);

            int counted = 0;
            for (int i = 0; i < n; i++)
            {
                var e = _entities[i];
                if (e.IsWild) continue;
                float f = e.GetFitness();
                _fitnessScratch[counted++] = f;
                fitnessSum += f;
                entropySum += e.Colony.Brain.GetCoordinatorEntropy(e.Brain.Context);

                int c = e.ColonyIndex;
                _colonyFitness[c] += f;
                _colonyAlive[c]++;
            }

            AvgEntropy = entropySum / math.max(counted, 1);
            AvgFitness = fitnessSum / math.max(counted, 1);

            int m = math.max(counted, 1);
            if (counted == 0) _fitnessScratch[0] = 0f;
            Array.Sort(_fitnessScratch, 0, m);

            _fitnessMedian = _fitnessScratch[m >> 1];
            float iqr = _fitnessScratch[(3 * m) >> 2] - _fitnessScratch[m >> 2];
            _fitnessSpread = iqr > 1e-3f
                ? iqr
                : math.max(1e-3f, math.abs(_fitnessMedian) * SimulationRules.Active.FitnessSpreadMedianFraction);
            _fitnessStatsReady = true;
            for (int c = 0; c < _colonies.Count; c++)
                if (_colonyAlive[c] > 0)
                    _colonies[c].Brain?.ReportMeanFitness(_colonyFitness[c] / _colonyAlive[c]);
        }
        else
        {
            _fitnessStatsReady = false;
        }
    }

    public float TerminalPenaltyFor(EntityModel model, DeathCause cause)
    {
        float z = _fitnessStatsReady ? (model.FinalFitness - _fitnessMedian) / _fitnessSpread : 0f;
        return TerminalPenaltyMath.For(SimulationRules.Active, cause, _fitnessStatsReady, z);
    }

    public EntityModel SelectMate(EntityModel self)
    {
        int found = self.Perception.FindEntitiesInRadius(self.Motor.Position(), self, _mateCandidates);
        if (found == 0) return null;

        int size = SimulationRules.Active.TournamentSize;
        if (size < 1) size = 1;

        EntityModel best = null;
        float bestFit = float.NegativeInfinity;

        for (int i = 0; i < size; i++)
        {
            var cand = _mateCandidates[_tournamentRng.Range(0, found)];
            if (cand == null || cand.IsDisposed || cand.IsDeathPending || cand.Colony != self.Colony) continue;

            float f = cand.SelectionFitness;
            if (f > bestFit) { bestFit = f; best = cand; }
        }

        Array.Clear(_mateCandidates, 0, found);
        return best;
    }

    public void Evolve()
    {
        for (int c = 0; c < _colonies.Count; c++)
            if (!_colonies[c].IsWild) EvolveColony(_colonies[c]);
    }

    private void EvolveColony(ColonyState colony)
    {
        var members = colony.Members;
        int n = members.Length;
        if (n < 2) return;

        if (n > _evolveIndices.Length)
        {
            int cap = _evolveIndices.Length;
            while (cap < n) cap <<= 1;
            _evolveIndices = new int[cap];
            _evolveKeys    = new float[cap];
        }

        for (int i = 0; i < n; i++)
        {
            int idx = members[i];
            _evolveIndices[i] = idx;
            _evolveKeys[i]    = _entities[idx].SelectionFitness;
        }

        Array.Sort(_evolveKeys, _evolveIndices, 0, n);

        int eliteCount = math.max(1, (int)(n * SimulationRules.Active.EliteFraction));
        if (eliteCount >= n) eliteCount = n - 1;

        int eliteStart = n - eliteCount;

        for (int r = 0; r < eliteStart; r++)
        {
            var target = _entities[_evolveIndices[r]];
            if (target.IsDisposed || target.IsDeathPending) continue;

            var pa = _entities[_evolveIndices[eliteStart + _tournamentRng.Range(0, eliteCount)]]
                     .Brain.Context.CoordMLP.Params;
            var pb = _entities[_evolveIndices[eliteStart + _tournamentRng.Range(0, eliteCount)]]
                     .Brain.Context.CoordMLP.Params;

            GeneticParameters.Crossover(in pa, in pb, out var child);

            var ctx = target.Brain.Context;
            var own = ctx.CoordMLP.Params;
            GeneticParameters.CopyLearningGenes(in child, ref own);

            ctx.CoordMLP.Params = own;
            for (int g = 0; g < ctx.ExecMLPs.Length; g++)
                ctx.ExecMLPs[g].Params = own;
        }
    }
}
