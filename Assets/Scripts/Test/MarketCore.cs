using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

using TheRavine.Extensions;
public class MarketCore
{
    private readonly ILogger logger;
    private readonly float commissionRate;
    private readonly int defaultLotTTL;
    private readonly int maxHistorySize;

    private readonly Dictionary<string, SortedDictionary<float, List<TradeLot>>> buyOrders = new();
    private readonly Dictionary<string, SortedDictionary<float, List<TradeLot>>> sellOrders = new();
    private readonly Dictionary<Guid, TradeLot> lotById = new();

    private readonly PriorityQueue<TradeLot, int> ttlQueue = new();
    private int currentTick = 0;

    private readonly CircularBuffer<TradeHistory> history;
    private readonly Dictionary<string, (float totalVolume, float totalValue, int totalTrades)> analytics = new();

    private readonly Dictionary<string, float> bestBuyPrice = new();
    private readonly Dictionary<string, float> bestSellPrice = new();
    private readonly HashSet<string> modifiedItems = new();

    public MarketCore(float commissionRate, int lotTTL, ILogger logger, int historySize = 1000)
    {
        this.commissionRate = commissionRate;
        this.defaultLotTTL = lotTTL;
        this.logger = logger;
        this.maxHistorySize = historySize;
        this.history = new CircularBuffer<TradeHistory>(historySize);
    }

    public async UniTask TickAsync(int batchSize = 50)
    {
        currentTick++;
        
        await ProcessExpiredLotsAsync(batchSize);
        await ProcessMatchingLotsAsync(batchSize);
        
        foreach (var item in modifiedItems)
        {
            UpdateBestPrice(item);
        }
        modifiedItems.Clear();
    }

    private async UniTask ProcessExpiredLotsAsync(int batchSize)
    {
        int count = 0;
        
        while (ttlQueue.Count > 0 && ttlQueue.TryPeek(out _, out int expireTick) && expireTick <= currentTick)
        {
            var lot = ttlQueue.Dequeue();
            if (lotById.ContainsKey(lot.Id))
            {
                CancelLot(lot.Id);
            }

            if (++count % batchSize == 0)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }
    }

    public void AddLot(TradeLot lot)
    {
        if (lot.Quantity <= 0 || lot.Price <= 0 || !lotById.TryAdd(lot.Id, lot)) return;

        int ttl = lot.TimeToLive < 1 ? defaultLotTTL : lot.TimeToLive;

        ttlQueue.Enqueue(lot, currentTick + ttl);
        var orders = lot.IsBuy ? buyOrders : sellOrders;

        if (!orders.TryGetValue(lot.Item, out var priceList))
            orders[lot.Item] = priceList = new SortedDictionary<float, List<TradeLot>>(
                lot.IsBuy ? Comparer<float>.Create((x, y) => y.CompareTo(x)) : null);

        if (!priceList.TryGetValue(lot.Price, out var lots))
            priceList[lot.Price] = lots = new List<TradeLot>();

        lots.Add(lot);

        modifiedItems.Add(lot.Item);
    }

    public void CancelLot(Guid id)
    {
        if (!lotById.TryGetValue(id, out var lot)) return;

        lot.Owner?.ReturnItem(lot, logger);
        RemoveLotFromBook(lot);
        lotById.Remove(id);

        modifiedItems.Add(lot.Item);
    }

    private void RemoveLotFromBook(TradeLot lot)
    {
        var orders = lot.IsBuy ? buyOrders : sellOrders;

        if (orders.TryGetValue(lot.Item, out var priceList) &&
            priceList.TryGetValue(lot.Price, out var lots))
        {
            lots.Remove(lot);
            if (lots.Count == 0) priceList.Remove(lot.Price);
            if (priceList.Count == 0) orders.Remove(lot.Item);
        }
    }

