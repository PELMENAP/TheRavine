using R3;
using System;

public class MortalityComponent : IComponent
{
    public event Action Died;
    private IDisposable sub;
    private bool _died;

    public bool IsDead => _died;

    public MortalityComponent(ReadOnlyReactiveProperty<float> health)
    {
        sub = health.Subscribe(h =>
        {
            if (_died || h > 0f) return;
            _died = true;
            Died?.Invoke();
        });
    }

    public void Dispose()
    {
        sub?.Dispose();
        sub = null;
        Died = null;
    }
}