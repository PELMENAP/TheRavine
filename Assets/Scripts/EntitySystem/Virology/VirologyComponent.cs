using System;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Virology
{
    public class VirologyComponent : IComponent
    {
        public const int MaxTapeLength = 192;
        public const int TapeCapacity = 320;
        public const int EndogenousLength = 24;
        public const byte NoSegment = byte.MaxValue;

        private Tape _tape;
        private Segment[] _segments;
        private int _segmentCount;
        private TranslationTable _table;
        private EffectModifiers _modifiers;
        private ushort[] _scratch;
        private byte[] _posAction;
        private float[] _posD2;
        private byte[] _posSeg;
        private XorShift32 _rng;
        private float _codonBudget;
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
        public ReadOnlySpan<Segment> Segments => new(_segments, 0, _segmentCount);
        public ProteinAction LastAction => (ProteinAction)_lastAction;
        public float LastAmp => _lastAmp;
        public bool LastExecuted => _lastExecuted;
        public int Head => _tape.Head;
        public int TapeLength => _tape.Length;
        public int SegmentCount => _created ? _segmentCount : 0;
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
            if ((uint)position >= (uint)_tape.Length) return -1;
            byte s = _posSeg[position];
            return s == NoSegment ? -1 : s;
        }

        public void ReinforceSegment(int index, float amount)
        {
            ref var s = ref _segments[index];
            s.Integrity = math.min(s.Integrity + amount, 1f);
            _valuesDirty = true;
        }

        public bool HasStrain(ulong strainId, out int index)
        {
            for (int i = 0; i < _segmentCount; i++)
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
            Array.Copy(_tape.Codons, s.Start, destination, 0, count);
            return count;
        }

        public ushort FirstCodonOf(int index) => _tape.Codons[_segments[index].Start];

        public bool TryFindLineageMatch(ulong signature, ulong strainId, out int index)
        {
            for (int i = 0; i < _segmentCount; i++)
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
            if (_segmentCount + 2 > _segments.Length) return false;
            if (_immuneCount > 0 && IsImmune(Segment.ComputeSignature(source, 0, count)))
            {
                ImmuneBlocks++;
                return false;
            }

            var rng = new XorShift32(seed == 0u ? 1u : seed);
            int index = _tape.Length > 0 ? (int)(rng.NextUInt() % (uint)(_tape.Length + 1)) : 0;

            SplitAt(index);
            int oldLength = _tape.Length;
            if (!_tape.TryInsert(index, source, 0, count))
            {
                RebuildSegmentMap();
                return false;
            }

            OnInserted(index, count, oldLength);
            ShiftSegments(index, count);

            _segments[_segmentCount++] = new Segment
            {
                Start = index,
                Length = count,
                StrainId = strainId,
                LineageId = lineageId,
                Signature = Segment.ComputeSignature(_tape.Codons, index, count),
                InsertTick = _tick,
                Integrity = 1f
            };

            RebuildSegmentMap();
            _segments[_segmentCount - 1].DominantAction = ResolveDominant(index, count);
            _segmentsDirty = true;
            return true;
        }

        private void AppendEndogenous(int count, ulong lineageId)
        {
            _segments[_segmentCount++] = new Segment
            {
                Start = 0,
                Length = count,
                StrainId = 0UL,
                LineageId = lineageId,
                Signature = Segment.ComputeSignature(_tape.Codons, 0, count),
                InsertTick = _tick,
                Integrity = 1f
            };

            RebuildSegmentMap();
            _segments[_segmentCount - 1].DominantAction = ResolveDominant(0, count);
            _segmentsDirty = true;
        }

        private void SeedEndogenous()
        {
            for (int i = 0; i < EndogenousLength; i++)
                _scratch[i] = (ushort)(_rng.NextUInt() & 0xFFFFu);

            InsertTranslated(0, _scratch, EndogenousLength);
            AppendEndogenous(EndogenousLength, _tape.ComputeHash(0, EndogenousLength));
        }

        public void Step(StatsComponent stats, float dt, in HostState host)
        {
            if (!_created || IsDisposed || stats == null || stats.IsDisposed) return;

            ref readonly var rules = ref SimulationRules.Frame;
            _tick++;

            float k = math.exp(-dt / math.max(rules.ModifierDecayTau, 1e-3f));
            Ribosome.Relax(ref _modifiers, k);
            _modifiers.ResetTransient();

            DecayIntegrity(dt, in rules);

            _codonBudget = math.min(_codonBudget + dt * rules.CodonsPerSecond, rules.MaxCodonsPerCycle);
            int n = (int)_codonBudget;
            _codonBudget -= n;

            for (int i = 0; i < n; i++)
            {
                if (_tape.Length <= 0 || stats.Hp <= 0f) break;
                StepCodon(stats, in rules, in host);
            }
        }

        private void StepCodon(StatsComponent stats, in SimulationRules.RulesFrame rules, in HostState host)
        {
            if (_dormantLeft > 0)
            {
                _dormantLeft--;
                _lastExecuted = false;
                return;
            }

            int pos = _tape.Head;
            _lastCodonIndex = pos;
            int owner = SegmentIndexAt(pos);

            if (owner >= 0) DegradeCodon(pos, owner, in rules);

            float healthBefore = stats.Hp;
            float energyBefore = stats.En;

            _modifiers.HealthDelta = 0f;
            _modifiers.EnergyDelta = 0f;
            _modifiers.ReinforceAmount = 0f;
            _modifiers.ReplicateRequested = false;
            _modifiers.ExciseRequested = false;

            var result = Ribosome.Step(ref _tape, ref _modifiers, ref _primed, owner,
                _posAction[pos], _posD2[pos], VirologyRuntime.Descriptors,
                _table.Sharpness, energyBefore, MaxTapeLength, in host);

            _lastAction = result.Action;
            _lastAmp = result.Amp;
            _lastExecuted = result.Executed;

            if (!result.Executed) { _failedCodons++; return; }
            _executedCodons++;

            stats.Hp = math.min(healthBefore + _modifiers.HealthDelta, stats.MaxHealth);
            stats.En = math.clamp(energyBefore - result.SpentEnergy + _modifiers.EnergyDelta, 0f, stats.MaxEnergy);

            _table.Sharpness = math.clamp(_table.Sharpness + _modifiers.SharpnessDelta * 0.01f, 0.05f, 3f);
            _modifiers.SharpnessDelta = 0f;

            if (_modifiers.Dormant) _dormantLeft = rules.DormantCodons;

            if (owner < 0) return;

            float invH = stats.MaxHealth > 0f ? 1f / stats.MaxHealth : 0f;
            float invE = stats.MaxEnergy > 0f ? 1f / stats.MaxEnergy : 0f;
            float delta = (stats.Hp - healthBefore) * invH
                        + (stats.En - energyBefore) * invE;

            ref var seg = ref _segments[owner];
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

            _valuesDirty = true;

            if (_modifiers.ImmunizeAmount > 0f && seg.IsEndogenous) Immunize(_modifiers.ImmunizeAmount);
            if (_modifiers.CaptureRequested && !seg.IsEndogenous) { TryCapture(owner, in rules); return; }
            if (_modifiers.ExciseRequested) { TryExcise(owner); return; }
            if (_modifiers.ReplicateRequested) TryReplicate(owner);
        }

        private ulong[] _immune;
        private int _immuneHead;
        private int _immuneCount;
        public int ImmuneBlocks { get; private set; }

        private void Remember(ulong signature)
        {
            if (_immune == null || _immune.Length == 0) return;
            for (int i = 0; i < _immuneCount; i++)
                if (_immune[i] == signature) return;

            _immune[_immuneHead] = signature;
            _immuneHead = (_immuneHead + 1) % _immune.Length;
            if (_immuneCount < _immune.Length) _immuneCount++;
        }

        public bool IsImmune(ulong signature)
        {
            int threshold = SimulationRules.Frame.ImmuneHammingThreshold;
            for (int i = 0; i < _immuneCount; i++)
                if (math.countbits(_immune[i] ^ signature) <= threshold) return true;
            return false;
        }

        private void Immunize(float amount)
        {
            for (int i = 0; i < _segmentCount; i++)
            {
                ref var s = ref _segments[i];
                if (s.IsEndogenous) continue;
                Remember(s.Signature);
                s.Integrity -= amount;
            }
            _valuesDirty = true;
        }

        private bool TryCapture(int owner, in SimulationRules.RulesFrame rules)
        {
            int endo = -1;
            for (int i = 0; i < _segmentCount; i++)
                if (_segments[i].IsEndogenous) { endo = i; break; }
            if (endo < 0) return false;

            var src = _segments[endo];
            int n = math.min(math.min(rules.CaptureLength, src.Length), _tape.Capacity - _tape.Length);
            if (n <= 0) return false;

            int from = src.Start + (int)(_rng.NextUInt() % (uint)(src.Length - n + 1));
            Array.Copy(_tape.Codons, from, _scratch, 0, n);

            ref var seg = ref _segments[owner];
            int insertAt = seg.Start + seg.Length;
            if (!InsertTranslated(insertAt, _scratch, n)) return false;

            for (int i = 0; i < _segmentCount; i++)
                if (i != owner && _segments[i].Start >= insertAt) _segments[i].Start += n;

            seg.Length   += n;
            seg.Signature = Segment.ComputeSignature(_tape.Codons, seg.Start, seg.Length);

            RebuildSegmentMap();
            _segmentsDirty = true;
            return true;
        }

        public bool TryExtractPayload(out ViralPayload payload)
        {
            payload = default;
            if (!_created || IsDisposed) return false;

            int best = -1;
            float bestMass = 0f;
            for (int i = 0; i < _segmentCount; i++)
            {
                var s = _segments[i];
                if (s.IsEndogenous) continue;
                float mass = s.Length * s.Integrity;
                if (mass <= bestMass) continue;
                bestMass = mass;
                best = i;
            }
            if (best < 0) return false;

            var seg = _segments[best];
            var codons = ViralPayloadPool.Rent();
            int count = math.min(seg.Length, codons.Length);
            Array.Copy(_tape.Codons, seg.Start, codons, 0, count);
            payload = new ViralPayload { Codons = codons, Count = count, StrainId = seg.StrainId, LineageId = seg.LineageId };
            return true;
        }

        public int RandomViralSegment()
        {
            int viral = 0;
            for (int i = 0; i < _segmentCount; i++)
                if (!_segments[i].IsEndogenous) viral++;
            if (viral == 0) return -1;

            int pick = (int)(_rng.NextUInt() % (uint)viral);
            for (int i = 0; i < _segmentCount; i++)
            {
                if (_segments[i].IsEndogenous) continue;
                if (pick-- == 0) return i;
            }
            return -1;
        }

        private void DegradeCodon(int pos, int owner, in SimulationRules.RulesFrame rules)
        {
            ref readonly var seg = ref _segments[owner];
            if (seg.IsEndogenous) return;

            float p = (1f - seg.Integrity) * rules.IntegrityMutationScale;
            if (p <= 0f || ViralMutator.NextUnit(ref _rng) >= p) return;

            _tape.Codons[pos] ^= (ushort)(1 << (int)(_rng.NextUInt() & 15u));
            TranslateAt(pos);
            _valuesDirty = true;
        }

        private void DecayIntegrity(float dt, in SimulationRules.RulesFrame rules)
        {
            float decay = rules.IntegrityDecayPerSecond * dt * (1f + _modifiers.Fever * rules.FeverIntegrityMul);
            if (decay <= 0f) return;

            for (int i = _segmentCount - 1; i >= 0; i--)
            {
                ref var s = ref _segments[i];
                if (s.IsEndogenous) continue;

                s.Integrity -= decay;
                _valuesDirty = true;
                if (s.Integrity < rules.IntegrityRemoveThreshold) TryExcise(i);
            }
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
            if (delta == 0f || (uint)owner >= (uint)_segmentCount) return;

            _segments[owner].NetFitnessDelta += delta;
            _valuesDirty = true;
        }

        private static void RemapOwner(ref int owner, int removed, int last)
        {
            if (owner == removed) owner = -1;
            else if (owner == last) owner = removed;
        }

        private void ShiftSegments(int from, int delta)
        {
            for (int i = 0; i < _segmentCount; i++)
                if (_segments[i].Start >= from) _segments[i].Start += delta;
        }

        private bool TryExcise(int index)
        {
            var seg = _segments[index];
            if (seg.IsEndogenous) return false;
            Remember(seg.Signature);

            int start = seg.Start;
            int len = seg.Length;
            if (len <= 0) return false;

            int oldLength = _tape.Length;
            _tape.Remove(start, len);
            OnRemoved(start, oldLength - _tape.Length, oldLength);

            for (int i = 0; i < _segmentCount; i++)
                if (i != index && _segments[i].Start > start) _segments[i].Start -= len;

            int last = --_segmentCount;
            if (index != last) _segments[index] = _segments[last];
            _segments[last] = default;

            RemapOwner(ref _modifiers.RegenOwner, index, last);
            RemapOwner(ref _modifiers.MetabolismOwner, index, last);

            RebuildSegmentMap();
            _lastCodonIndex = -1;
            _segmentsDirty = true;
            return true;
        }

        private bool TryReplicate(int index)
        {
            var seg = _segments[index];
            int len = seg.Length;
            if (len <= 0 || _tape.Length + len > _tape.Capacity) return false;
            if (_segmentCount >= _segments.Length) return false;

            int insertAt = seg.Start + len;
            Array.Copy(_tape.Codons, seg.Start, _scratch, 0, len);

            int oldLength = _tape.Length;
            if (!_tape.TryInsert(insertAt, _scratch, 0, len)) return false;

            ShiftCaches(insertAt, len, oldLength);
            Array.Copy(_posAction, seg.Start, _posAction, insertAt, len);
            Array.Copy(_posD2, seg.Start, _posD2, insertAt, len);
            ShiftSegments(insertAt, len);

            _segments[_segmentCount++] = new Segment
            {
                Start = insertAt,
                Length = len,
                StrainId = seg.StrainId,
                LineageId = seg.LineageId,
                Signature = seg.Signature,
                InsertTick = _tick,
                Integrity = seg.Integrity * SplitIntegrityPenalty,
                DominantAction = seg.DominantAction
            };

            RebuildSegmentMap();
            _segmentsDirty = true;
            return true;
        }

        private void SplitAt(int index)
        {
            for (int i = 0; i < _segmentCount; i++)
            {
                ref var s = ref _segments[i];
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

                _segments[_segmentCount++] = tail;
                return;
            }
        }

        private void TranslateAt(int pos)
        {
            _posAction[pos] = (byte)_table.Translate(_tape.Codons[pos], out _posD2[pos]);
        }

        private bool InsertTranslated(int index, ushort[] source, int count)
        {
            int oldLength = _tape.Length;
            if (!_tape.TryInsert(index, source, 0, count)) return false;
            OnInserted(math.clamp(index, 0, oldLength), count, oldLength);
            return true;
        }

        private void ShiftCaches(int index, int count, int oldLength)
        {
            int tail = oldLength - index;
            if (tail <= 0) return;
            Array.Copy(_posAction, index, _posAction, index + count, tail);
            Array.Copy(_posD2, index, _posD2, index + count, tail);
        }

        private void OnInserted(int index, int count, int oldLength)
        {
            ShiftCaches(index, count, oldLength);
            int end = index + count;
            for (int i = index; i < end; i++) TranslateAt(i);
        }

        private void OnRemoved(int index, int count, int oldLength)
        {
            int tail = oldLength - index - count;
            if (count <= 0 || tail <= 0) return;
            Array.Copy(_posAction, index + count, _posAction, index, tail);
            Array.Copy(_posD2, index + count, _posD2, index, tail);
        }

        private void RebuildSegmentMap()
        {
            int len = _tape.Length;
            Array.Fill(_posSeg, NoSegment, 0, len);
            for (int s = 0; s < _segmentCount; s++)
            {
                var seg = _segments[s];
                int end = math.min(seg.Start + seg.Length, len);
                for (int i = seg.Start; i < end; i++) _posSeg[i] = (byte)s;
            }
        }

        private bool Allocate(uint entitySeed)
        {
            if (_created) return false;
            if (!VirologyRuntime.IsReady)
            {
                UnityEngine.Debug.LogError(
                    $"[{nameof(VirologyComponent)}] VirologyRuntime not ready, component left uncreated (seed {entitySeed:X8})");
                return false;
            }
            _created = true;

            _rng = new XorShift32(entitySeed == 0u ? 1u : entitySeed);
            _tape = Tape.Create(TapeCapacity);
            _segments = new Segment[math.min(SimulationRules.Active.VirologyMaxSegments, NoSegment)];
            _segmentCount = 0;
            _scratch = new ushort[TapeCapacity];
            _posAction = new byte[TapeCapacity];
            _posD2 = new float[TapeCapacity];
            _posSeg = new byte[TapeCapacity];
            _modifiers = EffectModifiers.Neutral;
            _immune = new ulong[math.max(0, SimulationRules.Active.ImmuneMemorySize)];
            _immuneHead = 0;
            _immuneCount = 0;
            return true;
        }

        public void FillComponent(in GeneticParameters genetics, uint entitySeed)
        {
            if (!Allocate(entitySeed)) return;

            _table = TranslationTable.CreateFrom(VirologyRuntime.Prototype,
                math.max(genetics.Sharpness, 0.01f), genetics.GaussianNoise,
                entitySeed ^ 0x9E3779B9u);

            SeedEndogenous();
        }

        public void FillFromParent(VirologyComponent parent, in GeneticParameters genetics, uint entitySeed)
        {
            if (!Allocate(entitySeed)) return;

            var rules = SimulationRules.Active;

            _table = TranslationTable.Inherit(in parent._table, math.max(genetics.Sharpness, 0.01f),
                rules.VerticalTableMutationChance, _rng.NextUInt());

            int endo = -1;
            for (int i = 0; i < parent._segmentCount; i++)
                if (parent._segments[i].IsEndogenous) { endo = i; break; }

            int count = 0;
            if (endo >= 0)
            {
                var s = parent._segments[endo];
                count = ViralMutator.Transmit(parent._tape.Codons, s.Start, s.Length,
                    _scratch, rules.VerticalEndogenousMutationRate, ref _rng);
                count = math.min(count, MaxTapeLength);
            }

            if (count > 0)
            {
                InsertTranslated(0, _scratch, count);
                AppendEndogenous(count, parent._segments[endo].LineageId);
            }
            else SeedEndogenous();

            for (int i = 0; i < parent._segmentCount; i++)
            {
                var s = parent._segments[i];
                if (s.IsEndogenous) continue;
                float chance = s.Tamed ? rules.VerticalTamedChance : rules.VerticalTransmissionChance;
                if (ViralMutator.NextUnit(ref _rng) >= chance) continue;

                float rate = ViralMutator.ResolveRate(parent._tape.Codons[s.Start], parent._table.Centroids,
                    VirologyRuntime.CellBias, parent._table.Sharpness, parent._modifiers.MutationRateDelta);

                int written = ViralMutator.Transmit(parent._tape.Codons, s.Start, s.Length, _scratch, rate, ref _rng);
                if (written <= 0) continue;

                TryInsertSegment(_scratch, written, ViralMutator.ComputeStrainId(_scratch, written),
                    s.LineageId, _rng.NextUInt());
            }
        }

        public ProteinAction ResolveDominant(int start, int length)
        {
            if (!_created || length <= 0) return ProteinAction.Noop;

            Span<int> counts = stackalloc int[ProteinTable.ActionCount];
            counts.Clear();

            int end = math.min(start + length, _tape.Length);
            for (int i = start; i < end; i++) counts[_posAction[i]]++;

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

        public const int TamedThreshold = 8;

        private int _dormantLeft;

        public bool TryGetViralInputs(out float load, out float count, out float net)
        {
            load = 0f;
            count = 0f;
            net = 0f;
            if (!_created || IsDisposed) return false;

            float mass = 0f;
            float sum = 0f;
            int n = 0;

            for (int i = 0; i < _segmentCount; i++)
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

            for (int i = 0; i < _segmentCount; i++)
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
            if (!_created || IsDisposed || target.Count != _segmentCount) return false;

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

            for (int i = 0; i < _segmentCount; i++)
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

        public int Restrict(int index, ushort[] destination) => CopySegment(index, destination);
        public ulong LineageOf(int index) => _segments[index].LineageId;

        public ulong EndogenousLineage
        {
            get
            {
                if (!_created) return 0UL;
                for (int i = 0; i < _segmentCount; i++)
                    if (_segments[i].IsEndogenous) return _segments[i].LineageId;
                return 0UL;
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _created = false;
            _table.Release();
        }
    }
}
