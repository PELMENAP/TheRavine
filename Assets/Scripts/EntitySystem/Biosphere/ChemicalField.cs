using System.Collections.Generic;
using Unity.Mathematics;
using TheRavine.Extensions;

namespace TheRavine.EntityControl.Biosphere
{
    public sealed class ChemicalField
    {
        private readonly Dictionary<long, int> _slots;
        private readonly long[]   _keys;
        private readonly float4[] _amount;
        private readonly float4[] _delta;
        private int _count;

        public int Count => _count;
        public int Capacity => _keys.Length;
        public double Source { get; private set; }

        public ChemicalField(int capacity)
        {
            capacity = math.max(capacity, 16);
            _slots  = new Dictionary<long, int>(capacity);
            _keys   = new long[capacity];
            _amount = new float4[capacity];
            _delta  = new float4[capacity];
        }

        public static float4 Ambient(long key, float level, float variation, float scale)
        {
            float2 p = new float2(Position2Int.GetX(key), Position2Int.GetY(key)) * scale;
            float4 n = new float4(noise.snoise(p), noise.snoise(p + 37.1f), noise.snoise(p + 71.3f), noise.snoise(p + 113.7f));
            return math.max(level * (1f + variation * n), 0f);
        }

        public bool TryGet(long key, out float4 amount)
        {
            if (_slots.TryGetValue(key, out int slot)) { amount = _amount[slot]; return true; }
            amount = float4.zero;
            return false;
        }

        public float4 Total()
        {
            float4 sum = float4.zero;
            for (int i = 0; i < _count; i++) sum += _amount[i];
            return sum;
        }

        private int Ensure(long key, BiosphereRules rules)
        {
            if (_slots.TryGetValue(key, out int slot)) return slot;
            if (_count >= _keys.Length) return -1;
            slot = _count++;
            _slots[key] = slot;
            _keys[slot] = key;
            float4 start = Ambient(key, rules.ambientElement, rules.ambientVariation, rules.ambientNoiseScale);
            _amount[slot] = start;
            Source += math.csum(start);
            return slot;
        }

        public float4 Take(long key, float4 want, BiosphereRules rules)
        {
            int slot = Ensure(key, rules);
            if (slot < 0) return float4.zero;
            float4 taken = math.clamp(want, 0f, _amount[slot]);
            _amount[slot] -= taken;
            return taken;
        }

        public float4 Add(long key, float4 amount, BiosphereRules rules)
        {
            amount = math.max(amount, 0f);
            int slot = Ensure(key, rules);
            if (slot < 0) return amount;
            float4 room = math.max(rules.fieldMaxPerCell - _amount[slot], 0f);
            float4 put = math.min(amount, room);
            _amount[slot] += put;
            return amount - put;
        }

        public void Step(float dt, BiosphereRules rules)
        {
            if (!(dt > 0f) || _count == 0) return;
            float d = math.clamp(rules.fieldDiffusion * dt, 0f, 0.24f);
            float w = 1f - math.exp(-math.max(rules.weatheringRate, 0f) * dt);

            for (int i = 0; i < _count; i++) _delta[i] = float4.zero;

            if (d > 0f)
            {
                for (int i = 0; i < _count; i++)
                {
                    long key = _keys[i];
                    int x = Position2Int.GetX(key), z = Position2Int.GetY(key);
                    Flow(i, Position2Int.Pack(x + 1, z), d);
                    Flow(i, Position2Int.Pack(x, z + 1), d);
                }
            }

            for (int i = 0; i < _count; i++)
            {
                float4 a = math.max(_amount[i] + _delta[i], 0f);
                if (w > 0f)
                {
                    float4 ambient = Ambient(_keys[i], rules.ambientElement, rules.ambientVariation, rules.ambientNoiseScale);
                    float4 refill = math.max(ambient - a, 0f) * w;
                    a += refill;
                    Source += math.csum(refill);
                }
                _amount[i] = a;
            }
        }

        private void Flow(int i, long neighbourKey, float d)
        {
            if (!_slots.TryGetValue(neighbourKey, out int j)) return;
            float4 flux = (_amount[i] - _amount[j]) * d;
            _delta[i] -= flux;
            _delta[j] += flux;
        }
    }
}
