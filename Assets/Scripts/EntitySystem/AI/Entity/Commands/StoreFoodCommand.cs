using Unity.Mathematics;

public class StoreFoodCommand : EntityCommand
{
    public StoreFoodCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => model.Carrying > 0f && model.IsAtNest;

    protected override EntityCommandStatus OnBegin()
    {
        var nest = model.Nest;
        if (nest == null || !model.IsAtNest || model.Carrying <= 0f) return Fail();
        ref readonly var r = ref SimulationRules.Frame;
        float stored = model.TakeCarried() * model.CasteMods.StoreEfficiency;
        nest.Storage += stored;
        model.RecordStored(stored);
        model.Colony?.Stats.RecordStorageStored(stored);
        model.DepositTrail();
        model.ShareMemoryWithColony();
        if (model.TryGetFoodSite(out float2 site)) model.RecruitNearby(site, r.RecruitCount, nest.Radius);
        return Complete(r.StoreFoodReward * stored / math.max(r.NestStorageNorm, 1e-3f));
    }
}
