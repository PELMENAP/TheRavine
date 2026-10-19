using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

using TheRavine.Generator;
using Random = Unity.Mathematics.Random;

namespace TheRavine.EntityControl
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct MoveJob : IJobParallelForTransform
    {
        public NativeArray<float3> Pos, Vel, PerchPoint, Curve0, Curve1, Curve2;
        public NativeArray<byte> State, Flip;
        public NativeArray<float> Timer, Phase, Aux, Duration, Panic;
        [NativeDisableParallelForRestriction] public NativeArray<int> PerchRef;
        [ReadOnly] public NativeArray<float3> Acc;
        [ReadOnly] public NativeArray<byte> Flock;
        [ReadOnly] public NativeArray<float2> FlockAnim;
        [ReadOnly] public NativeArray<TreePerch> Trees;

        public BirdHeightSampler Height;
        public WindSampler Wind;
        public BirdParams P;
        public float DeltaTime;
        public float2 Viewer;
        public float3 CameraRight;
        public quaternion FlipRotation, IdentityRotation;
        public uint Seed;

        public void Execute(int i, TransformAccess transform)
        {
            float dt = DeltaTime;
            float3 p = Pos[i];
            float3 v = Vel[i];
            byte s = State[i];

            switch (s)
            {
                case BirdState.Landing:
                    Land(i, dt, ref p, ref v, ref s);
                    break;
                case BirdState.Perched:
                    Perch(i, dt, ref p, ref v, ref s);
                    break;
                default:
                    Fly(i, dt, ref p, ref v, ref s);
                    break;
            }

            Pos[i] = p;
            Vel[i] = v;
            State[i] = s;

            if (s != BirdState.Perched)
            {
                float2 anim = FlockAnim[Flock[i]];
                float speed = math.saturate(math.length(v) / P.MaxSpeed);
                float ph = Phase[i] + dt * anim.x * math.lerp(P.FlapMinScale, 1f, speed);
                if (ph >= anim.y) ph -= anim.y * math.floor(ph / anim.y);
                Phase[i] = ph;
            }

            float side = math.dot(v, CameraRight);
            if (math.abs(side) > P.FlipDeadzone)
            {
                byte f = side < 0f ? (byte)1 : (byte)0;
                if (f != Flip[i])
                {
                    Flip[i] = f;
                    transform.rotation = f != 0 ? FlipRotation : IdentityRotation;
                }
            }

            transform.position = p;
        }

        private void Fly(int i, float dt, ref float3 p, ref float3 v, ref byte s)
        {
            float2 w = Wind.Sample(p.xz) * P.WindDrift;
            float3 drift = new(w.x, 0f, w.y);
            float3 vAir = v - drift + Acc[i] * dt;

            float sq = math.lengthsq(vAir);
            float maxSq = P.MaxSpeed * P.MaxSpeed;
            if (sq > maxSq) vAir *= math.rsqrt(sq) * P.MaxSpeed;
            else if (s == BirdState.Flying && sq < P.MinAirSpeed * P.MinAirSpeed && sq > 1e-8f) vAir *= math.rsqrt(sq) * P.MinAirSpeed;

            v = vAir + drift;
            p += v * dt;

            float t = Timer[i] + dt;
            Timer[i] = t;

            if (s != BirdState.TakeOff)
            {
                float floor = Height.SampleTerrain(p.xz) + P.MinClearance;
                if (p.y < floor)
                {
                    p.y = floor;
                    if (v.y < 0f) v.y = 0f;
                }
            }

            if (s == BirdState.TakeOff)
            {
                if (t >= P.TakeOffDuration)
                {
                    s = BirdState.Flying;
                    Timer[i] = 0f;
                    Panic[i] = BirdState.PanicNone;
                }
                return;
            }

            if (s != BirdState.Approach) return;

            if (t > P.ApproachTimeout)
            {
                s = BirdState.Flying;
                Timer[i] = 0f;
                if (PerchRef[i] == BirdState.PerchGround) PerchRef[i] = BirdState.PerchNone;
                return;
            }

            float3 perch = PerchPoint[i];
            if (math.lengthsq(perch.xz - p.xz) > P.LandingStart * P.LandingStart) return;

            float3 toPerch = perch - p;
            float duration = math.clamp(math.length(toPerch) / P.LandingSpeed, P.LandingDuration.x, P.LandingDuration.y);

            float2 approachDir = math.normalizesafe(toPerch.xz, math.normalizesafe(v.xz, new float2(0f, 1f)));
            float2 windDir = Wind.Params.dir;
            float bias = math.saturate(Wind.Params.strength * P.LandingWindBias);
            float2 endDir = math.normalizesafe(math.lerp(approachDir, -windDir, bias), approachDir);
            float3 tangent = math.normalize(new float3(endDir.x, -P.LandingDescent, endDir.y)) * P.LandingFinalSpeed;

            float third = duration / 3f;
            Curve0[i] = p;
            Curve1[i] = p + v * third;
            Curve2[i] = perch - tangent * third;
            Duration[i] = duration;
            Timer[i] = 0f;
            s = BirdState.Landing;
        }

        private void Land(int i, float dt, ref float3 p, ref float3 v, ref byte s)
        {
            float duration = Duration[i];
            float t = Timer[i] + dt;
            Timer[i] = t;

            float3 p0 = Curve0[i], p1 = Curve1[i], p2 = Curve2[i], p3 = PerchPoint[i];
            float u = math.saturate(t / duration);
            float r = 1f - u;

            if (u >= 1f)
            {
                p = p3;
                v = float3.zero;
                s = BirdState.Perched;
                var rnd = Random.CreateFromIndex(Seed ^ (uint)i);
                Timer[i] = rnd.NextFloat(P.PerchDuration.x, P.PerchDuration.y);
                if (PerchRef[i] < 0) Aux[i] = rnd.NextFloat(P.HopInterval.x, P.HopInterval.y);
                Panic[i] = BirdState.PanicNone;
                return;
            }

            p = r * r * r * p0 + 3f * r * r * u * p1 + 3f * r * u * u * p2 + u * u * u * p3;
            v = (3f * r * r * (p1 - p0) + 6f * r * u * (p2 - p1) + 3f * u * u * (p3 - p2)) / duration;
        }

        private void Perch(int i, float dt, ref float3 p, ref float3 v, ref byte s)
        {
            float3 prev = p;
            float3 perch = PerchPoint[i];
            int perchRef = PerchRef[i];

            float timer = Timer[i] - dt;
            Timer[i] = timer;

            float panic = Panic[i];
            bool scared = false;
            if (panic >= 0f)
            {
                panic -= dt;
                Panic[i] = panic;
                scared = panic < 0f;
            }

            if (scared || timer <= 0f)
            {
                TakeOff(i, perch, perchRef, scared, ref p, ref v, ref s);
                return;
            }

            if (perchRef >= 0)
            {
                WindParams w = Wind.Params;
                float sway = P.SwayAmplitude * Aux[i] * w.strength * math.sin(w.time * w.pulse + Phase[i]);
                p = perch + new float3(w.dir.x, 0f, w.dir.y) * sway;
            }
            else
            {
                float hop = Aux[i] - dt;
                p = perch;
                if (hop <= 0f)
                {
                    float q = -hop / P.HopDuration;
                    if (q >= 1f)
                    {
                        var rnd = Random.CreateFromIndex(Seed ^ (uint)i);
                        float2 nxz = perch.xz + rnd.NextFloat2Direction() * P.HopDistance;
                        if (Height.IsPerchable(nxz))
                        {
                            perch = new float3(nxz.x, Height.SampleTerrain(nxz) + P.GroundPerchOffset, nxz.y);
                            PerchPoint[i] = perch;
                        }
                        p = perch;
                        hop = rnd.NextFloat(P.HopInterval.x, P.HopInterval.y);
                    }
                    else
                    {
                        p.y += P.HopHeight * math.sin(math.PI * q);
                    }
                }
                Aux[i] = hop;
            }

            v = dt > 0f ? (p - prev) / dt : float3.zero;
        }

        private void TakeOff(int i, float3 perch, int perchRef, bool scared, ref float3 p, ref float3 v, ref byte s)
        {
            var rnd = Random.CreateFromIndex(Seed ^ (uint)i);
            float2 outward;
            if (perchRef >= 0 && (perchRef >> BirdState.SlotShift) < Trees.Length)
                outward = math.normalizesafe(perch.xz - Trees[perchRef >> BirdState.SlotShift].Xz, rnd.NextFloat2Direction());
            else if (scared)
                outward = math.normalizesafe(perch.xz - Viewer, rnd.NextFloat2Direction());
            else
                outward = rnd.NextFloat2Direction();

            v = new float3(outward.x * P.TakeOffOut, P.TakeOffUp, outward.y * P.TakeOffOut);
            p = perch;
            s = BirdState.TakeOff;
            Timer[i] = 0f;
            Panic[i] = scared ? BirdState.PanicSpread : BirdState.PanicNone;
        }
    }

    [BurstCompile]
    public struct MoveMobsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float2> Velocities;
        [ReadOnly] public float DeltaTime;

        public void Execute(int index, TransformAccess transform)
        {
            float3 position = transform.position;
            position.x += Velocities[index].x * DeltaTime;
            position.z += Velocities[index].y * DeltaTime;
            transform.position = position;
        }
    }
}