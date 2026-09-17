using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public class VirologyComponent : IComponent
    {
        public const int MaxTapeLength = 192;
        public const int TapeCapacity = 320;
        public const int EndogenousLength = 24;

        private Tape _tape;
        private NativeList<Segment> _segments;
        private TranslationTable _table;
        private EffectModifiers _modifiers;
        private NativeArray<ushort> _scratch;
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
        public NativeList<Segment> Segments => _segments;
        public ProteinAction LastAction => (ProteinAction)_lastAction;
        public float LastAmp => _lastAmp;
        public bool LastExecuted => _lastExecuted;
        public int Head => _tape.Head;
        public int TapeLength => _tape.Length;
        public int SegmentCount => _created ? _segments.Length : 0;
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
        public NativeArray<float> Centroids => _table.Centroids;

        private bool _valuesDirty;

        public bool ValuesDirty => _valuesDirty;
        public void ConsumeValuesDirty() => _valuesDirty = false;

        public int SegmentIndexAt(int position)
        {
            for (int i = 0; i < _segments.Length; i++)
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
            for (int i = 0; i < _segments.Length; i++)
            {
                if (_segments[i].StrainId != strainId) continue;
                index = i;
                return true;
            }
            index = -1;
            return false;
        }

        public bool TryFindLineageMatch(ulong lineageId, ulong strainId, out int index)
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                var s = _segments[i];
                if (s.StrainId == 0UL || s.StrainId == strainId) continue;
                if (math.countbits(s.LineageId ^ lineageId) > LineageProximityBits) continue;
                index = i;
                return true;
            }
            index = -1;
            return false;
        }
        public int CopySegment(int index, NativeArray<ushort> destination)
        {
            var s = _segments[index];
            int count = math.min(s.Length, destination.Length);
            for (int i = 0; i < count; i++)
                destination[i] = _tape.Codons[s.Start + i];
            return count;
        }

        public ushort FirstCodonOf(int index) => _tape.Codons[_segments[index].Start];

        public bool TryInsertSegment(NativeArray<ushort> source, int count,
            ulong strainId, ulong lineageId, uint tick, uint seed)
        {
            if (!_created || IsDisposed || count <= 0) return false;
            if (_tape.Length + count > _tape.Capacity) return false;

            var rng = new XorShift32(seed == 0u ? 1u : seed);
            int index = _tape.Length > 0 ? (int)(rng.NextUInt() % (uint)(_tape.Length + 1)) : 0;

            SplitAt(index);
            if (!_tape.TryInsert(index, source, 0, count)) return false;

            for (int i = 0; i < _segments.Length; i++)
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
                InsertTick = _tick,
                Integrity = 1f,
                NetFitnessDelta = 0f,
                Tamed = false,
                DominantAction = ResolveDominant(index, count)
            });

            _segmentsDirty = true;
            return true;
        }

        private void SplitAt(int index)
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                var s = _segments[i];
                if (index <= s.Start || index >= s.Start + s.Length) continue;

                var tail = s;
                tail.Start = index;
                tail.Length = s.Start + s.Length - index;
                tail.Integrity = s.Integrity * SplitIntegrityPenalty;
                tail.NetFitnessDelta = 0f;

                s.Length = index - s.Start;
                s.Integrity *= SplitIntegrityPenalty;

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
                entitySeed ^ 0x9E3779B9u, Allocator.Persistent);

            _tape = Tape.Create(TapeCapacity, Allocator.Persistent);
            _segments = new NativeList<Segment>(4, Allocator.Persistent);
            _scratch = new NativeArray<ushort>(TapeCapacity, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            _modifiers = EffectModifiers.Neutral;

            SeedEndogenous(entitySeed);
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
                InsertTick = 0u,
                Integrity = 1f,
                NetFitnessDelta = 0f,
                DominantAction = ResolveDominant(0, EndogenousLength)
            });

            _segmentsDirty = true;
        }

        public ProteinAction ResolveDominant(int start, int length)
        {
            if (!_created || length <= 0) return ProteinAction.Noop;

            int best = 0;
            int bestCount = -1;
            var counts = new NativeArray<int>(ProteinTable.ActionCount, Allocator.Temp);

            int end = math.min(start + length, _tape.Length);
            for (int i = start; i < end; i++)
            {
                int a = CodonEmbedding.Translate(_tape.Codons[i], _table.Centroids,
                    VirologyRuntime.CellBias, _table.Sharpness, out _);
                counts[a]++;
            }

            for (int a = 0; a < ProteinTable.ActionCount; a++)
            {
                if (a == (int)ProteinAction.Junk || counts[a] <= bestCount) continue;
                bestCount = counts[a];
                best = a;
            }

            counts.Dispose();
            return (ProteinAction)best;
        }
        public void Step(StatsComponent stats)
        {
            if (!_created || IsDisposed || stats == null || stats.IsDisposed) return;

            _tick++;
            Ribosome.Relax(ref _modifiers);
            _modifiers.ResetTransient();
            _lastCodonIndex = _tape.Head;

            float energy = stats.Energy.Value;
            var result = Ribosome.Step(ref _tape, ref _modifiers, ref _primed,
                _table.Centroids, VirologyRuntime.CellBias, VirologyRuntime.Descriptors,
                _table.Sharpness, energy, MaxTapeLength);

            _lastAction = result.Action;
            _lastAmp = result.Amp;
            _lastExecuted = result.Executed;

            if (result.Executed) _executedCodons++;
            else { _failedCodons++; return; }

            float health = stats.Health.Value + _modifiers.HealthDelta;
            energy = energy - result.SpentEnergy + _modifiers.EnergyDelta;

            stats.Health.Value = math.min(health, stats.MaxHealth);
            stats.Energy.Value = math.clamp(energy, 0f, stats.MaxEnergy);

            _table.Sharpness = math.clamp(_table.Sharpness + _modifiers.SharpnessDelta * 0.01f, 0.05f, 3f);
            _modifiers.SharpnessDelta = 0f;

            int owner = SegmentIndexAt(_lastCodonIndex);
            if (owner < 0) return;

            float delta = _modifiers.HealthDelta + _modifiers.EnergyDelta - result.SpentEnergy;
            float reinforce = _modifiers.ReinforceAmount;
            if (delta == 0f && reinforce <= 0f) return;

            var seg = _segments[owner];
            seg.NetFitnessDelta += delta;
            if (reinforce > 0f) seg.Integrity = math.min(seg.Integrity + reinforce, 1f);
            _segments[owner] = seg;

            _modifiers.ReinforceAmount = 0f;
            _valuesDirty = true;
        }

                public void BuildInfectionViews(System.Collections.Generic.List<InfectionView> target)
        {
            target.Clear();
            if (!_created || IsDisposed) return;

            for (int i = 0; i < _segments.Length; i++)
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
            if (!_created || IsDisposed || target.Count != _segments.Length) return false;

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

            for (int i = 0; i < _segments.Length; i++)
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

        public int Restrict(int index, NativeArray<ushort> destination) => CopySegment(index, destination);
        public ulong LineageOf(int index) => _segments[index].LineageId;

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _created = false;
            _tape.Dispose();
            _table.Dispose();
            if (_segments.IsCreated) _segments.Dispose();
            if (_scratch.IsCreated) _scratch.Dispose();
        }
    }
}