using System;
using Unity.Mathematics;
using TheRavine.Base;

public sealed class WorldCodexService
{
    public const int SaveVersion = 1;

    private readonly SimulationRules _rules;
    private readonly EnergyLedger _ledger;
    private readonly ICodexPurse _purse;
    private readonly int _axes;
    private readonly LawRecord[] _laws;
    private readonly float[] _opposition;
    private readonly float[] _theta;
    private readonly float[] _applied;
    private readonly float[] _multipliers;
    private readonly float[] _perturb;
    private readonly float[] _perturbApplied;
    private readonly float[] _effective;
    private readonly float[] _thetaTime;
    private readonly CodexPriceEstimator _prices;
    private int _count;
    private int _nextId = 1;
    private bool _dirty;
    private float _netSum;
    private float _turnoverSum;
    private float _priceElapsed;
    private float _priceWindowTime;

    public int Version { get; private set; }
    public int LawCount => _count;
    public int PerturbationActive { get; private set; }
    public int Axes => _axes;
    public bool NeedsEnergySamples => _count > 0;
    public ReadOnlySpan<LawRecord> Laws => new(_laws, 0, _count);
    public ReadOnlySpan<float> Theta => _applied;
    public ReadOnlySpan<float> Opposition => _opposition;
    public ReadOnlySpan<float> Prices => _prices.Prices;
    public int PriceSamples => _prices.Samples;
    public bool PricesFromPrior => _prices.UsesPrior;

    public WorldCodexService(SimulationRules rules, EnergyLedger ledger, ICodexPurse purse)
    {
        _rules = rules;
        _ledger = ledger;
        _purse = purse;
        _axes = (int)VarianceAxis.Count;
        _laws = new LawRecord[math.max(rules.CodexMaxLaws, 1)];
        _opposition = new float[_axes];
        _theta = new float[_axes];
        _applied = new float[_axes];
        _multipliers = new float[_axes];
        _perturb = new float[_axes];
        _perturbApplied = new float[_axes];
        _effective = new float[_axes];
        _thetaTime = new float[_axes];
        _prices = new CodexPriceEstimator(_axes, rules.CodexPriceForgetting, rules.CodexPriceMinSamples,
            rules.CodexPriceMinExcitation, rules.CodexPriceInitCov, rules.CodexPricePrior);
        _priceWindowTime = rules.CodexWindow;
    }

    public float Balance(int authorId) => _purse != null ? _purse.Balance(authorId) : 0f;

    public bool TryGetLaw(int lawId, out LawRecord law)
    {
        int slot = Find(lawId);
        law = slot >= 0 ? _laws[slot] : default;
        return slot >= 0;
    }

    private int Find(int lawId)
    {
        for (int i = 0; i < _count; i++)
            if (_laws[i].Id == lawId) return i;
        return -1;
    }

    public CodexError Check(ReadOnlySpan<float> delta)
    {
        if (delta.Length != _axes) return CodexError.BadValue;

        var min = _rules.CodexAxisMin;
        var max = _rules.CodexAxisMax;
        float limit = _rules.CodexMaxDeltaPerAxis;
        int nonZero = 0;
        for (int k = 0; k < _axes; k++)
        {
            float d = delta[k];
            if (!math.isfinite(d)) return CodexError.BadValue;
            if (d == 0f) continue;
            nonZero++;
            if (math.abs(d) > limit) return CodexError.DeltaTooLarge;
            if (d < min[k] || d > max[k]) return CodexError.OutOfBounds;
        }

        if (nonZero == 0) return CodexError.EmptyLaw;
        if (nonZero > _rules.CodexMaxAxesPerLaw) return CodexError.TooManyAxes;
        if (CodexMath.Cost(delta, _prices.Prices) > _rules.CodexFreeBudget) return CodexError.Inadmissible;
        return CodexError.None;
    }

    public bool TryPropose(int authorId, ReadOnlySpan<float> delta, out int lawId, out CodexError error)
    {
        lawId = 0;
        error = authorId == CodexAuthor.None || _purse == null ? CodexError.NoPurse : Check(delta);
        if (error != CodexError.None) return false;
        if (_count >= _laws.Length) return Fail(CodexError.CodexFull, out error);

        float cost = _rules.CodexProposeCost;
        if (!Spend(authorId, cost)) return Fail(CodexError.InsufficientEnergy, out error);

        ref var law = ref _laws[_count++];
        law = default;
        var target = LawRecord.Delta(ref law);
        for (int k = 0; k < _axes; k++) target[k] = delta[k];
        law.Id = _nextId++;
        law.Author = authorId;
        law.Support = CodexMath.VoteGain(cost, _rules.CodexVoteScale);
        law.Created = SimulationClock.Time;
        law.Alive = 1;
        lawId = law.Id;
        _dirty = true;
        return true;
    }

