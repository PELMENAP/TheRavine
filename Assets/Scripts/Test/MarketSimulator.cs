using UnityEngine;
using System;
using System.Collections.Generic;

using TheRavine.Extensions;
using Random = TheRavine.Extensions.RavineRandom;

public class MarketSimulator
{
    private float currentGlobalTrade, globalTradeSpread = 10f; 
    private float lastTrade, targetTrade, targetTradeSpread = 30f; 
    private float currentTrade, currentTradeSpread = 10f;
    private int maxProductCount = 10;
    private Trader trader1, trader2;
    private readonly List<PeriodicEvent> events = new();
    public MarketSimulator(MarketCore marketCore)
    {
        trader1 = new Trader("Trader1", marketCore);
        trader2 = new Trader("Trader2", marketCore);

        events.Add(new PeriodicEvent(10, () => currentTrade = Random.RangeFloat(lastTrade, targetTrade))); // random can work even if target less than last
        events.Add(new PeriodicEvent(50, () => UpdateTargetTrade() ));
        events.Add(new PeriodicEvent(100, () => currentGlobalTrade = Random.RangeFloat(-globalTradeSpread, globalTradeSpread)));
    }

    private void WarmUp()
    {
        targetTrade = Random.RangeFloat(targetTradeSpread, targetTradeSpread * maxProductCount);
        UpdateTargetTrade();

        currentGlobalTrade = Random.RangeFloat(-globalTradeSpread, globalTradeSpread);
    }

    private void UpdateTargetTrade() // create a box where chose a random point - currentTrade
    {
        lastTrade = targetTrade; 
        targetTrade += Random.RangeFloat(-targetTradeSpread, targetTradeSpread);

        if(targetTrade <= 0)
            targetTrade = - targetTrade * 2;

        Debug.Log("Целевая цена изменилась на " + targetTrade);
    }
    public void Tick()
    {
        foreach (var e in events)
        {
            e.Tick();
        }

        float minTradePrice = currentTrade - currentTradeSpread, maxTradePrice = currentTrade + currentTradeSpread;
        trader1.CreateBuyLot("Gold", Random.RangeInt(1, maxProductCount), Random.RangeFloat(minTradePrice, maxTradePrice));
        trader2.CreateSellLot("Gold", Random.RangeInt(1, maxProductCount), Random.RangeFloat(minTradePrice, maxTradePrice));
    }
}

public class PeriodicEvent
{
    private readonly int period;
    private readonly Action onTick;
    private int tickCounter;

    public PeriodicEvent(int period, Action onTick)
    {
        this.period = period;
        this.onTick = onTick;
    }

    public void Tick()
    {
        tickCounter++;
        if (tickCounter >= period)
        {
            tickCounter = 0;
            onTick?.Invoke();
        }
    }
}