using System.Text;
using UnityEngine;
using TMPro;
using TheRavine.EntityControl;
using TheRavine.EntityControl.Virology;

using R3;

public class EntityView : AEntityView
{
    [SerializeField] private TextMeshPro label;
    [SerializeField] private float stressVisibleThreshold = 0.05f;
    public GameObject LabelObject => label.gameObject;

    private static readonly string[] ActionNames = System.Enum.GetNames(typeof(EntityAction));
    private static readonly string[] GoalNames   = System.Enum.GetNames(typeof(SharedHierarchicalBrain.Goal));

    private const string ParasiteHex = "#FF7366";
    private const string SymbiontHex = "#73FF80";
    private const string TamedHex    = "#A6A6A6";
    private const string DimHex      = "#8C8C8C";
    private const string LeaderHex   = "#FFD24D";
    private const string JuvenileHex = "#9FE8FF";
    private const string HungryHex   = "#FFA040";
    private const string PanicHex    = "#FF4040";
    private const string SatedHex    = "#7CD67C";
    private const string StressHex   = "#D080FF";
    private const string CarryHex    = "#E0C080";

    private static readonly string[] ColonyHex =
        { "#4DA6FF", "#FF6B6B", "#6BFF8A", "#FFD24D", "#C77DFF", "#4DFFF0", "#FF9E4D", "#FF6BD5" };

    private static readonly string[] CasteHex  = { "#D9D9D9", "#FF5A5A", "#5AD1FF", "#FFB3E6" };
    private static readonly string[] CasteName = { "Worker", "Soldier", "Scout", "Nurse" };

    private static readonly string[] PlanHex =
        { "#8FE36B", "#E3C46B", "#6BB5E3", "#E36B6B", "#9A9AE3", "#FF8C42", "#E36BC8", "#6BE3D2", "#8C8C8C" };

    private static readonly string[] SourceHex = { "#FFFFFF", "#FFE04D", "#66E0FF" };
    private static readonly string[] SourceTag = { "NN", "IN", "PL" };

    private static readonly string[] Gradient = BuildGradient();

    private readonly StringBuilder _sb = new(256);

    private static string[] BuildGradient()
    {
        var g = new string[11];
        for (int i = 0; i < g.Length; i++)
            g[i] = "#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.35f, 1f, 0.4f), i / 10f));
        return g;
    }

    private static string Grad(float fraction) => Gradient[Mathf.Clamp(Mathf.RoundToInt(fraction * 10f), 0, 10)];

    protected override void SetupBindings()
    {
        var vm = (EntityViewModel)ViewModel;
        var model = (EntityModel)vm.Entity;
        label.richText = true;
        label.color    = Color.white;

        model.OnUpdate.Subscribe(_ => Render(model));
    }

    private void Render(EntityModel model)
    {
        var sb = _sb;
        sb.Clear();

        var colony = model.Colony;
        if (colony != null)
            Open(sb, ColonyHex[(colony.ColonyId - 1 & 0x7FFFFFFF) % ColonyHex.Length]).Append('C').Append(colony.ColonyId).Close();

        if (model.IsJuvenile) Open(sb.Append(' '), JuvenileHex).Append("juv").Close();
        else
        {
            int caste = (int)model.Caste;
            Open(sb.Append(' '), CasteHex[caste]).Append(CasteName[caste]).Close();
        }
        if (model.IsLeader) sb.Append(' ').Append("<b>").Append("<color=").Append(LeaderHex).Append(">[LEAD]</color></b>");
        sb.Append('\n');

        var plan = model.Plan != null ? model.Plan.Kind : PlanKind.Count;
        if (plan < PlanKind.Count) Open(sb, PlanHex[(int)plan]).Append(PlanCatalog.Names[(int)plan]).Close();
        else Open(sb, DimHex).Append(GoalNames[(int)model.Brain.CurrentGoal]).Close();

        sb.Append(" > ");
        if (model.IsCommandRunning)
        {
            int src = (int)model.CurrentSource;
            int action = (int)model.LastAction;
            Open(sb, SourceHex[src]).Append((uint)action < (uint)ActionNames.Length ? ActionNames[action] : "?").Close();
            Open(sb.Append(' '), DimHex).Append('[').Append(SourceTag[src]).Append(']').Close();
        }
        else Open(sb, DimHex).Append("idle").Close();
        sb.Append('\n');

        var stats = model.Stats;
        float hp = stats.Health.Value, en = stats.Energy.Value;
        Open(sb, Grad(hp / stats.MaxHealth)).Append((int)hp).Append(" HP").Close();
        sb.Append(" / ");
        Open(sb, Grad(en / stats.MaxEnergy)).Append((int)en).Append(" EN").Close();
        if (model.Carrying > 0f)
            Open(sb.Append(' '), CarryHex).Append("+").Append((int)model.Carrying).Append(" carry").Close();
        sb.Append('\n');

        int flags = 0;
        if (model.IsHungry)                                        Flag(sb, ref flags, HungryHex, "HUNGRY");
        if ((model.InstinctLatches & InstinctBits.Panic) != 0)     Flag(sb, ref flags, PanicHex, "PANIC");
        if (model.IsSated)                                         Flag(sb, ref flags, SatedHex, "SATED");
        if (model.Stress > stressVisibleThreshold)
        {
            if (flags++ > 0) sb.Append(' ');
            Open(sb, StressHex).Append("STRESS ").Append((int)(model.Stress * 100f)).Append('%').Close();
        }
        if (flags > 0) sb.Append('\n');

        Open(sb, DimHex).Append(SpeechComponent.Encode(model.Speech.Own)).Close();

        var virology = model.Virology;
        if (virology != null && virology.TryGetViralSummary(out float net, out int viralCount, out bool allTamed))
        {
            string hex = allTamed ? TamedHex : net > 0f ? SymbiontHex : net < 0f ? ParasiteHex : DimHex;
            Open(sb.Append('\n'), hex).Append("V:").Append(viralCount).Append(' ')
                .Append(net >= 0f ? '+' : '-').Append((int)Mathf.Abs(net * 10f) / 10).Append('.')
                .Append((int)Mathf.Abs(net * 10f) % 10).Close();
        }

        label.SetText(sb);
    }

    private static void Flag(StringBuilder sb, ref int flags, string hex, string text)
    {
        if (flags++ > 0) sb.Append(' ');
        Open(sb, hex).Append(text).Close();
    }

    private static StringBuilder Open(StringBuilder sb, string hex) => sb.Append("<color=").Append(hex).Append('>');
}

internal static class RichTextExtensions
{
    public static StringBuilder Close(this StringBuilder sb) => sb.Append("</color>");
}
