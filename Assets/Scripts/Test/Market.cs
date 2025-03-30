using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

using TheRavine.Extensions;

public class Market : MonoBehaviour
{
    public float commissionRate = 0.01f;
    public int tickRate = 1;
    public int lotTTL = 10;
    private MarketSimulator marketSimulator;
    private MarketCore marketCore;
    private ILogger logger;
    private CancellationTokenSource cancellationToken;

    private void Start()
    {
        cancellationToken = new CancellationTokenSource();
        logger = new Logger(null);
        marketCore = new MarketCore(commissionRate, lotTTL, logger);
        marketSimulator = new MarketSimulator(marketCore);

        Tick().Forget();
    }

    private async UniTaskVoid Tick()
    {
        while(!cancellationToken.Token.IsCancellationRequested)
        {
            marketSimulator.Tick();
            await marketCore.TickAsync();
            await UniTask.Delay(900, cancellationToken: cancellationToken.Token);
        }
    }

    public MarketCore GetMarketCore() => marketCore;
    private void OnDestroy()
    {
        cancellationToken?.Cancel();
        cancellationToken?.Dispose();
    }
}