    public bool TryInvest(int authorId, int lawId, float energy, out CodexError error)
    {
        error = CheckInvestment(authorId, energy);
        if (error != CodexError.None) return false;

        int slot = Find(lawId);
        if (slot < 0) return Fail(CodexError.NoSuchLaw, out error);
        if (CodexMath.Cost(LawRecord.DeltaReadOnly(in _laws[slot]).Slice(0, _axes), _prices.Prices) > _rules.CodexFreeBudget)
            return Fail(CodexError.Inadmissible, out error);
        if (!Spend(authorId, energy)) return Fail(CodexError.InsufficientEnergy, out error);

        _laws[slot].Support += CodexMath.VoteGain(energy, _rules.CodexVoteScale);
        _dirty = true;
        return true;
    }

    public bool TryOppose(int authorId, int axis, float energy, out CodexError error)
    {
        error = CheckInvestment(authorId, energy);
        if (error != CodexError.None) return false;
        if ((uint)axis >= (uint)_axes) return Fail(CodexError.BadAxis, out error);
        if (!Spend(authorId, energy)) return Fail(CodexError.InsufficientEnergy, out error);

        _opposition[axis] += CodexMath.VoteGain(energy, _rules.CodexVoteScale);
        _dirty = true;
        return true;
    }

    private CodexError CheckInvestment(int authorId, float energy)
    {
        if (!math.isfinite(energy) || !(energy > 0f)) return CodexError.BadValue;
        if (authorId == CodexAuthor.None || _purse == null) return CodexError.NoPurse;
        if (energy < _rules.CodexMinInvest) return CodexError.EnergyTooSmall;
        return CodexError.None;
    }

    private static bool Fail(CodexError code, out CodexError error)
    {
        error = code;
        return false;
    }

    private bool Spend(int authorId, float amount)
    {
        if (!(amount > 0f)) return true;
        if (!_purse.TryDebit(authorId, amount)) return false;

        if (_ledger != null) _ledger.Flows.Codex += amount;
        return true;
    }

    public void NoteEnergy(in EnergyFlows last)
    {
        float turnover = last.Inflow + last.Outflow - last.Codex;
        float net = last.Inflow - last.Outflow + last.Codex;
        if (!math.isfinite(turnover) || !math.isfinite(net) || !(turnover > 0f)) return;

        _netSum += net;
        _turnoverSum += turnover;
    }

    public void Step(float dt)
    {
        dt = math.max(dt, 0f);
        float rate = _rules.CodexSupportDecay;
        float prune = _rules.CodexPruneSupport;

        if (dt > 0f)
        {
            for (int i = 0; i < _count; i++)
            {
                _laws[i].Support = CodexMath.Decay(_laws[i].Support, rate, dt);
                _dirty = true;
            }

            for (int k = 0; k < _axes; k++)
            {
                if (_opposition[k] <= 0f) continue;
                float o = CodexMath.Decay(_opposition[k], rate, dt);
                _opposition[k] = o < prune ? 0f : o;
                _dirty = true;
            }
        }

        for (int i = _count - 1; i >= 0; i--)
        {
            if (_laws[i].Support >= prune) continue;
            _laws[i] = _laws[--_count];
            _laws[_count] = default;
            _dirty = true;
        }

        UpdatePerturbation();
        if (_dirty) Reaggregate();
        SamplePrices(dt);
    }

    private void UpdatePerturbation()
    {
        var schedule = _rules.PerturbationSchedule;
        if (schedule.Length == 0 && PerturbationActive == 0) return;

        float now = SimulationClock.Time;
        Array.Clear(_perturb, 0, _perturb.Length);
        int active = 0;
        for (int i = 0; i < schedule.Length; i++)
        {
            var e = schedule[i];
            if (now < e.startTime || now >= e.startTime + e.duration) continue;
            if (!(e.multiplier > 0f) || !math.isfinite(e.multiplier) || (uint)e.axis >= (uint)_axes) continue;
            _perturb[(int)e.axis] += math.log(e.multiplier);
            active++;
        }
        PerturbationActive = active;

        for (int k = 0; k < _axes; k++)
            if (_perturb[k] != _perturbApplied[k]) { _dirty = true; break; }
    }

    private void Reaggregate()
    {
        _dirty = false;
        CodexMath.Aggregate(Laws, _opposition, _rules.CodexInertia, _rules.CodexAxisMin, _rules.CodexAxisMax, _theta);

        bool changed = false;
        for (int k = 0; k < _axes; k++) changed |= _theta[k] != _applied[k] || _perturb[k] != _perturbApplied[k];
        if (!changed) return;

        for (int k = 0; k < _axes; k++)
        {
            _applied[k]        = _theta[k];
            _perturbApplied[k] = _perturb[k];
            _effective[k]      = _theta[k] + _perturb[k];
        }
        CodexMath.Multipliers(_effective, _multipliers);
        _rules.ApplyLaws(_multipliers);
        Version++;
    }

