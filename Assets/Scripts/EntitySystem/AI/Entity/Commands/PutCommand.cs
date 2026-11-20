using Unity.Mathematics;

public class PutCommand : EntityCommand
{
    public PutCommand(EntityModel model) : base(model) { }

    public override bool CanExecute() => (model.Carrying > 0f || model.HasHeldObject) && model.IsAtNest;

    protected override EntityCommandStatus OnBegin()
    {
        var nest = model.Nest;
        if (nest == null || !CanExecute()) return Fail();
        ref readonly var r = ref SimulationRules.Frame;
        if (model.HasHeldObject)
        {
            float heat = model.DepositObject();
            return heat > 0f ? Complete(r.StoreFoodReward * heat / math.max(r.NestStorageNorm, 1e-3f)) : Fail();
        }
        double deliveryTime = model.CarryDuration;
        float carried = model.TakeCarried();
        float stored  = carried * model.CasteMods.StoreEfficiency;
        model.Ledger.Flows.StoreLoss += carried - stored;
        nest.Storage += stored;
        model.RecordStored(stored);
        var stats = model.Colony?.Stats;
        stats?.RecordStorageStored(stored);
        stats?.RecordInteraction(ColonyStats.Interaction.Delivered);
        stats?.RecordInteraction(ColonyStats.Interaction.DeliveredEnergy, stored);
        stats?.RecordInteraction(ColonyStats.Interaction.DeliverySeconds, deliveryTime);
        model.DepositTrail();
        model.ShareMemoryWithColony();
        if (model.TryGetFoodSite(out float2 site)) model.RecruitNearby(site, r.RecruitCount, nest.Radius);
        return Complete(r.StoreFoodReward * stored / math.max(r.NestStorageNorm, 1e-3f));
    }
}
