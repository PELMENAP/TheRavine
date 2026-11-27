public struct EnergyFlows
{
    public float FoodSpawn;
    public float FoodMaturation;
    public float ChildStart;
    public float BirthTransfer;
    public float FuelHeat;
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
    public float YieldLoss;
    public float OverflowLoss;
    public float StoreLoss;
    public float Wilt;
    public float SoilRelease;
    public float WarmthDraw;
    public float WarmthDecay;
    public float Codex;
    public float Social;

    public float SoilReturn;
    public float WarmthEmit;
    public float MetabolismCredit;

    public float BiosphereLight;
    public float BiosphereSeed;
    public float BiosphereUpkeep;
    public float BiosphereSoil;

    public readonly float Inflow
        => FoodSpawn + FoodMaturation + ChildStart + FuelHeat + SpawnStart + StarvationReturn + ClampGain + ViralNet + RescaleNet + BiosphereLight + BiosphereSeed;

    public readonly float Outflow
        => Basal + Movement + DigestionLoss + Speech + Threaten + Dash + Strike + Reproduce + RestHeal
         + CorpseSpoil + DeathLoss + YieldLoss + OverflowLoss + StoreLoss + Wilt + SoilRelease + WarmthDraw + WarmthDecay + Codex + Social + BiosphereUpkeep;

    public void Add(in EnergyFlows o)
    {
        FoodSpawn        += o.FoodSpawn;
        FoodMaturation   += o.FoodMaturation;
        ChildStart       += o.ChildStart;
        BirthTransfer    += o.BirthTransfer;
        FuelHeat         += o.FuelHeat;
        SpawnStart       += o.SpawnStart;
        StarvationReturn += o.StarvationReturn;
        ClampGain        += o.ClampGain;
        ViralNet         += o.ViralNet;
        RescaleNet       += o.RescaleNet;

        Basal         += o.Basal;
        Movement      += o.Movement;
        DigestionLoss += o.DigestionLoss;
        Speech        += o.Speech;
        Threaten      += o.Threaten;
        Dash          += o.Dash;
        Strike        += o.Strike;
        Reproduce     += o.Reproduce;
        RestHeal      += o.RestHeal;
        CorpseSpoil   += o.CorpseSpoil;
        DeathLoss     += o.DeathLoss;
        YieldLoss     += o.YieldLoss;
        OverflowLoss  += o.OverflowLoss;
        StoreLoss     += o.StoreLoss;
        Wilt          += o.Wilt;
        SoilRelease   += o.SoilRelease;
        WarmthDraw    += o.WarmthDraw;
        WarmthDecay   += o.WarmthDecay;
        Codex         += o.Codex;
        Social        += o.Social;

        SoilReturn       += o.SoilReturn;
        WarmthEmit       += o.WarmthEmit;
        MetabolismCredit += o.MetabolismCredit;

        BiosphereLight  += o.BiosphereLight;
        BiosphereSeed   += o.BiosphereSeed;
        BiosphereUpkeep += o.BiosphereUpkeep;
        BiosphereSoil   += o.BiosphereSoil;
    }
}

public sealed class EnergyLedger
{
    public EnergyFlows Flows;
    public EnergyFlows Last;
    public EnergyFlows Window;

    public float StockEntities;
    public float StockNest;
    public float StockFood;
    public float StockCorpses;
    public float StockSoil;
    public float StockWarmth;
    public float StockBiosphere;
    public float Stock;
    public float StockPrev;
    public float Residual;
    public float ResidualSum;
    public float SoilResidual;
    public float SoilResidualSum;
    public float WarmthResidual;
    public float WarmthResidualSum;
    public int   Samples;

    private bool _primed;

    public void ResetWindow() => Window = default;

    public void Sample(float entities, float nest, float food, float corpses, float soil, float warmth, float biosphere = 0f)
    {
        float soilPrev   = StockSoil;
        float warmthPrev = StockWarmth;
        StockEntities = entities;
        StockNest     = nest;
        StockFood     = food;
        StockCorpses  = corpses;
        StockSoil     = soil;
        StockWarmth   = warmth;
        StockBiosphere = biosphere;

        StockPrev = Stock;
        Stock     = entities + nest + food + corpses + soil + warmth + biosphere;
        Last      = Flows;
        Window.Add(in Last);
        Flows     = default;

        if (!_primed)
        {
            _primed        = true;
            Residual       = 0f;
            SoilResidual   = 0f;
            WarmthResidual = 0f;
            return;
        }

        Residual     = Stock - StockPrev - (Last.Inflow - Last.Outflow);
        ResidualSum += Residual;
        SoilResidual     = soil - soilPrev - (Last.SoilReturn - Last.SoilRelease - Last.BiosphereSoil);
        SoilResidualSum += SoilResidual;
        WarmthResidual     = warmth - warmthPrev - (Last.WarmthEmit - Last.WarmthDraw - Last.WarmthDecay);
        WarmthResidualSum += WarmthResidual;
        Samples++;
    }
}
