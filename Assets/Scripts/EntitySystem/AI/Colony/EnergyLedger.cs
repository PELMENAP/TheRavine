public struct EnergyFlows
{
    public float FoodSpawn;
    public float FoodMaturation;
    public float ChildStart;
    public float SpawnStart;
    public float StarvationReturn;
    public float ClampGain;
    public float ViralNet;
    public float RescaleNet;

    public float Basal;
    public float Movement;
    public float DigestionLoss;
    public float Speech;
    public float Threaten;
    public float Dash;
    public float Strike;
    public float Reproduce;
    public float RestHeal;
    public float CorpseSpoil;
    public float DeathLoss;
    public float SoilRelease;

    public float SoilReturn;
    public float MetabolismCredit;

    public readonly float Inflow
        => FoodSpawn + FoodMaturation + ChildStart + SpawnStart + StarvationReturn + ClampGain + ViralNet + RescaleNet;

    public readonly float Outflow
        => Basal + Movement + DigestionLoss + Speech + Threaten + Dash + Strike + Reproduce + RestHeal + CorpseSpoil + DeathLoss + SoilRelease;
}

public sealed class EnergyLedger
{
    public EnergyFlows Flows;
    public EnergyFlows Last;

    public float StockEntities;
    public float StockNest;
    public float StockFood;
    public float StockCorpses;
    public float StockSoil;
    public float Stock;
    public float StockPrev;
    public float Residual;
    public float ResidualSum;
    public float SoilResidual;
    public float SoilResidualSum;
    public int   Samples;

    private bool _primed;

    public void Sample(float entities, float nest, float food, float corpses, float soil)
    {
        float soilPrev = StockSoil;
        StockEntities = entities;
        StockNest     = nest;
        StockFood     = food;
        StockCorpses  = corpses;
        StockSoil     = soil;

        StockPrev = Stock;
        Stock     = entities + nest + food + corpses + soil;
        Last      = Flows;
        Flows     = default;

        if (!_primed)
        {
            _primed      = true;
            Residual     = 0f;
            SoilResidual = 0f;
            return;
        }

        Residual     = Stock - StockPrev - (Last.Inflow - Last.Outflow);
        ResidualSum += Residual;
        SoilResidual     = soil - soilPrev - (Last.SoilReturn - Last.SoilRelease);
        SoilResidualSum += SoilResidual;
        Samples++;
    }
}
