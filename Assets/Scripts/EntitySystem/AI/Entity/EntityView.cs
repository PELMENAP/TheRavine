using UnityEngine;
using TMPro;
using TheRavine.EntityControl;
using TheRavine.EntityControl.Virology;

using R3;

public class EntityView : AEntityView
{
    [SerializeField] private TextMeshPro label;
    [SerializeField] private VirologyProbe virologyProbe;
    public GameObject LabelObject => label.gameObject;

    private static readonly Color Neutral = Color.white;
    private static readonly Color Parasite = new(1f, 0.45f, 0.4f);
    private static readonly Color Symbiont = new(0.45f, 1f, 0.5f);
    private static readonly Color Tamed = new(0.65f, 0.65f, 0.65f);

    protected override void SetupBindings()
    {
        var vm = (EntityViewModel)ViewModel;
        var model = (EntityModel)vm.Entity;

        if (virologyProbe != null) virologyProbe.Bind(model);

        model.OnUpdate.Subscribe(_ =>
        {
            string text = $"{model.Brain.CurrentGoal} - {model.LastAction}\n"
                    + $"{(int)model.Stats.Health.Value} HP / {(int)model.Stats.Energy.Value} EN\n"
                    + model.Speech.OwnSpeech;

            if (virologyProbe != null && virologyProbe.HasSegments)
            {
                var list = virologyProbe.Infections;
                text += $"\nV:{list.Count} {virologyProbe.NetFitnessDelta:+0.0;-0.0;0}";
                label.color = Resolve(list, virologyProbe.NetFitnessDelta);
            }
            else label.color = Neutral;

            label.text = text;
        });
    }

    private static Color Resolve(System.Collections.Generic.IReadOnlyList<InfectionView> list, float net)
    {
        bool allTamed = true;
        for (int i = 0; i < list.Count; i++)
            if (list[i].StrainLabel != "ENDOGEN" && !list[i].Tamed) { allTamed = false; break; }

        if (allTamed) return Tamed;
        return net >= 0f ? Symbiont : Neutral is var _ && net < 0f ? Parasite : Neutral;
    }
}