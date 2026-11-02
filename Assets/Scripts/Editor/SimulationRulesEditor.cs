using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SimulationRules), true)]
public sealed class SimulationRulesEditor : Editor
{
    private const string SearchKey   = "SimulationRules.Editor.Search";
    private const string FoldoutKey  = "SimulationRules.Editor.Fold.";
    private const string OtherTitle  = "Прочее";

    private static readonly (string Title, string[] Keys)[] Groups =
    {
        ("Еда: рост, запасы, пастбище", new[] { "food", "pasture", "biome", "seed", "chunk", "matur", "fertil", "large", "corpse" }),
        ("Еда: поедание, переваривание, желудок", new[] { "eat", "digest", "stomach", "wellfed", "sated", "nutrition", "toxic", "carry", "pickup", "shareable" }),
        ("Энергия, голод, отдых", new[] { "starv", "basal", "energy", "hunger", "rest", "heal", "drain", "metabol", "upkeep", "idle" }),
        ("Ночь, сезоны, спячка", new[] { "night", "season", "winter", "dormant", "hibernat", "dusk", "dawn" }),
        ("Гнездо и колония", new[] { "nest", "colony", "quorum", "trophallaxis", "juvenile", "caste", "scout", "soldier", "nurse", "leader", "kin", "territor", "trail", "explor", "migrat", "recruit", "poi", "relocat" }),
        ("Движение и местность", new[] { "move", "speed", "terrain", "slope", "water", "path", "dash", "wander", "levy", "steer", "boid", "flock", "arrive", "leg", "stamina", "jump", "range", "radius" }),
        ("Стресс, страх, опасность", new[] { "stress", "fear", "panic", "alarm", "harm", "danger", "flee", "threat", "hazard", "fire", "burn" }),
        ("Вирусы и геном", new[] { "virus", "virol", "viral", "infect", "immun", "codon", "strain", "ribosom", "payload", "mutat", "bite", "groom", "cytoincompat", "transmi", "sick", "fever", "protein", "segment", "summit" }),
        ("Тепло", new[] { "warmth", "heat", "emit" }),
        ("Действия: атака, речь, размножение", new[] { "attack", "threaten", "speech", "share", "mimic", "reproduc", "child", "spawn", "action", "command", "duration", "infeasib", "timing", "signal" }),
        ("Награда и обучение", new[] { "reward", "learn", "gamma", "explor", "epsilon", "entropy", "critic", "ppo", "gradient", "optimiz", "clip", "curiosity", "novelty", "goal", "plan", "discount", "penalty", "eligib", "replay", "rnd", "decay", "lambda", "bias", "surprise", "drive", "homeost", "value", "weight" }),
        ("Отбор и фитнес", new[] { "fitness", "tournament", "elite", "generation", "mutation", "crossover", "gene", "select", "survival", "lifetime", "maxage", "mortal", "death" }),
        ("Камера и отображение", new[] { "camera", "cull", "lod", "visual", "render", "color", "colour", "alpha", "fade" }),
    };

    private List<string>[] _buckets;

    private void OnEnable() => _buckets = null;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureBuckets();

        string search = EditorPrefs.GetString(SearchKey, string.Empty);
        EditorGUI.BeginChangeCheck();
        search = EditorGUILayout.TextField("Поиск параметра", search);
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(SearchKey, search);

        if (!string.IsNullOrWhiteSpace(search))
        {
            DrawFiltered(search.Trim());
        }
        else
        {
            for (int g = 0; g < _buckets.Length; g++) DrawGroup(g);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGroup(int g)
    {
        var names = _buckets[g];
        if (names.Count == 0) return;

        string title = g < Groups.Length ? Groups[g].Title : OtherTitle;
        string key   = FoldoutKey + g;
        bool open = EditorPrefs.GetBool(key, false);
        bool next = EditorGUILayout.Foldout(open, $"{title} ({names.Count})", true);
        if (next != open) EditorPrefs.SetBool(key, next);
        if (!next) return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < names.Count; i++) DrawProperty(names[i]);
        EditorGUI.indentLevel--;
    }

    private void DrawFiltered(string search)
    {
        int shown = 0;
        for (int g = 0; g < _buckets.Length; g++)
        {
            var names = _buckets[g];
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                DrawProperty(names[i]);
                shown++;
            }
        }
        if (shown == 0) EditorGUILayout.HelpBox("Ничего не найдено", MessageType.Info);
    }

    private void DrawProperty(string name)
    {
        var property = serializedObject.FindProperty(name);
        if (property != null) EditorGUILayout.PropertyField(property, true);
    }

    private void EnsureBuckets()
    {
        if (_buckets != null) return;

        _buckets = new List<string>[Groups.Length + 1];
        for (int g = 0; g < _buckets.Length; g++) _buckets[g] = new List<string>();

        var it = serializedObject.GetIterator();
        if (!it.NextVisible(true)) return;
        do
        {
            if (it.name == "m_Script") continue;
            _buckets[GroupOf(it.name)].Add(it.name);
        }
        while (it.NextVisible(false));
    }

    private static int GroupOf(string name)
    {
        string lower = name.ToLowerInvariant();
        for (int g = 0; g < Groups.Length; g++)
        {
            string[] keys = Groups[g].Keys;
            for (int k = 0; k < keys.Length; k++)
                if (lower.Contains(keys[k])) return g;
        }
        return Groups.Length;
    }
}
