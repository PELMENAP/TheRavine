using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

namespace TheRavine.EntityControl.Virology
{
    public class VirologyComponent : IComponent
    {
        public const int MaxTapeLength = 192;
        public const int TapeCapacity = 320;
        public const int EndogenousLength = 24;

        private Tape _tape;
        private List<Segment> _segments;
        private TranslationTable _table;
        private EffectModifiers _modifiers;
        private ushort[] _scratch;
        private bool _primed;
        private uint _tick;
        private bool _created;

        private int _lastAction;
        private float _lastAmp;
        private bool _lastExecuted;
        private int _failedCodons;
        private int _executedCodons;
        private bool _segmentsDirty;

        public bool IsCreated => _created;
        public bool IsDisposed { get; private set; }
        public ref EffectModifiers Modifiers => ref _modifiers;
        public ref Tape TapeRef => ref _tape;
        public List<Segment> Segments => _segments;
        public ProteinAction LastAction => (ProteinAction)_lastAction;
        public float LastAmp => _lastAmp;
        public bool LastExecuted => _lastExecuted;
        public int Head => _tape.Head;
        public int TapeLength => _tape.Length;
        public int SegmentCount => _created ? _segments.Count : 0;
        public int FailedCodons => _failedCodons;
        public int ExecutedCodons => _executedCodons;
        public float Sharpness => _table.Sharpness;
        public uint TickCount => _tick;
        public bool SegmentsDirty => _segmentsDirty;
        public void ConsumeSegmentsDirty() => _segmentsDirty = false;

        public const float SplitIntegrityPenalty = 0.85f;
        public const float SuperinfectionBoost = 0.25f;
        public const int LineageProximityBits = 12;

        private int _lastCodonIndex;
        public int LastCodonIndex => _lastCodonIndex;
        public float[] Centroids => _table.Centroids;

        private bool _valuesDirty;

        public bool ValuesDirty => _valuesDirty;
        public void ConsumeValuesDirty() => _valuesDirty = false;

