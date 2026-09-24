using R3;
using System;

public enum DeathCause : byte
{
    Killed  = 0,
    Starved = 1,
    Virus   = 2,
    Toxic   = 3,
    Age     = 4,
}

public class MortalityComponent : IComponent
{
    public event Action Died;
    private IDisposable sub;
    private bool _died;
    private bool _causeLocked;

    public bool IsDead => _died;
    public DeathCause Cause { get; private set; } = DeathCause.Age;

    public MortalityComponent(ReadOnlyReactiveProperty<float> health)
    {
        sub = health.Subscribe(h =>
        {
            if (_died || h > 0f) return;
            _died = true;
            _causeLocked = true;
            Died?.Invoke();
        });
    }

    public void NoteHarm(DeathCause cause, float healthAfter)
    {
        if (_causeLocked || healthAfter > 0f) return;
        Cause = cause;
        _causeLocked = true;
    }

    public void Dispose()
    {
        sub?.Dispose();
        sub = null;
        Died = null;
    }
}