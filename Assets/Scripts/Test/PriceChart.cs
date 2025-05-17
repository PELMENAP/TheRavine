using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using ZLinq;

public class PriceChart : MonoBehaviour
{
    [Header("Маркет")]
    public Market market;
    public string trackedItem = "Gold";
    
    [Header("Настройки графика")]
    public LineRenderer lineRenderer;
    public int maxPoints = 200;
    public float updateInterval = 1f;
    
    [Header("Размеры области графика")]
    public float graphWidth = 10f;
    public float graphHeight = 5f;
    public Vector2 graphOffset = Vector2.zero;
    
    [Header("Отображение текста")]
    public TextMeshProUGUI totalLotsText;
    public TextMeshProUGUI buyLotsText;
    public TextMeshProUGUI sellLotsText;

    [Header("Дополнительная информация")]
    public TextMeshProUGUI maxPriceText;
    public TextMeshProUGUI minPriceText;

    public string numberFormat = "N0"; // Формат отображения чисел
    
    private Queue<float> priceHistory = new Queue<float>();
    private float minPrice = float.MaxValue;
    private float maxPrice = float.MinValue;
    private bool needsRescaling = true;
    
    private CancellationTokenSource cancellationToken;

    private void Start()
    {
        cancellationToken = new CancellationTokenSource();
        
        ConfigureLineRenderer();

        Tick().Forget();
    }
    
    private void ConfigureLineRenderer()
    {
        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = false;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.positionCount = 0;
            
            // Установка ширины линии
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
        }
    }
    
    public void AddPrice(float price)
    {
        if (price <= 0)
            return;

        if (priceHistory.Count >= maxPoints)
        {
            float oldestPrice = priceHistory.Dequeue();
            
            if (oldestPrice == minPrice || oldestPrice == maxPrice)
            {
                needsRescaling = true;
            }
        }

        priceHistory.Enqueue(price);

        if (price < minPrice)
        {
            minPrice = price;
            needsRescaling = true;
        }

        if (price > maxPrice)
        {
            maxPrice = price;
            needsRescaling = true;
        }

        UpdateGraph();
        UpdatePriceLabels();
    }

    private void UpdatePriceLabels()
    {
        if (maxPriceText != null)
        {
            maxPriceText.text = $"Макс: {maxPrice.ToString("F2")}";
        }

        if (minPriceText != null)
        {
            minPriceText.text = $"Мин: {minPrice.ToString("F2")}";
        }
    }
    private void UpdateGraph()
    {
        if (lineRenderer == null || priceHistory.Count == 0)
            return;

        if (needsRescaling && priceHistory.Count > 1)
        {
            var priceHistoryValueEnumerable = priceHistory.AsValueEnumerable();
            minPrice = priceHistoryValueEnumerable.Min();
            maxPrice = priceHistoryValueEnumerable.Max();
            needsRescaling = false;
            UpdatePriceLabels(); // Обновляем значения min/max в UI
        }

        lineRenderer.positionCount = priceHistory.Count;

        float priceRange = maxPrice - minPrice;
        if (priceRange <= 0.001f)
        {
            priceRange = maxPrice * 0.1f;
            if (priceRange <= 0.001f) priceRange = 1f;
        }

        int i = 0;
        foreach (var price in priceHistory)
        {
            float normalizedX = (float)i / (maxPoints - 1);
            float normalizedY = (price - minPrice) / priceRange;
            
            float x = normalizedX * graphWidth + graphOffset.x;
            float y = normalizedY * graphHeight + graphOffset.y;

            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
            i++;
        }
    }
    private async UniTaskVoid Tick()
    {
        while(!cancellationToken.Token.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: cancellationToken.Token);

            float averagePrice = market.GetMarketCore().GetAveragePrice(trackedItem);
            AddPrice(averagePrice);
            
            UpdateLotsInfo(market?.GetMarketCore());
        }
    }
    private void UpdateLotsInfo(MarketCore marketCore)
    {
        if (marketCore == null) return;
        
        UpdateTotalLotsText(marketCore.GetTotalLots());
        UpdateBuyLotsText(marketCore.GetBuyLotCount(trackedItem));
        UpdateSellLotsText(marketCore.GetSellLotCount(trackedItem));
    }
    public void UpdateTotalLotsText(int count)
    {
        if (totalLotsText != null)
        {
            totalLotsText.text = $"Всего лотов: {count.ToString(numberFormat)}";
        }
    }
    public void UpdateBuyLotsText(int count)
    {
        if (buyLotsText != null)
        {
            buyLotsText.text = $"Покупка: {count.ToString(numberFormat)}";
        }
    }
    public void UpdateSellLotsText(int count)
    {
        if (sellLotsText != null)
        {
            sellLotsText.text = $"Продажа: {count.ToString(numberFormat)}";
        }
    }
    public void ClearHistory()
    {
        priceHistory.Clear();
        minPrice = float.MaxValue;
        maxPrice = float.MinValue;

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }

        UpdatePriceLabels();
    }
    public void SetTrackedItem(string itemId)
    {
        cancellationToken?.Cancel();
        cancellationToken?.Dispose();
        cancellationToken = new CancellationTokenSource();

        if (string.IsNullOrEmpty(itemId))
            return;
            
        trackedItem = itemId;
        ClearHistory();
        Tick().Forget();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 bottomLeft = transform.position + new Vector3(graphOffset.x, graphOffset.y, 0);
        Vector3 topRight = bottomLeft + new Vector3(graphWidth, graphHeight, 0);
        
        Gizmos.DrawLine(bottomLeft, new Vector3(topRight.x, bottomLeft.y, 0));
        Gizmos.DrawLine(new Vector3(topRight.x, bottomLeft.y, 0), topRight);
        Gizmos.DrawLine(topRight, new Vector3(bottomLeft.x, topRight.y, 0));
        Gizmos.DrawLine(new Vector3(bottomLeft.x, topRight.y, 0), bottomLeft);
    }
    private void OnDestroy()
    {
        cancellationToken?.Cancel();
        cancellationToken?.Dispose();
    }
}