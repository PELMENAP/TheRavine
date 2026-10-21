using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using TheRavine.Generator;

namespace TheRavine.EntityControl
{
    public struct BirdGrid
    {
        [ReadOnly] public NativeArray<int> CellEnd;
        [ReadOnly] public NativeArray<int> Items;
        public int Mask;
        public float InvCell;
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct BirdGridJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Pos;
        public NativeArray<int> CellEnd, Items, BucketOf;
        public int Mask;
        public float InvCell;

        public void Execute()
        {
            int buckets = Mask + 1;
            for (int b = 0; b <= buckets; b++) CellEnd[b] = 0;

            for (int i = 0; i < Pos.Length; i++)
            {
                int b = BoidHash.Bucket((int2)math.floor(Pos[i].xz * InvCell), Mask);
                BucketOf[i] = b;
                CellEnd[b + 1]++;
            }

            for (int b = 0; b < buckets; b++) CellEnd[b + 1] += CellEnd[b];

            for (int i = 0; i < Pos.Length; i++)
                Items[CellEnd[BucketOf[i]]++] = i;
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct BirdSlotJob : IJob
    {
        public NativeArray<TreePerch> Trees;
        [ReadOnly] public NativeArray<int> TreeCellEnd, TreeItems;
        public int TreeMask;
        public float TreeInvCell;

        public BirdHeightSampler Height;
        [ReadOnly] public NativeArray<float3> Pos;
        public NativeArray<byte> State;
        public NativeArray<int> PerchRef;
        public NativeArray<float3> PerchPoint;
        public NativeArray<float> Timer, Aux;
        [ReadOnly] public NativeArray<int> FlockStart;
        public NativeArray<byte> FlockCommand;

        public BirdParams P;
        public float2 CameraForward;
        public uint Seed;
        public bool HeightValid;

        public void Execute()
        {
            for (int i = 0; i < State.Length; i++)
            {
                byte s = State[i];
                if (s != BirdState.Flying && s != BirdState.TakeOff) continue;
                int r = PerchRef[i];
                if (r >= 0)
                {
                    int t = r >> BirdState.SlotShift;
                    if (t < Trees.Length)
                    {
                        TreePerch tree = Trees[t];
                        tree.Slots &= ~(1ul << (r & BirdState.SlotMask));
                        Trees[t] = tree;
                    }
                }
                if (r != BirdState.PerchNone) PerchRef[i] = BirdState.PerchNone;
            }

            var rnd = new Random(Seed | 1u);
            for (int f = 0; f < FlockCommand.Length; f++)
            {
                if (FlockCommand[f] == 0) continue;
                FlockCommand[f] = 0;
                Plan(f, ref rnd);
            }
        }

        private void Plan(int flock, ref Random rnd)
        {
            int start = FlockStart[flock], end = FlockStart[flock + 1];
            float2 centroid = float2.zero;
            int count = 0;
            for (int i = start; i < end; i++)
            {
                if (State[i] != BirdState.Flying) continue;
                centroid += Pos[i].xz;
                count++;
            }
            if (count == 0) return;
            centroid /= count;

            int slots = P.SlotsTheta * P.SlotsHeight;
            ulong full = slots >= 64 ? ~0ul : (1ul << slots) - 1ul;
            bool useTree = Trees.Length > 0 && rnd.NextFloat() >= P.GroundPerchChance;
            int tree = useTree ? FindTree(centroid, full) : -1;
            if (tree < 0 && !HeightValid) return;

            float2 toCamera = math.normalizesafe(-CameraForward, new float2(0f, -1f));
            float baseAngle = math.atan2(toCamera.y, toCamera.x);

            for (int i = start; i < end; i++)
            {
                if (State[i] != BirdState.Flying) continue;

                if (tree >= 0)
                {
                    TreePerch t = Trees[tree];
                    if ((t.Slots & full) == full)
                    {
                        tree = FindTree(centroid, full);
                        if (tree < 0) return;
                        t = Trees[tree];
                    }

                    int slot = PickSlot(t.Slots, slots, ref rnd);
                    t.Slots |= 1ul << slot;
                    Trees[tree] = t;

                    int ti = slot % P.SlotsTheta;
                    int hi = slot / P.SlotsTheta;
                    float u = (hi + rnd.NextFloat()) / P.SlotsHeight;
                    float u2 = rnd.NextFloat();
                    float theta = baseAngle + ((ti + rnd.NextFloat()) / P.SlotsTheta - 0.5f) * math.PI;
                    float height = t.YMax - t.YMin;
                    float h = math.lerp(t.YMin + P.PerchMinHeight * height, t.YMax, math.sqrt(u));
                    float r = t.Radius * math.lerp(P.PerchRadiusMin, 1f, u2);
                    math.sincos(theta, out float sn, out float cs);

                    PerchPoint[i] = new float3(t.Xz.x + cs * r, h, t.Xz.y + sn * r);
                    PerchRef[i] = (tree << BirdState.SlotShift) | slot;
                    Aux[i] = height > 1e-3f ? (h - t.YMin) / height : 0f;
                }
                else
                {
                    bool found = false;
                    float2 q = centroid;
                    for (int k = 0; k < P.GroundPerchTries && !found; k++)
                    {
                        q = centroid + rnd.NextFloat2Direction() * (math.sqrt(rnd.NextFloat()) * P.GroundPerchSpread);
                        found = Height.IsPerchable(q);
                    }
                    if (!found) continue;

                    PerchPoint[i] = new float3(q.x, Height.SampleTerrain(q) + P.GroundPerchOffset, q.y);
                    PerchRef[i] = BirdState.PerchGround;
                    Aux[i] = 0f;
                }

                State[i] = BirdState.Approach;
                Timer[i] = 0f;
            }
        }

        private static int PickSlot(ulong used, int slots, ref Random rnd)
        {
            int free = slots - math.countbits(used & (slots >= 64 ? ~0ul : (1ul << slots) - 1ul));
            int k = rnd.NextInt(free);
            for (int s = 0; s < slots; s++)
            {
                if ((used & (1ul << s)) != 0) continue;
                if (k-- == 0) return s;
            }
            return 0;
        }

        private int FindTree(float2 c, ulong full)
        {
            int reach = (int)math.ceil(P.PerchSearchRadius * TreeInvCell);
            int2 cell = (int2)math.floor(c * TreeInvCell);
            float best = P.PerchSearchRadius * P.PerchSearchRadius;
            int bestIdx = -1;

            for (int dz = -reach; dz <= reach; dz++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                int b = BoidHash.Bucket(cell + new int2(dx, dz), TreeMask);
                int end = TreeCellEnd[b];
                for (int k = b == 0 ? 0 : TreeCellEnd[b - 1]; k < end; k++)
                {
                    int t = TreeItems[k];
                    TreePerch tree = Trees[t];
                    if ((tree.Slots & full) == full) continue;
                    float d2 = math.lengthsq(tree.Xz - c);
                    if (d2 >= best) continue;
                    best = d2;
                    bestIdx = t;
                }
            }
            return bestIdx;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public unsafe struct AccelerationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Pos, Vel, PerchPoint;
        [ReadOnly] public NativeArray<byte> State, Flock;
        [ReadOnly] public NativeArray<int> PerchRef;
        [ReadOnly] public NativeArray<float2> FlockTargets;
        [WriteOnly] public NativeArray<float3> Acc;
        [NativeDisableParallelForRestriction] public NativeArray<float> Panic;

        public BirdGrid Grid;
        public BirdPerchGrid Trees;
        public BirdHeightSampler Height;
        public WindSampler Wind;
        public BirdParams P;
        public float2 Viewer;
        public uint Seed;

        public void Execute(int i)
        {
            byte s = State[i];
            if (s == BirdState.Perched)
            {
                Acc[i] = float3.zero;
                CheckPanic(i);
                return;
            }
            if (s == BirdState.Landing)
            {
                Acc[i] = float3.zero;
                return;
            }

            float3 p = Pos[i];
            float3 v = Vel[i];
            float2 wind = Wind.Sample(p.xz) * P.WindDrift;
            float3 vAir = v - new float3(wind.x, 0f, wind.y);

            float3 flocking = Flocking(i, p, vAir);

            float2 target;
            float invSlow, flockWeight = 1f;
            float distXZ = 0f;
            int ownTree = -1;

            if (s == BirdState.Approach)
            {
                float3 perch = PerchPoint[i];
                target = perch.xz;
                invSlow = P.InvApproachSlow;
                distXZ = math.length(target - p.xz);
                flockWeight = math.saturate(distXZ * P.InvApproachFade);
                int r = PerchRef[i];
                if (r >= 0) ownTree = r >> BirdState.SlotShift;
            }
            else
            {
                target = FlockTargets[Flock[i]];
                invSlow = P.InvSlowRadius;
            }

            float2 toTarget = target - p.xz;
            float targetDistSq = math.lengthsq(toTarget);
            float2 steer = -vAir.xz;
            if (targetDistSq > 1e-6f)
            {
                float invDist = math.rsqrt(targetDistSq);
                steer += toTarget * (invDist * P.MaxSpeed * math.saturate(targetDistSq * invDist * invSlow));
            }

            float3 acc = flocking * flockWeight + new float3(steer.x, 0f, steer.y) * P.TargetWeight;

            float cruise = math.max(Height.SampleCanopy(p.xz), Height.SampleCanopy(p.xz + v.xz * P.Lookahead)) + P.CruiseAGL;
            float yT = cruise;
            if (s == BirdState.Approach)
            {
                float k = math.saturate((distXZ - P.LandingStart) * P.InvApproachSlow);
                yT = math.lerp(PerchPoint[i].y + P.LandingApproachHeight, cruise, k);
            }
            acc.y += P.Kp * (yT - p.y) - P.Kd * v.y;

            acc += AvoidTrees(p, ownTree);
            Acc[i] = acc;
        }

        private float3 Flocking(int i, float3 p, float3 vAir)
        {
            int2 cell = (int2)math.floor(p.xz * Grid.InvCell);
            int* seen = stackalloc int[9];
            int seenCount = 0;
            int flock = Flock[i];

            float avoidSq = P.Avoidance * P.Avoidance;
            float alongSq = P.Along * P.Along;
            float3 sep = float3.zero, ali = float3.zero, coh = float3.zero;
            int n = 0;
            int maxN = P.MaxNeighbors;
            uint rot = math.hash(new uint2((uint)i, Seed));

            for (int dz = -1; dz <= 1 && n < maxN; dz++)
            for (int dx = -1; dx <= 1 && n < maxN; dx++)
            {
                int b = BoidHash.Bucket(cell + new int2(dx, dz), Grid.Mask);
                bool dup = false;
                for (int k = 0; k < seenCount; k++) if (seen[k] == b) { dup = true; break; }
                if (dup) continue;
                seen[seenCount++] = b;

                int start = b == 0 ? 0 : Grid.CellEnd[b - 1];
                int end = Grid.CellEnd[b];
                int cnt = end - start;
                if (cnt == 0) continue;

                int idx = start + (int)(rot % (uint)cnt);
                for (int c = 0; c < cnt && n < maxN; c++, idx++)
                {
                    if (idx == end) idx = start;
                    int j = Grid.Items[idx];
                    if (j == i || Flock[j] != flock || !BirdState.IsAirborne(State[j])) continue;

                    float3 d = p - Pos[j];
                    float d2 = math.lengthsq(d);
                    if (d2 >= alongSq) continue;

                    if (d2 < avoidSq && d2 > 1e-6f)
                    {
                        float inv = math.rsqrt(d2);
                        sep += d * (inv * (P.Avoidance - d2 * inv));
                    }
                    ali += Vel[j];
                    coh += Pos[j];
                    n++;
                }
            }

            float3 result = sep * P.Weights.x;
            if (n > 0)
            {
                float invN = 1f / n;
                result += (ali * invN - vAir) * P.Weights.y + (coh * invN - p) * P.Weights.z;
            }
            result.y *= P.VerticalFlockScale;
            return result;
        }

        private float3 AvoidTrees(float3 p, int ownTree)
        {
            if (Trees.Trees.Length == 0) return float3.zero;

            int2 cell = Trees.Cell(p.xz);
            int* seen = stackalloc int[9];
            int seenCount = 0;
            float2 force = float2.zero;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int b = Trees.Bucket(cell + new int2(dx, dz));
                bool dup = false;
                for (int k = 0; k < seenCount; k++) if (seen[k] == b) { dup = true; break; }
                if (dup) continue;
                seen[seenCount++] = b;

                int end = Trees.End(b);
                for (int k = Trees.Start(b); k < end; k++)
                {
                    int t = Trees.Items[k];
                    if (t == ownTree) continue;
                    TreePerch tree = Trees.Trees[t];
                    if (p.y >= tree.YMax) continue;

                    float reach = tree.Radius + P.TreeMargin;
                    float2 d = p.xz - tree.Xz;
                    float d2 = math.lengthsq(d);
                    if (d2 >= reach * reach || d2 < 1e-6f) continue;

                    float inv = math.rsqrt(d2);
                    float w = 1f - d2 * inv / reach;
                    force += d * (inv * w);
                }
            }
            return new float3(force.x, 0f, force.y) * P.TreeAvoidWeight;
        }

        private void CheckPanic(int i)
        {
            if (Panic[i] >= 0f) return;

            float2 p = Pos[i].xz;
            if (math.distancesq(p, Viewer) < P.ScareRadiusSq)
            {
                Panic[i] = 0f;
                return;
            }

            int2 cell = (int2)math.floor(p * Grid.InvCell);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int b = BoidHash.Bucket(cell + new int2(dx, dz), Grid.Mask);
                int end = Grid.CellEnd[b];
                for (int k = b == 0 ? 0 : Grid.CellEnd[b - 1]; k < end; k++)
                {
                    int j = Grid.Items[k];
                    if (State[j] != BirdState.TakeOff || Panic[j] != BirdState.PanicSpread || math.distancesq(p, Pos[j].xz) >= P.PanicRadiusSq) continue;
                    var rnd = Random.CreateFromIndex(Seed ^ (uint)i);
                    Panic[i] = P.PanicDelay * rnd.NextFloat(0.5f, 1.5f);
                    return;
                }
            }
        }
    }
}
