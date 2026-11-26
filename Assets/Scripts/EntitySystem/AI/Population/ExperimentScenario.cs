using System;
using UnityEngine;
using Unity.Mathematics;

[Serializable]
public sealed class ScenarioColony
{
    public Vector2 nestOffset;
    [Min(0f)] public float foodMultiplier = 1f;
    [Min(0f)] public float foodRadius = 120f;
    [Min(0f)] public float foodStartSeconds;
    [Min(0f)] public float foodActiveSeconds;
    [Min(0f)] public float foodInactiveSeconds;
}

public readonly struct FoodZone
{
    public readonly float2 Center;
    public readonly float RadiusSq;
    public readonly float Multiplier;
    public readonly double Start;
    public readonly float Active;
    public readonly float Inactive;

    public FoodZone(float2 center, float radius, float multiplier, double start, float active, float inactive)
    {
        Center = center;
        RadiusSq = radius * radius;
        Multiplier = multiplier;
        Start = start;
        Active = active;
        Inactive = inactive;
    }

    public bool IsOn(double now)
    {
        double phase = now - Start;
        if (phase < 0d) return false;
        if (Active <= 0f || Inactive <= 0f) return true;
        return phase % (Active + Inactive) < Active;
    }

    public static float MultiplierAt(FoodZone[] zones, int count, float2 position, double now)
    {
        float result = 1f;
        for (int i = 0; i < count; i++)
        {
            ref readonly var z = ref zones[i];
            if (math.distancesq(z.Center, position) <= z.RadiusSq && z.IsOn(now)) result *= z.Multiplier;
        }
        return result;
    }
}

[CreateAssetMenu(fileName = "ExperimentScenario", menuName = "Simulation/Experiment Scenario")]
public sealed class ExperimentScenario : ScriptableObject
{
    public const int MaxColonies = 16;

    [SerializeField, Min(0)] private int foundersPerColony = 10;
    [SerializeField] private ScenarioColony[] colonies =
    {
        new ScenarioColony { nestOffset = new Vector2(-150f, 0f) },
        new ScenarioColony { nestOffset = new Vector2(150f, 0f) },
    };

    public int FoundersPerColony => foundersPerColony;
    public int ColonyCount => colonies != null ? Math.Min(colonies.Length, MaxColonies) : 0;
    public ScenarioColony ColonyAt(int index) => colonies[index];

    public float2 NestPosition(int index, Vector3 origin)
    {
        var offset = colonies[index].nestOffset;
        return new float2(origin.x + offset.x, origin.z + offset.y);
    }

    public int BuildZones(Vector3 origin, FoodZone[] destination)
    {
        int count = 0;
        for (int i = 0; i < ColonyCount && count < destination.Length; i++)
        {
            var c = colonies[i];
            if (c == null) continue;
            float multiplier = float.IsFinite(c.foodMultiplier) ? math.max(c.foodMultiplier, 0f) : 1f;
            if (multiplier == 1f) continue;
            float radius = float.IsFinite(c.foodRadius) ? math.max(c.foodRadius, 0f) : 0f;
            destination[count++] = new FoodZone(NestPosition(i, origin), radius, multiplier,
                math.max(c.foodStartSeconds, 0f), math.max(c.foodActiveSeconds, 0f), math.max(c.foodInactiveSeconds, 0f));
        }
        return count;
    }
}
