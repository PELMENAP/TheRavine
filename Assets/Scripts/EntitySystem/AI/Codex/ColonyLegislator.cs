using System;
using Unity.Mathematics;

public sealed class ColonyLegislator
{
    private sealed class State
    {
        public readonly float[] Delta = new float[(int)VarianceAxis.Count];
        public double NextAt;
        public float LastScore;
        public bool Primed;
        public bool LastWasMirror;
        public int LawId;
    }

    private const int Axes = (int)VarianceAxis.Count;

    private readonly WorldCodexService _codex;
    private readonly int[] _order = new int[Axes];
    private readonly float[] _trial = new float[Axes];
    private State[] _states = new State[8];

    public ColonyLegislator(WorldCodexService codex)
    {
        _codex = codex;
    }

    public void Tick(ColonyRegistry colonies, double now)
    {
        for (int c = 0; c < colonies.Count; c++)
        {
            var colony = colonies[c];
            if (colony.IsWild || !colony.EmaPrimed) continue;

            var state = StateOf(colony.Index);
            if (now < state.NextAt) continue;

            var r = SimulationRules.Active;
            state.NextAt = now + r.CodexWindow;
            Step(colony, state, r);
        }
    }

    private State StateOf(int index)
    {
        if (index >= _states.Length) Array.Resize(ref _states, math.max(index + 1, _states.Length * 2));
        return _states[index] ??= new State();
    }

    private void Step(ColonyState colony, State state, SimulationRules r)
    {
        float score = colony.Score;
        if (!state.Primed)
        {
            state.Primed = true;
            state.LastScore = score;
            return;
        }

        float trend = score - state.LastScore;
        state.LastScore = score;

        int author = CodexAuthor.ForColony(colony.ColonyId);
        float balance = _codex.Balance(author);
        if (balance < r.CodexMinColonyStore || balance < r.CodexMinInvest) return;

        if (state.LawId != 0 && !_codex.TryGetLaw(state.LawId, out _)) state.LawId = 0;

        bool mirror = false;
        if (state.LawId != 0)
        {
            if (trend > r.CodexTrendEpsilon)
            {
                if (!_codex.TryInvest(author, state.LawId, balance * r.CodexInvestFraction, out var error)
                    && error != CodexError.EnergyTooSmall && error != CodexError.InsufficientEnergy)
                    state.LawId = 0;
                return;
            }

            state.LawId = 0;
            mirror = !state.LastWasMirror;
        }

        if (mirror) Negate(state.Delta);
        else Perturb(state.Delta, r);
        state.LastWasMirror = mirror;
        Propose(author, state);
    }

    private void Propose(int author, State state)
    {
        if (_codex.Check(state.Delta) == CodexError.Inadmissible) Negate(state.Delta);

        if (_codex.TryPropose(author, state.Delta, out int lawId, out _)) state.LawId = lawId;
    }

    private void Perturb(float[] delta, SimulationRules r)
    {
        Array.Clear(delta, 0, delta.Length);
        for (int k = 0; k < Axes; k++) _order[k] = k;

        int picks = math.min(r.CodexMaxAxesPerLaw, Axes);
        var min = r.CodexAxisMin;
        var max = r.CodexAxisMax;
        float c = math.min(r.CodexPerturbation, r.CodexMaxDeltaPerAxis);
        for (int i = 0; i < picks; i++)
        {
            int j = RavineRandom.RangeInt(i, Axes);
            int axis = _order[j];
            _order[j] = _order[i];
            _order[i] = axis;

            float d = RavineRandom.RangeBool() ? c : -c;
            delta[axis] = d >= min[axis] && d <= max[axis] ? d : -d;
        }
    }

    private static void Negate(float[] delta)
    {
        for (int k = 0; k < delta.Length; k++) delta[k] = -delta[k];
    }
}
