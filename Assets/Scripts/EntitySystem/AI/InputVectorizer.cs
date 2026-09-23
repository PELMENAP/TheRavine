using System;
using System.Runtime.InteropServices;
using R3;
using Unity.Mathematics;

public struct VectorizerFrame
{
    public float  Health;
    public float  Energy;
    public float  DayPhase;
    public float  InDanger;
    public float  TimeToBreed;
    public float  NearestEntityDist;
    public float  NearestFoodDist;
    public float  ViralLoad;
    public float  ViralSegments;
    public float  ViralNet;
    public float2 FoodDir;
    public float2 EntityDir;
    public float2 NestDir;
    public float2 PoiDir;
    public int    MimickedAction;
    public double Now;
}

public class InputVectorizer : IDisposable
{
    public const int VectorSize  = 64;
    public const int ActionCount = 13;

    public const int TraceOffset     = 8;
    public const int DirectionOffset = TraceOffset + ActionCount;
    public const int StomachSlot     = DirectionOffset + 8;
    public const int WellFedSlot     = StomachSlot + 1;
    public const int ReservedSlots   = 5;
    public const int TailOffset      = StomachSlot + ReservedSlots;

    private float _maxHealth;
    private float _maxEnergy;
    private const float MaxDetectionRadius = 20f;

    private float _prevHealth;
    private float _prevEnergy;
    private bool  _initialized;

    private readonly float[] _vector = new float[VectorSize];

    private IDisposable _subHealth;
    private IDisposable _subEnergy;

    public InputVectorizer(ReactiveProperty<float> maxHealth, ReactiveProperty<float> maxEnergy)
    {
        _maxHealth = maxHealth.Value;
        _maxEnergy = maxEnergy.Value;

        _subHealth = maxHealth.Subscribe(v => _maxHealth = v);
        _subEnergy = maxEnergy.Subscribe(v => _maxEnergy = v);
    }

    public int GetVectorSize() => VectorSize;

    public float[] Vectorize(in VectorizerFrame f, double[] actionTimes, in SpeechHash speech, in TerrainSample terrain)
    {
        var v = _vector;
        float health = f.Health, energy = f.Energy;

        v[0] = math.saturate(health / _maxHealth);
        v[1] = f.ViralLoad;
        v[2] = math.saturate(energy / _maxEnergy);
        v[3] = f.ViralSegments;
        v[4] = _initialized ? math.clamp((health - _prevHealth) / _maxHealth, -1f, 1f) : 0f;
        v[5] = _initialized ? math.clamp((energy - _prevEnergy) / _maxEnergy, -1f, 1f) : 0f;

        _prevHealth  = health;
        _prevEnergy  = energy;
        _initialized = true;

        math.sincos(f.DayPhase * (2f * math.PI), out float daySin, out float dayCos);
        v[6] = daySin;
        v[7] = dayCos;

        ref readonly var rules = ref SimulationRules.Frame;
        float invNorm = rules.ActionTraceInvLogNorm;
        for (int i = 0; i < ActionCount; i++)
        {
            double since = f.Now - actionTimes[i];
            v[TraceOffset + i] = since < rules.ActionTraceHorizon
                ? math.min(math.log(1f + (float)math.max(since, 0d)) * invNorm, 1f)
                : 1f;
        }

        int d = DirectionOffset;
        v[d]     = f.FoodDir.x;   v[d + 1] = f.FoodDir.y;
        v[d + 2] = f.EntityDir.x; v[d + 3] = f.EntityDir.y;
        v[d + 4] = f.NestDir.x;   v[d + 5] = f.NestDir.y;
        v[d + 6] = f.PoiDir.x;    v[d + 7] = f.PoiDir.y;

        for (int i = 0; i < ReservedSlots; i++) v[StomachSlot + i] = 0f;

        int idx = TailOffset;
        v[idx++] = math.saturate(f.InDanger);
        v[idx++] = math.saturate(f.TimeToBreed);

        v[idx++] = speech.A;
        v[idx++] = speech.B;
        v[idx++] = speech.C;
        v[idx++] = speech.D;

        v[idx++] = f.NearestEntityDist >= 0f ? 1f - math.saturate(f.NearestEntityDist / MaxDetectionRadius) : 0f;
        v[idx++] = f.NearestFoodDist   >= 0f ? 1f - math.saturate(f.NearestFoodDist   / MaxDetectionRadius) : 0f;

        bool terrainValid = terrain.IsValid;
        if (terrainValid) WriteTerrain(in terrain, ref idx);
        else WriteTerrain(in TerrainSample.Invalid, ref idx);

        v[idx++] = terrainValid ? 1f : 0f;

        bool hasMimic = (uint)f.MimickedAction < (uint)ActionCount;
        v[idx++] = hasMimic ? 1f : 0f;
        v[idx++] = hasMimic ? (f.MimickedAction + 0.5f) / ActionCount : 0f;

        v[idx] = f.ViralNet;

        return v;
    }

    private void WriteTerrain(in TerrainSample t, ref int idx)
    {
        var v = _vector;
        v[idx++] = t.HeightNorm;
        v[idx++] = t.Slope;
        v[idx++] = t.GradX;
        v[idx++] = t.GradZ;
        v[idx++] = t.WaterProximity;
        v[idx++] = t.MoveCost;
        v[idx++] = t.MoveCostPX;
        v[idx++] = t.MoveCostNX;
        v[idx++] = t.MoveCostPZ;
        v[idx++] = t.MoveCostNZ;
        v[idx++] = t.Biome0;
        v[idx++] = t.Biome1;
        v[idx++] = t.Biome2;
        v[idx++] = t.Biome3;
        v[idx++] = t.Density2;
        v[idx++] = t.Density4;
        v[idx++] = t.Density8;
        v[idx++] = t.RelativeHeight;
    }

    public string HashFloatArray(float[] array)
    {
        if (array == null || array.Length == 0) return "00000000";

        var bits = MemoryMarshal.Cast<float, uint>(array.AsSpan());

        uint hash = 2166136261u;
        for (int i = 0; i < bits.Length; i++)
            hash = (hash ^ bits[i]) * 16777619u;
        return hash.ToString("X8");
    }

    public void Dispose()
    {
        _subHealth?.Dispose();
        _subEnergy?.Dispose();
        _subHealth = null;
        _subEnergy = null;
    }
}
