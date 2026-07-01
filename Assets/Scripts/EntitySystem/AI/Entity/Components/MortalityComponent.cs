using R3;
using System;

public class MortalityComponent : IComponent
{
    public event System.Action Died;
    private IDisposable sub;

    public MortalityComponent(ReadOnlyReactiveProperty<float> health)
    {
        sub = health.Subscribe(h => { if (h <= 0f) Died?.Invoke(); });
    }

    public void Dispose() => sub?.Dispose();
}