    private async UniTask ProcessMatchingLotsAsync(int batchSize)
    {
        HashSet<string> processedItems = new();

        foreach (var item in buyOrders.Keys.ToList())
        {
            if (processedItems.Contains(item) || !sellOrders.ContainsKey(item)) continue;
            processedItems.Add(item);

            var buys = buyOrders[item];
            var sells = sellOrders[item];

            int count = 0;
            bool modified = false;

            while (buys.Count > 0 && sells.Count > 0)
            {
                var buyPriceList = buys.First();
                var sellPriceList = sells.First();

                float buyPrice = buyPriceList.Key;
                float sellPrice = sellPriceList.Key;

                if (buyPrice < sellPrice) break;

                var buyLot = buyPriceList.Value[0];
                var sellLot = sellPriceList.Value[0];

                int tradeQuantity = Math.Min(buyLot.Quantity, sellLot.Quantity);
                float tradePrice = sellLot.Price * (1 - commissionRate);

                CompleteTrade(buyLot, sellLot, tradeQuantity, tradePrice);
                modified = true;

                if (++count % batchSize == 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            if (modified)
            {
                modifiedItems.Add(item);
            }
        }
    }

    private void CompleteTrade(TradeLot buy, TradeLot sell, int quantity, float price)
    {
        buy.Quantity -= quantity;
        sell.Quantity -= quantity;

        RecordTrade(buy.Item, price, quantity);

        if (buy.Quantity == 0) CancelLot(buy.Id);
        if (sell.Quantity == 0) CancelLot(sell.Id);
    }

    private void RecordTrade(string item, float price, int quantity)
    {
        history.Add(new TradeHistory(item, price, quantity));

        if (!analytics.ContainsKey(item))
            analytics[item] = (0, 0, 0);

        var (totalVolume, totalValue, totalTrades) = analytics[item];
        analytics[item] = (totalVolume + quantity, totalValue + price * quantity, totalTrades + 1);
    }

    private void UpdateBestPrice(string item)
    {
        bestBuyPrice[item] = buyOrders.TryGetValue(item, out var buyPrices) && buyPrices.Count > 0 
            ? buyPrices.First().Key
            : 0;
            
        bestSellPrice[item] = sellOrders.TryGetValue(item, out var sellPrices) && sellPrices.Count > 0 
            ? sellPrices.First().Key 
            : float.MaxValue;
    }

    public List<TradeHistory> GetTradeHistory(int count) => 
        history.TakeLast(Math.Min(count, history.Count)).ToList();

    public float GetAveragePrice(string item) =>
        analytics.TryGetValue(item, out var data) && data.totalVolume > 0
            ? data.totalValue / data.totalVolume
            : 0;
            
    public int GetTotalLots() => lotById.Count;
    
    public int GetBuyLotCount(string trackedItem) => 
        buyOrders.TryGetValue(trackedItem, out var data) ? data.Sum(x => x.Value.Count) : 0;
        
    public int GetSellLotCount(string trackedItem) => 
        sellOrders.TryGetValue(trackedItem, out var data) ? data.Sum(x => x.Value.Count) : 0;
        
    public int GetTotalTrades(string item) => 
        analytics.TryGetValue(item, out var data) ? data.totalTrades : 0;

    public float GetBestBuyPrice(string item) => 
        bestBuyPrice.TryGetValue(item, out var price) ? price : 0;

    public float GetBestSellPrice(string item) => 
        bestSellPrice.TryGetValue(item, out var price) ? price : float.MaxValue;
}





public class Trader
{
    public string Name { get; }
    private readonly MarketCore market;

    public Trader(string name, MarketCore market)
    {
        Name = name;
        this.market = market;
    }

    public void CreateBuyLot(string item, int quantity, float price, int ttl = 0)
    {
        market.AddLot(new TradeLot(item, quantity, price, true, this, ttl));
    }

    public void CreateSellLot(string item, int quantity, float price, int ttl = 0)
    {
        market.AddLot(new TradeLot(item, quantity, price, false, this, ttl));
    }

    public void ReturnItem(TradeLot lot, ILogger logger)
    {
        if(Name == "player")
            logger.LogInfo($"{Name} получил {lot.Quantity} {lot.Item} обратно (истек срок).");
    }
}

[Serializable]
public class TradeLot
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Item { get; }
    public int Quantity { get; set; }
    public float Price { get; }
    public bool IsBuy { get; }
    public int TimeToLive { get; set; }
    public Trader Owner { get; }

    public TradeLot(string item, int quantity, float price, bool isBuy, Trader owner, int ttl)
    {
        Item = item;
        Quantity = quantity;
        Price = price;
        IsBuy = isBuy;
        TimeToLive = ttl;
        Owner = owner;
    }
}

[Serializable]
public class TradeHistory
{
    public string Item { get; }
    public float Price { get; }
    public int Quantity { get; }

    public TradeHistory(string item, float price, int quantity)
    {
        Item = item;
        Price = price;
        Quantity = quantity;
    }
}
