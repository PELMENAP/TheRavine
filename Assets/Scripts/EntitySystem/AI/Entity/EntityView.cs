using UnityEngine;
using TMPro;
using TheRavine.EntityControl;
using TheRavine.EntityControl.Virology;

using R3;

public class EntityView : AEntityView
{
    [SerializeField] private TextMeshPro label;
    public GameObject LabelObject => label.gameObject;

    private static readonly Color Neutral = Color.white;
    private static readonly Color Parasite = new(1f, 0.45f, 0.4f);
    private static readonly Color Symbiont = new(0.45f, 1f, 0.5f);
    private static readonly Color Tamed = new(0.65f, 0.65f, 0.65f);

    protected override void SetupBindings()
    {
        var vm = (EntityViewModel)ViewModel;
        var model = (EntityModel)vm.Entity;

        model.OnUpdate.Subscribe(_ =>
        {
            string text = $"{model.Brain.CurrentGoal} - {model.LastAction}\n"
                    + $"{(int)model.Stats.Health.Value} HP / {(int)model.Stats.Energy.Value} EN\n"
                    + model.Speech.OwnSpeech;

            var virology = model.Virology;
            if (virology != null && virology.TryGetViralSummary(out float net, out int viralCount, out bool allTamed))
            {
                text += $"\nV:{viralCount} {net:+0.0;-0.0;0}";
                label.color = Resolve(net, allTamed);
            }
            else label.color = Neutral;

            label.text = text;
        });
    }

    private static Color Resolve(float net, bool allTamed)
    {
        if (allTamed) return Tamed;
        if (net > 0f) return Symbiont;
        if (net < 0f) return Parasite;
        return Neutral;
    }
}