    private void SamplePrices(float dt)
    {
        for (int k = 0; k < _axes; k++) _thetaTime[k] += _applied[k] * dt;
        _priceElapsed += dt;
        if (_priceElapsed < _priceWindowTime) return;

        if (_turnoverSum > 0f)
        {
            float inv = 1f / _priceElapsed;
            for (int k = 0; k < _axes; k++) _thetaTime[k] *= inv;
            _prices.Add(_thetaTime, _netSum / _turnoverSum);
        }

        Array.Clear(_thetaTime, 0, _axes);
        _netSum = 0f;
        _turnoverSum = 0f;
        _priceElapsed = 0f;
        _priceWindowTime = _rules.CodexWindow;
    }

    public void Capture(CodexSave save)
    {
        int count = _count;
        save.version = SaveVersion;
        save.axisCount = _axes;
        save.nextLawId = _nextId;
        save.lawIds = new int[count];
        save.lawAuthors = new int[count];
        save.lawSupports = new float[count];
        save.lawCreated = new float[count];
        save.lawDeltas = new float[count * _axes];
        for (int i = 0; i < count; i++)
        {
            save.lawIds[i] = _laws[i].Id;
            save.lawAuthors[i] = _laws[i].Author;
            save.lawSupports[i] = _laws[i].Support;
            save.lawCreated[i] = _laws[i].Created;
            LawRecord.DeltaReadOnly(in _laws[i]).Slice(0, _axes).CopyTo(save.lawDeltas.AsSpan(i * _axes, _axes));
        }

        save.opposition = (float[])_opposition.Clone();
        save.priceWeights = new float[_axes];
        save.priceCovariance = new float[_axes * _axes];
        save.priceMeanTheta = new float[_axes];
        _prices.CopyState(save.priceWeights, save.priceCovariance, save.priceMeanTheta, out save.priceMeanFlux, out save.priceSamples);
    }

    public bool Restore(CodexSave save)
    {
        Clear();
        if (save == null) return false;

        if (!TryRestoreLaws(save))
        {
            Clear();
            return false;
        }

        if (save.priceWeights != null && save.priceCovariance != null && save.priceMeanTheta != null)
            _prices.TryRestore(save.priceWeights, save.priceCovariance, save.priceMeanTheta, save.priceMeanFlux, save.priceSamples);

        _dirty = true;
        return true;
    }

    private bool TryRestoreLaws(CodexSave save)
    {
        if (save.version != SaveVersion || save.axisCount != _axes) return false;

        int count = save.lawIds?.Length ?? 0;
        if (count > _laws.Length || save.nextLawId < 1) return false;
        if ((save.lawAuthors?.Length ?? 0) != count || (save.lawSupports?.Length ?? 0) != count
            || (save.lawCreated?.Length ?? 0) != count || (save.lawDeltas?.Length ?? 0) != count * _axes) return false;
        if (save.opposition != null && save.opposition.Length != _axes) return false;

        for (int i = 0; i < count; i++)
        {
            int id = save.lawIds[i];
            float support = save.lawSupports[i];
            if (id < 1 || id >= save.nextLawId || save.lawAuthors[i] == CodexAuthor.None) return false;
            if (!math.isfinite(support) || support < 0f || !math.isfinite(save.lawCreated[i])) return false;
            for (int j = 0; j < i; j++) if (save.lawIds[j] == id) return false;
            if (!IsLegal(save.lawDeltas.AsSpan(i * _axes, _axes))) return false;
        }

        if (save.opposition != null)
            for (int k = 0; k < _axes; k++)
                if (!math.isfinite(save.opposition[k]) || save.opposition[k] < 0f) return false;

        for (int i = 0; i < count; i++)
        {
            ref var law = ref _laws[i];
            law = default;
            save.lawDeltas.AsSpan(i * _axes, _axes).CopyTo(LawRecord.Delta(ref law));
            law.Id = save.lawIds[i];
            law.Author = save.lawAuthors[i];
            law.Support = save.lawSupports[i];
            law.Created = save.lawCreated[i];
            law.Alive = 1;
        }

        _count = count;
        _nextId = save.nextLawId;
        if (save.opposition != null) save.opposition.CopyTo(_opposition, 0);
        return true;
    }

    private bool IsLegal(ReadOnlySpan<float> delta)
    {
        var min = _rules.CodexAxisMin;
        var max = _rules.CodexAxisMax;
        float limit = _rules.CodexMaxDeltaPerAxis;
        int nonZero = 0;
        for (int k = 0; k < _axes; k++)
        {
            float d = delta[k];
            if (!math.isfinite(d)) return false;
            if (d == 0f) continue;
            nonZero++;
            if (math.abs(d) > limit || d < min[k] || d > max[k]) return false;
        }
        return nonZero > 0 && nonZero <= _rules.CodexMaxAxesPerLaw;
    }

    private void Clear()
    {
        for (int i = 0; i < _count; i++) _laws[i] = default;
        _count = 0;
        _nextId = 1;
        Array.Clear(_opposition, 0, _axes);
        _prices.Reset();
        _dirty = true;
    }
}
