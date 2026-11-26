public class LegAnimator
{
    public void UpdateStates(Mimic mimic, float dt)
    {
        var store = mimic.Legs;
        for (int i = 0; i < store.Capacity; i++)
        {
            var leg = store[i];
            if (leg.State == LegState.Disabled) continue;

            UpdateState(mimic, ref leg, dt);
            store[i] = leg;
        }
    }

    private void UpdateState(Mimic mimic, ref LegParams leg, float dt)
    {
        switch (leg.State)
        {
            case LegState.Growing:
                if (leg.Progression > 0.9f)
                {
                    leg.State = LegState.Supporting;
                    mimic.deployedLegs++;
                }
                break;
            case LegState.Supporting:
                leg.LifeTimer -= dt;
                float dx = mimic.legPlacerOrigin.x - leg.Foot.x;
                float dz = mimic.legPlacerOrigin.z - leg.Foot.z;
                bool tooFar = (dx * dx + dz * dz) > (mimic.maxLegDistance * mimic.maxLegDistance);
                bool timeUp = leg.LifeTimer <= 0;
                bool canRetract = mimic.deployedLegs > mimic.minimumAnchoredParts;

                if ((tooFar || timeUp) && canRetract)
                {
                    leg.State = LegState.Retracting;
                    leg.GrowTarget = 0f;
                    mimic.deployedLegs--;
                }
                break;
            case LegState.Retracting:
                if (leg.Progression < 0.05f)
                {
                    leg.State = LegState.Disabled;
                    mimic.legCount--;
                }
                break;
        }
    }
}
