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

        public void FillComponent(in GeneticParameters genetics, uint entitySeed)
        {
            if (_created || !VirologyRuntime.IsReady) return;
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

            if (_segments.Length > 0)
            {
                var seg = _segments[0];
                seg.NetFitnessDelta += _modifiers.HealthDelta + _modifiers.EnergyDelta;
                _segments[0] = seg;
            }
        }

        public TranslationTable CloneTable(Allocator allocator) => _table.Clone(allocator);

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