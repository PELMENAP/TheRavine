using System;
using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.EntityControl.Virology
{
    public sealed class InfectionService : IDisposable
    {
        public const int MaxNeighbors = 16;
        public const int PayloadCapacity = 96;
        public const float AccuracyBias = 1.35f;
        public const float RecombinationChance = 0.35f;

        private readonly GameObject[] _neighbors = new GameObject[MaxNeighbors];
        private ushort[] _payload;
        private ushort[] _mutated;
        private XorShift32 _rng;
        private bool _created;

        public int Transmissions { get; private set; }
        public int Recombinations { get; private set; }
        public int SuperinfectionBlocks { get; private set; }
        public int FailedAttempts { get; private set; }

        public InfectionService(uint seed)
        {
            _payload = new ushort[PayloadCapacity];
            _mutated = new ushort[PayloadCapacity];
            _rng = new XorShift32(seed == 0u ? 0x1234567u : seed);
            _created = true;
        }

        public const float SelectionGain = 4f;
        public const float SelectionFloor = 0.05f;

        public void ProcessSpread(EntityModel[] batch, int start, int end, uint tick)
        {
            if (!_created || batch == null) return;
            if (start < 0) start = 0;
            if (end > batch.Length) end = batch.Length;

            for (int i = start; i < end; i++)
            {
                var donor = batch[i];
                if (donor == null || donor.IsDisposed || donor.IsDeathPending) continue;

                var virology = donor.Virology;
                if (virology == null || !virology.IsCreated || virology.IsDisposed) continue;
                if (!virology.Modifiers.SpreadRequested) continue;

                float amp = virology.Modifiers.SpreadAmp;
                virology.Modifiers.SpreadRequested = false;
                virology.Modifiers.SpreadAmp = 0f;

                TryTransmit(donor, virology, amp, tick);
            }
        }

        private void TryTransmit(EntityModel donor, VirologyComponent virology, float amp, uint tick)
        {
            int donorIndex = virology.SegmentIndexAt(virology.LastCodonIndex);
            if (donorIndex < 0) { FailedAttempts++; return; }

            int count = virology.Restrict(donorIndex, _payload);
            if (count <= 0) { FailedAttempts++; return; }

            int found = donor.Perception.FindEntitiesInRadius(
                donor.Motor.Position(), donor.SelfObject, _neighbors);
            if (found == 0) { FailedAttempts++; return; }

            var target = SelectTarget(donor, found, amp);
            if (target == null) { FailedAttempts++; return; }

            float hostViability = donor.Stats != null && !donor.Stats.IsDisposed && donor.Stats.MaxHealth > 0f
                ? math.saturate(donor.Stats.Health.Value / donor.Stats.MaxHealth)
                : 0f;

            float selection = math.saturate(0.5f + virology.NetFitnessOf(donorIndex) * SelectionGain)
                            * hostViability;

            float chance = math.saturate(amp * AccuracyBias) * math.max(selection, SelectionFloor);

            if (ViralMutator.NextUnit(ref _rng) > chance)
            {
                FailedAttempts++;
                return;
            }

            var receiver = target.Virology;

            float rate = ViralMutator.ResolveRate(virology.FirstCodonOf(donorIndex),
                virology.Centroids, VirologyRuntime.CellBias, virology.Sharpness,
                virology.Modifiers.MutationRateDelta);

            int mutatedCount = ViralMutator.Transmit(_payload, 0, count, _mutated, rate, ref _rng);
            if (mutatedCount <= 0) { FailedAttempts++; return; }

            ulong strainId = ViralMutator.ComputeStrainId(_mutated, mutatedCount);
            ulong lineageId = virology.LineageOf(donorIndex);

            if (receiver.HasStrain(strainId, out int existing))
            {
                receiver.ReinforceSegment(existing, VirologyComponent.SuperinfectionBoost);
                SuperinfectionBlocks++;
                return;
            }

            if (receiver.TryFindLineageMatch(lineageId, strainId, out int partner)
                && ViralMutator.NextUnit(ref _rng) < RecombinationChance)
            {
                if (Recombine(receiver, partner, mutatedCount, tick)) Recombinations++;
                else FailedAttempts++;
                return;
            }

            if (receiver.TryInsertSegment(_mutated, mutatedCount, strainId, lineageId, tick, _rng.NextUInt()))
                Transmissions++;
            else FailedAttempts++;
        }

        private bool Recombine(VirologyComponent receiver, int partner, int incomingCount, uint tick)
        {
            int residentCount = receiver.CopySegment(partner, _payload);
            if (residentCount <= 0) return false;

            int cutIncoming = 1 + (int)(_rng.NextUInt() % (uint)math.max(1, incomingCount - 1));
            int cutResident = 1 + (int)(_rng.NextUInt() % (uint)math.max(1, residentCount - 1));

            int tail = math.min(residentCount - cutResident, PayloadCapacity - cutIncoming);
            if (tail <= 0) return false;

            for (int i = 0; i < tail; i++)
                _mutated[cutIncoming + i] = _payload[cutResident + i];

            int total = cutIncoming + tail;

            ulong hybridStrain  = ViralMutator.ComputeStrainId(_mutated, total);
            ulong hybridLineage = receiver.LineageOf(partner);

            return receiver.TryInsertSegment(_mutated, total, hybridStrain, hybridLineage, tick, _rng.NextUInt());
        }

        private EntityModel SelectTarget(EntityModel donor, int found, float amp)
        {
            if (amp >= 0.5f)
            {
                EntityModel best = null;
                float minD = float.MaxValue;
                Vector3 origin = donor.Motor.Position();

                for (int i = 0; i < found; i++)
                {
                    var candidate = Resolve(_neighbors[i]);
                    if (candidate == null) continue;
                    float d = (candidate.Motor.Position() - origin).sqrMagnitude;
                    if (d >= minD) continue;
                    minD = d;
                    best = candidate;
                }
                return best;
            }

            int pick = (int)(_rng.NextUInt() % (uint)found);
            return Resolve(_neighbors[pick]);
        }

        private static EntityModel Resolve(GameObject go)
        {
            if (go == null) return null;
            var model = go.GetComponent<EntityViewModel>()?.Entity as EntityModel;
            if (model == null || model.IsDisposed || model.IsDeathPending) return null;

            var v = model.Virology;
            return v != null && v.IsCreated && !v.IsDisposed ? model : null;
        }

        public bool InjectStrain(EntityModel target, int length, uint seed, uint tick)
        {
            if (!_created || target?.Virology == null || !target.Virology.IsCreated) return false;

            length = math.clamp(length, 4, PayloadCapacity);
            var rng = new XorShift32(seed == 0u ? 1u : seed);
            for (int i = 0; i < length; i++)
                _mutated[i] = (ushort)(rng.NextUInt() & 0xFFFFu);

            ulong strainId = ViralMutator.ComputeStrainId(_mutated, length);
            return target.Virology.TryInsertSegment(_mutated, length, strainId, strainId, tick, rng.NextUInt());
        }
        
        public bool InjectRecipe(EntityModel target, ProteinAction[] recipe, uint tick, uint seed)
        {
            if (!_created || recipe == null || recipe.Length == 0) return false;

            var virology = target?.Virology;
            if (virology == null || !virology.IsCreated || virology.IsDisposed) return false;

            int count = StrainComposer.Compose(recipe, _mutated, seed);
            if (count <= 0) return false;

            ulong strainId = ViralMutator.ComputeStrainId(_mutated, count);
            return virology.TryInsertSegment(_mutated, count, strainId, strainId, tick, seed ^ 0xA5A5A5A5u);
        }
        public void Dispose()
        {
            if (!_created) return;
            _created = false;

        }
    }
}