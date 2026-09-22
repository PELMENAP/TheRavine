using System;
using System.Runtime.InteropServices;
using R3;
using UnityEngine;

public class InputVectorizer : IDisposable
{
    public const int VectorSize    = 64;
    public const int ActionCount   = 13;
    private const float HistoryDecay = 0.75f;
    private const float HistoryAlpha = 1f - HistoryDecay;

    private readonly float[] _actionFrequency = new float[ActionCount];
    private float _maxHealth;
    private float _maxEnergy;
    private const float MaxDetectionRadius = 20f;

    private float _prevHealth;
    private float _prevEnergy;
    private bool  _initialized;
    private int _historyPtr;

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
    public float[] Vectorize(
        float  health,
        float  energy,
        int    lastAction,
        int    timeOfDay,
        float  inDanger,
        float  timeToBreed,
        in SpeechHash speech,
        float  nearestEnemyDist = -1f,
        float  nearestFoodDist  = -1f,
        in TerrainSample terrain = default,
        int    mimickedAction = -1,
        float  viralLoad = 0f,
        float  viralSegments = 0f,
        float  viralNet = 0f)
    {
        int idx = 0;
        float hp  = Mathf.Clamp01(health / _maxHealth);
        float en  = Mathf.Clamp01(energy / _maxEnergy);

        _vector[idx++] = hp;
        _vector[idx++] = viralLoad;
        _vector[idx++] = en;
        _vector[idx++] = viralSegments;
        _vector[idx++] = Mathf.Clamp(
            _initialized ? (health - _prevHealth) / _maxHealth : 0f, -1f, 1f);
        _vector[idx++] = Mathf.Clamp(
            _initialized ? (energy - _prevEnergy) / _maxEnergy : 0f, -1f, 1f);

        _prevHealth  = health;
        _prevEnergy  = energy;
        _initialized = true;

        float angle  = timeOfDay / 24f * 2f * Mathf.PI;
        _vector[idx++] = Mathf.Sin(angle);
        _vector[idx++] = Mathf.Cos(angle);

        for (int i = 0; i < ActionCount; i++)
            _vector[idx++] = (lastAction == i) ? 1f : 0f;

        UpdateActionHistory(lastAction);
        for (int i = 0; i < ActionCount; i++)
            _vector[idx++] = _actionFrequency[i];

        _vector[idx++] = Mathf.Clamp01(inDanger);
        _vector[idx++] = Mathf.Clamp01(timeToBreed);

        _vector[idx++] = speech.A;
        _vector[idx++] = speech.B;
        _vector[idx++] = speech.C;
        _vector[idx++] = speech.D;

        _vector[idx++] = nearestEnemyDist >= 0f
            ? 1f - Mathf.Clamp01(nearestEnemyDist / MaxDetectionRadius)
            : 0f;
        _vector[idx++] = nearestFoodDist >= 0f
            ? 1f - Mathf.Clamp01(nearestFoodDist / MaxDetectionRadius)
            : 0f;

        bool terrainValid = terrain.IsValid;
        if (terrainValid) WriteTerrain(in terrain, ref idx);
        else WriteTerrain(in TerrainSample.Invalid, ref idx);

        _vector[idx++] = terrainValid ? 1f : 0f;

        bool hasMimic = (uint)mimickedAction < (uint)ActionCount;
        _vector[idx++] = hasMimic ? 1f : 0f;
        _vector[idx++] = hasMimic ? (mimickedAction + 0.5f) / ActionCount : 0f;

        _vector[idx++] = viralNet;

        return _vector;
    }

    private void WriteTerrain(in TerrainSample t, ref int idx)
    {
        _vector[idx++] = t.HeightNorm;
        _vector[idx++] = t.Slope;
        _vector[idx++] = t.GradX;
        _vector[idx++] = t.GradZ;
        _vector[idx++] = t.WaterProximity;
        _vector[idx++] = t.MoveCost;
        _vector[idx++] = t.MoveCostPX;
        _vector[idx++] = t.MoveCostNX;
        _vector[idx++] = t.MoveCostPZ;
        _vector[idx++] = t.MoveCostNZ;
        _vector[idx++] = t.Biome0;
        _vector[idx++] = t.Biome1;
        _vector[idx++] = t.Biome2;
        _vector[idx++] = t.Biome3;
        _vector[idx++] = t.Density2;
        _vector[idx++] = t.Density4;
        _vector[idx++] = t.Density8;
        _vector[idx++] = t.RelativeHeight;
    }

    private void UpdateActionHistory(int action)
    {
        float keep = HistoryDecay;
        for (int i = 0; i < ActionCount; i++)
            _actionFrequency[i] *= keep;

        if ((uint)action < (uint)ActionCount)
            _actionFrequency[action] += HistoryAlpha;
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