        public int SegmentIndexAt(int position)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (position >= s.Start && position < s.Start + s.Length) return i;
            }
            return -1;
        }
        public void ReinforceSegment(int index, float amount)
        {
            var s = _segments[index];
            s.Integrity = math.min(s.Integrity + amount, 1f);
            _segments[index] = s;
            _valuesDirty = true;
        }
        public bool HasStrain(ulong strainId, out int index)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].StrainId != strainId) continue;
                index = i;
                return true;
            }
            index = -1;
            return false;
        }

        public int CopySegment(int index, ushort[] destination)
        {
            var s = _segments[index];
            int count = math.min(s.Length, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = _tape.Codons[s.Start + i];
            return count;
        }

        public ushort FirstCodonOf(int index) => _tape.Codons[_segments[index].Start];

        public bool TryFindLineageMatch(ulong signature, ulong strainId, out int index)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (s.StrainId == 0UL || s.StrainId == strainId) continue;
                if (math.countbits(s.Signature ^ signature) > LineageProximityBits) continue;
                index = i;
                return true;
            }
            index = -1;
            return false;
        }

        public bool TryInsertSegment(ushort[] source, int count,
            ulong strainId, ulong lineageId, uint seed)
        {
            if (!_created || IsDisposed || count <= 0) return false;
            if (_tape.Length + count > _tape.Capacity) return false;

            var rng = new XorShift32(seed == 0u ? 1u : seed);
            int index = _tape.Length > 0 ? (int)(rng.NextUInt() % (uint)(_tape.Length + 1)) : 0;

            SplitAt(index);
            if (!_tape.TryInsert(index, source, 0, count)) return false;

            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (s.Start < index) continue;
                s.Start += count;
                _segments[i] = s;
            }

            _segments.Add(new Segment
            {
                Start = index,
                Length = count,
                StrainId = strainId,
                LineageId = lineageId,
                Signature = Segment.ComputeSignature(_tape.Codons, index, count),
                InsertTick = _tick,
                Integrity = 1f,
                NetFitnessDelta = 0f,
                Tamed = false,
                DominantAction = ResolveDominant(index, count)
            });

            _segmentsDirty = true;
            return true;
        }

        private void SeedEndogenous(uint entitySeed)
        {
            var rng = new XorShift32(entitySeed == 0u ? 1u : entitySeed);
            for (int i = 0; i < EndogenousLength; i++)
                _scratch[i] = (ushort)(rng.NextUInt() & 0xFFFFu);

            _tape.TryInsert(0, _scratch, 0, EndogenousLength);

            _segments.Add(new Segment
            {
                Start = 0,
                Length = EndogenousLength,
                StrainId = 0UL,
                LineageId = _tape.ComputeHash(0, EndogenousLength),
                Signature = Segment.ComputeSignature(_tape.Codons, 0, EndogenousLength),
                InsertTick = _tick,
                Integrity = 1f,
                NetFitnessDelta = 0f,
                DominantAction = ResolveDominant(0, EndogenousLength)
            });

            _segmentsDirty = true;
        }

        public void Step(StatsComponent stats, float dt)
        {
            if (!_created || IsDisposed || stats == null || stats.IsDisposed) return;

            _tick++;

            float k = math.exp(-dt / math.max(SimulationRules.Frame.ModifierDecayTau, 1e-3f));
            Ribosome.Relax(ref _modifiers, k);
            _modifiers.ResetTransient();

            if (_dormantLeft > 0)
            {
                _dormantLeft--;
                _lastExecuted = false;
                return;
            }

            _lastCodonIndex = _tape.Head;
            int owner = SegmentIndexAt(_lastCodonIndex);

            float healthBefore = stats.Health.Value;
            float energyBefore = stats.Energy.Value;

            var result = Ribosome.Step(ref _tape, ref _modifiers, ref _primed, owner,
                _table.Centroids, VirologyRuntime.CellBias, VirologyRuntime.Descriptors,
                _table.Sharpness, energyBefore, MaxTapeLength);

            _lastAction = result.Action;
            _lastAmp = result.Amp;
            _lastExecuted = result.Executed;

            if (!result.Executed) { _failedCodons++; return; }
            _executedCodons++;

            stats.Health.Value = math.min(healthBefore + _modifiers.HealthDelta, stats.MaxHealth);
            stats.Energy.Value = math.clamp(energyBefore - result.SpentEnergy + _modifiers.EnergyDelta,
                0f, stats.MaxEnergy);

            _table.Sharpness = math.clamp(_table.Sharpness + _modifiers.SharpnessDelta * 0.01f, 0.05f, 3f);
            _modifiers.SharpnessDelta = 0f;

            if (_modifiers.Dormant) _dormantLeft = DormantTicks;

            if (owner < 0) return;

            float invH = stats.MaxHealth > 0f ? 1f / stats.MaxHealth : 0f;
            float invE = stats.MaxEnergy > 0f ? 1f / stats.MaxEnergy : 0f;
            float delta = (stats.Health.Value - healthBefore) * invH
                        + (stats.Energy.Value - energyBefore) * invE;

            var seg = _segments[owner];
            seg.NetFitnessDelta += delta;

            float gain = seg.NetFitnessDelta - seg.PrevFitnessDelta;
            seg.PrevFitnessDelta = seg.NetFitnessDelta;

            if (gain >= 0f)
            {
                if (seg.TamedTicks < TamedThreshold) seg.TamedTicks++;
                if (seg.TamedTicks >= TamedThreshold) seg.Tamed = true;
            }
            else
            {
                seg.TamedTicks = 0;
                seg.Tamed = false;
            }

            if (_modifiers.ReinforceAmount > 0f)
                seg.Integrity = math.min(seg.Integrity + _modifiers.ReinforceAmount, 1f);
            _modifiers.ReinforceAmount = 0f;

            _segments[owner] = seg;
            _valuesDirty = true;

            if (_modifiers.ExciseRequested) { TryExcise(owner); return; }
            if (_modifiers.ReplicateRequested) TryReplicate(owner);
        }

        public void CreditDurableEffects(float regenEnergy, float metabolismEnergy, float maxEnergy)
        {
            if (!_created || IsDisposed || maxEnergy <= 0f) return;

            float inv = 1f / maxEnergy;
            CreditSegment(_modifiers.RegenOwner, regenEnergy * inv);
            CreditSegment(_modifiers.MetabolismOwner, metabolismEnergy * inv);
        }

        private void CreditSegment(int owner, float delta)
        {
            if (delta == 0f || (uint)owner >= (uint)_segments.Count) return;

            var s = _segments[owner];
            s.NetFitnessDelta += delta;
            _segments[owner] = s;
            _valuesDirty = true;
        }

        private static void RemapOwner(ref int owner, int removed, int last)
        {
            if (owner == removed) owner = -1;
            else if (owner == last) owner = removed;
        }

        private bool TryExcise(int index)
        {
            var seg = _segments[index];
            if (seg.IsEndogenous) return false;

            int start = seg.Start;
            int len = seg.Length;
            if (len <= 0) return false;

            _tape.Remove(start, len);

            for (int i = 0; i < _segments.Count; i++)
            {
                if (i == index) continue;
                var s = _segments[i];
                if (s.Start <= start) continue;
                s.Start -= len;
                _segments[i] = s;
            }

            int last = _segments.Count - 1;
            _segments.RemoveAtSwapBack(index);
            RemapOwner(ref _modifiers.RegenOwner, index, last);
            RemapOwner(ref _modifiers.MetabolismOwner, index, last);

            _lastCodonIndex = -1;
            _segmentsDirty = true;
            return true;
        }

        private bool TryReplicate(int index)
        {
            var seg = _segments[index];
            int len = seg.Length;
            if (len <= 0 || _tape.Length + len > _tape.Capacity) return false;

            int insertAt = seg.Start + len;
            for (int i = 0; i < len; i++)
                _scratch[i] = _tape.Codons[seg.Start + i];

            if (!_tape.TryInsert(insertAt, _scratch, 0, len)) return false;

            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (s.Start < insertAt) continue;
                s.Start += len;
                _segments[i] = s;
            }

            _segments.Add(new Segment
            {
                Start = insertAt,
                Length = len,
                StrainId = seg.StrainId,
                LineageId = seg.LineageId,
                Signature = seg.Signature,
                InsertTick = _tick,
                Integrity = seg.Integrity * SplitIntegrityPenalty,
                NetFitnessDelta = 0f,
                PrevFitnessDelta = 0f,
                TamedTicks = 0,
                Tamed = false,
                DominantAction = seg.DominantAction
            });

            _segmentsDirty = true;
            return true;
        }

        private void SplitAt(int index)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (index <= s.Start || index >= s.Start + s.Length) continue;

                var tail = s;
                tail.Start = index;
                tail.Length = s.Start + s.Length - index;
                tail.Signature = Segment.ComputeSignature(_tape.Codons, tail.Start, tail.Length);
                tail.Integrity = s.Integrity * SplitIntegrityPenalty;
                tail.NetFitnessDelta = 0f;
                tail.PrevFitnessDelta = 0f;
                tail.TamedTicks = 0;
                tail.Tamed = false;

                s.Length = index - s.Start;
                s.Signature = Segment.ComputeSignature(_tape.Codons, s.Start, s.Length);
                s.Integrity *= SplitIntegrityPenalty;
                s.PrevFitnessDelta = s.NetFitnessDelta;
                s.TamedTicks = 0;
                s.Tamed = false;

                _segments[i] = s;
                _segments.Add(tail);
                return;
            }
        }

        public void FillComponent(in GeneticParameters genetics, uint entitySeed)
        {
            if (_created) return;
            if (!VirologyRuntime.IsReady)
            {
                UnityEngine.Debug.LogError(
                    $"[{nameof(VirologyComponent)}] VirologyRuntime not ready, component left uncreated (seed {entitySeed:X8})");
                return;
            }
            _created = true;

            _table = TranslationTable.CreateFrom(VirologyRuntime.Prototype,
                math.max(genetics.Sharpness, 0.01f), genetics.GaussianNoise,
                entitySeed ^ 0x9E3779B9u);

            _tape = Tape.Create(TapeCapacity, Allocator.Persistent);
            _segments = new List<Segment>(4);
            _scratch = new ushort[TapeCapacity];
            _modifiers = EffectModifiers.Neutral;

            SeedEndogenous(entitySeed);
        }

        public ProteinAction ResolveDominant(int start, int length)
        {
            if (!_created || length <= 0) return ProteinAction.Noop;

            System.Span<int> counts = stackalloc int[ProteinTable.ActionCount];
            counts.Clear();

            int end = math.min(start + length, _tape.Length);
            for (int i = start; i < end; i++)
            {
                int a = CodonEmbedding.Translate(_tape.Codons[i], _table.Centroids,
                    VirologyRuntime.CellBias, _table.Sharpness, out _);
                counts[a]++;
            }

            int best = 0;
            int bestCount = -1;
            for (int a = 0; a < ProteinTable.ActionCount; a++)
            {
                if (a == (int)ProteinAction.Junk || counts[a] <= bestCount) continue;
                bestCount = counts[a];
                best = a;
            }

            return (ProteinAction)best;
        }
        
        public const int DormantTicks = 8;
        public const int TamedThreshold = 8;

        private int _dormantLeft;

        public float NetFitnessOf(int index) => _segments[index].NetFitnessDelta;

        public bool TryGetViralInputs(out float load, out float count, out float net)
        {
            load = 0f;
            count = 0f;
            net = 0f;
            if (!_created || IsDisposed) return false;

            float mass = 0f;
            float sum = 0f;
            int n = 0;

            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (s.IsEndogenous) continue;
                mass += s.Length * s.Integrity;
                sum += s.NetFitnessDelta;
                n++;
            }

            load = math.saturate(mass / MaxTapeLength);
            count = math.saturate(n * 0.125f);
            net = math.clamp(sum, -1f, 1f);
            return n > 0;
        }

        public void BuildInfectionViews(System.Collections.Generic.List<InfectionView> target)
        {
            target.Clear();
            if (!_created || IsDisposed) return;

            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                bool endogenous = s.IsEndogenous;
                target.Add(new InfectionView
                {
                    StrainLabel = endogenous ? "ENDOGEN" : ShortHex(s.StrainId),
                    LineageLabel = ShortHex(s.LineageId),
                    IsEndogenous = endogenous,
                    CodonCount = s.Length,
                    Integrity = s.Integrity,
                    AgeTicks = (int)(_tick - s.InsertTick),
                    Tamed = s.Tamed,
                    DominantAction = s.DominantAction,
                    NetFitnessDelta = s.NetFitnessDelta
                });
            }
        }

        public bool RefreshInfectionValues(System.Collections.Generic.List<InfectionView> target)
        {
            if (!_created || IsDisposed || target.Count != _segments.Count) return false;

            for (int i = 0; i < target.Count; i++)
            {
                var s = _segments[i];
                var v = target[i];
                v.Integrity = s.Integrity;
                v.NetFitnessDelta = s.NetFitnessDelta;
                v.AgeTicks = (int)(_tick - s.InsertTick);
                v.Tamed = s.Tamed;
                target[i] = v;
            }
            return true;
        }

        public bool TryGetViralSummary(out float netFitness, out int viralCount, out bool allTamed)
        {
            netFitness = 0f;
            viralCount = 0;
            allTamed = true;
            if (!_created || IsDisposed) return false;

            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                if (s.IsEndogenous) continue;
                viralCount++;
                netFitness += s.NetFitnessDelta;
                if (!s.Tamed) allTamed = false;
            }
            return viralCount > 0;
        }

        private static string ShortHex(ulong id)
            => ((uint)(id ^ (id >> 32))).ToString("X8");

        public TranslationTable CloneTable(Allocator allocator) => _table.Clone(allocator);

        public int Restrict(int index, ushort[] destination) => CopySegment(index, destination);
        public ulong LineageOf(int index) => _segments[index].LineageId;

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _created = false;
            _tape.Dispose();
            _table.Dispose();
        }
    }
}