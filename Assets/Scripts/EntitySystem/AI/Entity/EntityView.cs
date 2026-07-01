using UnityEngine;
using TMPro;
using TheRavine.EntityControl;

using R3;

public class EntityView : AEntityView
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private TextMeshPro label;

    protected override void SetupBindings()
    {
        var vm = (EntityViewModel)ViewModel;
        var model = (EntityModel)vm.Entity;

        model.OnUpdate.Subscribe(_ =>
        {
            label.text = $"{model.Brain.CurrentGoal}\n{(int)model.Stats.Health.Value} HP / {(int)model.Stats.Energy.Value} EN";
        });
    }
}