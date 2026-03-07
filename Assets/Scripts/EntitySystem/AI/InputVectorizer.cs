using System;
using R3;
using UnityEngine;

/// <summary>
///   [42..63] — резерв (zeros), готов для расширения
/// </summary>
public class InputVectorizer
{
    public const int VectorSize    = 64;
    public const int ActionCount   = 13;
    private const int HistoryLen   = 16;
    private const float HistoryDecay = 0.75f;
    private float _maxHealth;
    private float _maxEnergy;
    private const float MaxDetectionRadius = 20f;

    private float _prevHealth;
    private float _prevEnergy;
    private bool  _initialized;

    private readonly int[]   _actionHistory    = new int[HistoryLen];
    private readonly float[] _actionFrequency  = new float[ActionCount];
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
        string speech,
        float  nearestEnemyDist = -1f,
        float  nearestFoodDist  = -1f)
    {
        int idx = 0;
        float hp  = Mathf.Clamp01(health / _maxHealth);
        float en  = Mathf.Clamp01(energy / _maxEnergy);

        _vector[idx++] = hp;
        _vector[idx++] = 1f - hp;
        _vector[idx++] = en;
        _vector[idx++] = 1f - en;
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

        EncodeSpeech(speech, idx); idx += 4;

        _vector[idx++] = nearestEnemyDist >= 0f     
            ? 1f - Mathf.Clamp01(nearestEnemyDist / MaxDetectionRadius)
            : 0f;
        _vector[idx++] = nearestFoodDist >= 0f  
            ? 1f - Mathf.Clamp01(nearestFoodDist / MaxDetectionRadius)
            : 0f;

        while (idx < VectorSize)
            _vector[idx++] = 0f;

        return _vector;
    }

    private void UpdateActionHistory(int action)
    {
        _actionHistory[_historyPtr] = action;
        _historyPtr = (_historyPtr + 1) % HistoryLen;

        Array.Clear(_actionFrequency, 0, ActionCount);

        float weight = 1f;
        float total  = 0f;

        for (int t = 0; t < HistoryLen; t++)
        {
            int a = _actionHistory[(_historyPtr - 1 - t + HistoryLen) % HistoryLen];
            _actionFrequency[a] += weight;
            total  += weight;
            weight *= HistoryDecay;
        }

        if (total > 0f)
            for (int i = 0; i < ActionCount; i++)
                _actionFrequency[i] /= total;
    }

    private void EncodeSpeech(string speech, int startIdx)
    {
        if (string.IsNullOrEmpty(speech))
        {
            for (int i = 0; i < 4; i++) _vector[startIdx + i] = 0f;
            return;
        }

        ulong h1 = 5381UL, h2 = 2166136261UL;
        foreach (char c in speech)
        {
            h1 = ((h1 << 5) + h1) ^ c;
            h2 = (h2 ^ c) * 16777619UL;
        }
        _vector[startIdx + 0] = ((h1)        & 0xFFFF) / 65535f;
        _vector[startIdx + 1] = ((h1 >> 16)  & 0xFFFF) / 65535f;
        _vector[startIdx + 2] = ((h2)        & 0xFFFF) / 65535f;
        _vector[startIdx + 3] = ((h2 >> 16)  & 0xFFFF) / 65535f;
    }
    public string HashFloatArray(float[] array)
    {
        if (array == null || array.Length == 0) return "00000000";

        uint hash = 2166136261u;
        foreach (float value in array)
        {
            uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            hash = (hash ^ bits) * 16777619u;
        }
        return hash.ToString("X8");
    }

    public void Dispose()
    {
        _subHealth?.Dispose();
        _subEnergy?.Dispose();
    }
}