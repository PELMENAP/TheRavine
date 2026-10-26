using System;
using UnityEditor;
using UnityEngine;
using TheRavine.Inventory;

[CustomEditor(typeof(CraftService))]
public sealed class CraftServiceEditor : Editor
{
    private const int TopCount = 5;

    private CraftMatch[] _sorted = Array.Empty<CraftMatch>();

    private static int Compare(CraftMatch a, CraftMatch b)
    {
        int byScore = (b.Matched - b.Foreign).CompareTo(a.Matched - a.Foreign);
        return byScore != 0 ? byScore : a.Missing.CompareTo(b.Missing);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var service = (CraftService)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Craft grid", EditorStyles.boldLabel);

        var grid = service.Grid;
        for (int i = 0; i < grid.Count; i++)
        {
            var info = grid[i].Info;
            string title = info == null ? "-" : info.title;
            int amount = info == null ? 0 : grid[i].Amount;
            EditorGUILayout.LabelField($"Cell {i}", $"{title} x{amount}");
        }

        if (GUILayout.Button("Пересчитать"))
        {
            var matches = service.Evaluate();
            _sorted = (CraftMatch[])matches.Clone();
            Array.Sort(_sorted, Compare);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Closest recipes", EditorStyles.boldLabel);

        int shown = Math.Min(TopCount, _sorted.Length);
        if (shown == 0)
        {
            EditorGUILayout.LabelField("No data");
            return;
        }

        for (int i = 0; i < shown; i++)
        {
            var m = _sorted[i];
            string result = m.Recipe == null || m.Recipe.Result == null ? "-" : m.Recipe.Result.title;
            EditorGUILayout.LabelField(result, $"matched {m.Matched}  missing {m.Missing}  foreign {m.Foreign}");
        }
    }
}
