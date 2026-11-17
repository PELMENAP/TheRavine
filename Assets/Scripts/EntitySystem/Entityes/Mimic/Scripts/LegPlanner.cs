using UnityEngine;

public class LegPlanner
{
    private float spawnTimer;

    public void Update(Mimic mimic, float dt)
    {
        spawnTimer -= dt;
        if (spawnTimer <= 0 && mimic.legCount < mimic.maxLegs)
        {
            spawnTimer = mimic.newLegCooldown;
            TrySpawnLegs(mimic);
        }
    }

    private void TrySpawnLegs(Mimic mimic)
    {
        Vector3 forward = mimic.Velocity.sqrMagnitude > 0.1f ? mimic.Velocity.normalized : mimic.transform.forward;
        float randomAngle = RavineRandom.RangeFloat(-45f, 45f);
        float randomDist = RavineRandom.RangeFloat(mimic.minLegDistance, mimic.maxLegDistance);
        
        Vector3 direction = Quaternion.Euler(0, randomAngle, 0) * forward;
        Vector3 newFootPos = mimic.legPlacerOrigin + direction * randomDist;
        newFootPos.y = mimic.MapGenerator.SampleHeightBilinear(newFootPos.x, newFootPos.z);

        var store = mimic.Legs;
        float minDistance2 = mimic.minLegDistance * mimic.minLegDistance;
        bool tooClose = false;
        for (int i = 0; i < store.Capacity; i++)
        {
            var otherLeg = store[i];
            if (otherLeg.State == LegState.Disabled) continue;
            
            float dx = newFootPos.x - otherLeg.Foot.x;
            float dz = newFootPos.z - otherLeg.Foot.z;
            if (dx * dx + dz * dz < minDistance2)
            {
                tooClose = true;
                break;
            }
        }

        if (!tooClose)
        {
            float lifeTime = RavineRandom.RangeFloat(mimic.minLegLifetime, mimic.maxLegLifetime);
            for (int i = 0; i < mimic.partsPerLeg; i++)
            {
                if (mimic.legCount >= mimic.maxLegs) return;
                
                int slot = store.FindFree();
                if (slot < 0) return;

                float growCoef = RavineRandom.RangeFloat(mimic.minGrowCoef, mimic.maxGrowCoef);
                store.Activate(slot, mimic, newFootPos, growCoef, lifeTime);
                mimic.legCount++;
            }
        }
    }
}
