using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Entity2D))]
public class EntityDebugGizmo : MonoBehaviour
{
    [SerializeField] private bool showAlways = false;

    private Entity2D _entity;
    private static readonly string[] GoalLabels = { "SRV", "HNT", "FRG", "SOC" };
    private static readonly Color[]  GoalColors =
    {
        new Color(0.2f, 1f, 0.3f),
        new Color(1f, 0.2f, 0.2f),
        new Color(1f, 0.9f, 0.1f),
        new Color(0.3f, 0.5f, 1f),
    };

    private readonly RingBuffer<float> _entropyHistory = new(32);
    private float _sampleTimer;

    private void Awake() => _entity = GetComponent<Entity2D>();

    private void Update()
    {
        _sampleTimer += Time.deltaTime;
        if (_sampleTimer > 0.5f)
        {
            _sampleTimer = 0f;
            _entropyHistory.Push(_entity.BrainContext.CoordMLP.AverageEntropy);
        }
    }

    private void OnDrawGizmosSelected() => DrawGizmos();
    private void OnDrawGizmos() { if (showAlways) DrawGizmos(); }

    private void DrawGizmos()
    {
        if (_entity?.BrainContext == null) return;

        var ctx    = _entity.BrainContext;
        var pos    = transform.position;
        int goal   = (int)ctx.CurrentGoal;
        Color col  = GoalColors[goal];

        Gizmos.color = col;
        Gizmos.DrawWireSphere(pos, 0.4f);

#if UNITY_EDITOR
        DrawEntropyBar(pos, ctx.CoordMLP.AverageEntropy, col);
        DrawGoalLabel(pos, goal, ctx);
        DrawTrainingMeter(pos, ctx);
#endif
    }

#if UNITY_EDITOR
    private void DrawEntropyBar(Vector3 pos, float entropy, Color col)
    {
        float barWidth  = 1.2f;
        float barHeight = 0.1f;
        float fillWidth = barWidth * Mathf.Clamp01(entropy / 2f);
        Vector3 barBase = pos + Vector3.up * 0.7f - Vector3.right * barWidth * 0.5f;

        UnityEditor.Handles.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        UnityEditor.Handles.DrawSolidRectangleWithOutline(
            new Rect(barBase.x, barBase.y, barWidth, barHeight),
            new Color(0.1f, 0.1f, 0.1f, 0.7f), Color.clear);

        UnityEditor.Handles.color = col;
        UnityEditor.Handles.DrawSolidRectangleWithOutline(
            new Rect(barBase.x, barBase.y, fillWidth, barHeight),
            col, Color.clear);

        var style = new GUIStyle { fontSize = 8, normal = { textColor = Color.white } };
        UnityEditor.Handles.Label(barBase + Vector3.right * barWidth + Vector3.right * 0.05f,
            $"H:{entropy:F2}", style);
    }

    private void DrawGoalLabel(Vector3 pos, int goal, EntityBrainContext ctx)
    {
        var style = new GUIStyle
        {
            fontSize  = 9,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = GoalColors[goal] }
        };

        string label = $"{GoalLabels[goal]} T:{ctx.CoordMLP.TrainingSteps}\n"
                     + $"Steps left: {ctx.GoalStepsLeft}";

        UnityEditor.Handles.Label(pos + Vector3.up * 0.9f, label, style);
    }

    private void DrawTrainingMeter(Vector3 pos, EntityBrainContext ctx)
    {
        float steps  = ctx.CoordMLP.TrainingSteps;
        float warmup = 1000f;
        float norm   = Mathf.Clamp01(steps / warmup);

        var style = new GUIStyle { fontSize = 8, normal = { textColor = Color.gray } };
        UnityEditor.Handles.Label(pos + Vector3.down * 0.5f,
            $"[{new string('|', Mathf.RoundToInt(norm * 10))}{new string('.', 10 - Mathf.RoundToInt(norm * 10))}]",
            style);
    }
#endif
}