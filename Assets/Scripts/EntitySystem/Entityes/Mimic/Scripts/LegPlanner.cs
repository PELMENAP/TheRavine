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
        float randomAngle = Random.Range(-45f, 45f);
        float randomDist = Random.Range(mimic.minLegDistance, mimic.maxLegDistance);
        
        Vector3 direction = Quaternion.Euler(0, randomAngle, 0) * forward;
        Vector3 newFootPos = mimic.legPlacerOrigin + direction * randomDist;
        newFootPos.y = mimic.MapGenerator.SampleHeightBilinear(newFootPos.x, newFootPos.z);

        bool tooClose = false;
        foreach (var otherLeg in mimic.Pool.GetAll())
        {
            if (otherLeg.state == LegState.Disabled) continue;
            
            float dx = newFootPos.x - otherLeg.footPosition.x;
            float dz = newFootPos.z - otherLeg.footPosition.z;
            if (dx * dx + dz * dz < mimic.minLegDistance * mimic.minLegDistance)
            {
                tooClose = true;
                break;
            }
        }

        if (!tooClose)
        {
            float lifeTime = Random.Range(mimic.minLegLifetime, mimic.maxLegLifetime);
            for (int i = 0; i < mimic.partsPerLeg; i++)
            {
                if (mimic.legCount >= mimic.maxLegs) return;
                
                LegData leg = mimic.Pool.Get();
                if (leg == null) return;

                float growCoef = Random.Range(mimic.minGrowCoef, mimic.maxGrowCoef);
                leg.Activate(mimic, newFootPos, growCoef, lifeTime);
                mimic.legCount++;
            }
        }
    }
}