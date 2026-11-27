using Unity.Collections;
using Unity.Mathematics;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public struct WeatherStrike
    {
        public float3 Position;
        public float Time;
        public float Strength;
        public int Ground;
    }

    public sealed partial class WeatherEvent
    {
        private NativeArray<WeatherStrike> _strikes;
        private int _strikeCount;
        private int _stepFirstStrike;
        private int _stepGround;
        private float4 _strikeBounds;

        public int StrikeCount => _strikeCount;
        public int StrikeCapacity => _strikes.Length;
        public int StepFirstStrike => math.max(_stepFirstStrike, _strikeCount - _strikes.Length);
        public int StepGroundStrikes => _stepGround;
        public float4 StrikeBounds => _strikeBounds;

        public WeatherStrike StrikeAt(int serial) => _strikes[(int)((uint)serial % (uint)_strikes.Length)];

        public float LightningDamageAt(float2 p)
        {
            if (_disposed || _stepGround == 0) return 0f;
            float4 b = _strikeBounds;
            if (p.x < b.x || p.y < b.y || p.x > b.z || p.y > b.w) return 0f;

            float sum = 0f;
            for (int s = StepFirstStrike; s < _strikeCount; s++)
            {
                WeatherStrike strike = StrikeAt(s);
                if (strike.Ground == 0) continue;
                sum += WeatherMath.StrikeDamage(math.distance(p, strike.Position.xz),
                    _rules.creatureStrikeRadius, _rules.strikeDamage, strike.Strength);
            }
            return sum;
        }

        public bool TryFindNearestStrike(float2 p, float radius, out float2 source, out float distance)
        {
            source = default;
            distance = -1f;
            if (_disposed || _stepGround == 0 || !(radius > 0f)) return false;

            float best = radius * radius;
            bool found = false;
            for (int s = StepFirstStrike; s < _strikeCount; s++)
            {
                WeatherStrike strike = StrikeAt(s);
                if (strike.Ground == 0) continue;
                float d2 = math.distancesq(p, strike.Position.xz);
                if (d2 > best) continue;
                best = d2;
                source = strike.Position.xz;
                found = true;
            }

            if (found) distance = math.sqrt(best);
            return found;
        }

        private void StrikeStep(float step)
        {
            _stepFirstStrike = _strikeCount;
            _stepGround = 0;
            _strikeBounds = EmptyBounds;

            for (int i = 0; i < _count; i++)
            {
                float charge = _charge[i] * WeatherMath.Envelope(_age[i], _life[i]);
                float expected = WeatherMath.StrikeRate(charge, _rules) * step;
                if (!(expected > 0f)) continue;

                int strikes = (int)expected;
                if (RavineRandom.RangeFloat() < expected - strikes) strikes++;
                for (int s = 0; s < strikes; s++) Strike(i, charge);
            }
        }

        private void Strike(int cell, float charge)
        {
            float2 around = _position[cell] + RandomDisk() * (_radius[cell] * _rules.strikeSpread);
            bool hasGround = _map.TrySampleHeightBilinear(around.x, around.y, out float groundY);

            if (charge < _rules.groundThreshold || (hasGround && groundY >= _rules.cloudPlaneY))
            {
                Record(new float3(around.x, _rules.cloudPlaneY, around.y), charge, false);
                return;
            }
            if (!hasGround) return;

            FireEvent fire = _events.Fire;
            float bestSize = 0f;
            float3 bestPoint = default;
            long bestCell = 0L;

            for (int k = 0; k < _rules.candidateSamples; k++)
            {
                float2 q = around + RandomDisk() * _rules.strikeRadius;
                int cx = (int)math.floor(q.x * MapGenerator.InvScale);
                int cz = (int)math.floor(q.y * MapGenerator.InvScale);
                long packed = Position2Int.Pack(cx << MapGenerator.CellShift, cz << MapGenerator.CellShift);
                if (fire == null || _map.Commands.HasPendingRemove(packed) || !_map.TryGetObject(packed, out ObjectInstInfo inst)) continue;

                float size = fire.SizeOf(inst.PrefabID);
                if (size <= bestSize || inst.Position.y + size >= _rules.cloudPlaneY) continue;
                bestSize = size;
                bestPoint = new float3(inst.Position.x, inst.Position.y + size, inst.Position.z);
                bestCell = Position2Int.Pack(cx, cz);
            }

            if (bestSize <= 0f)
            {
                Record(new float3(around.x, groundY, around.y), charge, true);
                return;
            }

            Record(bestPoint, charge, true);
            float ignite = _rules.ignitionPerStrike * (1f - WetnessAt(bestPoint.xz));
            if (RavineRandom.RangeFloat() < ignite) fire.TryIgnite(bestCell);
        }

        private void Record(float3 position, float strength, bool ground)
        {
            _strikes[(int)((uint)_strikeCount % (uint)_strikes.Length)] = new WeatherStrike
            {
                Position = position,
                Time = (float)StepTime,
                Strength = strength,
                Ground = ground ? 1 : 0
            };
            _strikeCount++;
            if (!ground) return;

            _stepGround++;
            float2 at = position.xz;
            float reach = _rules.creatureStrikeRadius;
            _strikeBounds.xy = math.min(_strikeBounds.xy, at - reach);
            _strikeBounds.zw = math.max(_strikeBounds.zw, at + reach);
        }

        private static float2 RandomDisk()
        {
            math.sincos(RavineRandom.RangeFloat() * (2f * math.PI), out float s, out float c);
            return new float2(c, s) * math.sqrt(RavineRandom.RangeFloat());
        }
    }
}
