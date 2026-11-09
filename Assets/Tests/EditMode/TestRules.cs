using UnityEngine;

public static class TestRules
{
    public static SimulationRules Bind()
    {
        var rules = ScriptableObject.CreateInstance<SimulationRules>();
        SimulationRules.Bind(rules);
        return rules;
    }

    public static void Release(SimulationRules rules)
    {
        SimulationRules.Bind(null);
        if (rules != null) Object.DestroyImmediate(rules);
    }
}
